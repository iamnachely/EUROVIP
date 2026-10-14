using Common.Data.Entities;
using Dapper;
using EuroVip.Models;
using System.Data;
using System.Data.SqlClient;
using System.Text.Json;

namespace EuroVip.Repositories
{
    public interface IHotelRepository
    {
        Task<PaginaResultado<HotelListaItem>> ListarAsync(HotelFiltro f);
        Task<HotelEstadisticas> EstadisticasAsync();
        Task<Inicio?> ObtenerAsync(int id);
        Task<int> CrearAsync(HotelGuardarDto dto);
        Task<bool> EditarAsync(HotelGuardarDto dto);
        Task<bool?> CambiarEstadoAsync(int id);
        Task<Inicio?> EliminarAsync(int id);
        Task<IEnumerable<CodigoNombre>> IdiomasAsync();
        Task<IEnumerable<CodigoNombre>> MonedasAsync();
        Task<IEnumerable<string>> DestinosAsync();
        Task<List<string>> NombresAsync();
        Task<IEnumerable<Campanya>> CampanyasAsync(string idioma);
        Task<IEnumerable<Contenedor>> ContenedoresAsync(string idioma);
        Task AuditarAsync(string tipo, int id, object cambios, string usuario, string? ip, string? navegador);
    }

    /// <summary>
    /// Carga de hoteles sobre [Eurovip].[inicio] con tipo_servicio = 'HOTEL'.
    /// Mismo modelo que BackTiTan (Inicio2), pero con filtros y paginación en SQL (no en memoria),
    /// id generado dentro de una transacción con bloqueo y auditoría en cada cambio.
    /// </summary>
    public class HotelRepository : IHotelRepository
    {
        public const string TipoHotel = "HOTEL";
        // Eurovip carga hoteles SOLO en español: el idioma se fija aquí y no se acepta otro.
        public const string IdiomaHotel = "ES";
        private readonly string _cs;

        public HotelRepository(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DefaultConnection")
                  ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection");
        }

        private SqlConnection Conexion() => new(_cs);

        public async Task<PaginaResultado<HotelListaItem>> ListarAsync(HotelFiltro f)
        {
            var tam = Math.Clamp(f.Tam, 6, 60);
            var pagina = Math.Max(1, f.Pagina);

            var where = new List<string> { "i.tipo_servicio = @tipo", "i.id_idioma = @idioma" };
            var p = new DynamicParameters();
            p.Add("tipo", TipoHotel);
            p.Add("idioma", IdiomaHotel);

            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                where.Add(@"(i.id_destino LIKE @q OR i.origen LIKE @q OR i.descripcion LIKE @q
                             OR i.contenedor LIKE @q OR c.nombre LIKE @q OR CAST(i.id_servicio AS varchar(12)) = @qExacto)");
                p.Add("q", $"%{f.Q.Trim()}%");
                p.Add("qExacto", f.Q.Trim());
            }
            if (f.Estado is 0 or 1) { where.Add("ISNULL(i.estado, 1) = @estado"); p.Add("estado", f.Estado); }
            if (f.Contenedor is > 0) { where.Add("i.id_contenedor = @contenedor"); p.Add("contenedor", f.Contenedor); }
            if (f.Campanya is > 0) { where.Add("i.id_campanya = @campanya"); p.Add("campanya", f.Campanya); }

            var orden = f.Orden switch
            {
                "precio_asc" => "i.precioWeb ASC, i.id_servicio DESC",
                "precio_desc" => "i.precioWeb DESC, i.id_servicio DESC",
                "nombre" => "i.id_destino ASC",
                "posicion" => "ISNULL(i.posicion_destino, 9999) ASC, i.id_destino ASC",
                _ => "i.id_servicio DESC"
            };

            p.Add("offset", (pagina - 1) * tam);
            p.Add("tam", tam);

            var filtro = string.Join(" AND ", where);
            var sql = $@"
                SELECT COUNT(*)
                FROM Eurovip.inicio i
                LEFT JOIN Eurovip.campanya c ON c.id_campanya = i.id_campanya AND c.id_idioma = i.id_idioma
                WHERE {filtro};

                SELECT i.id_servicio, i.id_idioma, i.id_destino, i.descripcion, i.caratulaDestino, i.precioWeb,
                       i.id_moneda, i.origen, i.id_pais_origen, i.id_paisDestino, i.id_campanya, c.nombre AS Nombre_Campanya,
                       i.id_contenedor, i.contenedor, i.posicion_destino, i.noches, CAST(ISNULL(i.estado, 1) AS bit) AS Estado
                FROM Eurovip.inicio i
                LEFT JOIN Eurovip.campanya c ON c.id_campanya = i.id_campanya AND c.id_idioma = i.id_idioma
                WHERE {filtro}
                ORDER BY {orden}
                OFFSET @offset ROWS FETCH NEXT @tam ROWS ONLY;";

            using var cn = Conexion();
            using var multi = await cn.QueryMultipleAsync(sql, p);
            var total = await multi.ReadSingleAsync<int>();
            var items = await multi.ReadAsync<HotelListaItem>();
            return new PaginaResultado<HotelListaItem> { Items = items, Total = total, Pagina = pagina, Tam = tam };
        }

