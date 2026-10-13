using Dapper;
using EuroVip.Models;
using System.Data;
using System.Data.SqlClient;
using System.Text.Json;

namespace EuroVip.Repositories
{
    public interface ICircuitoRepository
    {
        SqlConnection Conexion();

        // Catálogo
        Task<IEnumerable<CircuitoResumen>> ListarAsync(string? q, string? pais);
        Task<CircuitoDetalle?> ObtenerAsync(string codigo);
        Task<bool> CodigoExisteAsync(string codigo, string? excepto = null);
        Task<bool> NombreExisteAsync(string nombre, string? exceptoCodigo = null);
        Task GuardarAsync(CircuitoGuardarDto dto, SqlConnection cn, SqlTransaction tx);
        Task GuardarAsync(CircuitoGuardarDto dto);
        Task<int> EliminarAsync(string codigo);
        Task<int> CambiarEstadoSalidaAsync(int idServicio, bool activo);
        Task<int> EliminarSalidaAsync(int idServicio);

        // Categorías
        Task<IEnumerable<CategoriaDto>> CategoriasAsync();
        Task<string?> GuardarCategoriaAsync(CategoriaDto dto);
        Task<string?> EliminarCategoriaAsync(string categoria);

        // Búsquedas para los formularios
        Task<IEnumerable<Opcion>> ProveedoresAsync(string? tipo, string? q);
        Task<IEnumerable<Opcion>> PaisesAsync();
        Task<IEnumerable<Opcion>> DestinosAsync(string? q, string? pais);
        Task<IEnumerable<CircuitoHotelDto>> HotelesAsync(string? q, string? destino, string? pais);
        Task<IEnumerable<Opcion>> AeropuertosAsync(string? q, string? pais);
        Task<IEnumerable<Opcion>> CampanyasAsync();
        Task<IEnumerable<Opcion>> TasasAsync();
        Task<IEnumerable<Opcion>> RegimenesAsync();
        Task<IEnumerable<Opcion>> MonedasAsync();
        Task<decimal> PrecioBaseMinimoAsync();

        Task AuditarAsync(string tipo, IEnumerable<int> ids, object cambios, string usuario, string? ip, string? navegador);
    }

    /// <summary>
    /// Catálogo de circuitos (equivale a /Circuito/Index de BackTiTan) sobre el esquema Eurovip.
    /// En Eurovip cada circuito es solo ES: 1 fila en circuito por código.
    /// </summary>
    public class CircuitoRepository : ICircuitoRepository
    {
        public const string Idioma = "ES";
        private readonly string _cs;

        public CircuitoRepository(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DefaultConnection")
                  ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection");
        }

        public SqlConnection Conexion() => new(_cs);

        // ------------------------------------------------------------------ catálogo

