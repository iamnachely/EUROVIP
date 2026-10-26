using EuroVip.Models;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace EuroVip.Controllers
{
    /// <summary>
    /// "Carga manual" del menú: lista de ofertas de hotel cargadas (buscar, filtrar, editar, activar/desactivar,
    /// eliminar una o varias, exportar). Desde aquí se abren la Carga Express y la Clásica.
    /// </summary>
    public class OfertasController : Controller
    {
        private readonly IOfertaRepository _repo;
        private readonly ICircuitoRepository _aud;
        private readonly ILogger<OfertasController> _logger;

        public OfertasController(IOfertaRepository repo, ICircuitoRepository aud, ILogger<OfertasController> logger)
        {
            _repo = repo;
            _aud = aud;
            _logger = logger;
        }

        public IActionResult Index() { ViewData["Tipo"] = OfertaRepository.Tipo; return View(); }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] OfertaFiltro f)
        {
            var (items, total) = await _repo.ListarAsync(f);
            return Json(new { items, total, pagina = Math.Max(1, f.Pagina), tam = Math.Clamp(f.Tam, 10, 100), stats = await _repo.EstadisticasAsync(OfertaRepository.TipoDe(f.Tipo)) });
        }

        [HttpGet] public async Task<IActionResult> Destinos(string? tipo) => Json(await _repo.DestinosConOfertasAsync(OfertaRepository.TipoDe(tipo)));
        [HttpGet] public async Task<IActionResult> Habitaciones(int hotel) => Json(await _repo.HabitacionesAsync(hotel));

        [HttpGet]
        public async Task<IActionResult> Obtener(int id)
        {
            var o = await _repo.ObtenerAsync(id);
            return o is null ? NotFound(new { mensaje = "No encontramos esa oferta." }) : Json(o);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar([FromBody] OfertaEditarDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(new { mensaje = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });
            var antes = await _repo.ObtenerAsync(dto.Id_Servicio);
            // Si se cambia la fecha de entrada, tiene que ser desde mañana (las de hoy o pasadas no se venden ni salen en la web).
            if (antes is not null && dto.Desde.Date != antes.Desde.Date && dto.Desde.Date <= DateTime.Today)
                return BadRequest(new { mensaje = $"La fecha de entrada tiene que ser desde mañana ({DateTime.Today.AddDays(1):dd/MM/yyyy}). Con una fecha de hoy o pasada la oferta no se vende ni sale en la web." });
            var error = await _repo.EditarAsync(dto);
            if (error is not null) return BadRequest(new { mensaje = error });
            await Auditar("Oferta_Editar", new[] { dto.Id_Servicio }, new { antes, despues = dto });
            return Json(new { ok = true, mensaje = $"Oferta #{dto.Id_Servicio} actualizada." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lote([FromBody] OfertaLoteDto dto)
        {
            if (dto.Ids.Count == 0) return BadRequest(new { mensaje = "No elegiste ninguna oferta." });
            var n = await _repo.LoteAsync(dto);
            await Auditar("Oferta_Lote", dto.Ids, dto);
            var que = dto.Activo is true ? "activadas" : dto.Activo is false ? "desactivadas" : "actualizadas";
            return Json(new { ok = true, mensaje = $"{n} oferta{(n == 1 ? "" : "s")} {que}." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar([FromBody] List<int> ids)
        {
            if (ids is null || ids.Count == 0) return BadRequest(new { mensaje = "No elegiste ninguna oferta." });
            var n = await _repo.EliminarAsync(ids);
            await Auditar("Oferta_Eliminar", ids, new { ids });
            return Json(new { ok = true, mensaje = $"Se eliminaron {n} oferta{(n == 1 ? "" : "s")}." });
        }

        [HttpGet]
        public async Task<IActionResult> Exportar([FromQuery] OfertaFiltro f)
        {
            f.Pagina = 1; f.Tam = 100;
            var todas = new List<OfertaItem>();
            while (true)
            {
                var (items, total) = await _repo.ListarAsync(f);
                todas.AddRange(items);
                if (todas.Count >= total || !items.Any()) break;
                f.Pagina++;
            }
            var sb = new StringBuilder();
            sb.AppendLine(Csv.Fila("Nº", "Hotel", "Destino", "Régimen", "Entrada", "Salida", "Noches", "Campaña", "Precio base", "Tasas", "Descuento %", "Precio web",
                                   "Moneda", "Origen", "Vuelos", "Activa", "Código", "Usuario"));
            foreach (var o in todas)
                sb.AppendLine(Csv.Fila(o.Id_Servicio, o.Hotel, o.Destino, $"{o.Id_Regimen} {o.Regimen}", o.Desde.ToString("dd/MM/yyyy"), o.Hasta.ToString("dd/MM/yyyy"),
                    o.Noches, o.Campanya, o.Precio_Base, o.Tasas, o.Descuento, o.PrecioWeb, o.Id_Moneda, o.Origen, o.Vuelos, o.Activo, o.Codigo, o.Usuario));
            return Csv.Archivo(sb, $"eurovip-{(OfertaRepository.TipoDe(f.Tipo) == OfertaRepository.TipoPaquete ? "paquetes" : "solo-hotel")}-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        private Task Auditar(string tipo, IEnumerable<int> ids, object cambios) =>
            _aud.AuditarAsync(tipo, ids, cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
