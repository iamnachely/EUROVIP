using Dapper;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Consultas comunes de las cargas (FASE 38), de solo lectura (las usan todas las pantallas):
    ///   • /Codigos/Disponible?codigo=&amp;producto=&amp;clave=  → revisa el código interno mientras se escribe.
    ///   • /Codigos/Proveedores                              → todos los proveedores (para el buscador Tom Select).
    ///   • /Codigos/Monedas                                  → las monedas.
    /// </summary>
    public class CodigosController : Controller
    {
        private readonly ICircuitoRepository _db;
        private readonly IMemoryCache _cache;
        public CodigosController(ICircuitoRepository db, IMemoryCache cache) { _db = db; _cache = cache; }

        [HttpGet]
        public async Task<IActionResult> Disponible(string? codigo, string producto, string? clave, string? anterior)
        {
            var c = CodigoInterno.Limpiar(codigo);
            if (c is null) return Json(new { ok = true, vacio = true, mensaje = "Opcional: letras y números, sin espacios." });
            using var cn = _db.Conexion();
            var error = await CodigoInterno.ErrorAsync(cn, c, producto, clave, anterior);
            return Json(new { ok = error is null, vacio = false, mensaje = error ?? $"«{c}» está libre." });
        }

        [HttpGet]
        public async Task<IActionResult> Proveedores()
        {
            var lista = await _cache.GetOrCreateAsync("ev:proveedores", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                using var cn = _db.Conexion();
                return (await cn.QueryAsync<Opcion>(@"
                    SELECT id_proveedor AS Valor, LTRIM(RTRIM(nombre)) AS Texto, id_tipo_proveedor AS Extra
                    FROM Eurovip.proveedor
                    WHERE id_proveedor <> '0' AND LTRIM(nombre) NOT LIKE 'Elegir%'   -- filas de relleno de ViajeVDT
                    ORDER BY CASE WHEN id_tipo_proveedor = 'Tour Operador' THEN 0 ELSE 1 END, nombre")).ToList();
            });
            return Json(lista);
        }

        [HttpGet]
        public async Task<IActionResult> Monedas() => Json(await _db.MonedasAsync());
    }
}