        public async Task<IEnumerable<CircuitoResumen>> ListarAsync(string? q, string? pais)
        {
            using var cn = Conexion();
            return await cn.QueryAsync<CircuitoResumen>(@"
                SELECT c.id AS Codigo, c.nombre_circuito AS Nombre, c.noches AS Noches, c.id_proveedor AS Id_Proveedor,
                       p.nombre AS Proveedor, c.caratula AS Caratula,
                       (SELECT STRING_AGG(cd.id_destino, ' · ') FROM Eurovip.circuito_destino cd WHERE cd.nombre_circuito = c.nombre_circuito AND cd.id_idioma = c.id_idioma) AS Destinos,
                       (SELECT STRING_AGG(x.id_pais, ',') FROM (SELECT DISTINCT cd.id_pais FROM Eurovip.circuito_destino cd WHERE cd.nombre_circuito = c.nombre_circuito AND cd.id_idioma = c.id_idioma) x) AS Paises,
                       (SELECT COUNT(*) FROM Eurovip.servicio_circuito_alojamiento h WHERE h.nombre_circuito = c.nombre_circuito AND h.id_idioma = c.id_idioma) AS Hoteles,
                       (SELECT COUNT(*) FROM Eurovip.circuito_itinerario d WHERE d.nombre_circuito = c.nombre_circuito AND d.id_idioma = c.id_idioma) AS Dias,
                       ISNULL(sal.Salidas, 0) AS Salidas, ISNULL(sal.Activas, 0) AS SalidasActivas, sal.Proxima AS ProximaSalida, sal.Desde AS PrecioDesde
                FROM Eurovip.circuito c
                LEFT JOIN Eurovip.proveedor p ON p.id_proveedor = c.id_proveedor
                OUTER APPLY (
                    SELECT COUNT(*) AS Salidas,
                           SUM(CASE WHEN s.activo = 1 THEN 1 ELSE 0 END) AS Activas,
                           MIN(CASE WHEN s.activo = 1 AND sc.desde >= CAST(GETDATE() AS date) THEN sc.desde END) AS Proxima,
                           MIN(CASE WHEN s.activo = 1 AND sc.desde >= CAST(GETDATE() AS date) THEN Eurovip.fnPrecioWeb(s.id_servicio) END) AS Desde
                    FROM Eurovip.servicio_circuito sc JOIN Eurovip.servicio s ON s.id_servicio = sc.id_servicio
                    WHERE sc.nombre_circuito = c.nombre_circuito AND sc.id_idoma = c.id_idioma
                ) sal
                WHERE c.id_idioma = @Idioma
                  AND (@q IS NULL OR c.nombre_circuito LIKE '%' + @q + '%' OR c.id LIKE '%' + @q + '%'
                       OR EXISTS (SELECT 1 FROM Eurovip.circuito_destino cd WHERE cd.nombre_circuito = c.nombre_circuito AND cd.id_idioma = c.id_idioma AND cd.id_destino LIKE '%' + @q + '%'))
                  AND (@pais IS NULL OR EXISTS (SELECT 1 FROM Eurovip.circuito_destino cd WHERE cd.nombre_circuito = c.nombre_circuito AND cd.id_idioma = c.id_idioma AND cd.id_pais = @pais))
                ORDER BY c.nombre_circuito",
                new { Idioma, q = string.IsNullOrWhiteSpace(q) ? null : q.Trim(), pais = string.IsNullOrWhiteSpace(pais) ? null : pais.Trim() });
        }

