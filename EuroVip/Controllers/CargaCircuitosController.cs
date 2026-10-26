using EuroVip.Services;
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

        /// <summary>
        /// Plantilla del Excel de circuito, igual al que se usa en BackTiTan (lo que lee ExcelCircuitoParser):
        /// columna A = hoteles; de la B en adelante una fila de FECHAS y justo debajo su PRECIO (se pueden poner varios bloques);
        /// más abajo Aerolínea, Tasa, Segmento 1 y Segmento 2. La hoja «Instrucciones» lo explica (esa hoja no se lee).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Plantilla()
        {
            // Hoteles de ejemplo que SÍ existen en Eurovip (así el ejemplo se reconoce al subirlo); si no hay, nombres de muestra
            List<string> hoteles;
            using (var cn = _repo.Conexion())
                hoteles = (await Dapper.SqlMapper.QueryAsync<string>(cn, @"SELECT TOP 3 a.nombre FROM Eurovip.alojamiento a
                    WHERE a.id_idioma = 'ES' AND a.activo = 1 AND a.id_alojamiento IN (SELECT x.id_alojamiento FROM Eurovip.servicio_circuito_alojamiento x) ORDER BY a.nombre")).ToList();
            hoteles.AddRange(new[] { "Hotel Riu Plaza España", "Hotel Ilunion Pío XII", "Hotel Meliá Sevilla" }.Skip(hoteles.Count));
            var primera = DateTime.Today.AddDays(((int)DayOfWeek.Saturday - (int)DateTime.Today.DayOfWeek + 7) % 7 + 14);
            var fechas = Enumerable.Range(0, 12).Select(i => primera.AddDays(i * 7)).ToList();
            object?[] Fechas(int desde) => new object?[] { null }.Concat(fechas.Skip(desde).Take(6).Cast<object?>()).ToArray();
            object?[] Precios(int baseP) => new object?[] { null }.Concat(Enumerable.Range(0, 6).Select(i => (object?)(baseP + (i % 3) * 25m))).ToArray();
            var f1 = Fechas(0); f1[0] = "HOTELES";
            var p1 = Precios(1790); p1[0] = hoteles[0];
            var f2 = Fechas(6); f2[0] = hoteles[1];
            var p2 = Precios(1840); p2[0] = hoteles[2];
            var circuito = new ExcelTabla.Hoja("Circuito", Array.Empty<string>(), new List<object?[]>
            {
                f1, p1, f2, p2,
                new object?[] { null },
                new object?[] { "Aerolínea", "IBEROJET" },
                new object?[] { "Tasa", 260m },
                new object?[] { "Segmento 1", "18:30 - 21:20" },
                new object?[] { "Segmento 2", "23:20 - 17:35" },
            }, new double[] { 34, 13, 13, 13, 13, 13, 13 });
            var ayuda = new ExcelTabla.Hoja("Instrucciones", new[] { "Qué", "Cómo" }, new List<object?[]>
            {
                new object?[] { "Hoteles", "Columna A: un hotel por fila, con el nombre como está en Eurovip (Herramientas › Alojamientos)." },
                new object?[] { "Fechas", "De la columna B en adelante: una fila con las fechas de salida (formato fecha)." },
                new object?[] { "Precios", "Justo debajo de cada fecha, el precio TOTAL por persona de esa salida. Puedes poner varios bloques de fechas y precios." },
                new object?[] { "Aerolínea", "Opcional. Nombre o código de la aerolínea (en Eurovip el vuelo es opcional)." },
                new object?[] { "Tasa", "Opcional. Tasas que se restan del precio total." },
                new object?[] { "Segmento 1 / 2", "Opcional. Horario del vuelo de ida y de vuelta (salida - llegada)." },
                new object?[] { "Importante", "Solo se lee la primera hoja (Circuito). Los datos del circuito (nombre, noches, campañas…) se ponen en la pantalla." },
            }, new double[] { 18, 110 });
            return File(ExcelTabla.Crear(circuito, ayuda), CargaExcel.TipoXlsx, "Eurovip-plantilla-circuito.xlsx");
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

        /// <summary>Cómo va la carga en curso (la pantalla lo consulta cada medio segundo para la barra de progreso).</summary>
        [HttpGet]
        public IActionResult Progreso(string id, [FromServices] IProgresoCargas progreso) =>
            Json(progreso.Leer(id) is { } p ? new { porcentaje = p.Porcentaje, paso = p.Paso } : new { porcentaje = 0, paso = "" });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] CargaCircuitoDto dto)
        {
            var errores = ErroresModelo();
            if (errores.Count > 0) return BadRequest(new { mensaje = errores[0], errores });

            try
            {
                var (resultado, errs) = await _carga.CargarAsync(dto, User.Identity?.Name ?? "eurovip",
                    HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), Request.Headers[ProgresoCargas.Cabecera].ToString());
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
