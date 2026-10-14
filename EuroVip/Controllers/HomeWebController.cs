using Dapper;
using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace EuroVip.Controllers
{
    /// <summary>
    /// "Home de la web": editor visual de lo que ve el cliente (equivale a /Inicio2/Index + /Carrusel/Index de BackTiTan).
    /// Carrusel arriba (slides web/tablet/móvil con su precio "desde") y debajo las secciones con sus tarjetas,
    /// que se ordenan arrastrando. Todo sobre el esquema Eurovip, solo ES.
    /// </summary>
    public class HomeWebController : Controller
    {
        private const long MaxImagen = 8 * 1024 * 1024;
        private readonly IHomeWebRepository _repo;
        private readonly ICircuitoRepository _db;
        private readonly IConfiguration _cfg;
        private readonly ILogger<HomeWebController> _logger;

        private readonly IImagenesLocales _img;

        public HomeWebController(IHomeWebRepository repo, ICircuitoRepository db, IConfiguration cfg, IImagenesLocales img, ILogger<HomeWebController> logger)
        {
            _img = img;
            _repo = repo;
            _db = db;
            _cfg = cfg;
            _logger = logger;
        }

        private string Slider(string k) => _cfg[$"Eurovip:Slider:{k}"] ?? "";

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> Datos()
        {
            var d = await _repo.DatosAsync();
            d.Imagenes = new HwImagenes { Web = Slider("Url"), Tablet = Slider("UrlMd"), Movil = Slider("UrlSm") };
            foreach (var t in d.Contenedores.SelectMany(c => c.Tarjetas).Concat(d.SinContenedor)) PonerImagen(t);
            return Json(d);
        }

        /// <summary>
        /// Imagen de la tarjeta desde las carpetas del proyecto (lo que se carga en Hoteles y Circuitos):
        ///  • HOTEL: /imagenes/hoteles/{nombre del hotel}.webp
        ///  • CIRCUITO: la portada del circuito (/imagenes/circuitos/{circuito.caratula})
        ///  • si no hay archivo, la URL que tenga la tarjeta (si es una dirección web o una ruta del sitio).
        /// </summary>
        private void PonerImagen(HwTarjeta t)
        {
            if (string.Equals(t.Tipo_Servicio, "HOTEL", StringComparison.OrdinalIgnoreCase))
                t.Imagen = _img.Buscar("hoteles", t.Id_Destino)?.Url;
            else if (string.Equals(t.Tipo_Servicio, "CIRCUITO", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t.Caratula_Circuito))
                t.Imagen = t.Caratula_Circuito.StartsWith("/") || Uri.IsWellFormedUriString(t.Caratula_Circuito, UriKind.Absolute)
                    ? t.Caratula_Circuito
                    : _img.BuscarArchivo("circuitos", t.Caratula_Circuito.Trim())?.Url;
            var c = t.CaratulaDestino?.Trim();
            if (t.Imagen is null && !string.IsNullOrEmpty(c) && (c.StartsWith("/") || c.StartsWith("http://") || c.StartsWith("https://")))
                t.Imagen = c;
        }

        // ================= SECCIONES =================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> OrdenSecciones([FromBody] List<int> ids)
        {
            await _repo.OrdenContenedoresAsync(ids);
            await Auditar("Home_OrdenSecciones", new { ids });
            return Json(new { ok = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Seccion([FromBody] HwContenedorDto dto)
        {
            if (!ModelState.IsValid) return Error();
            var id = await _repo.GuardarContenedorAsync(dto);
            await Auditar(dto.Id_Contenedor is null ? "Home_SeccionCrear" : "Home_SeccionEditar", dto);
            return Json(new { ok = true, id, mensaje = dto.Id_Contenedor is null ? "Sección creada." : "Sección guardada." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSeccion(int id)
        {
            var n = await _repo.EliminarContenedorAsync(id);
            await Auditar("Home_SeccionEliminar", new { id, tarjetasMovidas = n });
            return Json(new { ok = true, mensaje = n > 0 ? $"Sección eliminada. Sus {n} tarjeta(s) quedaron en «Sin sección»." : "Sección eliminada." });
        }

        // ================= TARJETAS =================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> OrdenTarjetas([FromBody] HwOrdenDto dto)
        {
            await _repo.OrdenTarjetasAsync(dto);
            await Auditar("Home_OrdenTarjetas", dto);
            return Json(new { ok = true });
        }

        public class EstadoDto { public int Id { get; set; } public bool Estado { get; set; } }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EstadoTarjeta([FromBody] EstadoDto dto)
        {
            var r = await _repo.EstadoTarjetaAsync(dto.Id, dto.Estado);
            if (r is null) return NotFound(new { mensaje = "No encontramos esa tarjeta." });
            await Auditar("Home_TarjetaEstado", dto);
            return Json(new { ok = true, mensaje = dto.Estado ? "Tarjeta visible en la web." : "Tarjeta oculta en la web." });
        }

        [HttpGet]
        public async Task<IActionResult> Tarjeta(int id)
        {
            var t = await _repo.TarjetaAsync(id);
            if (t is not null) PonerImagen(t);
            return t is null ? NotFound(new { mensaje = "No encontramos esa tarjeta." }) : Json(t);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarTarjeta([FromBody] HwTarjetaDto dto)
        {
            if (!ModelState.IsValid) return Error();
            if (!string.IsNullOrWhiteSpace(dto.CaratulaDestino) && !dto.CaratulaDestino.StartsWith("http") && !dto.CaratulaDestino.StartsWith("/"))
                return BadRequest(new { mensaje = "La imagen debe ser una URL (https://…) o una ruta que empiece por /" });
            var antes = dto.Id is int i ? await _repo.TarjetaAsync(i) : null;
            var id = await _repo.GuardarTarjetaAsync(dto);
            await Auditar(dto.Id is null ? "Home_TarjetaCrear" : "Home_TarjetaEditar", new { antes, despues = dto });
            return Json(new { ok = true, id, mensaje = dto.Id is null ? "Tarjeta agregada a la Home." : "Tarjeta guardada." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarTarjeta(int id)
        {
            var antes = await _repo.TarjetaAsync(id);
            if (!await _repo.EliminarTarjetaAsync(id)) return NotFound(new { mensaje = "No encontramos esa tarjeta." });
            await Auditar("Home_TarjetaEliminar", new { antes });
            return Json(new { ok = true, mensaje = "Tarjeta quitada de la Home." });
        }

        /// <summary>Pone en la Home lo más barato que está a la venta (Eurovip.spActualizaInicio) respetando el orden ya puesto.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> TraerOfertas([FromBody] HwActualizarDto? solo)
        {
            try
            {
                var n = await _repo.ActualizarDesdeOfertasAsync(string.IsNullOrWhiteSpace(solo?.Id_Destino) ? null : solo);
                await Auditar("Home_TraerOfertas", new { claves = n });
                return Json(new { ok = true, mensaje = n == 0 ? "No hay ofertas ni circuitos a la venta para traer." : $"Se actualizaron {n} destino(s) con su precio más bajo." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error trayendo ofertas a la Home");
                return StatusCode(500, new { mensaje = "No se pudo actualizar: " + ex.Message });
            }
        }

        // ================= CARRUSEL =================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> OrdenSlides([FromBody] List<int> ids)
        {
            await _repo.OrdenSlidesAsync(ids);
            await Auditar("Carrusel_Orden", new { ids });
            return Json(new { ok = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Slide([FromBody] HwSlideDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.Url) && !dto.Url.StartsWith("http") && !dto.Url.StartsWith("/"))
                return BadRequest(new { mensaje = "El enlace debe empezar por https:// o por /" });
            if (!await _repo.EditarSlideAsync(dto)) return NotFound(new { mensaje = "No encontramos ese slide." });
            await Auditar("Carrusel_Editar", dto);
            return Json(new { ok = true, mensaje = "Slide guardado." });
        }

        /// <summary>Nuevo slide con sus 3 imágenes (web obligatoria; tablet y móvil opcionales: si faltan se usa la web).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(3 * MaxImagen + 200_000)]
        public async Task<IActionResult> NuevoSlide([FromForm] string nombre, [FromForm] string orientacion, [FromForm] string? url, [FromForm] bool activo,
                                                    IFormFile? web, IFormFile? tablet, IFormFile? movil)
        {
            if (web is null) return BadRequest(new { mensaje = "Sube al menos la imagen para web." });
            var baseNombre = Regex.Replace((nombre ?? "").Trim(), @"[^A-Za-z0-9_\-]", "");
            if (baseNombre.Length < 3) return BadRequest(new { mensaje = "Escribe un nombre de archivo de al menos 3 letras (sin espacios ni símbolos)." });
            var ext = await ImagenSegura.ExtensionAsync(web);
            if (ext is null) return BadRequest(new { mensaje = "La imagen web debe ser JPG, PNG o WebP." });
            var ruta = $"{baseNombre}.{(ext == "jpg" ? "jpg" : ext)}";
            if (ruta.Length > 100) return BadRequest(new { mensaje = "El nombre es demasiado largo." });
            if (await _repo.ExisteRutaAsync(ruta)) return BadRequest(new { mensaje = $"Ya hay un slide llamado «{ruta}». Usa otro nombre." });

            var error = await GuardarImagenes(ruta, web, tablet ?? web, movil ?? web, nuevo: true);
            if (error is not null) return BadRequest(new { mensaje = error });
            var id = await _repo.CrearSlideAsync(ruta, orientacion, url, activo);
            await Auditar("Carrusel_Crear", new { id, ruta, orientacion, url, activo, tablet = tablet is not null, movil = movil is not null });
            return Json(new { ok = true, id, mensaje = "Slide creado." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(3 * MaxImagen + 200_000)]
        public async Task<IActionResult> ImagenesSlide([FromForm] int id, IFormFile? web, IFormFile? tablet, IFormFile? movil)
        {
            var s = await _repo.SlideAsync(id);
            if (s is null) return NotFound(new { mensaje = "No encontramos ese slide." });
            if (web is null && tablet is null && movil is null) return BadRequest(new { mensaje = "Elige al menos una imagen." });
            var error = await GuardarImagenes(s.Ruta, web, tablet, movil, nuevo: false);
            if (error is not null) return BadRequest(new { mensaje = error });
            await Auditar("Carrusel_Imagenes", new { id, s.Ruta, web = web is not null, tablet = tablet is not null, movil = movil is not null });
            return Json(new { ok = true, mensaje = "Imágenes reemplazadas. Si no ves el cambio, recarga (la web puede tenerlas en caché)." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSlide(int id)
        {
            var ruta = await _repo.EliminarSlideAsync(id);
            if (ruta is null) return NotFound(new { mensaje = "No encontramos ese slide." });
            await Auditar("Carrusel_Eliminar", new { id, ruta });   // los archivos de imagen se conservan (como BackTiTan)
            return Json(new { ok = true, mensaje = "Slide eliminado del carrusel." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SlideDestino([FromBody] HwSlideDestinoDto dto)
        {
            if (!ModelState.IsValid) return Error();
            var id = await _repo.GuardarSlideDestinoAsync(dto);
            await Auditar("Carrusel_Precio", dto);
            return Json(new { ok = true, id, mensaje = "Precio guardado." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSlideDestino(int id)
        {
            if (!await _repo.EliminarSlideDestinoAsync(id)) return NotFound(new { mensaje = "No encontramos ese precio." });
            await Auditar("Carrusel_PrecioEliminar", new { id });
            return Json(new { ok = true, mensaje = "Precio quitado del slide." });
        }

        [HttpGet]
        public async Task<IActionResult> PrecioMinimo(string origen, string destino, int campanya, string? regimen) =>
            Json(new { precio = await _repo.PrecioMinimoAsync(origen, destino, campanya, regimen) });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalcularPrecios()
        {
            var n = await _repo.RecalcularPreciosSlidesAsync();
            await Auditar("Carrusel_Recalcular", new { actualizados = n });
            return Json(new { ok = true, mensaje = n == 0 ? "No había precios que cambiar (o no hay ofertas a la venta para esos destinos)." : $"Se actualizaron {n} precio(s) del carrusel." });
        }

        // ================= AJUSTES =================
        [HttpGet]
        public async Task<IActionResult> Ajustes() => Json(new { precioBaseMinimo = await _db.PrecioBaseMinimoAsync() });

        public class AjustesDto { public decimal PrecioBaseMinimo { get; set; } }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarAjustes([FromBody] AjustesDto dto)
        {
            if (dto.PrecioBaseMinimo < 0 || dto.PrecioBaseMinimo > 100000) return BadRequest(new { mensaje = "Precio base mínimo inválido." });
            var antes = await _db.PrecioBaseMinimoAsync();
            using var cn = _db.Conexion();
            await cn.ExecuteAsync("UPDATE Eurovip.config_sistema SET valor = @v WHERE clave = 'precio_base_minimo'", new { v = dto.PrecioBaseMinimo.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            await Auditar("Config_PrecioMinimo", new { antes, despues = dto.PrecioBaseMinimo });
            return Json(new { ok = true, mensaje = $"Precio base mínimo: {dto.PrecioBaseMinimo:N0}." });
        }

        // ================= helpers =================
        private async Task<string?> GuardarImagenes(string ruta, IFormFile? web, IFormFile? tablet, IFormFile? movil, bool nuevo)
        {
            var lotes = new[] { (web, "Fisico", "web"), (tablet, "FisicoMd", "tablet"), (movil, "FisicoSm", "móvil") };
            foreach (var (f, _, et) in lotes)
            {
                if (f is null) continue;
                if (f.Length > MaxImagen) return $"La imagen {et} pesa más de 8 MB.";
                if (await ImagenSegura.ExtensionAsync(f) is null) return $"La imagen {et} debe ser JPG, PNG o WebP.";
            }
            foreach (var (f, clave, et) in lotes)
            {
                if (f is null) continue;
                var carpeta = Slider(clave);
                if (string.IsNullOrWhiteSpace(carpeta)) return "Falta configurar la carpeta de imágenes del carrusel (Eurovip:Slider).";
                Directory.CreateDirectory(carpeta);
                var destino = Path.Combine(carpeta, ruta);
                if (nuevo && System.IO.File.Exists(destino)) return $"Ya existe un archivo «{ruta}» en la carpeta {et}. Usa otro nombre.";
                await using var fs = new FileStream(destino, FileMode.Create);
                await f.CopyToAsync(fs);
            }
            return null;
        }

        private IActionResult Error() => BadRequest(new { mensaje = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        private Task Auditar(string tipo, object cambios) =>
            _db.AuditarAsync(tipo, Array.Empty<int>(), cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
