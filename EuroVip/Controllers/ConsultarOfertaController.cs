using EuroVip.Models;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace EuroVip.Controllers
{
    /// <summary>
    /// "Consultar ofertas" (equivale a /ConsultarOferta/Index de BackTiTan): busca ofertas de hotel y salidas de circuito
    /// con muchos filtros y permite editarlas, activarlas/desactivarlas, duplicarlas (también a otras fechas) y poner tasas, en masa.
    /// </summary>
    public class ConsultarOfertaController : Controller
    {
        private readonly IConsultaRepository _repo;
        private readonly IOfertaRepository _ofertas;
        private readonly ICircuitoRepository _db;
        private readonly ILogger<ConsultarOfertaController> _logger;

        public ConsultarOfertaController(IConsultaRepository repo, IOfertaRepository ofertas, ICircuitoRepository db, ILogger<ConsultarOfertaController> logger)
        {
            _repo = repo;
            _ofertas = ofertas;
            _db = db;
            _logger = logger;
        }

        public IActionResult Index() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Buscar([FromBody] ConsultaFiltro f)
        {
            try
            {
                var (filas, resumen) = await _repo.BuscarAsync(f);
                return Json(new { filas, resumen, pagina = Math.Max(1, f.Pagina), tam = Math.Clamp(f.Tam, 10, 200) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Consultar ofertas");
                return StatusCode(500, new { mensaje = "No se pudo buscar: " + ex.Message });
            }
        }

        /// <summary>Ids de TODO lo filtrado (para "seleccionar todos los resultados").</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Ids([FromBody] ConsultaFiltro f) =>
            Json((await _repo.TodasAsync(f, ConsultaRepository.MaxMasivo)).Select(x => x.Id_Servicio));

        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var d = await _repo.DetalleAsync(id);
            return d is null ? NotFound(new { mensaje = "No encontramos ese servicio." }) : Json(d);
        }

        [HttpGet] public async Task<IActionResult> Usuarios() => Json(await _repo.UsuariosAsync());
        [HttpGet] public async Task<IActionResult> Destinos() => Json(await _ofertas.DestinosConOfertasAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar([FromBody] ConsultaEditarDto d)
        {
            if (d.Ids.Count == 0) return BadRequest(new { mensaje = "No elegiste ninguna oferta." });
            if (d.Ids.Count > ConsultaRepository.MaxMasivo) return BadRequest(new { mensaje = $"Máximo {ConsultaRepository.MaxMasivo} ofertas por vez." });
            if (d.PrecioBase is not null && d.SumarPrecio is not null) return BadRequest(new { mensaje = "Elige fijar el precio O sumarle, no las dos cosas." });
            if (d.PrecioBase is not null)
            {
                var min = await _db.PrecioBaseMinimoAsync();
                if (d.PrecioBase < min) return BadRequest(new { mensaje = $"El precio base no puede ser menor que {min:N0}." });
            }
            if (d is { Moneda: null, Id_Campanya: null, Proveedor: null, PrecioBase: null, SumarPrecio: null or 0, Descuento: null, PrecioMasMenos: null, Activo: null, Aerolinea: null })
                return BadRequest(new { mensaje = "No elegiste ningún cambio." });
            try
            {
                var n = await _repo.EditarAsync(d);
                await Auditar("Consulta_Editar", d.Ids, d);
                return Json(new { ok = true, mensaje = $"Se actualizaron {n} oferta{(n == 1 ? "" : "s")}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editando en Consultar ofertas");
                return StatusCode(500, new { mensaje = "No se guardó nada: " + ex.Message });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicar([FromBody] ConsultaDuplicarDto d)
        {
            if (d.Ids.Count == 0) return BadRequest(new { mensaje = "No elegiste ninguna oferta." });
            if (d.Dias is < -365 or > 730) return BadRequest(new { mensaje = "Los días a mover deben estar entre -365 y 730." });
            if (d.Veces > 1 && d.Dias == 0) return BadRequest(new { mensaje = "Para hacer varias copias indica cuántos días separa cada una." });
            try
            {
                var nuevos = await _repo.DuplicarAsync(d, User.Identity?.Name ?? "eurovip");
                await Auditar("Consulta_Duplicar", nuevos, new { origen = d.Ids, d.Id_Campanya, d.Dias, d.Veces, d.Origen });
                return Json(new { ok = true, nuevos, mensaje = $"Se crearon {nuevos.Count} copia{(nuevos.Count == 1 ? "" : "s")}." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error duplicando en Consultar ofertas");
                return StatusCode(500, new { mensaje = "No se creó ninguna copia: " + ex.Message });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Tasa([FromBody] ConsultaTasaDto d)
        {
            if (d.Ids.Count == 0) return BadRequest(new { mensaje = "No elegiste ninguna oferta." });
            if (string.IsNullOrWhiteSpace(d.Id_Tasa)) return BadRequest(new { mensaje = "Elige el suplemento o tasa." });
            if (!d.Quitar && d.Valor < 0) return BadRequest(new { mensaje = "El valor no puede ser negativo." });
            var n = await _repo.TasaAsync(d);
            await Auditar("Consulta_Tasa", d.Ids, d);
            return Json(new { ok = true, mensaje = d.Quitar ? $"Se quitó «{d.Id_Tasa}» de {n} oferta(s)." : $"«{d.Id_Tasa}» = {d.Valor:N2} en {n} oferta(s)." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Exportar([FromForm] string filtro)
        {
            var f = System.Text.Json.JsonSerializer.Deserialize<ConsultaFiltro>(filtro, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            var filas = await _repo.TodasAsync(f, 20000);
            var sb = new StringBuilder();
            sb.AppendLine(Csv.Fila("Nº", "Tipo", "Destino", "Hotel / circuito", "Régimen", "Habitación", "Entrada", "Salida", "Noches", "Origen", "Campaña", "Proveedor",
                                   "Precio base", "Tasas", "Descuento %", "Más/menos", "Precio web", "Moneda", "Vuelo", "Activa", "Código", "Usuario", "Alta"));
            foreach (var x in filas)
                sb.AppendLine(Csv.Fila(x.Id_Servicio, x.Id_Tipo_Servicio, x.Destino, x.NombreAlojamiento ?? x.Nombre_Circuito, x.Regimen, x.Habitacion,
                    x.Inicio?.ToString("dd/MM/yyyy"), x.Regreso?.ToString("dd/MM/yyyy"), x.Noches, x.Origen, x.NombreCampanya, x.NombreProveedor,
                    x.Precio_Base, x.TotalTasa, x.Descuento, x.Precio_Mas_Menos, x.PrecioWeb, x.Id_Moneda,
                    x.ConVuelo ? $"{x.NombreAerolinea} {x.Vuelo}".Trim() : "Sin vuelo", x.Activo, x.Codigo, x.Usuario, x.Apertura?.ToString("dd/MM/yyyy")));
            return Csv.Archivo(sb, $"eurovip-consulta-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        private Task Auditar(string tipo, IEnumerable<int> ids, object cambios) =>
            _db.AuditarAsync(tipo, ids, cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
