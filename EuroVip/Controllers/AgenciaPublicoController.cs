using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Editor PÚBLICO de la agencia (como AgenciaPublicoController de BackTiTan): la agencia abre el link único que le llegó por
    /// WhatsApp, ve el flyer de su destino, puede poner su logo, cambiar precios, quitar hoteles, elegir el formato y descargar el PNG.
    /// Sin login: solo con un token vigente de Eurovip.flyer_agencia_link (32 caracteres al azar; se puede desactivar o hacer vencer).
    /// </summary>
    [AllowAnonymous]
    public class AgenciaPublicoController : Controller
    {
        private readonly IAgenciaRepository _repo;
        private readonly IFlyerRepository _flyers;
        private readonly IFlyerImagenService _img;
        private readonly AgenciaImagenes _arch;
        private readonly IConfiguration _cfg;

        public AgenciaPublicoController(IAgenciaRepository repo, IFlyerRepository flyers, IFlyerImagenService img, AgenciaImagenes arch, IConfiguration cfg)
        {
            _repo = repo;
            _flyers = flyers;
            _img = img;
            _arch = arch;
            _cfg = cfg;
        }

        [HttpGet("agencia/editor/{token}")]
        public async Task<IActionResult> Ver(string token)
        {
            var link = await _repo.LinkVigenteAsync(token, contarVisita: true);
            if (link is null) return View("TokenInvalido");
            var destino = (await _flyers.DestinosAsync(IFlyerRepository.Agencia))
                              .FirstOrDefault(d => string.Equals(d.Id_Destino, link.Id_Destino, StringComparison.OrdinalIgnoreCase))
                          ?? new FlyerDestino { Id_Destino = link.Id_Destino, Nombre_Destino = link.Nombre_Destino ?? link.Id_Destino };
            destino.Fondo = _img.FondoUrl(destino.Nombre_Destino);
            var web = (_cfg["Eurovip:Web"] is { Length: > 0 } w ? w : "http://eurovip.com.do").TrimEnd('/');
            var regimen = destino.Hoteles.Select(h => h.Regimen).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
            var partes = new[] { "Oferta especial", regimen, string.IsNullOrWhiteSpace(destino.Origen) ? null : "Salida " + destino.Origen }.Where(p => !string.IsNullOrWhiteSpace(p));
            return View(new AgenciaPublicoModelo
            {
                Token = token, Nombre_Agencia = link.Nombre_Agencia, Destino = destino, Logo = "/img/flyer/logo-eurovip-travel.png",
                Web = web, WebTexto = System.Text.RegularExpressions.Regex.Replace(web, "^https?://(www\\.)?", ""), Simbolo = "RD$",
                Subtitulo = string.Join(" · ", partes!)
            });
        }

        [HttpPost("agencia/logo/subir"), IgnoreAntiforgeryToken, RequestSizeLimit(6_000_000)]
        public async Task<IActionResult> SubirLogo(IFormFile? logo, [FromForm] string? token)
        {
            var link = await _repo.LinkVigenteAsync(token ?? "", contarVisita: false);
            if (link is null) return Unauthorized(new { error = "El link ya no es válido." });
            if (logo is null || logo.Length == 0) return BadRequest(new { error = "Elige una imagen." });
            try { return Ok(new { url = await _arch.GuardarLogoAsync(AgenciaImagenes.ClaveAgencia(link.Nombre_Agencia ?? token), logo) }); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpGet("agencia/logo/obtener")]
        public async Task<IActionResult> ObtenerLogo(string? token)
        {
            var link = await _repo.LinkVigenteAsync(token ?? "", contarVisita: false);
            return Ok(new { url = link is null ? null : _arch.LogoUrl(AgenciaImagenes.ClaveAgencia(link.Nombre_Agencia ?? token)) });
        }
    }
}
