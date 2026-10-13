using EuroVip.Models;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// "Mi perfil" (clic en el nombre de arriba) y la campana de notificaciones.
    /// El usuario ve su información, sube su foto y edita nombre, apellido y teléfono.
    /// Correo, usuario y contraseña NO se cambian aquí (el servidor ni los lee).
    /// </summary>
    public class PerfilController : Controller
    {
        private const long MaxFoto = 3 * 1024 * 1024;
        private readonly IPerfilRepository _repo;
        private readonly ICircuitoRepository _aud;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PerfilController> _logger;

        public PerfilController(IPerfilRepository repo, ICircuitoRepository aud, IWebHostEnvironment env, ILogger<PerfilController> logger)
        {
            _repo = repo;
            _aud = aud;
            _env = env;
            _logger = logger;
        }

        private string Yo => User.Identity?.Name ?? "";

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> Datos()
        {
            var p = await _repo.PerfilAsync(Yo);
            return p is null ? NotFound(new { mensaje = "No encontramos tu usuario." }) : Json(p);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] PerfilEditarDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(new { mensaje = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });
            var antes = await _repo.PerfilAsync(Yo);
            if (!await _repo.GuardarAsync(Yo, dto)) return NotFound(new { mensaje = "No encontramos tu usuario." });
            HttpContext.Session.SetString("NombreUsuario", $"{dto.Nombre.Trim()} {dto.Apellido.Trim()}".Trim());
            await Auditar("Perfil_Editar", new { antes = new { antes?.Nombre, antes?.Apellido, antes?.Telefono }, despues = dto });
            return Json(new { ok = true, mensaje = "Tus datos se guardaron." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFoto + 100_000)]
        public async Task<IActionResult> Foto(IFormFile? archivo)
        {
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > MaxFoto) return BadRequest(new { mensaje = "La imagen pesa más de 3 MB." });

            // se comprueba el contenido real, no solo la extensión
            var cab = new byte[12];
            using (var s = archivo.OpenReadStream()) { _ = await s.ReadAsync(cab); }
            var ext = cab switch
            {
                [0xFF, 0xD8, 0xFF, ..] => "jpg",
                [0x89, 0x50, 0x4E, 0x47, ..] => "png",
                [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50] => "webp",
                _ => null
            };
            if (ext is null) return BadRequest(new { mensaje = "La imagen debe ser JPG, PNG o WebP." });

            var perfil = await _repo.PerfilAsync(Yo);
            if (perfil is null) return NotFound(new { mensaje = "No encontramos tu usuario." });

            var carpeta = Path.Combine(_env.WebRootPath, PerfilFoto.Carpeta.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(carpeta);
            var nombre = $"{perfil.CodigoUsuario}-{DateTime.Now:yyyyMMddHHmmss}.{ext}";
            await using (var fs = new FileStream(Path.Combine(carpeta, nombre), FileMode.CreateNew))
                await archivo.CopyToAsync(fs);

            var ruta = $"/{PerfilFoto.Carpeta}/{nombre}";
            var anterior = await _repo.CambiarFotoAsync(Yo, ruta);
            BorrarFoto(anterior);
            HttpContext.Session.SetString("Imagen", ruta);
            await Auditar("Perfil_Foto", new { anterior, nueva = ruta });
            return Json(new { ok = true, foto = ruta, mensaje = "¡Foto actualizada!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarFoto()
        {
            var anterior = await _repo.CambiarFotoAsync(Yo, null);
            BorrarFoto(anterior);
            HttpContext.Session.Remove("Imagen");
            await Auditar("Perfil_Foto", new { anterior, nueva = (string?)null });
            return Json(new { ok = true, mensaje = "Se quitó la foto." });
        }

        // ---------------- notificaciones ----------------
        [HttpGet]
        public async Task<IActionResult> Notificaciones() => Json(await _repo.NotificacionesAsync(Yo));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarLeidas()
        {
            await _repo.MarcarLeidasAsync(Yo);
            return Json(new { ok = true });
        }

        /// <summary>Solo borra archivos que Eurovip subió a su propia carpeta.</summary>
        private void BorrarFoto(string? ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta) || !ruta.StartsWith($"/{PerfilFoto.Carpeta}/")) return;
            var nombre = Path.GetFileName(ruta);
            var fisico = Path.Combine(_env.WebRootPath, PerfilFoto.Carpeta.Replace('/', Path.DirectorySeparatorChar), nombre);
            try { if (System.IO.File.Exists(fisico)) System.IO.File.Delete(fisico); }
            catch (Exception ex) { _logger.LogWarning(ex, "No se pudo borrar la foto anterior {Ruta}", ruta); }
        }

        private Task Auditar(string tipo, object cambios) =>
            _aud.AuditarAsync(tipo, Array.Empty<int>(), cambios, Yo, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
