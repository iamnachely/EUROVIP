using Dapper;

namespace EuroVip.Repositories
{
    // ---------------- modelos ----------------
    public class CargaLog
    {
        public int Id { get; set; }
        public string Tipo_Carga { get; set; } = "";
        public string Usuario { get; set; } = "";
        public string? Ip { get; set; }
        public DateTime Fecha_Hora { get; set; }
        public DateTime? Fecha_Hora_Fin { get; set; }
        public string? Origen { get; set; }
        public string? Destino { get; set; }
        public string? Campanyas { get; set; }
        public int? Noches { get; set; }
        public DateTime? Fecha_Inicio { get; set; }
        public DateTime? Fecha_Fin { get; set; }
        public string? Codigo_Oferta { get; set; }
        public string? Estatus { get; set; }
        public string? Mensaje { get; set; }
        public int? Registros_Insertados { get; set; }
        public int? Registros_No_Insertados { get; set; }
        public int SalidasVivas { get; set; }
    }

    public class CargaFiltro
    {
        public string? Q { get; set; }
        public string? Estado { get; set; }
        public string? Tipo { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public int Pagina { get; set; } = 1;
        public int Tam { get; set; } = 20;
    }

    public class CargaKpis
    {
        public int Total { get; set; }
        public int Correctas { get; set; }
        public int ConErrores { get; set; }
        public int EnProceso { get; set; }
        public int SalidasCreadas { get; set; }
        public int Hoy { get; set; }
    }

    public class SalidaReporte
    {
        public int Id_Servicio { get; set; }
        public string Tipo { get; set; } = "";
        public string? Nombre { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public string? Campanya { get; set; }
        public string? Categoria { get; set; }
        public string? Origen { get; set; }
        public int Vuelos { get; set; }
        public decimal Precio_Base { get; set; }
        public decimal Descuento { get; set; }
        public decimal? PrecioWeb { get; set; }
        public string Id_Moneda { get; set; } = "";
        public bool Activo { get; set; }
        public string? Codigo { get; set; }
        public string? Usuario { get; set; }
        public DateTime Apertura { get; set; }
    }

    public class PuntoSerie
    {
        public string Etiqueta { get; set; } = "";
        public string? Clave { get; set; }
        public int Valor { get; set; }
        public decimal? Extra { get; set; }
    }

    public class ActividadItem
    {
        public int Id { get; set; }
        public string Tipo_Mantenimiento { get; set; } = "";
        public string Usuario { get; set; } = "";
        public DateTime Fecha_Hora { get; set; }
        public string? Ids_Servicio { get; set; }
        public int Cantidad_Servicios { get; set; }
    }

    public class ReporteTablero
    {
        public int HotelesVitrina { get; set; }
        public int HotelesVisibles { get; set; }
        public int Circuitos { get; set; }
        public int SalidasActivas { get; set; }
        public int SalidasProximas30 { get; set; }
        public int SalidasConVuelo { get; set; }
        public decimal? PrecioMedio { get; set; }
        public int CargasMes { get; set; }
        public List<PuntoSerie> SalidasPorMes { get; set; } = new();
        public List<PuntoSerie> TopCircuitos { get; set; } = new();
        public List<PuntoSerie> CargasPorDia { get; set; } = new();
        public List<PuntoSerie> ActividadPorUsuario { get; set; } = new();
        public List<ActividadItem> UltimosCambios { get; set; } = new();
    }

    public interface IReporteRepository
    {
        Task<(IEnumerable<CargaLog> Items, int Total)> CargasAsync(CargaFiltro f);
        Task<CargaKpis> CargaKpisAsync();
        Task<CargaLog?> CargaAsync(int id);
        Task<IEnumerable<SalidaReporte>> SalidasDeCargaAsync(string codigo);
        Task<IEnumerable<string>> TiposCargaAsync();
        Task<ReporteTablero> TableroAsync();
        Task<IEnumerable<SalidaReporte>> SalidasAsync(bool soloActivas, DateTime? desde, DateTime? hasta);
    }

