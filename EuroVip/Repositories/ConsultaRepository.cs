using Dapper;
using EuroVip.Models;
using System.Data;
using System.Data.SqlClient;

namespace EuroVip.Repositories
{
    public interface IConsultaRepository
    {
        Task<(List<ConsultaFila> Filas, ConsultaResumen Resumen)> BuscarAsync(ConsultaFiltro f);
        Task<List<ConsultaFila>> TodasAsync(ConsultaFiltro f, int max);
        Task<ConsultaDetalle?> DetalleAsync(int id);
        Task<int> EditarAsync(ConsultaEditarDto d);
        Task<List<int>> DuplicarAsync(ConsultaDuplicarDto d, string usuario);
        Task<int> TasaAsync(ConsultaTasaDto d);
        Task<IEnumerable<Opcion>> UsuariosAsync();
    }

    /// <summary>
    /// "Consultar ofertas" sobre [Eurovip]: busca con Eurovip.fnFiltro (ofertas y circuitos, con o sin vuelo),
    /// edita con Eurovip.spEditaServicio y duplica con Eurovip.spDuplicaServicio (copias de ViajeVDT, CAMBIO 38-40).
    /// Toda acción en masa va en UNA transacción y al terminar se refresca la vitrina web de lo afectado.
    /// </summary>
    public class ConsultaRepository : IConsultaRepository
    {
        public const int MaxMasivo = 2000;
        private readonly ICircuitoRepository _db;
        private readonly IOfertaRepository _ofertas;
        private readonly ILogger<ConsultaRepository> _logger;

        public ConsultaRepository(ICircuitoRepository db, IOfertaRepository ofertas, ILogger<ConsultaRepository> logger)
        {
            _db = db;
            _ofertas = ofertas;
            _logger = logger;
        }

        private static (string Sql, DynamicParameters P) Consulta(ConsultaFiltro f)
        {
            var p = new DynamicParameters();
            p.Add("idServicio", f.Id_Servicio); p.Add("idTipo", Nulo(f.Tipo)); p.Add("moneda", Nulo(f.Moneda));
            p.Add("camp", f.Campanyas.Count == 1 ? f.Campanyas[0] : (short?)null);
            p.Add("prov", Nulo(f.Proveedor)); p.Add("po", Nulo(f.PaisOrigen)); p.Add("ori", Nulo(f.Origen));
            p.Add("pd", Nulo(f.PaisDestino)); p.Add("des", Nulo(f.Destino));
            p.Add("ini", f.InicioDesde?.Date ?? (f.Pasadas || f.Id_Servicio is not null ? null : DateTime.Today)); p.Add("fin", f.InicioHasta?.Date);
            p.Add("apD", f.AperturaDesde?.Date); p.Add("apH", f.AperturaHasta?.Date); p.Add("usu", Nulo(f.Usuario));
            p.Add("pD", f.PrecioDesde); p.Add("pH", f.PrecioHasta); p.Add("dD", f.DescuentoDesde); p.Add("dH", f.DescuentoHasta);
            p.Add("aloj", f.Id_Alojamiento); p.Add("noc", f.Noches); p.Add("act", f.Activo); p.Add("cod", Nulo(f.Codigo));

            var w = new List<string>();
            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                w.Add("(x.destino LIKE @q OR x.nombreAlojamiento LIKE @q OR x.nombre_circuito LIKE @q OR x.codigo LIKE @q OR x.origen LIKE @q OR CAST(x.id_servicio AS varchar(12)) = @qExacto)");
                p.Add("q", $"%{f.Q.Trim()}%"); p.Add("qExacto", f.Q.Trim());
            }
            if (f.Campanyas.Count > 1) { w.Add("x.id_campanya IN @camps"); p.Add("camps", f.Campanyas.Distinct().ToArray()); }
            if (!string.IsNullOrWhiteSpace(f.Regimen)) { w.Add("(x.regimen LIKE @reg OR x.id_regimen = @regExacto)"); p.Add("reg", f.Regimen.Trim() + "%"); p.Add("regExacto", f.Regimen.Trim()); }
            if (f.Mes is >= 1 and <= 12) { w.Add("MONTH(x.inicio) = @mes"); p.Add("mes", f.Mes); }
            var dias = f.Dias.Where(d => d is >= 0 and <= 6).Distinct().ToArray();
            if (dias.Length is > 0 and < 7) { w.Add("((DATEPART(WEEKDAY, x.inicio) + @@DATEFIRST - 1) % 7) IN @dias"); p.Add("dias", dias); }
            if (f.ConVuelo is not null) { w.Add("x.conVuelo = @cv"); p.Add("cv", f.ConVuelo.Value); }

