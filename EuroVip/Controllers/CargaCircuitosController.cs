using EuroVip.Models;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Carga de circuitos en DOS versiones que guardan exactamente igual:
    ///   • Express  — todo junto en un flujo guiado (puede crear el circuito en el mismo paso).
    ///   • Clásica  — igual que /CargarCircuito/Index2 de BackTiTan (Inicio · Vuelos · Calendario · Cargar Excel).
    /// En Eurovip los vuelos son OPCIONALES.
    /// </summary>
    public class CargaCircuitosController : Controller
    {
        private readonly ICargaCircuitoService _carga;
        private readonly IExcelCircuitoParser _excel;
        private readonly ICircuitoRepository _repo;
        private readonly ILogger<CargaCircuitosController> _logger;

        public CargaCircuitosController(ICargaCircuitoService carga, IExcelCircuitoParser excel, ICircuitoRepository repo, ILogger<CargaCircuitosController> logger)
        {
            _carga = carga;
            _excel = excel;
            _repo = repo;
            _logger = logger;
        }

        public IActionResult Express(string? circuito)
        {
            ViewData["Circuito"] = circuito;
            return View();
        }

        public IActionResult Clasica(string? circuito)
        {
            ViewData["Circuito"] = circuito;
            return View();
        }

        [HttpGet]
        public IActionResult CodigoServicio() =>
            Json(new { codigo = $"{User.Identity?.Name}{DateTime.Now:yyyyMMddHHmmss}" is var c && c.Length > 30 ? c[..30] : c });

        /// <summary>Revisión previa ("Revisar formulario"): devuelve la lista de problemas sin guardar nada.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Validar([FromBody] CargaCircuitoDto dto)
        {
            var errores = ErroresModelo();
            if (errores.Count == 0) errores = await _carga.ValidarAsync(dto);
            var servicios = dto.Salidas.Count * Math.Max(1, dto.Campanyas.Count);
            return Json(new { ok = errores.Count == 0, errores, servicios });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] CargaCircuitoDto dto)
        {
            var errores = ErroresModelo();
            if (errores.Count > 0) return BadRequest(new { mensaje = errores[0], errores });

            try
            {
                var (resultado, errs) = await _carga.CargarAsync(dto, User.Identity?.Name ?? "eurovip",
                    HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
                if (resultado is null) return BadRequest(new { mensaje = errs.FirstOrDefault(), errores = errs });

                await _repo.AuditarAsync("Circuito_Carga", resultado.Ids, new { dto.Modo, resultado.Circuito, resultado.Codigo, resultado.LogId },
                    User.Identity?.Name ?? "desconocido", HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
                return Json(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en carga de circuito");
                return StatusCode(500, new { mensaje = "Error inesperado: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> LeerExcel(IFormFile? archivo)
        {
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige un archivo Excel." });
            if (!archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { mensaje = "El archivo debe ser .xlsx (Excel moderno)." });
            try
            {
                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms);
                ms.Position = 0;
                return Json(await _excel.LeerAsync(ms));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer el Excel {Archivo}", archivo.FileName);
                return BadRequest(new { mensaje = "No pude leer el Excel: " + ex.Message });
            }
        }

        private List<string> ErroresModelo() =>
            ModelState.Where(x => x.Value?.Errors.Count > 0)
                      .SelectMany(x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? $"Dato inválido en {x.Key}" : e.ErrorMessage))
                      .Distinct().ToList();
    }
}
