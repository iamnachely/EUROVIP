using Dapper;
using EuroVip.Repositories;

namespace EuroVip.Services
{
    /// <summary>
    /// Imagen por defecto (punto 2 del pedido: «si el usuario no cargó imagen, que el sistema coja una por defecto linda para que la web
    /// no se quede en blanco; tómala de la que ya tenemos agregada y que no rompa la carga de imágenes»):
    ///   • Un producto sin foto (ni portada nueva ni la de antes por nombre) recibe como portada una imagen que YA existe:
    ///     la del destino o la general (config_sistema «imagen_por_defecto»). Ver Multimedia.PonerPorDefectoAsync.
    ///   • Se hace al guardar (formulario y Excel) y una vez para todo lo que ya estaba cargado (RellenarTodoAsync).
    ///   • Si después se sube una foto, reemplaza a la de por defecto como siempre.
    /// </summary>
    public interface IImagenesPorDefecto
    {
        Task<bool> AsegurarAsync(string tipo, string clave, string? destino, bool yaTieneFoto);
        Task<Dictionary<string, int>> RellenarTodoAsync();
    }

    public class ImagenesPorDefecto : IImagenesPorDefecto
    {
        private readonly IMultimedia _mm;
        private readonly ICircuitoRepository _db;
        private readonly IImagenesLocales _img;
        private readonly ILogger<ImagenesPorDefecto> _log;
        private readonly IHttpClientFactory _http;
        /// <summary>Si se configura (ImagenesPorDefecto:UrlFotosViejas, ej. https://bktitan.eurovip.com.do), las fotos de antes por nombre
        /// (wwwroot/imagenes/…) también se buscan allí: así se puede rellenar desde otro equipo sin tapar fotos que sí existen en el servidor.</summary>
        private readonly string? _urlViejas;
        public ImagenesPorDefecto(IMultimedia mm, ICircuitoRepository db, IImagenesLocales img, ILogger<ImagenesPorDefecto> log, IHttpClientFactory http, IConfiguration cfg)
        {
            _mm = mm; _db = db; _img = img; _log = log; _http = http;
            _urlViejas = string.IsNullOrWhiteSpace(cfg["ImagenesPorDefecto:UrlFotosViejas"]) ? null : cfg["ImagenesPorDefecto:UrlFotosViejas"]!.TrimEnd('/');
        }

        public async Task<bool> AsegurarAsync(string tipo, string clave, string? destino, bool yaTieneFoto)
        {
            if (yaTieneFoto) return false;
            try { return await _mm.PonerPorDefectoAsync(tipo, clave, destino); }
            catch (Exception ex) { _log.LogWarning(ex, "No se pudo poner la imagen por defecto a {Tipo} {Clave}", tipo, clave); return false; }
        }

