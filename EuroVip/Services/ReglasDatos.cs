using Dapper;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace EuroVip.Services
{
    /// <summary>
    /// Reglas comunes para que no se carguen datos mal creados en ningún mantenimiento
    /// (destinos como "-pego" o "65401", hoteles llamados "222", espacios de más…).
    /// Las usan Herramientas, Circuitos, Flyers y la vitrina de Hoteles. Devuelven el mensaje de error o null.
    /// </summary>
    public static class ReglasDatos
    {
        private static readonly Regex VariosEspacios = new(@"\s+", RegexOptions.Compiled);

        /// <summary>Palabras que se cargaron como "destino" y no lo son.</summary>
        private static readonly HashSet<string> NoSonLugares = new(StringComparer.OrdinalIgnoreCase)
        {
            "falta", "por cuenta del cliente", "sin destino", "ninguno", "ninguna", "n/a", "na", "nd", "prueba", "test", "xxx", "otro", "otros", "varios"
        };

        /// <summary>Quita espacios al principio y al final y deja uno solo entre palabras.</summary>
        public static string Limpiar(string? s) => VariosEspacios.Replace(s ?? "", " ").Trim();

        private static int Letras(string s) => s.Count(char.IsLetter);

        /// <summary>Nombre de un lugar (país o destino): al menos 3 letras, empieza por letra, sin números ni símbolos raros.</summary>
        public static string? Lugar(string que, string valor)
        {
            valor = Limpiar(valor);
            if (NoSonLugares.Contains(valor)) return $"«{valor}» no es un {que}: escribe el nombre real.";
            if (Letras(valor) < 3) return $"«{valor}» no sirve como {que}: escribe el nombre completo (al menos 3 letras).";
            if (!char.IsLetter(valor[0])) return $"«{valor}» no sirve como {que}: tiene que empezar por una letra.";
            if (valor.Any(char.IsDigit)) return $"«{valor}» no sirve como {que}: el nombre de un {que} no lleva números.";
            var raro = valor.FirstOrDefault(ch => !(char.IsLetter(ch) || ch is ' ' or '-' or '\'' or '’' or '.' or ',' or '(' or ')'));
            if (raro != default) return $"«{valor}» no sirve como {que}: tiene el símbolo «{raro}».";
            return null;
        }

        /// <summary>Nombres en general (hotel, habitación, campaña…): tiene que llevar letras y empezar por letra o número.</summary>
        public static string? Nombre(string que, string valor, int minLetras = 2)
        {
            valor = Limpiar(valor);
            if (Letras(valor) < minLetras) return $"«{valor}» no sirve como {que}: tiene que llevar letras, no solo números o símbolos.";
            if (!char.IsLetterOrDigit(valor[0]) && valor[0] is not '(' and not '"' and not '¡' and not '¿')
                return $"«{valor}» no sirve como {que}: tiene que empezar por una letra o un número.";
            return null;
        }

        /// <summary>Códigos (país, moneda, IATA…): solo letras (o letras y números) en mayúscula, con el largo indicado.</summary>
        public static string? Codigo(string que, string valor, int min, int max, bool conNumeros = false)
        {
            var ok = valor.Length >= min && valor.Length <= max && valor.All(ch => ch is >= 'A' and <= 'Z' || (conNumeros && ch is >= '0' and <= '9'));
            if (ok) return null;
            var largo = min == max ? $"{min}" : $"de {min} a {max}";
            return $"«{valor}» no sirve como {que}: tienen que ser {largo} letras{(conNumeros ? " o números" : "")}, sin espacios ni símbolos.";
        }

        /// <summary>
        /// Para pantallas donde se escribe un destino (circuitos, flyers, vitrina de hoteles): tiene que ser un destino real
        /// que exista en Herramientas › Destinos y, si se indica el país, de ese país. Devuelve el error o null.
        /// </summary>
        public static async Task<string?> ErrorDestinoAsync(SqlConnection cn, string? destino, string? pais = null)
        {
            destino = Limpiar(destino);
            if (destino == "") return "Falta el destino.";
            var e = Lugar("destino", destino);
            if (e is not null) return e;
            var paises = (await cn.QueryAsync<(string Id, string Nombre)>(@"SELECT d.id_pais, ISNULL(p.nombre, d.id_pais) FROM Eurovip.destino d
                LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES' WHERE d.id_destino = @destino AND d.id_idioma = 'ES'", new { destino })).ToList();
            if (paises.Count == 0) return $"El destino «{destino}» no existe. Créalo primero en Herramientas › Destinos (o elige uno de la lista).";
            if (!string.IsNullOrWhiteSpace(pais) && !paises.Any(p => p.Id.Equals(pais.Trim(), StringComparison.OrdinalIgnoreCase)))
                return $"El destino «{destino}» es de {string.Join(" / ", paises.Select(p => p.Nombre))}, no del país elegido. El país y el destino tienen que concordar.";
            return null;
        }
    }
}
