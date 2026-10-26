using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Paquetes: la misma pantalla que Solo Hotel (/Ofertas/Index), pero lista y carga servicios tipo PAQUETE.
    /// Buscar, editar, activar y eliminar usan las mismas acciones de OfertasController (con ?tipo=PAQUETE).
    /// </summary>
    public class PaquetesController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Tipo"] = OfertaRepository.TipoPaquete;
            return View("~/Views/Ofertas/Index.cshtml");
        }
    }
}
