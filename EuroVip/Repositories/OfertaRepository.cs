using Dapper;
using EuroVip.Models;
using System.Data;
using System.Data.SqlClient;

namespace EuroVip.Repositories
{
    public interface IOfertaRepository
    {
        Task<(IEnumerable<OfertaItem> Items, int Total)> ListarAsync(OfertaFiltro f);
        Task<OfertaEstadisticas> EstadisticasAsync();
        Task<OfertaDetalle?> ObtenerAsync(int id);
        Task<string?> EditarAsync(OfertaEditarDto dto);
        Task<int> LoteAsync(OfertaLoteDto dto);
        Task<int> EliminarAsync(IEnumerable<int> ids);
        Task<IEnumerable<OfertaHabitacionDetalle>> HabitacionesAsync(int idAlojamiento);
        Task<IEnumerable<Opcion>> DestinosConOfertasAsync();
        Task RefrescarVitrinaAsync(IEnumerable<VitrinaClave> claves);
        Task<List<VitrinaClave>> ClavesVitrinaAsync(SqlConnection cn, SqlTransaction? tx, IEnumerable<int> ids);
    }

    public record VitrinaClave(short Id_Campanya, string Id_Moneda, string Id_Pais_Origen, string Origen, string Id_Pais, string Id_Destino);

    /// <summary>
    /// Ofertas de hotel cargadas (servicio tipo OFERTA) en el esquema Eurovip: buscar, editar, activar/desactivar y eliminar.
    /// Corrige lo que en BackTiTan afectaba a otras ofertas: aquí cada cambio toca SOLO las salidas elegidas.
    /// </summary>
    public class OfertaRepository : IOfertaRepository
    {
        public const string Tipo = "OFERTA";
        private const string Idioma = "ES";
        private readonly ICircuitoRepository _db;
        private readonly ILogger<OfertaRepository> _logger;