        public async Task<CircuitoDetalle?> ObtenerAsync(string codigo)
        {
            using var cn = Conexion();
            var c = await cn.QueryFirstOrDefaultAsync<CircuitoDetalle>(@"
                SELECT c.id AS Codigo, c.nombre_circuito AS Nombre, c.id_proveedor AS Id_Proveedor, p.nombre AS Proveedor,
                       c.noches AS Noches, c.caratula AS Caratula, c.itinerario AS Itinerario
                FROM Eurovip.circuito c LEFT JOIN Eurovip.proveedor p ON p.id_proveedor = c.id_proveedor
                WHERE c.id = @codigo AND c.id_idioma = @Idioma", new { codigo, Idioma });
            if (c is null) return null;

            var p = new { nombre = c.Nombre, Idioma };
            using var multi = await cn.QueryMultipleAsync(@"
                SELECT cd.id_destino AS Id_Destino, cd.id_pais AS Id_Pais, pa.nombre AS Pais
                FROM Eurovip.circuito_destino cd
                LEFT JOIN Eurovip.pais pa ON pa.id_pais = cd.id_pais AND pa.id_idioma = cd.id_idioma
                WHERE cd.nombre_circuito = @nombre AND cd.id_idioma = @Idioma ORDER BY cd.id_destino;

                SELECT h.id_alojamiento AS Id_Alojamiento, h.categoria AS Categoria, a.nombre AS Nombre, a.id_destino AS Destino,
                       a.categoria AS Estrellas, COALESCE(h.caratula, a.caratula) AS Caratula
                FROM Eurovip.servicio_circuito_alojamiento h
                LEFT JOIN Eurovip.alojamiento a ON a.id_alojamiento = h.id_alojamiento AND a.id_idioma = h.id_idioma
                WHERE h.nombre_circuito = @nombre AND h.id_idioma = @Idioma ORDER BY h.categoria, a.nombre;

                SELECT dia AS Dia, titulo AS Titulo, descripcion AS Descripcion, visitas AS Visitas, regimen AS Regimen,
                       transporte AS Transporte, tipo_alojamiento AS Tipo_Alojamiento, caratula AS Caratula
                FROM Eurovip.circuito_itinerario WHERE nombre_circuito = @nombre AND id_idioma = @Idioma ORDER BY dia;

                SELECT s.id_servicio AS Id_Servicio, sc.desde AS Desde, sc.hasta AS Hasta, s.id_campanya AS Id_Campanya, ca.nombre AS Campanya,
                       s.id_moneda AS Id_Moneda, s.codigo AS Codigo, s.categoria_circuito AS Categoria,
                       COALESCE(v.origen, sc.origen) AS Origen, s.precio_base AS Precio_Base, s.descuento AS Descuento,
                       Eurovip.fnPrecioWeb(s.id_servicio) AS PrecioWeb,
                       (SELECT COUNT(*) FROM Eurovip.servicio_itinerario x WHERE x.id_servicio = s.id_servicio) AS Vuelos,
                       s.activo AS Activo, s.usuario AS Usuario
                FROM Eurovip.servicio_circuito sc
                JOIN Eurovip.servicio s ON s.id_servicio = sc.id_servicio
                LEFT JOIN Eurovip.campanya ca ON ca.id_campanya = s.id_campanya AND ca.id_idioma = s.id_idioma
                LEFT JOIN Eurovip.servicio_itinerario v ON v.id_servicio = s.id_servicio AND v.segmento = 1
                WHERE sc.nombre_circuito = @nombre AND sc.id_idoma = @Idioma
                ORDER BY sc.desde, s.id_servicio;", p);

            c.Destinos = (await multi.ReadAsync<CircuitoDestinoDto>()).ToList();
            c.Hoteles = (await multi.ReadAsync<CircuitoHotelDto>()).ToList();
            c.Dias = (await multi.ReadAsync<CircuitoDiaDto>()).ToList();
            c.Salidas = (await multi.ReadAsync<CircuitoSalida>()).ToList();
            return c;
        }

        public async Task<bool> CodigoExisteAsync(string codigo, string? excepto = null)
        {
            using var cn = Conexion();
            return await cn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Eurovip.circuito WHERE LTRIM(RTRIM(id)) = @codigo AND (@excepto IS NULL OR id <> @excepto)",
                new { codigo = codigo.Trim(), excepto }) > 0;
        }

        public async Task<bool> NombreExisteAsync(string nombre, string? exceptoCodigo = null)
        {
            using var cn = Conexion();
            return await cn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Eurovip.circuito WHERE nombre_circuito = @nombre AND id_idioma = @Idioma AND (@excepto IS NULL OR id <> @excepto)",
                new { nombre = nombre.Trim(), Idioma, excepto = exceptoCodigo }) > 0;
        }

        public async Task GuardarAsync(CircuitoGuardarDto dto)
        {
            using var cn = Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            await GuardarAsync(dto, cn, tx);
            tx.Commit();
        }

