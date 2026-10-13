using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace EuroVip.Controllers
{
    /// <summary>Pestaña "Reportes": tablero con indicadores y gráficos de lo cargado, y exportación a CSV.</summary>
    public class ReportesController : Controller
    {
        private readonly IReporteRepository _repo;
        public ReportesController(IReporteRepository repo) => _repo = repo;

        public IActionResult Index() => View();

        [HttpGet] public async Task<IActionResult> Datos() => Json(await _repo.TableroAsync());

        [HttpGet]
        public async Task<IActionResult> Salidas(bool soloActivas = true, DateTime? desde = null, DateTime? hasta = null) =>
            Json(await _repo.SalidasAsync(soloActivas, desde ?? DateTime.Today, hasta));

        [HttpGet]
        public async Task<IActionResult> ExportarSalidas(bool soloActivas = true, DateTime? desde = null, DateTime? hasta = null)
        {
            var salidas = await _repo.SalidasAsync(soloActivas, desde ?? DateTime.Today, hasta);
            var sb = new StringBuilder();
            sb.AppendLine(Csv.Fila("Nº salida", "Tipo", "Circuito / producto", "Salida", "Regreso", "Campaña", "Categoría", "Origen", "Vuelos",
                                   "Precio base", "Descuento %", "Precio web", "Moneda", "Activa", "Código de carga", "Usuario", "Cargada el"));
            foreach (var s in salidas)
                sb.AppendLine(Csv.Fila(s.Id_Servicio, s.Tipo, s.Nombre, s.Desde?.ToString("dd/MM/yyyy"), s.Hasta?.ToString("dd/MM/yyyy"), s.Campanya,
                    s.Categoria, s.Origen, s.Vuelos, s.Precio_Base, s.Descuento, s.PrecioWeb, s.Id_Moneda, s.Activo, s.Codigo, s.Usuario, s.Apertura.ToString("dd/MM/yyyy")));
            return Csv.Archivo(sb, $"eurovip-salidas-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }
    }
}
