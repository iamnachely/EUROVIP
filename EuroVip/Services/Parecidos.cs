using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EuroVip.Services
{
    /// <summary>
    /// ¿Ya existe o se parece? (pedido de la usuaria: «que vaya revisando en tiempo real si existe: en rojo si existe, en
    /// amarillo si se va pareciendo a algo que existe, para que no se creen cosas repetidas»).
    ///   • «existe»: igual sin contar mayúsculas, acentos, espacios ni símbolos («Punta  Cana» = «punta-cana»).
    ///   • «parecido»: igual quitando palabras de relleno («Hotel Riu Palace» ≈ «Riu Palace»), uno contiene al otro,
    ///     o casi igual (bigramas de Dice ≥ 0,7 o 1-2 letras distintas: «Bavaro» ≈ «Bávaro Beach»).
    /// Los códigos cortos (DO, EUR, TI…) solo se comparan exactos.
    /// </summary>
    public static partial class Parecidos
    {
        private static readonly HashSet<string> Relleno = new(StringComparer.Ordinal)
        {
            "hotel", "hoteles", "resort", "resorts", "spa", "the", "by", "and", "y", "e", "el", "la", "los", "las", "de", "del", "da", "do", "di",
            "suites", "suite", "all", "inclusive", "todo", "incluido"
        };

        /// <summary>minúsculas, sin acentos y solo letras/números separados por un espacio.</summary>
        public static string Normal(string? s)
        {
            var sb = new StringBuilder();
            foreach (var ch in (s ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant())
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }
            return Espacios().Replace(sb.ToString(), " ").Trim();
        }

        public static string Compacto(string? s) => Normal(s).Replace(" ", "");

        private static string SinRelleno(string? s) => string.Concat(Normal(s).Split(' ').Where(p => !Relleno.Contains(p)));

        /// <summary>null si no se parece; si no, ("existe" | "parecido", puntos 0..1).</summary>
        public static (string Nivel, double Puntos)? Comparar(string? escrito, string? existente, bool soloExacto)
        {
            var a = Compacto(escrito);
            var b = Compacto(existente);
            if (a.Length == 0 || b.Length == 0) return null;
            if (a == b) return ("existe", 1);
            if (soloExacto || a.Length < 3) return null;
            var (ra, rb) = (SinRelleno(escrito), SinRelleno(existente));
            if (ra.Length >= 3 && ra == rb) return ("parecido", .97);
            var (corto, largo) = a.Length <= b.Length ? (a, b) : (b, a);
            if (corto.Length >= 4 && largo.Contains(corto, StringComparison.Ordinal)) return ("parecido", .75 + .2 * corto.Length / largo.Length);
            var dice = Dice(a, b);
            if (dice >= .7) return ("parecido", dice);
            if (a.Length >= 5 && Math.Abs(a.Length - b.Length) <= 2 && Distancia(a, b) <= (a.Length >= 10 ? 2 : 1)) return ("parecido", .8);
            return null;
        }

        private static double Dice(string a, string b)
        {
            if (a.Length < 2 || b.Length < 2) return 0;
            var ba = new Dictionary<string, int>();
            for (var i = 0; i < a.Length - 1; i++) { var k = a.Substring(i, 2); ba[k] = ba.GetValueOrDefault(k) + 1; }
            var comunes = 0;
            for (var i = 0; i < b.Length - 1; i++)
            {
                var k = b.Substring(i, 2);
                if (ba.TryGetValue(k, out var n) && n > 0) { comunes++; ba[k] = n - 1; }
            }
            return 2.0 * comunes / (a.Length - 1 + b.Length - 1);
        }

        private static int Distancia(string a, string b)
        {
            var d = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) d[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                var previo = d[0]; d[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var t = d[j];
                    d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1), previo + (a[i - 1] == b[j - 1] ? 0 : 1));
                    previo = t;
                }
            }
            return d[b.Length];
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex Espacios();
    }
}