        /// <summary>
        /// Crea o reemplaza el circuito completo (datos + destinos + hoteles + itinerario) dentro de la transacción recibida.
        /// Al editar, los hijos se borran y se vuelven a insertar; si cambia el nombre, las salidas cargadas
        /// (servicio_circuito) se actualizan solas por la FK ON UPDATE CASCADE.
        /// </summary>
        public async Task GuardarAsync(CircuitoGuardarDto dto, SqlConnection cn, SqlTransaction tx)
        {
            var codigo = dto.Codigo.Trim();
            var nombre = dto.Nombre.Trim();
            var original = string.IsNullOrWhiteSpace(dto.CodigoOriginal) ? null : dto.CodigoOriginal.Trim();

            if (original is null)
            {
                await cn.ExecuteAsync(@"
                    INSERT INTO Eurovip.circuito (id, nombre_circuito, id_idioma, id_proveedor, noches, caratula, itinerario)
                    VALUES (@codigo, @nombre, @Idioma, @Id_Proveedor, @Noches, @Caratula, @Itinerario)",
                    new { codigo, nombre, Idioma, dto.Id_Proveedor, dto.Noches, Caratula = Nulo(dto.Caratula), Itinerario = Nulo(dto.Itinerario) }, tx);
            }
            else
            {
                var nombreAnterior = await cn.ExecuteScalarAsync<string?>(
                    "SELECT nombre_circuito FROM Eurovip.circuito WHERE id = @original AND id_idioma = @Idioma", new { original, Idioma }, tx)
                    ?? throw new InvalidOperationException("El circuito ya no existe.");

                var p = new { nombre = nombreAnterior, Idioma };
                await cn.ExecuteAsync(@"
                    DELETE FROM Eurovip.circuito_destino WHERE nombre_circuito = @nombre AND id_idioma = @Idioma;
                    DELETE FROM Eurovip.circuito_itinerario WHERE nombre_circuito = @nombre AND id_idioma = @Idioma;
                    DELETE FROM Eurovip.servicio_circuito_alojamiento WHERE nombre_circuito = @nombre AND id_idioma = @Idioma;", p, tx);

                await cn.ExecuteAsync(@"
                    UPDATE Eurovip.circuito
                    SET id = @codigo, nombre_circuito = @nombre, id_proveedor = @Id_Proveedor, noches = @Noches,
                        caratula = @Caratula, itinerario = @Itinerario
                    WHERE id = @original AND id_idioma = @Idioma",
                    new { codigo, nombre, dto.Id_Proveedor, dto.Noches, Caratula = Nulo(dto.Caratula), Itinerario = Nulo(dto.Itinerario), original, Idioma }, tx);
            }

            foreach (var d in dto.Destinos.DistinctBy(x => (x.Id_Destino.Trim().ToUpperInvariant(), x.Id_Pais.Trim().ToUpperInvariant())))
            {
                await cn.ExecuteAsync(@"
                    INSERT INTO Eurovip.circuito_destino (nombre_circuito, id_destino, id_idioma, id_pais, id)
                    VALUES (@nombre, @destino, @Idioma, @pais, @codigo)",
                    new { nombre, destino = d.Id_Destino.Trim(), Idioma, pais = d.Id_Pais.Trim().ToUpperInvariant(), codigo }, tx);
            }

            foreach (var h in dto.Hoteles.DistinctBy(x => (x.Id_Alojamiento, x.Categoria.Trim().ToUpperInvariant())))
            {
                await cn.ExecuteAsync("Eurovip.spInsertarServicioCircuitoAlojamiento",
                    new { nombre_circuito = nombre, id_idioma = Idioma, id_alojamiento = h.Id_Alojamiento, categoria = h.Categoria.Trim(), caratula = Nulo(h.Caratula) },
                    tx, commandType: CommandType.StoredProcedure);
            }

            var dia = 0;
            foreach (var d in dto.Dias.Where(x => !string.IsNullOrWhiteSpace(x.Titulo)))
            {
                dia++;
                await cn.ExecuteAsync("Eurovip.spInsertaCircuitoItinerario", new
                {
                    nombre_circuito = nombre,
                    idioma = Idioma,
                    dia,
                    titulo = Corta(d.Titulo.Trim(), 200),
                    descripcion = string.IsNullOrWhiteSpace(d.Descripcion) ? "-" : d.Descripcion.Trim(),
                    visitas = Nulo(d.Visitas),
                    regimen = Corta(Nulo(d.Regimen), 200),
                    transporte = Corta(Nulo(d.Transporte), 200),
                    tipo_alojamiento = Corta(Nulo(d.Tipo_Alojamiento), 20),
                    caratula = Corta(Nulo(d.Caratula), 200)
                }, tx, commandType: CommandType.StoredProcedure);
            }
        }

        /// <summary>Borra el circuito y TODO lo que depende de él, en el orden correcto de las FK.</summary>
        public async Task<int> EliminarAsync(string codigo)
        {
            using var cn = Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var nombre = await cn.ExecuteScalarAsync<string?>(
                "SELECT nombre_circuito FROM Eurovip.circuito WHERE id = @codigo AND id_idioma = @Idioma", new { codigo, Idioma }, tx);
            if (nombre is null) return 0;

            var p = new { nombre, Idioma };
            var salidas = await cn.ExecuteScalarAsync<int>(@"
                DECLARE @ids TABLE (id int PRIMARY KEY);
                INSERT INTO @ids SELECT id_servicio FROM Eurovip.servicio_circuito WHERE nombre_circuito = @nombre AND id_idoma = @Idioma;
                DELETE FROM Eurovip.servicio_itinerario WHERE id_servicio IN (SELECT id FROM @ids);
                DELETE FROM Eurovip.servicio_tasa       WHERE id_Servicio IN (SELECT id FROM @ids);
                DELETE FROM Eurovip.servicio_nota       WHERE id_servicio IN (SELECT id FROM @ids);
                DELETE FROM Eurovip.alojamiento_circuito WHERE id_servicio IN (SELECT id FROM @ids);
                DELETE FROM Eurovip.servicio_circuito   WHERE id_servicio IN (SELECT id FROM @ids);
                DELETE FROM Eurovip.servicio            WHERE id_servicio IN (SELECT id FROM @ids);
                DELETE FROM Eurovip.circuito_itinerario WHERE nombre_circuito = @nombre AND id_idioma = @Idioma;
                DELETE FROM Eurovip.servicio_circuito_alojamiento WHERE nombre_circuito = @nombre AND id_idioma = @Idioma;
                DELETE FROM Eurovip.circuito_destino    WHERE nombre_circuito = @nombre AND id_idioma = @Idioma;
                DELETE FROM Eurovip.circuito            WHERE nombre_circuito = @nombre AND id_idioma = @Idioma;
                SELECT COUNT(*) FROM @ids;", p, tx);
            tx.Commit();
            return salidas + 1;
        }

        public async Task<int> CambiarEstadoSalidaAsync(int idServicio, bool activo)
        {
            using var cn = Conexion();
            return await cn.ExecuteAsync("UPDATE Eurovip.servicio SET activo = @activo WHERE id_servicio = @idServicio AND id_tipo_servicio = 'CIRCUITO'",
                new { idServicio, activo });
        }

        public async Task<int> EliminarSalidaAsync(int idServicio)
        {
            using var cn = Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var n = await cn.ExecuteAsync(@"
                DELETE FROM Eurovip.servicio_itinerario  WHERE id_servicio = @idServicio;
                DELETE FROM Eurovip.servicio_tasa        WHERE id_Servicio = @idServicio;
                DELETE FROM Eurovip.servicio_nota        WHERE id_servicio = @idServicio;
                DELETE FROM Eurovip.alojamiento_circuito WHERE id_servicio = @idServicio;
                DELETE FROM Eurovip.servicio_circuito    WHERE id_servicio = @idServicio;
                DELETE FROM Eurovip.servicio WHERE id_servicio = @idServicio AND id_tipo_servicio = 'CIRCUITO';", new { idServicio }, tx);
            tx.Commit();
            return n;
        }

        // ------------------------------------------------------------------ categorías

        public async Task<IEnumerable<CategoriaDto>> CategoriasAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<CategoriaDto>("SELECT categoria AS Categoria, descripcion AS Descripcion FROM Eurovip.categoria ORDER BY categoria");
        }

        public async Task<string?> GuardarCategoriaAsync(CategoriaDto dto)
        {
            using var cn = Conexion();
            var cat = dto.Categoria.Trim();
            var original = string.IsNullOrWhiteSpace(dto.Original) ? null : dto.Original.Trim();
            if ((original is null || !original.Equals(cat, StringComparison.OrdinalIgnoreCase)) &&
                await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.categoria WHERE categoria = @cat", new { cat }) > 0)
                return $"La categoría \"{cat}\" ya existe.";

            try
            {
                if (original is null)
                    await cn.ExecuteAsync("INSERT INTO Eurovip.categoria (categoria, descripcion) VALUES (@cat, @desc)", new { cat, desc = Nulo(dto.Descripcion) });
                else
                    await cn.ExecuteAsync("UPDATE Eurovip.categoria SET categoria = @cat, descripcion = @desc WHERE categoria = @original",
                        new { cat, desc = Nulo(dto.Descripcion), original });
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return "No se puede renombrar: la categoría ya se usa en salidas cargadas.";
            }
            return null;
        }

        public async Task<string?> EliminarCategoriaAsync(string categoria)
        {
            using var cn = Conexion();
            var usos = await cn.ExecuteScalarAsync<int>(@"
                SELECT (SELECT COUNT(*) FROM Eurovip.servicio WHERE categoria_circuito = @categoria)
                     + (SELECT COUNT(*) FROM Eurovip.servicio_circuito_alojamiento WHERE categoria = @categoria)", new { categoria });
            if (usos > 0) return $"No se puede eliminar: \"{categoria}\" está en uso ({usos} registros).";
            await cn.ExecuteAsync("DELETE FROM Eurovip.categoria WHERE categoria = @categoria", new { categoria });
            return null;
        }

        // ------------------------------------------------------------------ búsquedas

        public async Task<IEnumerable<Opcion>> ProveedoresAsync(string? tipo, string? q)
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Opcion>(@"
                SELECT TOP 60 id_proveedor AS Valor, nombre AS Texto, id_tipo_proveedor AS Extra
                FROM Eurovip.proveedor
                WHERE id_proveedor <> '0' AND LTRIM(nombre) NOT LIKE 'Elegir%'   -- filas de relleno de ViajeVDT
                  AND (@tipo IS NULL OR id_tipo_proveedor = @tipo)
                  AND (@q IS NULL OR nombre LIKE '%' + @q + '%' OR id_proveedor = @q)
                ORDER BY CASE WHEN nombre LIKE @q + '%' THEN 0 ELSE 1 END, nombre",
                new { tipo = Nulo(tipo), q = Nulo(q) });
        }

        public async Task<IEnumerable<Opcion>> PaisesAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Opcion>(
                "SELECT id_pais AS Valor, nombre AS Texto, nombre_continente AS Extra FROM Eurovip.pais WHERE id_idioma = @Idioma ORDER BY CASE id_pais WHEN 'DO' THEN 0 ELSE 1 END, nombre",
                new { Idioma });
        }

        public async Task<IEnumerable<Opcion>> DestinosAsync(string? q, string? pais)
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Opcion>(@"
                SELECT TOP 50 d.id_destino AS Valor, d.id_destino AS Texto, d.id_pais AS Extra, p.nombre AS Extra2
                FROM Eurovip.destino d LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = d.id_idioma
                WHERE d.id_idioma = @Idioma AND (@pais IS NULL OR d.id_pais = @pais) AND (@q IS NULL OR d.id_destino LIKE '%' + @q + '%')
                ORDER BY CASE WHEN d.id_destino LIKE @q + '%' THEN 0 ELSE 1 END, d.id_destino",
                new { Idioma, q = Nulo(q), pais = Nulo(pais) });
        }

        public async Task<IEnumerable<CircuitoHotelDto>> HotelesAsync(string? q, string? destino, string? pais)
        {
            using var cn = Conexion();
            return await cn.QueryAsync<CircuitoHotelDto>(@"
                SELECT TOP 40 a.id_alojamiento AS Id_Alojamiento, a.nombre AS Nombre, a.id_destino AS Destino, a.categoria AS Estrellas, a.caratula AS Caratula
                FROM Eurovip.alojamiento a
                WHERE a.id_idioma = @Idioma AND a.activo = 1
                  AND (@q IS NULL OR a.nombre LIKE '%' + @q + '%' OR CAST(a.id_alojamiento AS varchar(20)) = @q)
                  AND (@destino IS NULL OR a.id_destino = @destino)
                  AND (@pais IS NULL OR a.id_pais = @pais)
                ORDER BY CASE WHEN a.nombre LIKE @q + '%' THEN 0 ELSE 1 END, a.nombre",
                new { Idioma, q = Nulo(q), destino = Nulo(destino), pais = Nulo(pais) });
        }

        public async Task<IEnumerable<Opcion>> AeropuertosAsync(string? q, string? pais)
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Opcion>(@"
                SELECT TOP 40 e.id_estacion AS Valor, e.nombre AS Texto, e.id_destino AS Extra, e.id_pais AS Extra2
                FROM Eurovip.estacion e
                WHERE e.id_idioma = @Idioma AND e.id_tipo_estacion = 'AEROPUERTO'
                  AND (@pais IS NULL OR e.id_pais = @pais)
                  AND (@q IS NULL OR e.id_estacion = @q OR e.nombre LIKE '%' + @q + '%' OR e.id_destino LIKE '%' + @q + '%')
                ORDER BY CASE WHEN e.id_estacion = @q THEN 0 WHEN e.id_destino LIKE @q + '%' THEN 1 ELSE 2 END, e.id_destino",
                new { Idioma, q = Nulo(q), pais = Nulo(pais) });
        }

