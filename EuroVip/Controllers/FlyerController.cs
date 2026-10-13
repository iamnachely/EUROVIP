using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>Flyers (equivale a /Flyer/Hub de BackTiTan). En construcción.</summary>
    public class FlyerController : Controller
    {
        public IActionResult Hub()
        {
            ViewData["Title"] = "Flyers";
            ViewData["Icono"] = "bi-file-earmark-richtext";
            ViewData["Texto"] = "Aquí vas a crear flyers de las ofertas listos para compartir, con plantillas, precios al día y descarga en imagen o PDF.";
            return View("Pronto");
        }
    }
}
