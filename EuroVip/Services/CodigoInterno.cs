using System.Data;
using System.Text.RegularExpressions;
using Dapper;

namespace EuroVip.Services
{
    /// <summary>
    /// Código interno de cada producto (FASE 38): solo letras y números (sin espacios, tildes ni símbolos), hasta 30,
    /// y único entre TODOS los productos (vista Eurovip.vCodigoInterno). Lo usan los formularios, el Excel y
    /// /Codigos/Disponible (la revisión mientras se escribe).
    /// Los códigos que ya existían antes de la regla no se revisan si no se cambian (hay circuitos con espacios).
    /// </summary>
    public static class CodigoInterno
    {
        public const int Largo = 30;
        private static readonly Regex Valido = new(@"^[A-Za-z0-9]{1,30}$", RegexOptions.Compiled);

        /// <summary>Productos de la vista (columna producto) y cómo se llaman en pantalla.</summary>
        public static readonly IReadOnlyDictionary<string, string> Productos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HOTEL"] = "Hoteles", ["INICIO"] = "Home (tarjeta)", ["OFERTA"] = "Solo Hotel", ["PAQUETES"] = "Paquetes",
            ["CIRCUITO"] = "Circuitos (salidas)", ["CIRCUITO_CAT"] = "Circuitos", ["EXCURSION"] = "Excursiones",
            ["TRASLADO"] = "Traslados", ["CRUCERO"] = "Cruceros",
        };

        /// <summary>Vacío → null; si no, sin espacios a los lados.</summary>
        public static string? Limpiar(string? codigo) => string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();

        /// <summary>Error de forma (letras y números, máximo 30) o null.</summary>
        public static string? ErrorFormato(string? codigo)
        {
            var c = Limpiar(codigo);
            if (c is null) return null;
            if (c.Length > Largo) return $"El código puede tener como máximo {Largo} caracteres.";
            if (!Valido.IsMatch(c)) return "El código solo puede tener letras y números: sin espacios, tildes (á, ñ…) ni símbolos.";
            return null;
        }

        /// <summary>Quién usa ya el código (otro producto u otra fila del mismo), o null si está libre.
        /// producto + clave = la fila que se está editando (para no chocar consigo misma).</summary>
        public static async Task<(string Producto, string Nombre)?> UsadoPorAsync(IDbConnection cn, string codigo, string producto, string? clave, IDbTransaction? tx = null)
        {
            var r = await cn.QueryFirstOrDefaultAsync<Uso>(@"
                SELECT TOP 1 producto AS Producto, ISNULL(nombre, '') AS Nombre FROM Eurovip.vCodigoInterno
                WHERE codigo = @codigo AND NOT (producto = @producto AND clave = ISNULL(@clave, '#'))",
                new { codigo, producto, clave }, tx);
            return r is null ? null : (r.Producto, r.Nombre);
        }
        private sealed class Uso { public string Producto { get; set; } = ""; public string Nombre { get; set; } = ""; }

        /// <summary>Forma + único. Si el código no cambió respecto al anterior (códigos viejos), no se revisa.</summary>
        public static async Task<string?> ErrorAsync(IDbConnection cn, string? codigo, string producto, string? clave, string? anterior = null)
        {
            var c = Limpiar(codigo);
            if (c is null) return null;
            if (anterior is not null && string.Equals(c, anterior.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
            if (ErrorFormato(c) is string f) return f;
            if (await UsadoPorAsync(cn, c, producto, clave) is var (p, n))
                return $"El código «{c}» ya lo usa {(string.IsNullOrWhiteSpace(n) ? "otro producto" : $"«{n}»")} en {(Productos.TryGetValue(p, out var t) ? t : p)}.";
            return null;
        }
    }
}
