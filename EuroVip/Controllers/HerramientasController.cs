using EuroVip.Models;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EuroVip.Controllers
{
    /// <summary>
    /// "Herramientas" del menú: un solo lugar con todos los mantenimientos básicos
    /// (destinos, monedas, campañas, países, idiomas, tasas, regímenes, estaciones, proveedores,
    /// alojamientos y habitaciones). Index = tablero con tarjetas; Catalogo/{id} = la vista de cada uno.
    /// </summary>
    public class HerramientasController : Controller
    {
        private readonly IHerramientasRepository _repo;
        private readonly ICircuitoRepository _aud;
        private readonly ILogger<HerramientasController> _logger;

        public HerramientasController(IHerramientasRepository repo, ICircuitoRepository aud, ILogger<HerramientasController> logger)
        {
            _repo = repo;
            _aud = aud;
            _logger = logger;
        }

        public IActionResult Index() => View();

        public IActionResult Catalogo(string id)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return RedirectToAction(nameof(Index));
            return View(c);
        }

        [HttpGet] public async Task<IActionResult> Resumen() => Json(await _repo.ResumenAsync());
        [HttpGet] public async Task<IActionResult> Actividad() => Json(await _repo.ActividadAsync(8));

        [HttpGet]
        public async Task<IActionResult> Buscar(string? q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Json(Array.Empty<object>());
            return Json(await _repo.BuscarGlobalAsync(q));
        }

        [HttpGet]
        public async Task<IActionResult> Listar(string id, string? q, int pagina = 1, int tam = 25)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            var filtros = Request.Query.Where(x => x.Key.StartsWith("f_")).ToDictionary(x => x.Key[2..], x => x.Value.ToString());
            var (filas, total) = await _repo.ListarAsync(c, q, filtros, pagina, tam);
            return Json(new { filas, total, pagina = Math.Max(1, pagina), tam = Math.Clamp(tam, 10, 100) });
        }

        [HttpGet]
        public async Task<IActionResult> Lista(string nombre, string? q)
        {
            try { return Json(await _repo.ListaAsync(nombre, q)); }
            catch (ArgumentException) { return BadRequest(new { mensaje = "Lista desconocida." }); }
        }

        public class PeticionDto
        {
            public Dictionary<string, JsonElement> Clave { get; set; } = new();
            public Dictionary<string, JsonElement> Datos { get; set; } = new();
            public bool Nuevo { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Obtener(string id, [FromBody] PeticionDto dto)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            var fila = await _repo.ObtenerAsync(c, dto.Clave);
            if (fila is null) return NotFound(new { mensaje = $"No encontramos {c.Ese} {c.Singular}." });
            var usos = await _repo.UsosAsync(c, dto.Clave);
            return Json(new { fila, usos = usos.Select(u => new { u.Etiqueta, u.N }) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(string id, [FromBody] PeticionDto dto)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            try
            {
                var (clave, error) = await _repo.GuardarAsync(c, dto.Datos, dto.Nuevo);
                if (error is not null) return BadRequest(new { mensaje = error });
                await Auditar($"Herramienta_{c.Clave}_{(dto.Nuevo ? "Crear" : "Editar")}", new { clave, datos = dto.Datos });
                return Json(new { ok = true, clave, mensaje = dto.Nuevo ? $"Se creó {c.El} {c.Singular}." : $"Se guardaron los cambios de {c.El} {c.Singular}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando {Catalogo}", c.Clave);
                return StatusCode(500, new { mensaje = "No se pudo guardar: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(string id, [FromBody] PeticionDto dto)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            var antes = await _repo.ObtenerAsync(c, dto.Clave);
            var error = await _repo.EliminarAsync(c, dto.Clave);
            if (error is not null) return BadRequest(new { mensaje = error });
            await Auditar($"Herramienta_{c.Clave}_Eliminar", new { antes });
            return Json(new { ok = true, mensaje = $"Se eliminó {c.El} {c.Singular}." });
        }

        private Task Auditar(string tipo, object cambios) =>
            _aud.AuditarAsync(tipo, Array.Empty<int>(), cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