        public async Task<HotelEstadisticas> EstadisticasAsync()
        {
            using var cn = Conexion();
            return await cn.QuerySingleAsync<HotelEstadisticas>(@"
                SELECT COUNT(*) AS Total,
                       ISNULL(SUM(CASE WHEN ISNULL(estado, 1) = 1 THEN 1 ELSE 0 END), 0) AS Visibles,
                       ISNULL(SUM(CASE WHEN ISNULL(estado, 1) = 0 THEN 1 ELSE 0 END), 0) AS Ocultos,
                       AVG(precioWeb) AS PrecioPromedio
                FROM Eurovip.inicio WHERE tipo_servicio = @tipo AND id_idioma = @idioma", new { tipo = TipoHotel, idioma = IdiomaHotel });
        }

        public async Task<Inicio?> ObtenerAsync(int id)
        {
            using var cn = Conexion();
            return await cn.QueryFirstOrDefaultAsync<Inicio>(
                "SELECT * FROM Eurovip.inicio WHERE id_servicio = @id AND tipo_servicio = @tipo AND id_idioma = @idioma",
                new { id, tipo = TipoHotel, idioma = IdiomaHotel });
        }

        public async Task<int> CrearAsync(HotelGuardarDto dto)
        {
            using var cn = Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction(IsolationLevel.Serializable);

            // Mismo criterio de id que BackTiTan (MAX + 1) pero bloqueando la tabla para evitar duplicados.
            var id = await cn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(MAX(id_servicio), 0) + 1 FROM Eurovip.inicio WITH (UPDLOCK, HOLDLOCK)", transaction: tx);

            var cont = await DatosContenedorAsync(cn, tx, dto.Id_Contenedor);

            await cn.ExecuteAsync(@"
                INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen,
                    id_paisDestino, id_destino, caratulaDestino, descripcion, latitud, longitud, precioWeb,
                    id_contenedor, contenedor, posicion_contenedor, posicion_destino, estado, notas, noches)
                VALUES (@Id, @Id_Idioma, @Tipo, @Id_Campanya, @Id_Moneda, @Id_Pais_Origen, @Origen,
                    @Id_PaisDestino, @Id_Destino, @CaratulaDestino, @Descripcion, @Latitud, @Longitud, @PrecioWeb,
                    @Id_Contenedor, @Contenedor, @Posicion_Contenedor, @Posicion_Destino, @Estado, @Notas, @Noches)",
                Parametros(dto, id, cont), tx);

            tx.Commit();
            return id;
        }

        public async Task<bool> EditarAsync(HotelGuardarDto dto)
        {
            using var cn = Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var cont = await DatosContenedorAsync(cn, tx, dto.Id_Contenedor);

            var filas = await cn.ExecuteAsync(@"
                UPDATE Eurovip.inicio SET
                    id_campanya = @Id_Campanya, id_moneda = @Id_Moneda,
                    id_pais_origen = @Id_Pais_Origen, origen = @Origen, id_paisDestino = @Id_PaisDestino,
                    id_destino = @Id_Destino, caratulaDestino = @CaratulaDestino, descripcion = @Descripcion,
                    latitud = @Latitud, longitud = @Longitud, precioWeb = @PrecioWeb,
                    id_contenedor = @Id_Contenedor, contenedor = @Contenedor, posicion_contenedor = @Posicion_Contenedor,
                    posicion_destino = @Posicion_Destino, estado = @Estado, notas = @Notas, noches = @Noches
                WHERE id_servicio = @Id AND tipo_servicio = @Tipo AND id_idioma = @Id_Idioma",
                Parametros(dto, dto.Id_Servicio!.Value, cont), tx);

            tx.Commit();
            return filas == 1;
        }

        public async Task<bool?> CambiarEstadoAsync(int id)
        {
            using var cn = Conexion();
            return await cn.QueryFirstOrDefaultAsync<bool?>(@"
                UPDATE Eurovip.inicio SET estado = CASE WHEN ISNULL(estado, 1) = 1 THEN 0 ELSE 1 END
                OUTPUT CAST(inserted.estado AS bit)
                WHERE id_servicio = @id AND tipo_servicio = @tipo AND id_idioma = @idioma", new { id, tipo = TipoHotel, idioma = IdiomaHotel });
        }

        public async Task<Inicio?> EliminarAsync(int id)
        {
            using var cn = Conexion();
            return await cn.QueryFirstOrDefaultAsync<Inicio>(
                "DELETE FROM Eurovip.inicio OUTPUT deleted.* WHERE id_servicio = @id AND tipo_servicio = @tipo AND id_idioma = @idioma",
                new { id, tipo = TipoHotel, idioma = IdiomaHotel });
        }

        public async Task<IEnumerable<CodigoNombre>> IdiomasAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<CodigoNombre>("SELECT id_idioma AS Codigo, nombre AS Nombre FROM Eurovip.idioma ORDER BY CASE id_idioma WHEN 'ES' THEN 0 ELSE 1 END, nombre");
        }