    /// <summary>Historial de cargas (Eurovip.Log_Cargas) y tablero de reportes del BackOffice.</summary>
    public class ReporteRepository : IReporteRepository
    {
        private readonly ICircuitoRepository _db;
        public ReporteRepository(ICircuitoRepository db) => _db = db;

        public async Task<(IEnumerable<CargaLog> Items, int Total)> CargasAsync(CargaFiltro f)
        {
            var tam = Math.Clamp(f.Tam, 10, 100);
            var pagina = Math.Max(1, f.Pagina);
            var p = new DynamicParameters();
            var where = new List<string> { "1 = 1" };
            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                where.Add("(l.usuario LIKE @q OR l.destino LIKE @q OR l.origen LIKE @q OR l.codigo_oferta LIKE @q OR l.mensaje LIKE @q OR CAST(l.id AS varchar(12)) = @qx)");
                p.Add("q", $"%{f.Q.Trim()}%"); p.Add("qx", f.Q.Trim());
            }
            if (!string.IsNullOrWhiteSpace(f.Estado)) { where.Add("l.estatus = @estado"); p.Add("estado", f.Estado); }
            if (!string.IsNullOrWhiteSpace(f.Tipo)) { where.Add("l.tipo_carga = @tipo"); p.Add("tipo", f.Tipo); }
            if (f.Desde is not null) { where.Add("l.fecha_hora >= @desde"); p.Add("desde", f.Desde.Value.Date); }
            if (f.Hasta is not null) { where.Add("l.fecha_hora < @hasta"); p.Add("hasta", f.Hasta.Value.Date.AddDays(1)); }
            p.Add("offset", (pagina - 1) * tam); p.Add("tam", tam);
            var filtro = string.Join(" AND ", where);

            using var cn = _db.Conexion();
            using var m = await cn.QueryMultipleAsync($@"
                SELECT COUNT(*) FROM Eurovip.Log_Cargas l WHERE {filtro};
                SELECT l.id, l.tipo_carga, l.usuario, l.ip, l.fecha_hora, l.fecha_hora_fin, l.origen, l.destino, l.campanyas, l.noches,
                       l.fecha_inicio, l.fecha_fin, l.codigo_oferta, l.estatus, l.mensaje, l.registros_insertados, l.registros_no_insertados,
                       (SELECT COUNT(*) FROM Eurovip.servicio s WHERE s.codigo = l.codigo_oferta AND l.codigo_oferta IS NOT NULL) AS SalidasVivas
                FROM Eurovip.Log_Cargas l WHERE {filtro}
                ORDER BY l.id DESC OFFSET @offset ROWS FETCH NEXT @tam ROWS ONLY;", p);
            var total = await m.ReadSingleAsync<int>();
            return (await m.ReadAsync<CargaLog>(), total);
        }

