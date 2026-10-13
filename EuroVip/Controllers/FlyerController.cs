using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Flyers (equivale a /Flyer/Hub de BackTiTan, sus 11 pantallas en una): destinos y precios, generar imágenes,
    /// galería, contactos y envío por WhatsApp (cuenta de BackTiTan) con progreso e historial.
    /// </summary>
    public class FlyerController : Controller
    {
        private static readonly string[] RolesEnvio = { "Superadministrador", "Administrador" };
        private readonly IFlyerRepository _repo;
        private readonly IFlyerImagenService _img;
        private readonly WhatsAppService _wa;
        private readonly FlyerEnvioJob _job;
        private readonly ICircuitoRepository _db;
        private readonly ILogger<FlyerController> _logger;

        public FlyerController(IFlyerRepository repo, IFlyerImagenService img, WhatsAppService wa, FlyerEnvioJob job, ICircuitoRepository db, ILogger<FlyerController> logger)
        {
            _repo = repo;
            _img = img;
            _wa = wa;
            _job = job;
            _db = db;
            _logger = logger;
        }

        private bool PuedeEnviar => RolesEnvio.Contains(HttpContext.Session.GetString("Rol") ?? "", StringComparer.OrdinalIgnoreCase);

        public IActionResult Hub() => View();
        public IActionResult Index() => RedirectToAction(nameof(Hub));

        [HttpGet]
        public async Task<IActionResult> Datos()
        {
            var configs = await _repo.ConfigsAsync();
            foreach (var c in configs) c.Fondo = _img.FondoUrl(c.Nombre_Destino ?? c.Id_Destino);
            var destinos = await _repo.DestinosAsync();
            foreach (var d in destinos) d.Fondo = _img.FondoUrl(d.Nombre_Destino);
            var contactos = await _repo.ContactosAsync();
            return Json(new
            {
                configs, destinos, galeria = _img.Galeria(), envios = await _repo.EnviosAsync(),
                contactos = new { total = contactos.Count, activos = contactos.Count(c => c.Activo) },
                whatsapp = _wa.Configurado, puedeEnviar = PuedeEnviar, estado = _job.Estado
            });
        }

        // ---------------- destinos ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Config([FromBody] FlyerConfig c)
        {
            if (!ModelState.IsValid) return Error();
            var id = await _repo.GuardarConfigAsync(c);
            await Auditar(c.Id > 0 ? "Flyer_ConfigEditar" : "Flyer_ConfigCrear", c);
            return Json(new { ok = true, id, mensaje = c.Id > 0 ? "Destino guardado." : "Destino agregado a los flyers." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfig(int id)
        {
            if (!await _repo.EliminarConfigAsync(id)) return NotFound(new { mensaje = "No encontramos ese destino." });
            await Auditar("Flyer_ConfigEliminar", new { id });
            return Json(new { ok = true, mensaje = "Destino quitado de los flyers." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPrecios(int? id)
        {
            var n = await _repo.ActualizarPreciosAsync(id);
            await Auditar("Flyer_Precios", new { id, destinosConPrecio = n });
            return Json(new { ok = true, mensaje = n == 0 ? "No hay ofertas a la venta que coincidan con los destinos configurados." : $"Precios al día en {n} destino(s)." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Generar([FromQuery] bool actualizar = true)
        {
            try
            {
                if (actualizar) await _repo.ActualizarPreciosAsync();
                var imgs = await _img.GenerarAsync(await _repo.DestinosAsync());
                await Auditar("Flyer_Generar", new { imagenes = imgs.Select(i => i.Archivo) });
                return Json(new { ok = true, imagenes = imgs, mensaje = imgs.Count == 0 ? "No se generó ningún flyer: ningún destino tiene precios." : $"Se generaron {imgs.Count} flyer(s)." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando flyers");
                return StatusCode(500, new { mensaje = "No se pudieron generar: " + ex.Message });
            }
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Fondo([FromForm] string destino, IFormFile? archivo)
        {
            if (string.IsNullOrWhiteSpace(destino) || destino.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return BadRequest(new { mensaje = "Destino inválido." });
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > 8_000_000) return BadRequest(new { mensaje = "La imagen pesa más de 8 MB." });
            var ext = await ImagenSegura.ExtensionAsync(archivo);
            if (ext is null or "webp") return BadRequest(new { mensaje = "El fondo debe ser JPG o PNG." });
            var url = await _img.GuardarFondoAsync(destino, archivo, ext);
            await Auditar("Flyer_Fondo", new { destino });
            return Json(new { ok = true, url, mensaje = "Fondo actualizado." });
        }

        // ---------------- galería ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult EliminarImagen(string carpeta, string archivo) =>
            _img.Eliminar(carpeta, archivo) ? Json(new { ok = true, mensaje = "Flyer eliminado." }) : NotFound(new { mensaje = "No encontramos ese archivo." });

        [HttpGet]
        public IActionResult Descargar([FromQuery] string[] f)
        {
            var rutas = f.Select(x => x.Split('/', 2)).Where(p => p.Length == 2).Select(p => _img.Fisico(p[0], p[1])).Where(r => r is not null).Cast<string>().ToList();
            if (rutas.Count == 0) return NotFound("No hay flyers para descargar.");
            if (rutas.Count == 1) return PhysicalFile(rutas[0], "image/png", Path.GetFileName(rutas[0]));
            return File(_img.Zip(rutas), "application/zip", $"flyers-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        }

        // ---------------- contactos ----------------
        [HttpGet] public async Task<IActionResult> Contactos() => Json(await _repo.ContactosAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Contacto([FromBody] FlyerContacto c)
        {
            if (!ModelState.IsValid) return Error();
            var num = FlyerRepository.LimpiarNumero(c.Numero);
            if (num.Length is < 8 or > 15) return BadRequest(new { mensaje = "El número debe tener entre 8 y 15 dígitos, con código de país (ej. 34600111222)." });
            var id = await _repo.GuardarContactoAsync(c);
            if (id == -1) return BadRequest(new { mensaje = "Ese número ya está en la lista." });
            await Auditar(c.Id > 0 ? "Flyer_ContactoEditar" : "Flyer_ContactoCrear", new { id });
            return Json(new { ok = true, id, mensaje = c.Id > 0 ? "Contacto guardado." : "Contacto agregado." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarContacto(int id)
        {
            if (!await _repo.EliminarContactoAsync(id)) return NotFound(new { mensaje = "No encontramos ese contacto." });
            await Auditar("Flyer_ContactoEliminar", new { id });
            return Json(new { ok = true, mensaje = "Contacto eliminado." });
        }

        public class EstadoDto { public List<int> Ids { get; set; } = new(); public bool Activo { get; set; } }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EstadoContactos([FromBody] EstadoDto d)
        {
            var n = await _repo.EstadoContactosAsync(d.Ids, d.Activo);
            await Auditar("Flyer_ContactoEstado", d);
            return Json(new { ok = true, mensaje = $"{n} contacto(s) {(d.Activo ? "activados" : "desactivados")}." });
        }

        public class ImportarDto { public string Texto { get; set; } = ""; }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar([FromBody] ImportarDto d)
        {
            var (nuevos, repetidos, invalidos) = await _repo.ImportarAsync(d.Texto);
            await Auditar("Flyer_ContactoImportar", new { nuevos, repetidos, invalidos });
            return Json(new { ok = true, mensaje = $"{nuevos} nuevo(s) · {repetidos} ya estaban · {invalidos} sin número válido." });
        }

        // ---------------- envío ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Probar()
        {
            var (ok, mensaje) = await _wa.ProbarAsync();
            return ok ? Json(new { ok, mensaje }) : BadRequest(new { mensaje });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Enviar([FromBody] FlyerEnviarDto d)
        {
            if (!_wa.Configurado) return BadRequest(new { mensaje = "WhatsApp no está configurado en este servidor." });
            if (!PuedeEnviar) return StatusCode(403, new { mensaje = "Solo un administrador puede enviar flyers." });
            var rutas = d.Imagenes.Select(x => x.Split('/', 2)).Where(p => p.Length == 2).Select(p => _img.Fisico(p[0], p[1])).Where(r => r is not null).Cast<string>().ToList();
            if (rutas.Count == 0) return BadRequest(new { mensaje = "Elige al menos un flyer de la galería." });
            if (rutas.Count > 10) return BadRequest(new { mensaje = "Máximo 10 flyers por envío." });

            List<(string, string?)> dest;
            if (!string.IsNullOrWhiteSpace(d.NumeroPrueba))
            {
                var n = FlyerRepository.LimpiarNumero(d.NumeroPrueba);
                if (n.Length is < 8 or > 15) return BadRequest(new { mensaje = "Número de prueba inválido (con código de país)." });
                dest = new() { (n, "Prueba") };
            }
            else
            {
                var todos = await _repo.ContactosAsync();
                dest = todos.Where(c => c.Activo && (d.Contactos.Count == 0 || d.Contactos.Contains(c.Id)))
                            .Select(c => (FlyerRepository.LimpiarNumero(c.Numero), c.Nombre_Completo)).Where(x => x.Item1.Length >= 8).Distinct().ToList();
                if (dest.Count == 0) return BadRequest(new { mensaje = "No hay contactos activos para enviar." });
            }
            var carpeta = d.Imagenes.First().Split('/')[0];
            if (!_job.Iniciar(dest, rutas, d.Texto, carpeta, User.Identity?.Name ?? "")) return Conflict(new { mensaje = "Ya hay un envío en curso. Espera a que termine o cancélalo." });
            await Auditar("Flyer_Enviar", new { destinatarios = dest.Count, prueba = !string.IsNullOrWhiteSpace(d.NumeroPrueba), imagenes = d.Imagenes, d.Texto });
            return Json(new { ok = true, mensaje = $"Envío iniciado a {dest.Count} contacto(s)." });
        }

        [HttpGet] public IActionResult Estado() => Json(_job.Estado);

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Cancelar() { _job.Cancelar(); return Json(new { ok = true, mensaje = "Cancelando… termina el contacto actual y se detiene." }); }

        private IActionResult Error() => BadRequest(new { mensaje = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        private Task Auditar(string tipo, object cambios) =>
            _db.AuditarAsync(tipo, Array.Empty<int>(), cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