        public async Task<IEnumerable<CodigoNombre>> MonedasAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<CodigoNombre>("SELECT id_moneda AS Codigo, nombre AS Nombre FROM Eurovip.moneda ORDER BY id_moneda");
        }

        /// <summary>Destinos (zonas) para el campo "Destino" del hotel: los de Eurovip.destino y los ya usados en hoteles.</summary>
        /// <summary>Nombres (id_destino) de todos los hoteles: la imagen se relaciona por nombre.</summary>
        public async Task<List<string>> NombresAsync()
        {
            using var cn = Conexion();
            return (await cn.QueryAsync<string>("SELECT LTRIM(RTRIM(id_destino)) FROM Eurovip.inicio WHERE tipo_servicio = @TipoHotel",
                new { TipoHotel })).ToList();
        }

        public async Task<IEnumerable<string>> DestinosAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<string>(@"
                SELECT d FROM (SELECT LTRIM(RTRIM(id_destino)) d FROM Eurovip.destino WHERE id_idioma = 'ES'
                               UNION SELECT LTRIM(RTRIM(caratulaDestino)) FROM Eurovip.inicio WHERE tipo_servicio = 'HOTEL') x
                WHERE ISNULL(d, '') <> '' AND d NOT LIKE 'http%' ORDER BY d");
        }


        public async Task<IEnumerable<Campanya>> CampanyasAsync(string idioma)
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Campanya>(
                "SELECT id_campanya, id_idioma, id_tipo_servicio, nombre FROM Eurovip.campanya WHERE id_idioma = @idioma ORDER BY nombre",
                new { idioma = idioma.Trim().ToUpperInvariant() });
        }

        public async Task<IEnumerable<Contenedor>> ContenedoresAsync(string idioma)
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Contenedor>(
                "SELECT id_contenedor, nombre, posicion, estado, id_idioma FROM Eurovip.contenedores WHERE id_idioma = @idioma AND estado = 1 ORDER BY posicion",
                new { idioma = idioma.Trim().ToLowerInvariant() });
        }

        public async Task AuditarAsync(string tipo, int id, object cambios, string usuario, string? ip, string? navegador)
        {
            using var cn = Conexion();
            await cn.ExecuteAsync(@"
                INSERT INTO Eurovip.auditoria_consulta_oferta (ids_servicio, cantidad_servicios, tipo_mantenimiento, usuario, cambios_realizados, ip_usuario, navegador)
                VALUES (@ids, 1, @tipo, @usuario, @cambios, @ip, @navegador)",
                new
                {
                    ids = JsonSerializer.Serialize(new[] { id }),
                    tipo,
                    usuario,
                    cambios = JsonSerializer.Serialize(cambios),
                    ip,
                    navegador = navegador is { Length: > 200 } ? navegador[..200] : navegador
                });
        }

        private static async Task<Contenedor?> DatosContenedorAsync(SqlConnection cn, SqlTransaction tx, int? id)
        {
            if (id is not > 0) return null;
            return await cn.QueryFirstOrDefaultAsync<Contenedor>(
                "SELECT id_contenedor, nombre, posicion, estado, id_idioma FROM Eurovip.contenedores WHERE id_contenedor = @id",
                new { id }, tx);
        }

        private static object Parametros(HotelGuardarDto d, int id, Contenedor? cont) => new
        {
            Id = id,
            Tipo = TipoHotel,
            Id_Idioma = IdiomaHotel,   // siempre ES: se ignora lo que venga del formulario
            d.Id_Campanya,
            Id_Moneda = d.Id_Moneda.Trim().ToUpperInvariant(),
            Id_Pais_Origen = d.Id_Pais_Origen.Trim().ToUpperInvariant(),
            Origen = d.Origen.Trim(),
            Id_PaisDestino = d.Id_PaisDestino.Trim().ToUpperInvariant(),
            Id_Destino = d.Id_Destino.Trim(),
            CaratulaDestino = string.IsNullOrWhiteSpace(d.CaratulaDestino) ? null : d.CaratulaDestino.Trim(),
            Descripcion = d.Descripcion.Trim(),
            Latitud = string.IsNullOrWhiteSpace(d.Latitud) ? null : d.Latitud.Trim(),
            Longitud = string.IsNullOrWhiteSpace(d.Longitud) ? null : d.Longitud.Trim(),
            // Igual que BackTiTan: el precio web se publica redondeado a entero.
            PrecioWeb = d.PrecioWeb is null ? (decimal?)null : Math.Round(d.PrecioWeb.Value, 0, MidpointRounding.AwayFromZero),
            Id_Contenedor = cont?.Id_Contenedor,
            Contenedor = cont is null ? null : (cont.Nombre.Length > 30 ? cont.Nombre[..30] : cont.Nombre),
            Posicion_Contenedor = cont?.Posicion,
            d.Posicion_Destino,
            d.Estado,
            Notas = string.IsNullOrWhiteSpace(d.Notas) ? null : d.Notas.Trim(),
            d.Noches
        };
    }
}
