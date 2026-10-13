using EuroVip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Página de error amigable para TODO el sistema (como la de BackTiTan, con el diseño de Eurovip).
    /// • Excepciones → /Error/500 (UseExceptionHandler).
    /// • Respuestas vacías 4xx/5xx (404, 405…) → /Error/{código} (UseStatusCodePagesWithReExecute).
    /// Si la llamada venía de JavaScript (fetch / guardar), responde JSON { mensaje, referencia } en vez de la página.
    /// La vista no usa el menú (_Layout) para que se vea aunque el error venga de la base de datos o del propio menú.
    /// </summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;
        private readonly IWebHostEnvironment _env;

        public ErrorController(ILogger<ErrorController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        /// <summary>Solo en desarrollo: provoca un error de verdad para ver la página. En producción responde 404.</summary>
        [Route("Error/Probar")]
        public IActionResult Probar() =>
            _env.IsDevelopment() ? throw new InvalidOperationException("Error de prueba provocado desde /Error/Probar") : NotFound();

        [Route("Error/{codigo:int?}")]
        public IActionResult Index(int? codigo)
        {
            var ex = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var reejecutado = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var cod = ex?.Error is not null ? 500 : codigo ?? 500;
            if (cod is < 400 or > 599) cod = 500;

            // Código corto para que el usuario se lo pase a Informática y se encuentre en el log.
            // (TraceIdentifier se repite por conexión: se genera uno propio por error y se escribe en el log.)
            var referencia = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var ruta = ex?.Path ?? reejecutado?.OriginalPath ?? "";

            if (ex?.Error is not null)
                _logger.LogError(ex.Error, "Error {Referencia} en {Ruta} (usuario {Usuario})", referencia, ruta, User.Identity?.Name ?? "anónimo");
            else if (cod >= 500)
                _logger.LogWarning("Respuesta {Codigo} {Referencia} en {Ruta}", cod, referencia, ruta);
            else
                _logger.LogInformation("Respuesta {Codigo} {Referencia} en {Ruta} (usuario {Usuario})", cod, referencia, ruta, User.Identity?.Name ?? "anónimo");

            var modelo = ErrorViewModel.Para(cod, referencia, ruta);
            if (_env.IsDevelopment() && ex?.Error is not null)
                modelo.Detalle = ex.Error.ToString();

            Response.StatusCode = cod;
            // La petición re-ejecutada conserva el método original: un POST que falla es un "guardar" desde JavaScript.
            var desdeJs = Request.Headers.XRequestedWith == "XMLHttpRequest"
                          || Request.Headers.Accept.ToString().Contains("application/json")
                          || !HttpMethods.IsGet(Request.Method);
            if (desdeJs)
                return new ObjectResult(new { mensaje = $"{modelo.Titulo}. {modelo.Mensaje} (código {referencia})", referencia }) { StatusCode = cod };
            // Imágenes, scripts o archivos que no existen: solo el código, sin página entera.
            if (Path.HasExtension(ruta) && !ruta.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                return StatusCode(cod);

            return View("~/Views/Shared/Error.cshtml", modelo);
        }
    }
}
