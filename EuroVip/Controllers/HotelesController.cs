using EuroVip.Models;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Fase 2 — Carga de Hoteles. Equivale a /Inicio2/Index?tipoServicio=HOTEL + modal "Nueva Oferta" de BackTiTan,
    /// guardando en [Eurovip].[inicio] con tipo_servicio = 'HOTEL'.
    /// </summary>
    public class HotelesController : Controller
    {
        private readonly IHotelRepository _repo;
        private readonly ILogger<HotelesController> _logger;

        public HotelesController(IHotelRepository repo, ILogger<HotelesController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var modelo = new HotelesPaginaViewModel
            {
                Monedas = await _repo.MonedasAsync(),
                Estadisticas = await _repo.EstadisticasAsync()
            };
            return View(modelo);
        }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] HotelFiltro filtro)
        {
            var resultado = await _repo.ListarAsync(filtro);
            var stats = await _repo.EstadisticasAsync();
            return Json(new { resultado.Items, resultado.Total, resultado.Pagina, resultado.Tam, resultado.Paginas, stats });
        }

        [HttpGet]
        public async Task<IActionResult> Estadisticas() => Json(await _repo.EstadisticasAsync());

        [HttpGet]
        public async Task<IActionResult> Obtener(int id)
        {
            var hotel = await _repo.ObtenerAsync(id);
            return hotel is null ? NotFound(new { mensaje = "No encontramos ese hotel." }) : Json(hotel);
        }

        // Hoteles solo en ES: campañas y secciones siempre del idioma español.
        [HttpGet]
        public async Task<IActionResult> Campanyas() => Json(await _repo.CampanyasAsync(HotelRepository.IdiomaHotel));

        [HttpGet]
        public async Task<IActionResult> Contenedores() => Json(await _repo.ContenedoresAsync(HotelRepository.IdiomaHotel));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] HotelGuardarDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(x => x.Key, x => x.Value!.Errors.First().ErrorMessage);
                return BadRequest(new { mensaje = "Revisa los campos marcados.", errores });
            }

            try
            {
                if (dto.Id_Servicio is null)
                {
                    var id = await _repo.CrearAsync(dto);
                    await Auditar("Hotel_Crear", id, dto);
                    _logger.LogInformation("Hotel {Id} creado por {Usuario}", id, User.Identity?.Name);
                    return Json(new { ok = true, id, mensaje = $"¡Listo! \"{dto.Id_Destino}\" se agregó a la vitrina." });
                }

                var antes = await _repo.ObtenerAsync(dto.Id_Servicio.Value);
                if (antes is null)
                    return NotFound(new { mensaje = "Ese hotel ya no existe. Recarga la página." });

                await _repo.EditarAsync(dto);
                await Auditar("Hotel_Editar", dto.Id_Servicio.Value, new { antes, despues = dto });
                return Json(new { ok = true, id = dto.Id_Servicio, mensaje = "Cambios guardados." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando hotel {@Dto}", dto);
                return StatusCode(500, new { mensaje = "No se pudo guardar. Intenta de nuevo en unos segundos." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id)
        {
            var nuevo = await _repo.CambiarEstadoAsync(id);
            if (nuevo is null) return NotFound(new { mensaje = "No encontramos ese hotel." });
            await Auditar("Hotel_Estado", id, new { estado_nuevo = nuevo });
            return Json(new { ok = true, estado = nuevo, mensaje = nuevo == true ? "Ahora se ve en la web." : "Oculto de la web." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var eliminado = await _repo.EliminarAsync(id);
            if (eliminado is null) return NotFound(new { mensaje = "No encontramos ese hotel." });
            await Auditar("Hotel_Eliminar", id, eliminado);
            _logger.LogInformation("Hotel {Id} eliminado por {Usuario}", id, User.Identity?.Name);
            return Json(new { ok = true, mensaje = $"\"{eliminado.Id_Destino}\" se eliminó." });
        }

        private Task Auditar(string tipo, int id, object cambios) =>
            _repo.AuditarAsync(tipo, id, cambios,
                User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
    }
}