        public async Task<IEnumerable<Opcion>> CampanyasAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Opcion>(@"
                SELECT CAST(id_campanya AS varchar(10)) AS Valor, nombre AS Texto, id_tipo_servicio AS Extra
                FROM Eurovip.campanya WHERE id_idioma = @Idioma
                ORDER BY CASE WHEN id_tipo_servicio = 'CIRCUITO' THEN 0 ELSE 1 END, nombre", new { Idioma });
        }

        public async Task<IEnumerable<Opcion>> TasasAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Opcion>("SELECT id_tasa AS Valor, id_tasa AS Texto FROM Eurovip.tasa WHERE id_idioma = @Idioma ORDER BY id_tasa", new { Idioma });
        }

        public async Task<IEnumerable<Opcion>> RegimenesAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Opcion>("SELECT id_regimen AS Valor, descripcion AS Texto FROM Eurovip.regimen WHERE id_idioma = @Idioma ORDER BY id_regimen", new { Idioma });
        }

        public async Task<IEnumerable<Opcion>> MonedasAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<Opcion>("SELECT id_moneda AS Valor, nombre AS Texto FROM Eurovip.moneda ORDER BY id_moneda");
        }

        public async Task<decimal> PrecioBaseMinimoAsync()
        {
            using var cn = Conexion();
            var v = await cn.ExecuteScalarAsync<string?>("SELECT valor FROM Eurovip.config_sistema WHERE clave = 'precio_base_minimo'");
            return decimal.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 300m;
        }

        public async Task AuditarAsync(string tipo, IEnumerable<int> ids, object cambios, string usuario, string? ip, string? navegador)
        {
            using var cn = Conexion();
            var lista = ids.ToList();
            await cn.ExecuteAsync(@"
                INSERT INTO Eurovip.auditoria_consulta_oferta (ids_servicio, cantidad_servicios, tipo_mantenimiento, usuario, cambios_realizados, ip_usuario, navegador)
                VALUES (@ids, @n, @tipo, @usuario, @cambios, @ip, @navegador)",
                new
                {
                    ids = JsonSerializer.Serialize(lista),
                    n = lista.Count,
                    tipo,
                    usuario,
                    cambios = JsonSerializer.Serialize(cambios),
                    ip,
                    navegador = navegador is { Length: > 200 } ? navegador[..200] : navegador
                });
        }

        private static string? Nulo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        private static string? Corta(string? s, int max) => s is null ? null : (s.Length > max ? s[..max] : s);
    }
}
