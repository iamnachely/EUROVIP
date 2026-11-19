using Dapper;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EuroVip.Services
{
    /// <summary>
    /// Hoteles para las cargas con Excel de Traslados (la del BackOffice y la del proveedor). 20/11/2026: el traslado va de un aeropuerto a un
    /// HOTEL; en el Excel se escribe el nombre del hotel (o «Nombre · Destino» si hay dos con el mismo nombre) y el destino sale del hotel.
    /// </summary>
    public static class HotelesExcel
    {
        public sealed record Hotel(int Id, string Nombre, string Destino, string Etiqueta);

        public static string Norm(string? s)
        {
            var d = (s ?? "").Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>Hoteles activos con la etiqueta que se escribe en el Excel: el nombre, o «Nombre · Destino» si hay dos con el mismo nombre.</summary>
        public static async Task<List<Hotel>> ListarAsync(IDbConnection cn)
        {
            var filas = (await cn.QueryAsync<(int Id, string? Nombre, string? Destino)>(@"SELECT a.id_alojamiento, LTRIM(RTRIM(a.nombre)), LTRIM(RTRIM(a.id_destino))
                FROM Eurovip.alojamiento a WHERE ISNULL(a.activo, 1) = 1 ORDER BY a.nombre")).Where(f => !string.IsNullOrWhiteSpace(f.Nombre)).ToList();
            var repetidos = filas.GroupBy(f => Norm(f.Nombre)).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            return filas.Select(f => new Hotel(f.Id, f.Nombre!, f.Destino ?? "", repetidos.Contains(Norm(f.Nombre)) ? $"{f.Nombre} · {f.Destino}" : f.Nombre!)).ToList();
        }

        public static (Hotel? Hotel, string? Error) Buscar(string? texto, List<Hotel> hoteles)
        {
            var t = (texto ?? "").Trim();
            if (t == "") return (null, "Escribe el hotel al que llega el traslado (columna Hotel).");
            if (int.TryParse(t, out var id) && hoteles.FirstOrDefault(h => h.Id == id) is { } porId) return (porId, null);
            var exacto = hoteles.Where(h => Norm(h.Etiqueta) == Norm(t)).ToList();
            if (exacto.Count == 1) return (exacto[0], null);
            var porNombre = hoteles.Where(h => Norm(h.Nombre) == Norm(t)).ToList();
            if (porNombre.Count == 1) return (porNombre[0], null);
            if (porNombre.Count > 1) return (null, $"Hay {porNombre.Count} hoteles llamados «{t}»: escribe «Nombre · Destino».");
            return (null, $"No encontramos el hotel «{t}». Elígelo de la lista.");
        }

        /// <summary>Un hotel de ejemplo para la plantilla: el n-ésimo del destino pedido o, si no hay, del catálogo.</summary>
        public static string Ejemplo(List<Hotel> hoteles, int n, string destino) =>
            (hoteles.Where(h => h.Destino.Equals(destino, StringComparison.OrdinalIgnoreCase)).Skip(n).FirstOrDefault() ?? hoteles.Skip(n).FirstOrDefault())?.Etiqueta ?? "";
    }
}