            var sql = @"FROM Eurovip.fnFiltro(@idServicio, 'ES', @idTipo, @moneda, @camp, @prov, @po, @ori, @pd, @des, @ini, @fin, @apD, @apH, @usu, @pD, @pH, @dD, @dH, @aloj, @noc, @act, @cod) x"
                      + (w.Count > 0 ? " WHERE " + string.Join(" AND ", w) : "");
            return (sql, p);
        }

        private static string Orden(string o) => o switch
        {
            "precio" => "x.precioWeb, x.inicio",
            "destino" => "x.destino, x.inicio",
            "recientes" => "x.id_servicio DESC",
            _ => "x.inicio, x.destino, x.id_servicio"
        };

        private const string Cols = @"x.id_servicio AS Id_Servicio, x.id_tipo_servicio AS Id_Tipo_Servicio, x.id_moneda AS Id_Moneda, x.id_campanya AS Id_Campanya, x.codigo AS Codigo,
            x.nombreCampanya AS NombreCampanya, x.origen AS Origen, x.id_pais_origen AS Id_Pais_Origen, x.destino AS Destino, x.id_pais_destino AS Id_Pais_Destino,
            x.inicio AS Inicio, x.regreso AS Regreso, x.noches AS Noches, x.codigoOperador AS CodigoOperador, x.nombreProveedor AS NombreProveedor, x.apertura AS Apertura,
            x.usuario AS Usuario, x.nota AS Nota, x.activo AS Activo, x.id_alojamiento AS Id_Alojamiento, x.nombreAlojamiento AS NombreAlojamiento, x.regimen AS Regimen,
            x.id_regimen AS Id_Regimen, x.habitacion AS Habitacion, x.nombre_circuito AS Nombre_Circuito, x.conVuelo AS ConVuelo, x.codigoAerolinea AS CodigoAerolinea,
            x.nombreAerolinea AS NombreAerolinea, x.vuelo AS Vuelo, x.id_estacion_origen AS Id_Estacion_Origen, x.id_estacion_destino AS Id_Estacion_Destino,
            x.precio_base AS Precio_Base, x.descuento AS Descuento, x.precio_mas_menos AS Precio_Mas_Menos, x.totalTasa AS TotalTasa, x.precioWeb AS PrecioWeb";

        public async Task<(List<ConsultaFila> Filas, ConsultaResumen Resumen)> BuscarAsync(ConsultaFiltro f)
        {
            var tam = Math.Clamp(f.Tam, 10, 200);
            var pag = Math.Max(1, f.Pagina);
            var (from, p) = Consulta(f);
            p.Add("salto", (pag - 1) * tam); p.Add("tam", tam);
            using var cn = _db.Conexion();
            var resumen = await cn.QueryFirstAsync<ConsultaResumen>($@"
                SELECT COUNT(*) AS Total, SUM(CASE WHEN x.id_tipo_servicio = 'OFERTA' THEN 1 ELSE 0 END) AS Ofertas,
                       SUM(CASE WHEN x.id_tipo_servicio = 'CIRCUITO' THEN 1 ELSE 0 END) AS Circuitos, SUM(CASE WHEN x.activo = 1 THEN 1 ELSE 0 END) AS Activas,
                       SUM(CASE WHEN x.conVuelo = 1 THEN 1 ELSE 0 END) AS ConVuelo, MIN(x.precioWeb) AS PrecioMin, MAX(x.precioWeb) AS PrecioMax,
                       MIN(x.inicio) AS PrimeraEntrada, MAX(x.inicio) AS UltimaEntrada {from}", p, commandTimeout: 120);
            var filas = (await cn.QueryAsync<ConsultaFila>($"SELECT {Cols} {from} ORDER BY {Orden(f.Orden)} OFFSET @salto ROWS FETCH NEXT @tam ROWS ONLY", p, commandTimeout: 120)).ToList();
            return (filas, resumen);
        }

        /// <summary>Todas las filas (para exportar o para "seleccionar todo lo filtrado"), con tope.</summary>
        public async Task<List<ConsultaFila>> TodasAsync(ConsultaFiltro f, int max)
        {
            var (from, p) = Consulta(f);
            p.Add("max", max);
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<ConsultaFila>($"SELECT TOP (@max) {Cols} {from} ORDER BY {Orden(f.Orden)}", p, commandTimeout: 180)).ToList();
        }

