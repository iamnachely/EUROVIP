using Dapper;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Quién creó y quién cambió por última vez un registro de las cargas (FASE 33: columnas usuario_alta, fecha_alta,
    /// usuario_modificacion y fecha_modificacion que llenan los triggers trEurovip_*_quien). Solo lectura; lo usa
    /// wwwroot/js/eurovip-quien.js en la cabecera de los editores de Hoteles, Solo Hotel/Paquetes, Circuitos, Excursiones,
    /// Traslados y Cruceros.
    /// </summary>
    public class QuienController : Controller
    {
        private readonly ICircuitoRepository _db;
        public QuienController(ICircuitoRepository db) => _db = db;

        // qué → (tabla, condición, columnas). Lista cerrada: el nombre de la tabla nunca sale de la petición.
        // Lo anterior a la FASE 33 usa lo que esas tablas ya guardaban: servicio.usuario (quién cargó la oferta) y apertura;
        // traslado/crucero.usuario (el último que guardó).
        private const string Base = "usuario_alta AS Usuario_Alta, fecha_alta AS Fecha_Alta, usuario_modificacion AS Usuario_Modificacion, fecha_modificacion AS Fecha_Modificacion";
        private const string ConUltimo = "usuario_alta AS Usuario_Alta, fecha_alta AS Fecha_Alta, COALESCE(usuario_modificacion, usuario) AS Usuario_Modificacion, fecha_modificacion AS Fecha_Modificacion";
        private static readonly Dictionary<string, (string Tabla, string Where, string Cols)> Que = new(StringComparer.OrdinalIgnoreCase)
        {
            ["hotel"] = ("inicio", "id_servicio = @id AND tipo_servicio = 'HOTEL' AND id_idioma = 'ES'", Base),
            ["oferta"] = ("servicio", "id_servicio = @id",
                "COALESCE(usuario_alta, usuario) AS Usuario_Alta, COALESCE(fecha_alta, apertura) AS Fecha_Alta, usuario_modificacion AS Usuario_Modificacion, fecha_modificacion AS Fecha_Modificacion"),
            ["circuito"] = ("circuito", "id = @id AND id_idioma = 'ES'", Base),
            ["excursion"] = ("servicio_excursion", "id_Excursion = @id", Base),
            ["traslado"] = ("traslado", "id_traslado = @id", ConUltimo),
            ["crucero"] = ("crucero", "id_crucero = @id", ConUltimo),
        };

        [HttpGet]
        public async Task<IActionResult> Ver(string que, string id)
        {
            if (!Que.TryGetValue(que ?? "", out var q) || string.IsNullOrWhiteSpace(id)) return BadRequest(new { mensaje = "Registro desconocido." });
            object clave = q.Tabla == "circuito" ? id.Trim() : int.TryParse(id, out var n) ? n : -1;
            using var cn = _db.Conexion();
            var r = await cn.QueryFirstOrDefaultAsync<Fila>($@"
                SELECT TOP 1 {q.Cols} FROM Eurovip.[{q.Tabla}] WHERE {q.Where}", new { id = clave });
            return r is null ? NotFound(new { mensaje = "No encontrado." }) : Json(r);
        }

        public sealed class Fila
        {
            public string? Usuario_Alta { get; set; }
            public DateTime? Fecha_Alta { get; set; }
            public string? Usuario_Modificacion { get; set; }
            public DateTime? Fecha_Modificacion { get; set; }
        }
    }
}
