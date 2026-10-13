using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>Consultar ofertas (equivale a /ConsultarOferta/Index de BackTiTan). En construcción.</summary>
    public class ConsultarOfertaController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Consultar ofertas";
            ViewData["Icono"] = "bi-search-heart";
            ViewData["Texto"] = "Aquí vas a buscar cualquier oferta o salida cargada (por hotel, destino, fechas, precio o campaña) y ver todo su detalle: habitaciones, tasas, vuelos y notas.";
            return View("Pronto");
        }
    }
}