        /// <summary>¿Existe la foto de antes (wwwroot/imagenes/{tipo}/{archivo})? En este servidor o, si está configurado, en el publicado.</summary>
        private async Task<bool> ArchivoViejoAsync(string tipo, string? archivo)
        {
            if (string.IsNullOrWhiteSpace(archivo)) return false;
            if (_img.BuscarArchivo(tipo, archivo) is not null) return true;
            if (_urlViejas is null) return false;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, $"{_urlViejas}/imagenes/{tipo}/{Uri.EscapeDataString(archivo)}");
                using var r = await _http.CreateClient("fotos").SendAsync(req);
                return r.IsSuccessStatusCode && (r.Content.Headers.ContentType?.MediaType ?? "").StartsWith("image/");
            }
            catch { return true; }   // si no se puede comprobar, mejor no tapar una foto que quizá existe
        }

        public async Task<Dictionary<string, int>> RellenarTodoAsync()
        {
            var r = new Dictionary<string, int>();
            using var cn = _db.Conexion();
            const string SinPortada = "NOT EXISTS (SELECT 1 FROM Eurovip.multimedia m WHERE m.tipo = '{0}' AND m.{1} = {2} AND m.uso = 'PORTADA')";
            string Sin(string tipo, string col, string valor) => string.Format(SinPortada, tipo, col, valor);

            async Task Poner(string clave, IEnumerable<(string Clave, string? Destino, Func<Task<bool>> Vieja)> l, string tipo)
            {
                var n = 0;
                foreach (var x in l)
                    if (await AsegurarAsync(tipo, x.Clave, x.Destino, await x.Vieja())) n++;
                r[clave] = n;
            }
            static Func<Task<bool>> No() => () => Task.FromResult(false);
            static Func<Task<bool>> Si(bool v) => () => Task.FromResult(v);

            // Destinos que salen en la web (Home, ofertas, excursiones, traslados, circuitos) sin foto: la general
            var destinos = (await cn.QueryAsync<string>($@"
                SELECT DISTINCT LTRIM(RTRIM(d.id_destino)) FROM Eurovip.destino d
                WHERE d.id_idioma = 'ES' AND ISNULL(d.caratula, '') = '' AND {Sin("DESTINO", "id_destino", "d.id_destino")}
                  AND (EXISTS (SELECT 1 FROM Eurovip.inicio i WHERE i.id_destino = d.id_destino)
                    OR EXISTS (SELECT 1 FROM Eurovip.servicio_destino sd WHERE sd.destino = d.id_destino)
                    OR EXISTS (SELECT 1 FROM Eurovip.servicio_excursion e WHERE LTRIM(RTRIM(e.id_destino)) = d.id_destino)
                    OR EXISTS (SELECT 1 FROM Eurovip.traslado t WHERE t.id_destino = d.id_destino)
                    OR EXISTS (SELECT 1 FROM Eurovip.circuito_destino cd WHERE cd.id_destino = d.id_destino))")).ToList();
            await Poner("Destinos", destinos.Select(d => (d, (string?)null, (Func<Task<bool>>)(() => ArchivoViejoAsync("destinos", _img.NombreArchivo(d))))), "DESTINO");

            var aloj = await cn.QueryAsync<(int Id, string? Destino)>($@"
                SELECT a.id_alojamiento, LTRIM(RTRIM(a.id_destino)) FROM Eurovip.alojamiento a
                WHERE a.id_idioma = 'ES' AND ISNULL(a.caratula, '') = '' AND {Sin("ALOJAMIENTO", "id_alojamiento", "a.id_alojamiento")}");
            await Poner("Hoteles del catálogo", aloj.Select(a => (a.Id.ToString(), a.Destino, No())), "ALOJAMIENTO");

            var hoteles = await cn.QueryAsync<(int Id, string Nombre, string? Destino)>($@"
                SELECT i.id_servicio, LTRIM(RTRIM(i.id_destino)), LTRIM(RTRIM(i.caratulaDestino)) FROM Eurovip.inicio i
                WHERE i.tipo_servicio = 'HOTEL' AND {Sin("HOTEL", "id_hotel", "i.id_servicio")}");
            await Poner("Hoteles (vitrina)", hoteles.Select(h => (h.Id.ToString(), h.Destino, (Func<Task<bool>>)(() => ArchivoViejoAsync("hoteles", _img.NombreArchivo(h.Nombre))))), "HOTEL");

            var exc = await cn.QueryAsync<(int Id, string Nombre, string? Destino)>($@"
                SELECT e.id_Excursion, e.nombre, LTRIM(RTRIM(e.id_destino)) FROM Eurovip.servicio_excursion e WHERE {Sin("EXCURSION", "id_excursion", "e.id_Excursion")}");
            await Poner("Excursiones", exc.Select(e => (e.Id.ToString(), e.Destino, (Func<Task<bool>>)(() => ArchivoViejoAsync("excursiones", Recortar(_img.NombreCircuito(e.Nombre ?? ""), 120) + ".webp")))), "EXCURSION");

            var tra = await cn.QueryAsync<(int Id, string? Destino, string? Imagen)>($@"
                SELECT t.id_traslado, t.id_destino, t.imagen FROM Eurovip.traslado t WHERE {Sin("TRASLADO", "id_traslado", "t.id_traslado")}");
            await Poner("Traslados", tra.Select(t => (t.Id.ToString(), t.Destino, (Func<Task<bool>>)(() => ArchivoViejoAsync("traslados", t.Imagen)))), "TRASLADO");

            var cir = await cn.QueryAsync<(string Nombre, string? Destino, string? Caratula)>($@"
                SELECT c.nombre_circuito, (SELECT TOP 1 cd.id_destino FROM Eurovip.circuito_destino cd WHERE cd.nombre_circuito = c.nombre_circuito), c.caratula
                FROM Eurovip.circuito c WHERE {Sin("CIRCUITO", "nombre_circuito", "c.nombre_circuito")}");
            await Poner("Circuitos", cir.Select(c => (c.Nombre, c.Destino, Si(!string.IsNullOrWhiteSpace(c.Caratula)))), "CIRCUITO");

            var cru = await cn.QueryAsync<int>($"SELECT c.id_crucero FROM Eurovip.crucero c WHERE {Sin("CRUCERO", "id_crucero", "c.id_crucero")}");
            await Poner("Cruceros", cru.Select(c => (c.ToString(), (string?)null, No())), "CRUCERO");

            // Tarjetas de la Home con foto propia (excursiones, traslados, cruceros, manuales…) sin foto: la de su destino o la general.
            // Las de hoteles, circuitos y Solo Hotel/Paquetes usan la foto del hotel, del circuito o del destino (ya rellenadas arriba).
            var tarjetas = await cn.QueryAsync<(int Id, string? Destino, string? Caratula)>($@"
                SELECT i.id, LTRIM(RTRIM(i.id_destino)), i.caratulaDestino FROM Eurovip.inicio i
                WHERE i.tipo_servicio NOT IN ('HOTEL', 'CIRCUITO', 'OFERTA', 'PAQUETES', 'COMBINADO') AND {Sin("INICIO", "id_inicio", "i.id")}");
            await Poner("Tarjetas de la Home", tarjetas.Select(t => (t.Id.ToString(), t.Destino,
                (Func<Task<bool>>)(async () => !string.IsNullOrWhiteSpace(t.Caratula) && (t.Caratula!.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    || await ArchivoViejoAsync("destinos", Path.GetFileName(t.Caratula.Split('?')[0])))))), "INICIO");

            // Días del itinerario de circuito sin foto: la portada del circuito (no van en multimedia: columna circuito_itinerario.caratula)
            r["Días de itinerario"] = await cn.ExecuteAsync(@"
                UPDATE ci SET caratula = v.ruta_url
                FROM Eurovip.circuito_itinerario ci
                CROSS APPLY (SELECT TOP 1 c.caratula AS ruta_url FROM Eurovip.circuito c WHERE c.nombre_circuito = ci.nombre_circuito AND ISNULL(c.caratula, '') <> '') v
                WHERE ISNULL(ci.caratula, '') = ''");
            return r;
        }

        private static string Recortar(string s, int max) => s.Length > max ? s[..max] : s;
    }
}
