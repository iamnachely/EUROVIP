using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Fotos de los productos (Eurovip.imagen + Eurovip.multimedia, documento esquema-multimedia.md) para el componente
    /// eurovip-multimedia.js: listar, subir (portada, intermedia, galería), quitar, ordenar, texto alternativo y "hacer portada".
    /// No está en ningún módulo: el permiso se mira según el producto (excursiones → módulo Excursiones, etc.).
    /// </summary>
    public class MultimediaController : Controller
    {
        private readonly IMultimedia _mm;
        private readonly ICircuitoRepository _aud;
        private readonly ILogger<MultimediaController> _logger;

        public MultimediaController(IMultimedia mm, ICircuitoRepository aud, ILogger<MultimediaController> logger)
        {
            _mm = mm; _aud = aud; _logger = logger;
        }

        public sealed record OrdenDto(string Tipo, string Clave, List<int> Ids);
        public sealed record TextoDto(int Id, string? Texto);

        /// <summary>null si puede; si no, el resultado de error.</summary>
        private IActionResult? Permiso(string? tipo)
        {
            var t = _mm.Tipo(tipo);
            if (t is null) return BadRequest(new { mensaje = "Tipo de producto desconocido." });
            var p = HttpContext.Items[AccesoModuloFilter.ClavePermisos] as PermisosUsuario;
            return p is null || !p.Puede(t.Modulo)
                ? StatusCode(403, new { mensaje = "No tienes acceso a las fotos de este producto. Pídeselo a un administrador." })
                : null;
        }

        [HttpGet]
        public async Task<IActionResult> Listar(string tipo, string clave)
        {
            if (Permiso(tipo) is { } no) return no;
            try { return Json(new { items = await _mm.ListarAsync(tipo, clave) }); }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        /// <summary>Sube una o varias fotos. Portada e intermedia: solo la primera (reemplaza la que había).</summary>
        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(60_000_000)]
        public async Task<IActionResult> Subir([FromForm] string tipo, [FromForm] string clave, [FromForm] string uso, [FromForm] string? texto, List<IFormFile> archivos)
        {
            if (Permiso(tipo) is { } no) return no;
            if (archivos is null || archivos.Count == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            uso = (uso ?? "").Trim().ToUpperInvariant();
            if (uso != "GALERIA") archivos = archivos.Take(1).ToList();
            if (archivos.Count > 20) return BadRequest(new { mensaje = "Sube como máximo 20 fotos a la vez." });
            try
            {
                if (!await _mm.ExisteProductoAsync(tipo, clave)) return NotFound(new { mensaje = "Guarda primero el producto: las fotos van con él." });
                var subidas = new List<MultimediaItem>();
                var errores = new List<string>();
                foreach (var a in archivos)
                {
                    if (a.Length == 0 || a.Length > 10_000_000) { errores.Add($"{a.FileName}: pesa más de 10 MB o está vacía."); continue; }
                    if (await ImagenSegura.ExtensionAsync(a) is null) { errores.Add($"{a.FileName}: no es una imagen JPG, PNG o WebP."); continue; }
                    try
                    {
                        await using var st = a.OpenReadStream();
                        subidas.Add(await _mm.SubirAsync(tipo, clave, uso, st, a.FileName, texto));
                    }
                    catch (InvalidOperationException ex) { errores.Add($"{a.FileName}: {ex.Message}"); }
                }
                if (subidas.Count > 0)
                    await Auditar("Multimedia_Subir", new { tipo, clave, uso, fotos = subidas.Select(s => new { s.Id, s.Ruta, s.Tamano_Bytes, s.Nombre_Original }) });
                if (subidas.Count == 0) return BadRequest(new { mensaje = string.Join(" ", errores) });
                var mensaje = uso switch { "PORTADA" => "Portada guardada.", "INTERMEDIA" => "Foto intermedia guardada.", _ => subidas.Count == 1 ? "Foto añadida a la galería." : $"{subidas.Count} fotos añadidas a la galería." };
                return Json(new { ok = true, items = subidas, errores, mensaje = errores.Count > 0 ? $"{mensaje} No se subieron: {string.Join(" ", errores)}" : mensaje });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subiendo fotos {Tipo} {Clave}", tipo, clave);
                return StatusCode(500, new { mensaje = "No se pudo guardar la foto: " + ex.Message });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Quitar(int id)
        {
            // El permiso se mira con el tipo de la foto guardada, no con lo que mande la pantalla
            if (await _mm.ObtenerAsync(id) is not { } actual) return NotFound(new { mensaje = "Esa foto ya no existe." });
            if (Permiso(actual.Tipo) is { } no) return no;
            var m = await _mm.QuitarAsync(id);
            if (m is null) return NotFound(new { mensaje = "Esa foto ya no existe." });
            await Auditar("Multimedia_Quitar", new { m.Id, m.Tipo, m.Clave, m.Uso, m.Ruta });
            return Json(new { ok = true, mensaje = "Foto quitada." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Ordenar([FromBody] OrdenDto dto)
        {
            if (Permiso(dto.Tipo) is { } no) return no;   // OrdenarAsync solo toca las fotos de ese tipo y producto
            try
            {
                await _mm.OrdenarAsync(dto.Tipo, dto.Clave, dto.Ids ?? new());
                await Auditar("Multimedia_Ordenar", dto);
                return Json(new { ok = true, mensaje = "Orden de la galería guardado." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Texto([FromBody] TextoDto dto)
        {
            if (await _mm.ObtenerAsync(dto.Id) is not { } actual) return NotFound(new { mensaje = "Esa foto ya no existe." });
            if (Permiso(actual.Tipo) is { } no) return no;
            if (!await _mm.TextoAsync(dto.Id, dto.Texto)) return NotFound(new { mensaje = "Esa foto ya no existe." });
            await Auditar("Multimedia_Texto", dto);
            return Json(new { ok = true, mensaje = "Descripción de la foto guardada." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Portada(int id)
        {
            if (await _mm.ObtenerAsync(id) is not { } actual) return NotFound(new { mensaje = "Esa foto ya no existe." });
            if (Permiso(actual.Tipo) is { } no) return no;
            var m = await _mm.HacerPortadaAsync(id);
            if (m is null) return NotFound(new { mensaje = "Esa foto ya no existe." });
            await Auditar("Multimedia_Portada", new { m.Id, m.Tipo, m.Clave });
            return Json(new { ok = true, mensaje = "Ahora es la portada." });
        }

        private Task Auditar(string tipo, object cambios) =>
            _aud.AuditarAsync(tipo, Array.Empty<int>(), cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