        public OfertaRepository(ICircuitoRepository db, ILogger<OfertaRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        private const string Base = @"
            FROM Eurovip.servicio s
            JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.secuencia = 1
            JOIN Eurovip.alojamiento a       ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
            LEFT JOIN Eurovip.regimen r      ON r.id_regimen = sd.id_regimen AND r.id_idioma = s.id_idioma
            LEFT JOIN Eurovip.campanya ca    ON ca.id_campanya = s.id_campanya AND ca.id_idioma = s.id_idioma
            LEFT JOIN Eurovip.proveedor p    ON p.id_proveedor = s.id_Proveedor
            LEFT JOIN Eurovip.servicio_itinerario v ON v.id_servicio = s.id_servicio AND v.segmento = 1
            LEFT JOIN Eurovip.servicio_origen so    ON so.id_servicio = s.id_servicio
            LEFT JOIN Eurovip.servicio_destino sdx  ON sdx.id_servicio = s.id_servicio   -- destino de la oferta (FASE 11)
            WHERE s.id_tipo_servicio = 'OFERTA' AND s.id_idioma = 'ES'";

        private const string Columnas = @"
            s.id_servicio AS Id_Servicio, s.nombre AS Nombre, a.id_alojamiento AS Id_Alojamiento, a.nombre AS Hotel, a.caratula AS Caratula,
            COALESCE(sdx.destino, a.id_destino) AS Destino, COALESCE(sdx.id_pais_destino, a.id_pais) AS Id_Pais, sd.id_regimen AS Id_Regimen, r.descripcion AS Regimen,
            sd.desde AS Desde, sd.hasta AS Hasta, ISNULL(sd.noches, s.noches) AS Noches, s.id_campanya AS Id_Campanya, ca.nombre AS Campanya,
            s.id_moneda AS Id_Moneda, s.precio_base AS Precio_Base, s.descuento AS Descuento, Eurovip.fnTotalTasa(s.id_servicio) AS Tasas,
            Eurovip.fnPrecioWeb(s.id_servicio) AS PrecioWeb, COALESCE(v.origen, so.origen) AS Origen,
            (SELECT COUNT(*) FROM Eurovip.servicio_itinerario x WHERE x.id_servicio = s.id_servicio) AS Vuelos,
            (SELECT COUNT(*) FROM Eurovip.servicio_habitacion h WHERE h.id_servicio = s.id_servicio) AS Habitaciones,
            s.activo AS Activo, s.codigo AS Codigo, s.usuario AS Usuario, p.nombre AS Proveedor";

        public async Task<(IEnumerable<OfertaItem> Items, int Total)> ListarAsync(OfertaFiltro f)
        {
            var tam = Math.Clamp(f.Tam, 10, 100);
            var pagina = Math.Max(1, f.Pagina);
            var p = new DynamicParameters();
            var where = new List<string>();
            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                where.Add("(a.nombre LIKE @q OR COALESCE(sdx.destino, a.id_destino) LIKE @q OR s.codigo LIKE @q OR s.nombre LIKE @q OR ca.nombre LIKE @q OR CAST(s.id_servicio AS varchar(12)) = @qx OR CAST(a.id_alojamiento AS varchar(12)) = @qx)");
                p.Add("q", $"%{f.Q.Trim()}%"); p.Add("qx", f.Q.Trim());
            }
            if (!string.IsNullOrWhiteSpace(f.Destino)) { where.Add("COALESCE(sdx.destino, a.id_destino) = @destino"); p.Add("destino", f.Destino); }
            if (f.Campanya is > 0) { where.Add("s.id_campanya = @campanya"); p.Add("campanya", f.Campanya); }
            if (f.Estado is 0 or 1) { where.Add("s.activo = @estado"); p.Add("estado", f.Estado == 1); }
            if (f.Vuelo == 1) where.Add("v.id_servicio IS NOT NULL");
            if (f.Vuelo == 0) where.Add("v.id_servicio IS NULL");
            if (f.Desde is not null) { where.Add("sd.desde >= @desde"); p.Add("desde", f.Desde.Value.Date); }
            if (f.Hasta is not null) { where.Add("sd.desde <= @hasta"); p.Add("hasta", f.Hasta.Value.Date); }
            if (!f.Pasadas && f.Desde is null) where.Add("sd.desde >= CAST(GETDATE() AS date)");
            var orden = f.Orden switch
            {
                "precio" => "Eurovip.fnPrecioWeb(s.id_servicio), sd.desde",
                "hotel" => "a.nombre, sd.desde",
                "recientes" => "s.id_servicio DESC",
                _ => "sd.desde, a.nombre, s.id_servicio"
            };
            var filtro = where.Count > 0 ? " AND " + string.Join(" AND ", where) : "";
            p.Add("offset", (pagina - 1) * tam); p.Add("tam", tam);

            using var cn = _db.Conexion();
            using var m = await cn.QueryMultipleAsync($@"
                SELECT COUNT(*) {Base} {filtro};
                SELECT {Columnas} {Base} {filtro} ORDER BY {orden} OFFSET @offset ROWS FETCH NEXT @tam ROWS ONLY;", p);
            var total = await m.ReadSingleAsync<int>();
            return (await m.ReadAsync<OfertaItem>(), total);
        }

        public async Task<OfertaEstadisticas> EstadisticasAsync()
        {
            using var cn = _db.Conexion();
            return await cn.QuerySingleAsync<OfertaEstadisticas>($@"
                SELECT COUNT(*) AS Total,
                       ISNULL(SUM(CASE WHEN s.activo = 1 THEN 1 ELSE 0 END), 0) AS Activas,
                       ISNULL(SUM(CASE WHEN s.activo = 1 AND sd.desde >= CAST(GETDATE() AS date) THEN 1 ELSE 0 END), 0) AS Futuras,
                       COUNT(DISTINCT a.id_alojamiento) AS Hoteles,
                       ISNULL(SUM(CASE WHEN v.id_servicio IS NOT NULL THEN 1 ELSE 0 END), 0) AS ConVuelo,
                       MIN(CASE WHEN s.activo = 1 AND sd.desde >= CAST(GETDATE() AS date) THEN Eurovip.fnPrecioWeb(s.id_servicio) END) AS PrecioDesde
                {Base}");
        }

        public async Task<OfertaDetalle?> ObtenerAsync(int id)
        {
            using var cn = _db.Conexion();
            var o = await cn.QueryFirstOrDefaultAsync<OfertaDetalle>(
                $"SELECT {Columnas}, s.id_Proveedor AS Id_Proveedor, COALESCE(v.id_pais_origen, so.id_pais_origen) AS Id_Pais_Origen {Base} AND s.id_servicio = @id", new { id });
            if (o is null) return null;
            using var m = await cn.QueryMultipleAsync(@"
                SELECT ah.id_habitacion AS Id_Habitacion, ah.nombre AS Nombre, ah.id_tipo_habitacion AS Id_Tipo_Habitacion, sh.precio AS Precio
                FROM Eurovip.servicio_habitacion sh
                JOIN Eurovip.alojamiento_habitacion ah ON ah.id_alojamiento = sh.id_alojamiento AND ah.id_habitacion = sh.id_habitacion AND ah.id_idioma = sh.id_idioma
                WHERE sh.id_servicio = @id ORDER BY sh.precio, ah.nombre;
                SELECT id_tasa AS Id_Tasa, valor AS Valor FROM Eurovip.servicio_tasa WHERE id_Servicio = @id ORDER BY id_tasa;
                SELECT x.Secuencia, x.Tipo, x.Nota1 FROM Eurovip.servicio s
                JOIN Eurovip.servicio_descripcion sd ON sd.codigo = s.codigo AND sd.id_idioma = s.id_idioma
                CROSS APPLY (VALUES (CAST(1 AS smallint), CAST('Incluye' AS varchar(20)), sd.incluye), (CAST(2 AS smallint), 'No incluye', sd.no_incluye),
                                    (CAST(3 AS smallint), 'Condiciones', sd.nota), (CAST(4 AS smallint), 'Oferta', sd.oferta_especial)) x (Secuencia, Tipo, Nota1)
                WHERE s.id_servicio = @id AND ISNULL(x.Nota1, '') <> '' ORDER BY x.Secuencia;
                SELECT segmento, id_proveedor, id_estacion_origen, id_estacion_destino, vuelo, fecha_salida, hora_salida, fecha_regreso, hora_regreso
                FROM Eurovip.servicio_itinerario WHERE id_servicio = @id ORDER BY segmento;", new { id });
            o.HabitacionesDetalle = (await m.ReadAsync<OfertaHabitacionDetalle>()).ToList();
            o.TasasDetalle = (await m.ReadAsync<OfertaTasa>()).ToList();
            o.Notas = (await m.ReadAsync<OfertaNota>()).ToList();
            o.VuelosDetalle = (await m.ReadAsync<OfertaVuelo>()).ToList();
            return o;
        }

        /// <summary>Edita UNA salida: precio, descuento, campaña, régimen, fechas, habitaciones y suplementos.</summary>
        public async Task<string?> EditarAsync(OfertaEditarDto d)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var actual = await cn.QueryFirstOrDefaultAsync<(int Aloj, short Camp)>(
                "SELECT sd.id_alojamiento, s.id_campanya FROM Eurovip.servicio s JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.secuencia = 1 WHERE s.id_servicio = @id AND s.id_tipo_servicio = 'OFERTA'",
                new { id = d.Id_Servicio }, tx);
            if (actual.Aloj == 0) return "Esa oferta ya no existe.";
            if (d.Habitaciones.Count == 0) return "Deja al menos una habitación.";
            if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.campanya WHERE id_campanya = @c AND id_idioma = 'ES'", new { c = d.Id_Campanya }, tx) == 0)
                return "Esa campaña no existe.";

            var claves = await ClavesVitrinaAsync(cn, tx, new[] { d.Id_Servicio });
            var tasas = d.Suplementos.Sum(x => x.Valor);
            await cn.ExecuteAsync(@"
                UPDATE Eurovip.servicio SET precio_base = @pb, descuento = @desc, id_campanya = @camp, activo = @activo, noches = @noches WHERE id_servicio = @id;
                UPDATE Eurovip.servicio_detalle SET id_regimen = @reg, desde = @desde, hasta = DATEADD(DAY, @noches, @desde), noches = @noches
                WHERE id_servicio = @id AND secuencia = 1;
                DELETE FROM Eurovip.servicio_habitacion WHERE id_servicio = @id;
                DELETE FROM Eurovip.servicio_tasa WHERE id_Servicio = @id;",
                new { id = d.Id_Servicio, pb = d.PrecioTotal - tasas, desc = d.Descuento, camp = d.Id_Campanya, activo = d.Activo, noches = d.Noches, reg = d.Id_Regimen, desde = d.Desde.Date }, tx);
            foreach (var h in d.Habitaciones.DistinctBy(x => x.Id_Habitacion))
                await cn.ExecuteAsync("Eurovip.InsertarServicioHabitacion",
                    new { Id_Servicio = d.Id_Servicio, Secuencia = 1, Id_Alojamiento = actual.Aloj, Id_Idioma = Idioma, h.Id_Habitacion, h.Precio }, tx, commandType: CommandType.StoredProcedure);
            foreach (var s in d.Suplementos.Where(x => !string.IsNullOrWhiteSpace(x.Id_Tasa)).DistinctBy(x => x.Id_Tasa))
                await cn.ExecuteAsync("Eurovip.spInsertaServicioSuplemento",
                    new { numeroServicio = d.Id_Servicio, nombreSuplemento = s.Id_Tasa, codigoIdioma = Idioma, valor = s.Valor }, tx, commandType: CommandType.StoredProcedure);
            // Si cambia la fecha, los vuelos se mueven igual (mismo desfase de días)
            await cn.ExecuteAsync(@"
                DECLARE @dif int = DATEDIFF(DAY, (SELECT MIN(fecha_salida) FROM Eurovip.servicio_itinerario WHERE id_servicio = @id), @desde);
                IF @dif IS NOT NULL AND @dif <> 0
                    UPDATE Eurovip.servicio_itinerario SET fecha_salida = DATEADD(DAY, @dif, fecha_salida), fecha_regreso = DATEADD(DAY, @dif, fecha_regreso) WHERE id_servicio = @id;",
                new { id = d.Id_Servicio, desde = d.Desde.Date }, tx);
            claves.AddRange(await ClavesVitrinaAsync(cn, tx, new[] { d.Id_Servicio }));
            tx.Commit();
            await RefrescarVitrinaAsync(claves);
            return null;
        }

        public async Task<int> LoteAsync(OfertaLoteDto d)
        {
            if (d.Ids.Count == 0) return 0;
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            var claves = await ClavesVitrinaAsync(cn, null, d.Ids);
            var n = await cn.ExecuteAsync(@"
                UPDATE Eurovip.servicio SET
                    activo = COALESCE(@activo, activo),
                    precio_base = precio_base + ISNULL(@ajuste, 0),
                    descuento = COALESCE(@descuento, descuento)
                WHERE id_servicio IN @ids AND id_tipo_servicio = 'OFERTA'",
                new { activo = d.Activo, ajuste = d.AjustePrecio, descuento = d.Descuento, ids = d.Ids });
            await RefrescarVitrinaAsync(claves);
            return n;
        }

        public async Task<int> EliminarAsync(IEnumerable<int> ids)
        {
            var lista = ids.Distinct().ToList();
            if (lista.Count == 0) return 0;
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            var claves = await ClavesVitrinaAsync(cn, null, lista);
            using var tx = cn.BeginTransaction();
            var validos = (await cn.QueryAsync<int>("SELECT id_servicio FROM Eurovip.servicio WHERE id_servicio IN @lista AND id_tipo_servicio = 'OFERTA'", new { lista }, tx)).ToList();
            foreach (var id in validos)
                await cn.ExecuteAsync("Eurovip.spEliminaServicio", new { idServicio = id }, tx, commandType: CommandType.StoredProcedure);
            tx.Commit();
            await RefrescarVitrinaAsync(claves);
            return validos.Count;
        }

        public async Task<IEnumerable<OfertaHabitacionDetalle>> HabitacionesAsync(int idAlojamiento)
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<OfertaHabitacionDetalle>(@"
                SELECT id_habitacion AS Id_Habitacion, nombre AS Nombre, id_tipo_habitacion AS Id_Tipo_Habitacion, precio AS Precio
                FROM Eurovip.alojamiento_habitacion WHERE id_alojamiento = @idAlojamiento AND id_idioma = 'ES' ORDER BY id_habitacion",
                new { idAlojamiento });
        }

        public async Task<IEnumerable<Opcion>> DestinosConOfertasAsync()
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<Opcion>($@"
                SELECT COALESCE(sdx.destino, a.id_destino) AS Valor, COALESCE(sdx.destino, a.id_destino) AS Texto, CAST(COUNT(*) AS varchar(10)) AS Extra {Base}
                GROUP BY COALESCE(sdx.destino, a.id_destino) ORDER BY COALESCE(sdx.destino, a.id_destino)");
        }

        public async Task<List<VitrinaClave>> ClavesVitrinaAsync(SqlConnection cn, SqlTransaction? tx, IEnumerable<int> ids) =>
            (await cn.QueryAsync<VitrinaClave>(@"
                SELECT DISTINCT s.id_campanya AS Id_Campanya, s.id_moneda AS Id_Moneda,
                       COALESCE(v.id_pais_origen, so.id_pais_origen) AS Id_Pais_Origen, COALESCE(v.origen, so.origen) AS Origen,
                       a.id_pais AS Id_Pais, a.id_destino AS Id_Destino
                FROM Eurovip.servicio s
                JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.secuencia = 1
                JOIN Eurovip.alojamiento a ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
                LEFT JOIN Eurovip.servicio_itinerario v ON v.id_servicio = s.id_servicio AND v.segmento = 1
                LEFT JOIN Eurovip.servicio_origen so ON so.id_servicio = s.id_servicio
                WHERE s.id_servicio IN @ids AND s.id_tipo_servicio = 'OFERTA'
                  AND COALESCE(v.origen, so.origen) IS NOT NULL", new { ids = ids.ToList() }, tx)).ToList();

        /// <summary>Recalcula la vitrina web de hoteles para cada combinación afectada (fuera de transacción).</summary>
        public async Task RefrescarVitrinaAsync(IEnumerable<VitrinaClave> claves)
        {
            using var cn = _db.Conexion();
            foreach (var k in claves.Distinct())
            {
                try
                {
                    await cn.ExecuteAsync("Eurovip.spActualizaAlojamientoDestino", new
                    {
                        idIdioma = Idioma, idTipoServicio = Tipo, idCampanya = k.Id_Campanya, idMoneda = k.Id_Moneda,
                        idPaisOrigen = k.Id_Pais_Origen, idOrigen = k.Origen, idPaisDestino = k.Id_Pais, idDestino = k.Id_Destino
                    }, commandType: CommandType.StoredProcedure);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "No se pudo refrescar la vitrina de hoteles {@Clave}", k); }
            }
        }
    }
}
