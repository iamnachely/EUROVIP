using Dapper;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Puestos de prioridad (FASE 37): quién ocupa el 1, 2, 3… en la misma lista de la web que un registro, para que el
    /// selector de wwwroot/js/eurovip-prioridad.js muestre el orden, el siguiente libre y con quién se intercambia.
    /// Solo lectura: el orden sin repetidos lo mantienen los triggers trEurovip_*_peso (Eurovip.spPesoOrdenar).
    /// </summary>
    public class PrioridadController : Controller
    {
        private readonly ICircuitoRepository _db;
        public PrioridadController(ICircuitoRepository db) => _db = db;

        // qué → tabla de Eurovip.vPeso
        private static readonly Dictionary<string, string> Tabla = new(StringComparer.OrdinalIgnoreCase)
        {
            ["oferta"] = "servicio", ["paquete"] = "servicio", ["hotel"] = "inicio", ["tarjeta"] = "inicio",
            ["circuito"] = "circuito", ["excursion"] = "excursion", ["traslado"] = "traslado", ["crucero"] = "crucero",
        };

        /// <summary>
        /// GET /Prioridad/Ocupados?que=&amp;id= (registro guardado) o, si es nuevo, con lo que define su lista:
        /// aloj + tipo (ofertas), destino (excursiones/traslados), contenedor o tipo (Home). Circuitos y cruceros son una sola lista.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Ocupados(string que, string? id, int? aloj, string? tipo, string? destino, int? contenedor)
        {
            if (!Tabla.TryGetValue(que ?? "", out var tabla)) return BadRequest(new { mensaje = "Lista desconocida." });
            using var cn = _db.Conexion();
            (string? Ambito, string? Unidad) yo = (null, null);
            if (!string.IsNullOrWhiteSpace(id))
            {
                var sqlYo = que!.ToLowerInvariant() switch
                {
                    "hotel" => "SELECT TOP 1 v.ambito, v.unidad FROM Eurovip.vPeso v JOIN Eurovip.inicio i ON v.tabla = 'inicio' AND v.clave = CAST(i.id AS varchar(120)) WHERE i.id_servicio = TRY_CAST(@id AS int) AND i.tipo_servicio = 'HOTEL' ORDER BY CASE WHEN i.id_idioma = 'ES' THEN 0 ELSE 1 END",
                    "circuito" => "SELECT TOP 1 v.ambito, v.unidad FROM Eurovip.vPeso v JOIN Eurovip.circuito c ON v.tabla = 'circuito' AND v.clave = c.nombre_circuito + '|' + c.id_idioma WHERE (c.id = @id OR c.nombre_circuito = @id) AND c.id_idioma = 'ES'",
                    _ => "SELECT TOP 1 ambito, unidad FROM Eurovip.vPeso WHERE tabla = @tabla AND clave = @id"
                };
                yo = await cn.QueryFirstOrDefaultAsync<(string?, string?)>(sqlYo, new { tabla, id = id.Trim() });
            }
            if (yo.Ambito is null)
            {
                // registro nuevo: la lista donde va a quedar
                var ambito = tabla switch
                {
                    "servicio" when aloj is not null => await cn.ExecuteScalarAsync<string?>(
                        "SELECT TOP 1 @tipo + '|' + ISNULL(id_destino, '') FROM Eurovip.alojamiento WHERE id_alojamiento = @aloj AND id_idioma = 'ES'",
                        new { aloj, tipo = (tipo ?? "OFERTA").Trim().ToUpperInvariant() == "PAQUETES" ? "PAQUETES" : "OFERTA" }),
                    "inicio" when contenedor is not null => "C|" + contenedor,
                    "inicio" when !string.IsNullOrWhiteSpace(tipo) => "T|" + tipo.Trim().ToUpperInvariant(),
                    "circuito" => "ES",
                    "crucero" => "CRUCERO",
                    "excursion" or "traslado" when !string.IsNullOrWhiteSpace(destino) => destino.Trim(),
                    _ => null
                };
                yo = (ambito, aloj?.ToString());
            }
            if (yo.Ambito is null) return Json(new { ambito = (string?)null, lista = Array.Empty<object>() });

            var lista = (await cn.QueryAsync<Puesto>(@"
                SELECT MIN(peso) AS Peso, unidad AS Unidad, MIN(nombre) AS Nombre
                FROM Eurovip.vPeso WHERE tabla = @tabla AND ambito = @a AND peso IS NOT NULL
                GROUP BY unidad ORDER BY MIN(peso)", new { tabla, a = yo.Ambito })).ToList();
            lista.ForEach(p => p.Yo = p.Unidad == yo.Unidad);
            var texto = tabla switch
            {
                "servicio" => "hoteles de " + yo.Ambito.Split('|').Last(),
                "inicio" => yo.Ambito.StartsWith("C|") ? "tarjetas de esta sección de la Home" : "tarjetas sin sección",
                "circuito" => "circuitos",
                "crucero" => "cruceros",
                _ => (tabla == "excursion" ? "excursiones de " : "traslados de ") + yo.Ambito
            };
            return Json(new { ambito = texto, lista });
        }

        public sealed class Puesto
        {
            public short Peso { get; set; }
            public string Unidad { get; set; } = "";
            public string? Nombre { get; set; }
            public bool Yo { get; set; }
        }
    }
}