        public async Task<CargaKpis> CargaKpisAsync()
        {
            using var cn = _db.Conexion();
            return await cn.QuerySingleAsync<CargaKpis>(@"
                SELECT COUNT(*) AS Total,
                       ISNULL(SUM(CASE WHEN estatus = N'Correcto' THEN 1 ELSE 0 END), 0) AS Correctas,
                       ISNULL(SUM(CASE WHEN estatus = N'Revisar' THEN 1 ELSE 0 END), 0) AS ConErrores,
                       ISNULL(SUM(CASE WHEN estatus = N'En proceso' THEN 1 ELSE 0 END), 0) AS EnProceso,
                       ISNULL(SUM(CASE WHEN estatus = N'Correcto' THEN registros_insertados ELSE 0 END), 0) AS SalidasCreadas,
                       ISNULL(SUM(CASE WHEN fecha_hora >= CAST(GETDATE() AS date) THEN 1 ELSE 0 END), 0) AS Hoy
                FROM Eurovip.Log_Cargas");
        }

        public async Task<CargaLog?> CargaAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<CargaLog>(@"
                SELECT l.*, (SELECT COUNT(*) FROM Eurovip.servicio s WHERE s.codigo = l.codigo_oferta AND l.codigo_oferta IS NOT NULL) AS SalidasVivas
                FROM Eurovip.Log_Cargas l WHERE l.id = @id", new { id });
        }

        private const string SelectSalidas = @"
            SELECT s.id_servicio AS Id_Servicio, s.id_tipo_servicio AS Tipo, s.nombre AS Nombre,
                   sc.desde AS Desde, sc.hasta AS Hasta,
                   ca.nombre AS Campanya, s.categoria_circuito AS Categoria,
                   COALESCE(v.origen, sc.origen) AS Origen,
                   (SELECT COUNT(*) FROM Eurovip.servicio_itinerario x WHERE x.id_servicio = s.id_servicio) AS Vuelos,
                   s.precio_base AS Precio_Base, s.descuento AS Descuento, Eurovip.fnPrecioWeb(s.id_servicio) AS PrecioWeb,
                   s.id_moneda AS Id_Moneda, s.activo AS Activo, s.codigo AS Codigo, s.usuario AS Usuario, s.apertura AS Apertura
            FROM Eurovip.servicio s
            LEFT JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
            LEFT JOIN Eurovip.campanya ca ON ca.id_campanya = s.id_campanya AND ca.id_idioma = s.id_idioma
            LEFT JOIN Eurovip.servicio_itinerario v ON v.id_servicio = s.id_servicio AND v.segmento = 1";

        public async Task<IEnumerable<SalidaReporte>> SalidasDeCargaAsync(string codigo)
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<SalidaReporte>(SelectSalidas + " WHERE s.codigo = @codigo ORDER BY Desde, s.id_servicio", new { codigo });
        }

        public async Task<IEnumerable<SalidaReporte>> SalidasAsync(bool soloActivas, DateTime? desde, DateTime? hasta)
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<SalidaReporte>(SelectSalidas + @"
                WHERE (@activas = 0 OR s.activo = 1)
                  AND (@desde IS NULL OR sc.desde >= @desde) AND (@hasta IS NULL OR sc.desde <= @hasta)
                ORDER BY Desde, s.nombre, s.id_servicio",
                new { activas = soloActivas ? 1 : 0, desde = desde?.Date, hasta = hasta?.Date });
        }

        public async Task<IEnumerable<string>> TiposCargaAsync()
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<string>("SELECT DISTINCT tipo_carga FROM Eurovip.Log_Cargas ORDER BY tipo_carga");
        }

