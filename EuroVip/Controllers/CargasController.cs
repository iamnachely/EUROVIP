using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace EuroVip.Controllers
{
    /// <summary>Pestaña "Cargas": historial de todas las cargas (Eurovip.Log_Cargas) con su detalle.</summary>
    public class CargasController : Controller
    {
        private readonly IReporteRepository _repo;
        public CargasController(IReporteRepository repo) => _repo = repo;

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] CargaFiltro f)
        {
            var (items, total) = await _repo.CargasAsync(f);
            var kpis = await _repo.CargaKpisAsync();
            var tipos = await _repo.TiposCargaAsync();
            return Json(new { items, total, pagina = Math.Max(1, f.Pagina), tam = Math.Clamp(f.Tam, 10, 100), kpis, tipos });
        }

        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var carga = await _repo.CargaAsync(id);
            if (carga is null) return NotFound(new { mensaje = "No encontramos esa carga." });
            var salidas = string.IsNullOrWhiteSpace(carga.Codigo_Oferta) ? Enumerable.Empty<SalidaReporte>() : await _repo.SalidasDeCargaAsync(carga.Codigo_Oferta);
            return Json(new { carga, salidas });
        }

        [HttpGet]
        public async Task<IActionResult> Exportar([FromQuery] CargaFiltro f)
        {
            f.Pagina = 1; f.Tam = 100;
            var todas = new List<CargaLog>();
            while (true)
            {
                var (items, total) = await _repo.CargasAsync(f);
                todas.AddRange(items);
                if (todas.Count >= total || !items.Any()) break;
                f.Pagina++;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Csv.Fila("Nº", "Fecha", "Tipo", "Usuario", "Circuito/Destino", "Origen", "Campañas", "Desde", "Hasta", "Código", "Estado", "Salidas creadas", "Mensaje"));
            foreach (var c in todas)
                sb.AppendLine(Csv.Fila(c.Id, c.Fecha_Hora.ToString("dd/MM/yyyy HH:mm"), c.Tipo_Carga, c.Usuario, c.Destino, c.Origen, c.Campanyas,
                    c.Fecha_Inicio?.ToString("dd/MM/yyyy"), c.Fecha_Fin?.ToString("dd/MM/yyyy"), c.Codigo_Oferta, c.Estatus, c.Registros_Insertados, c.Mensaje));
            return Csv.Archivo(sb, $"eurovip-cargas-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }
    }

    /// <summary>CSV para Excel en español: separador ";", UTF-8 con BOM y comillas cuando hace falta.</summary>
    internal static class Csv
    {
        public static string Fila(params object?[] celdas) => string.Join(";", celdas.Select(Celda));

        private static string Celda(object? v)
        {
            var s = v switch
            {
                null => "",
                decimal d => d.ToString("0.##", new CultureInfo("es-ES")),
                bool b => b ? "Sí" : "No",
                _ => v.ToString() ?? ""
            };
            return s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        public static FileContentResult Archivo(StringBuilder sb, string nombre) =>
            new(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), "text/csv; charset=utf-8") { FileDownloadName = nombre };
    }
}
