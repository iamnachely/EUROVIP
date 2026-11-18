using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// CAMBIO 139 · Botón «Precio mínimo» de cada vista de carga: ver y establecer el mínimo de esa vista.
    /// La validación está en cada carga (formularios y Excel); aquí solo se lee y se guarda el valor.
    /// </summary>
    public class PreciosMinimosController : Controller
    {
        private readonly ICircuitoRepository _db;
        public PreciosMinimosController(ICircuitoRepository db) => _db = db;

        /// <summary>El mínimo de una vista: { modulo, nombre, minimo } (minimo null = sin mínimo).</summary>
        [HttpGet]
        public async Task<IActionResult> Valor(string modulo)
        {
            if (PreciosMinimos.Normalizar(modulo) is not { } m) return BadRequest(new { mensaje = "Esa vista no existe." });
            using var cn = _db.Conexion();
            return Json(new { modulo = m, nombre = PreciosMinimos.Modulos[m].Nombre, minimo = await PreciosMinimos.ObtenerAsync(cn, m) });
        }

        public sealed class GuardarDto { public string Modulo { get; set; } = ""; public decimal? Precio { get; set; } }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] GuardarDto d)
        {
            if (PreciosMinimos.Normalizar(d.Modulo) is not { } m) return BadRequest(new { mensaje = "Esa vista no existe." });
            if (d.Precio is < 0 or > 100_000_000) return BadRequest(new { mensaje = "Escribe un precio de 0 a 100,000,000 (o déjalo vacío para no tener mínimo)." });
            var precio = d.Precio is > 0 ? Math.Round(d.Precio.Value, 2) : (decimal?)null;   // 0 o vacío = sin mínimo
            var usuario = User.Identity?.Name ?? "desconocido";
            using var cn = _db.Conexion();
            var antes = await PreciosMinimos.GuardarAsync(cn, m, precio, usuario);
            await _db.AuditarAsync("PrecioMinimo_" + m, Array.Empty<int>(), new { antes, despues = precio }, usuario,
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            var nombre = PreciosMinimos.Modulos[m].Nombre;
            return Json(new
            {
                ok = true, modulo = m, minimo = precio,
                mensaje = precio is null ? $"{nombre}: sin precio mínimo. Se puede cargar cualquier precio."
                                         : $"{nombre}: el precio mínimo ahora es {PreciosMinimos.Cifra(precio.Value)}. No se cargará nada por debajo."
            });
        }
    }
}