        public async Task<ReporteTablero> TableroAsync()
        {
            using var cn = _db.Conexion();
            using var m = await cn.QueryMultipleAsync(@"
                DECLARE @hoy date = CAST(GETDATE() AS date);

                SELECT
                    (SELECT COUNT(*) FROM Eurovip.inicio WHERE tipo_servicio = 'HOTEL' AND id_idioma = 'ES') AS HotelesVitrina,
                    (SELECT COUNT(*) FROM Eurovip.inicio WHERE tipo_servicio = 'HOTEL' AND id_idioma = 'ES' AND ISNULL(estado, 1) = 1) AS HotelesVisibles,
                    (SELECT COUNT(*) FROM Eurovip.circuito WHERE id_idioma = 'ES') AS Circuitos,
                    (SELECT COUNT(*) FROM Eurovip.servicio s JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio WHERE s.activo = 1 AND sc.desde >= @hoy) AS SalidasActivas,
                    (SELECT COUNT(*) FROM Eurovip.servicio s JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio WHERE s.activo = 1 AND sc.desde >= @hoy AND sc.desde < DATEADD(DAY, 30, @hoy)) AS SalidasProximas30,
                    (SELECT COUNT(DISTINCT s.id_servicio) FROM Eurovip.servicio s JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio JOIN Eurovip.servicio_itinerario i ON i.id_servicio = s.id_servicio WHERE s.activo = 1 AND sc.desde >= @hoy) AS SalidasConVuelo,
                    (SELECT AVG(Eurovip.fnPrecioWeb(s.id_servicio)) FROM Eurovip.servicio s JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio WHERE s.activo = 1 AND sc.desde >= @hoy) AS PrecioMedio,
                    (SELECT COUNT(*) FROM Eurovip.Log_Cargas WHERE fecha_hora >= DATEADD(DAY, -30, @hoy)) AS CargasMes;

                -- Salidas activas por mes (próximos 12 meses, incluye meses en 0)
                WITH meses AS (
                    SELECT DATEFROMPARTS(YEAR(@hoy), MONTH(@hoy), 1) AS m, 0 AS n
                    UNION ALL SELECT DATEADD(MONTH, 1, m), n + 1 FROM meses WHERE n < 11
                )
                SELECT FORMAT(me.m, 'yyyy-MM') AS Clave, FORMAT(me.m, 'MMM yy', 'es-ES') AS Etiqueta,
                       (SELECT COUNT(*) FROM Eurovip.servicio s JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
                        WHERE s.activo = 1 AND sc.desde >= me.m AND sc.desde < DATEADD(MONTH, 1, me.m) AND sc.desde >= @hoy) AS Valor,
                       (SELECT MIN(Eurovip.fnPrecioWeb(s.id_servicio)) FROM Eurovip.servicio s JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
                        WHERE s.activo = 1 AND sc.desde >= me.m AND sc.desde < DATEADD(MONTH, 1, me.m) AND sc.desde >= @hoy) AS Extra
                FROM meses me ORDER BY me.m;

                -- Circuitos con más salidas activas
                SELECT TOP 8 sc.nombre_circuito AS Etiqueta, c.id AS Clave, COUNT(*) AS Valor, MIN(Eurovip.fnPrecioWeb(s.id_servicio)) AS Extra
                FROM Eurovip.servicio s JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
                LEFT JOIN Eurovip.circuito c ON c.nombre_circuito = sc.nombre_circuito AND c.id_idioma = sc.id_idoma
                WHERE s.activo = 1 AND sc.desde >= @hoy
                GROUP BY sc.nombre_circuito, c.id ORDER BY COUNT(*) DESC, sc.nombre_circuito;

                -- Cargas por día (últimos 30 días, incluye días en 0)
                WITH dias AS (SELECT DATEADD(DAY, -29, @hoy) AS d UNION ALL SELECT DATEADD(DAY, 1, d) FROM dias WHERE d < @hoy)
                SELECT FORMAT(d, 'yyyy-MM-dd') AS Clave, FORMAT(d, 'dd MMM', 'es-ES') AS Etiqueta,
                       (SELECT COUNT(*) FROM Eurovip.Log_Cargas l WHERE l.fecha_hora >= d AND l.fecha_hora < DATEADD(DAY, 1, d)) AS Valor,
                       (SELECT ISNULL(SUM(registros_insertados), 0) FROM Eurovip.Log_Cargas l WHERE l.fecha_hora >= d AND l.fecha_hora < DATEADD(DAY, 1, d) AND l.estatus = N'Correcto') AS Extra
                FROM dias ORDER BY d;

                -- Cambios por usuario (últimos 30 días, auditoría)
                SELECT TOP 8 usuario AS Etiqueta, COUNT(*) AS Valor
                FROM Eurovip.auditoria_consulta_oferta WHERE fecha_hora >= DATEADD(DAY, -30, @hoy)
                GROUP BY usuario ORDER BY COUNT(*) DESC;

                -- Últimos cambios
                SELECT TOP 15 id, tipo_mantenimiento, usuario, fecha_hora, ids_servicio, cantidad_servicios
                FROM Eurovip.auditoria_consulta_oferta ORDER BY id DESC;");

            var t = await m.ReadSingleAsync<ReporteTablero>();
            t.SalidasPorMes = (await m.ReadAsync<PuntoSerie>()).ToList();
            t.TopCircuitos = (await m.ReadAsync<PuntoSerie>()).ToList();
            t.CargasPorDia = (await m.ReadAsync<PuntoSerie>()).ToList();
            t.ActividadPorUsuario = (await m.ReadAsync<PuntoSerie>()).ToList();
            t.UltimosCambios = (await m.ReadAsync<ActividadItem>()).ToList();
            return t;
        }
    }
}