        public async Task<ConsultaDetalle?> DetalleAsync(int id)
        {
            var f = (await TodasAsync(new ConsultaFiltro { Id_Servicio = id, Activo = null, Pasadas = true }, 1)).FirstOrDefault();
            if (f is null) return null;
            using var cn = _db.Conexion();
            using var m = await cn.QueryMultipleAsync(@"
                SELECT ah.id_habitacion AS Id_Habitacion, ah.nombre AS Nombre, ah.id_tipo_habitacion AS Id_Tipo_Habitacion, sh.precio AS Precio
                FROM Eurovip.servicio_habitacion sh
                JOIN Eurovip.alojamiento_habitacion ah ON ah.id_alojamiento = sh.id_alojamiento AND ah.id_habitacion = sh.id_habitacion AND ah.id_idioma = sh.id_idioma
                WHERE sh.id_servicio = @id ORDER BY sh.precio, ah.nombre;
                SELECT id_tasa AS Id_Tasa, valor AS Valor FROM Eurovip.servicio_tasa WHERE id_Servicio = @id ORDER BY id_tasa;
                SELECT secuencia AS Secuencia, tipo AS Tipo, nota1 AS Nota1 FROM Eurovip.servicio_nota WHERE id_servicio = @id ORDER BY secuencia;
                SELECT segmento, id_proveedor, id_estacion_origen, id_estacion_destino, vuelo, fecha_salida, hora_salida, fecha_regreso, hora_regreso
                FROM Eurovip.servicio_itinerario WHERE id_servicio = @id ORDER BY segmento;", new { id });
            return new ConsultaDetalle
            {
                Fila = f,
                Habitaciones = (await m.ReadAsync<OfertaHabitacionDetalle>()).ToList(),
                Tasas = (await m.ReadAsync<OfertaTasa>()).ToList(),
                Notas = (await m.ReadAsync<OfertaNota>()).ToList(),
                Vuelos = (await m.ReadAsync<OfertaVuelo>()).ToList()
            };
        }

        // ---------------------------------------------------------------- acciones en masa
        public async Task<int> EditarAsync(ConsultaEditarDto d)
        {
            var ids = d.Ids.Distinct().Take(MaxMasivo).ToList();
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            var antes = await ClavesAsync(cn, null, ids);
            using (var tx = cn.BeginTransaction())
            {
                foreach (var id in ids)
                {
                    decimal? precio = d.PrecioBase;
                    if (precio is null && d.SumarPrecio is not null and not 0)
                        precio = await cn.ExecuteScalarAsync<decimal>("SELECT precio_base + @s FROM Eurovip.servicio WHERE id_servicio = @id", new { s = d.SumarPrecio, id }, tx);
                    await cn.ExecuteAsync("Eurovip.spEditaServicio", new
                    {
                        numeroServicio = id, idMoneda = Nulo(d.Moneda), idCampanya = d.Id_Campanya, idProveedor = Nulo(d.Proveedor),
                        precioBase = precio, descuento = d.Descuento, precioMasMenos = d.PrecioMasMenos, activo = d.Activo, idAerolinea = Nulo(d.Aerolinea)
                    }, tx, commandType: CommandType.StoredProcedure);
                }
                tx.Commit();
            }
            await RefrescarAsync(cn, ids, antes);
            return ids.Count;
        }

        public async Task<List<int>> DuplicarAsync(ConsultaDuplicarDto d, string usuario)
        {
            var ids = d.Ids.Distinct().ToList();
            var veces = Math.Clamp(d.Veces, 1, 52);
            if (ids.Count * veces > MaxMasivo) throw new InvalidOperationException($"Son demasiadas copias ({ids.Count * veces}). El máximo es {MaxMasivo} por vez.");
            var nuevos = new List<int>();
            using var cn = _db.Conexion();
            await cn.OpenAsync();

            // Ninguna copia puede quedar con salida de hoy o pasada (no se vendería ni saldría en la web).
            var primera = await cn.ExecuteScalarAsync<DateTime?>(@"
                SELECT MIN(f) FROM (SELECT sd.desde AS f FROM Eurovip.servicio_detalle sd WHERE sd.id_servicio IN @ids
                                    UNION ALL SELECT sc.desde FROM Eurovip.servicio_circuito sc WHERE sc.id_servicio IN @ids) x", new { ids });
            if (primera is not null && primera.Value.Date.AddDays(Math.Min(d.Dias, d.Dias * veces)) <= DateTime.Today)
                throw new InvalidOperationException($"Alguna copia quedaría con salida de hoy o pasada ({primera.Value.Date.AddDays(Math.Min(d.Dias, d.Dias * veces)):dd/MM/yyyy}). " +
                    $"Las salidas tienen que ser desde mañana ({DateTime.Today.AddDays(1):dd/MM/yyyy}): cambia los días a mover.");

            using (var tx = cn.BeginTransaction())
            {
                foreach (var id in ids)
                    for (var v = 1; v <= veces; v++)
                    {
                        var p = new DynamicParameters(new
                        {
                            numeroServicio = id, idCampanya = d.Id_Campanya, diasDesplazar = d.Dias * v, idPaisOrigen = Nulo(d.PaisOrigen)?.ToUpperInvariant(),
                            origen = Nulo(d.Origen), codigoEstacionOrigen = Nulo(d.EstacionOrigen)?.ToUpperInvariant(), usuario = usuario.Length > 15 ? usuario[..15] : usuario
                        });
                        p.Add("nuevoId", dbType: DbType.Int32, direction: ParameterDirection.Output);
                        await cn.ExecuteAsync("Eurovip.spDuplicaServicio", p, tx, commandType: CommandType.StoredProcedure);
                        nuevos.Add(p.Get<int>("nuevoId"));
                    }
                tx.Commit();
            }
            await RefrescarAsync(cn, nuevos, new());
            return nuevos;
        }

        /// <summary>Pone (o quita) un suplemento/tasa en todos los servicios elegidos. El precio base no cambia: el precio web sube o baja.</summary>
        public async Task<int> TasaAsync(ConsultaTasaDto d)
        {
            var ids = d.Ids.Distinct().Take(MaxMasivo).ToList();
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using (var tx = cn.BeginTransaction())
            {
                foreach (var id in ids)
                {
                    await cn.ExecuteAsync("DELETE FROM Eurovip.servicio_tasa WHERE id_Servicio = @id AND id_tasa = @t", new { id, t = d.Id_Tasa }, tx);
                    if (!d.Quitar)
                        await cn.ExecuteAsync("INSERT INTO Eurovip.servicio_tasa (id_Servicio, id_tasa, id_idioma, valor) VALUES (@id, @t, 'ES', @v)", new { id, t = d.Id_Tasa, v = d.Valor }, tx);
                }
                tx.Commit();
            }
            await RefrescarAsync(cn, ids, new());
            return ids.Count;
        }

        public async Task<IEnumerable<Opcion>> UsuariosAsync()
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<Opcion>("SELECT DISTINCT usuario AS Valor, usuario AS Texto FROM Eurovip.servicio WHERE usuario IS NOT NULL ORDER BY 1");
        }

        // ---------------------------------------------------------------- vitrina web
        private record ClaveCircuito(short Camp, string Moneda, string PaisOrigen, string Origen, string PaisDestino);

        private async Task<(List<VitrinaClave> Ofertas, List<ClaveCircuito> Circuitos)> ClavesAsync(SqlConnection cn, SqlTransaction? tx, List<int> ids)
        {
            if (ids.Count == 0) return (new(), new());
            var of = await _ofertas.ClavesVitrinaAsync(cn, tx, ids);
            var ci = (await cn.QueryAsync<ClaveCircuito>(@"
                SELECT DISTINCT s.id_campanya AS Camp, s.id_moneda AS Moneda, sc.id_pais_origen AS PaisOrigen, sc.origen AS Origen, cd.id_pais AS PaisDestino
                FROM Eurovip.servicio s
                    JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
                    JOIN Eurovip.circuito_destino cd ON cd.nombre_circuito = sc.nombre_circuito AND cd.id_idioma = sc.id_idoma
                WHERE s.id_servicio IN @ids AND sc.origen IS NOT NULL", new { ids }, tx)).ToList();
            return (of, ci);
        }

        /// <summary>Refresca la vitrina de lo afectado (antes y después: si cambió la campaña, se limpia la vieja).</summary>
        private async Task RefrescarAsync(SqlConnection cn, List<int> ids, (List<VitrinaClave> Ofertas, List<ClaveCircuito> Circuitos) antes)
        {
            try
            {
                var despues = await ClavesAsync(cn, null, ids);
                var ofertas = (antes.Ofertas ?? new()).Concat(despues.Ofertas).Distinct().ToList();
                if (ofertas.Count > 0) await _ofertas.RefrescarVitrinaAsync(ofertas);
                foreach (var k in (antes.Circuitos ?? new()).Concat(despues.Circuitos).Distinct())
                    await cn.ExecuteAsync("Eurovip.spActualizaAlojamientoCircuito",
                        new { idIdioma = "ES", idTipoServicio = "CIRCUITO", idCampanya = k.Camp, idMoneda = k.Moneda, idPaisOrigen = k.PaisOrigen, idOrigen = k.Origen, idPaisDestino = k.PaisDestino },
                        commandType: CommandType.StoredProcedure, commandTimeout: 120);
            }
            catch (Exception ex)
            {
                // los cambios ya están guardados; la vitrina se puede refrescar luego desde la Home
                _logger.LogWarning(ex, "No se pudo refrescar la vitrina tras una acción de Consultar ofertas");
            }
        }

        private static string? Nulo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
