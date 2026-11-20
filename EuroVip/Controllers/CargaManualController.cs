using EuroVip.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EuroVip.Models;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Carga manual de ofertas de hotel en DOS versiones que guardan igual:
    ///   • Express — varios hoteles, calendario y matriz de precios hotel × fecha.
    ///   • Clásica — igual que /CargarExcel/Index de BackTiTan (Inicio · Hoteles · Calendario · Vuelos · Cargar Excel).
    /// Los vuelos son OPCIONALES.
    /// </summary>
    public class CargaManualController : Controller
    {
        private readonly ICargaOfertaService _carga;
        private readonly IExcelOfertaParser _excel;
        private readonly ICircuitoRepository _aud;
        private readonly ILogger<CargaManualController> _logger;

        public CargaManualController(ICargaOfertaService carga, IExcelOfertaParser excel, ICircuitoRepository aud, ILogger<CargaManualController> logger)
        {
            _carga = carga;
            _excel = excel;
            _aud = aud;
            _logger = logger;
        }

        // ?tipo=PAQUETES → la misma carga, pero guarda Paquetes (sin tipo: Solo Hotel = OFERTA)
        public IActionResult Express(string? tipo) { ViewData["Tipo"] = OfertaRepository.TipoDe(tipo); return View(); }
        public IActionResult Clasica(string? tipo) { ViewData["Tipo"] = OfertaRepository.TipoDe(tipo); return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Validar([FromBody] CargaOfertaDto dto)
        {
            var errores = ErroresModelo();
            if (errores.Count == 0) errores = await _carga.ValidarAsync(dto);
            return Json(new { ok = errores.Count == 0, errores, servicios = dto.Hoteles.Sum(h => h.Salidas.Count) * Math.Max(1, dto.Campanyas.Count), precioMinimo = _carga.PrecioMinimo?.Info(), precioMinimoLista = _carga.PrecioMinimo?.Lista });
        }

        /// <summary>Cómo va la carga en curso (la pantalla lo consulta cada medio segundo para la barra de progreso).</summary>
        [HttpGet]
        public IActionResult Progreso(string id, [FromServices] IProgresoCargas progreso) =>
            Json(progreso.Leer(id) is { } p
                ? new { existe = true, porcentaje = p.Porcentaje, paso = p.Paso, terminado = p.Terminado, estado = p.Estado, cuerpo = p.Terminado ? p.Cuerpo : null }
                : new { existe = false, porcentaje = 0, paso = "", terminado = false, estado = 0, cuerpo = (object?)null });

        /// <summary>
        /// Guarda la carga. Con la cabecera X-Segundo-Plano la carga corre en el servidor AUNQUE el navegador pierda la conexión (red, proxy, tiempo
        /// de espera): se responde 202 enseguida y la pantalla consulta /Progreso?id=… hasta que trae el resultado. Así una carga larga (decenas de
        /// ofertas) no termina en «Failed to fetch». Sin la cabecera, funciona como siempre (espera el resultado).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] CargaOfertaDto dto, [FromServices] IProgresoCargas progreso, [FromServices] IServiceScopeFactory scopes)
        {
            var errores = ErroresModelo();
            if (errores.Count > 0) return BadRequest(new { mensaje = errores[0], errores });
            var usuario = User.Identity?.Name ?? "eurovip";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var navegador = Request.Headers.UserAgent.ToString();
            var idProgreso = Request.Headers[ProgresoCargas.Cabecera].ToString();

            if (Request.Headers["X-Segundo-Plano"] == "1" && !string.IsNullOrWhiteSpace(idProgreso) && idProgreso.Length <= 64)
            {
                progreso.Reportar(idProgreso, 1, "Recibido. Empezando la carga…");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        var (estado, cuerpo) = await EjecutarGuardarAsync(dto, usuario, ip, navegador, idProgreso,
                            scope.ServiceProvider.GetRequiredService<ICargaOfertaService>(), scope.ServiceProvider.GetRequiredService<ICircuitoRepository>());
                        progreso.Terminar(idProgreso, estado, cuerpo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error inesperado en carga manual (segundo plano)");
                        progreso.Terminar(idProgreso, 500, new { mensaje = "Error inesperado: " + ex.Message });
                    }
                });
                return Accepted(new { segundoPlano = true, id = idProgreso });
            }

            var (st, body) = await EjecutarGuardarAsync(dto, usuario, ip, navegador, idProgreso, _carga, _aud);
            return StatusCode(st, body);
        }

        /// <summary>La carga de verdad. Devuelve el código HTTP y el cuerpo, para responderlos ya o dejarlos en el progreso.</summary>
        private async Task<(int Estado, object Cuerpo)> EjecutarGuardarAsync(CargaOfertaDto dto, string usuario, string? ip, string navegador, string idProgreso, ICargaOfertaService carga, ICircuitoRepository aud)
        {
            try
            {
                var (r, errs) = await carga.CargarAsync(dto, usuario, ip, navegador, idProgreso);
                if (r is null) return (400, new { mensaje = errs.FirstOrDefault(), errores = errs, precioMinimo = carga.PrecioMinimo?.Info(), precioMinimoLista = carga.PrecioMinimo?.Lista });   // CAMBIO 139
                await aud.AuditarAsync(OfertaRepository.TipoDe(dto.Tipo) == OfertaRepository.TipoPaquete ? "Paquete_Carga" : "Oferta_Carga", r.Ids, new { dto.Modo, r.Codigo, r.LogId }, usuario, ip, navegador);
                return (200, r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en carga manual");
                return (500, new { mensaje = "Error inesperado: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> LeerExcel(IFormFile? archivo)
        {
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige un archivo Excel." });
            if (!archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) && !archivo.FileName.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { mensaje = "El archivo debe ser .xlsx (Excel moderno). Si es .xls, ábrelo y guárdalo como .xlsx." });
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

        /// <summary>Plantilla de ejemplo: fila 1 = fechas (B en adelante), filas siguientes = hotel + precio total por fecha.</summary>
        [HttpGet]
        public async Task<IActionResult> Plantilla()
        {
            using var cn = _aud.Conexion();
            return File(await PlantillaXlsxAsync(cn), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Eurovip-plantilla-carga-manual.xlsx");
        }

        /// <summary>El archivo de la plantilla (fase 2 de proveedores: la misma la descarga el proveedor desde Gestión).</summary>
        public static async Task<byte[]> PlantillaXlsxAsync(System.Data.IDbConnection cnE)
        {
            var estados = await EuroVip.Services.EstadosOferta.ListarAsync(cnE);   // CAMBIO 131: columna opcional «Estado» con su lista
            using var ms = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
            {
                var wb = doc.AddWorkbookPart();
                wb.Workbook = new Workbook();
                var st = wb.AddNewPart<WorkbookStylesPart>();
                st.Stylesheet = new Stylesheet(
                    new Fonts(new Font(), new Font(new Bold())),
                    new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
                    new Borders(new Border()),
                    new CellFormats(new CellFormat(), new CellFormat { NumberFormatId = 14, ApplyNumberFormat = true, FontId = 1, ApplyFont = true }, new CellFormat { FontId = 1, ApplyFont = true }));
                var ws = wb.AddNewPart<WorksheetPart>();
                var data = new SheetData();
                ws.Worksheet = new Worksheet(new Columns(new Column { Min = 1, Max = 1, Width = 34, CustomWidth = true }, new Column { Min = 2, Max = 2, Width = 16, CustomWidth = true }, new Column { Min = 3, Max = 3, Width = 10, CustomWidth = true }, new Column { Min = 4, Max = 11, Width = 13, CustomWidth = true }), data);
                wb.Workbook.AppendChild(new Sheets(new Sheet { Id = wb.GetIdOfPart(ws), SheetId = 1, Name = "Ofertas" }));

                string Ref(int col, int fila) => $"{(char)('A' + col - 1)}{fila}";
                var primera = DateTime.Today.AddDays(((int)DayOfWeek.Saturday - (int)DateTime.Today.DayOfWeek + 7) % 7 + 7);
                var fechas = Enumerable.Range(0, 6).Select(i => primera.AddDays(i * 7)).ToList();

                var enc = new Row { RowIndex = 1 };
                enc.Append(new Cell { CellReference = "A1", DataType = CellValues.String, CellValue = new CellValue("Hotel (nombre como en Eurovip)"), StyleIndex = 2 });
                enc.Append(new Cell { CellReference = "B1", DataType = CellValues.String, CellValue = new CellValue("Estado"), StyleIndex = 2 });
                enc.Append(new Cell { CellReference = "C1", DataType = CellValues.String, CellValue = new CellValue("Cupo"), StyleIndex = 2 });   // CAMBIO 132: habitaciones para vender por fecha
                for (var i = 0; i < fechas.Count; i++)
                    enc.Append(new Cell { CellReference = Ref(i + 4, 1), StyleIndex = 1, CellValue = new CellValue(fechas[i].ToOADate().ToString(CultureInfo.InvariantCulture)) });
                data.Append(enc);

                var ejemplos = new[] { ("Riu Palace Bavaro", 1290m), ("Barcelo Bavaro Palace - All Inclusive", 1150m) };
                for (var h = 0; h < ejemplos.Length; h++)
                {
                    var row = new Row { RowIndex = (uint)(h + 2) };
                    row.Append(new Cell { CellReference = Ref(1, h + 2), DataType = CellValues.String, CellValue = new CellValue(ejemplos[h].Item1) });
                    if (estados.ElementAtOrDefault(h) is { } ej) row.Append(new Cell { CellReference = Ref(2, h + 2), DataType = CellValues.String, CellValue = new CellValue(ej.Nombre) });
                    if (h == 0) row.Append(new Cell { CellReference = Ref(3, h + 2), CellValue = new CellValue("10") });   // ejemplo: 10 habitaciones por fecha; vacío = sin límite
                    for (var i = 0; i < fechas.Count; i++)
                        row.Append(i == 3
                            ? new Cell { CellReference = Ref(i + 4, h + 2), DataType = CellValues.String, CellValue = new CellValue("//") }
                            : new Cell { CellReference = Ref(i + 4, h + 2), CellValue = new CellValue((ejemplos[h].Item2 + i * 20).ToString(CultureInfo.InvariantCulture)) });
                    data.Append(row);
                }
                var nota = new Row { RowIndex = 5 };
                nota.Append(new Cell { CellReference = "A5", DataType = CellValues.String, CellValue = new CellValue("Nota: el precio es el TOTAL que paga el cliente. Escribe // o deja vacío si esa fecha no se vende. Estado (opcional): Confirmada, Bajo petición… (columna B). Cupo (opcional): cuántas habitaciones hay para vender en cada fecha; vacío = sin límite.") });
                data.Append(nota);
                // Lista desplegable del Estado (B2:B500): solo avisa si se escribe otro (al subirlo se busca el parecido)
                var lista = string.Join(",", estados.Select(e => e.Nombre.Replace(",", " ").Replace("\"", "")));
                if (estados.Count > 0 && lista.Length <= 250)
                    ws.Worksheet.Append(new DataValidations(new DataValidation
                    {
                        Type = DataValidationValues.List, AllowBlank = true, ShowErrorMessage = true, ShowInputMessage = true, ErrorStyle = DataValidationErrorStyleValues.Warning,
                        ErrorTitle = "No está en la lista", Error = "Ese estado no existe todavía. Si sigues, al subir el Excel te aviso para elegirlo o crearlo.",
                        PromptTitle = "Estado (opcional)", Prompt = "Confirmada, Bajo petición… o déjalo vacío.",
                        SequenceOfReferences = new ListValue<StringValue> { InnerText = "B2:B500" }, Formula1 = new Formula1("\"" + lista + "\"")
                    }) { Count = 1 });
                wb.Workbook.Save();
            }
            return ms.ToArray();
        }

        private List<string> ErroresModelo() =>
            ModelState.Where(x => x.Value?.Errors.Count > 0)
                      .SelectMany(x => x.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? $"Dato inválido en {x.Key}" : e.ErrorMessage))
                      .Distinct().ToList();
    }
}
