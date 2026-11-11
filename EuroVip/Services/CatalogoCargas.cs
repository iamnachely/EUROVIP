using System.Data;
using Dapper;

namespace EuroVip.Services
{
    /// <summary>
    /// Moneda y proveedor de las cargas (FASE 38), iguales en formularios y Excel:
    ///   • Moneda: tiene que existir en Eurovip.moneda (DOP, USD, EUR…). Vacía = DOP.
    ///   • Proveedor: opcional; en el Excel se puede escribir su código o su nombre.
    /// También arma la hoja «Proveedores» de las plantillas de Excel.
    /// </summary>
    public static class CatalogoCargas
    {
        public sealed record Proveedor(string Id, string Nombre, string? Tipo);

        public static async Task<HashSet<string>> MonedasAsync(IDbConnection cn, IDbTransaction? tx = null) =>
            (await cn.QueryAsync<string>("SELECT LTRIM(RTRIM(id_moneda)) FROM Eurovip.moneda", transaction: tx)).Select(m => m.ToUpperInvariant()).ToHashSet();

        public static async Task<List<Proveedor>> ProveedoresAsync(IDbConnection cn) =>
            (await cn.QueryAsync<Proveedor>(@"SELECT id_proveedor AS Id, LTRIM(RTRIM(nombre)) AS Nombre, id_tipo_proveedor AS Tipo
                FROM Eurovip.proveedor WHERE id_proveedor <> '0' AND LTRIM(nombre) NOT LIKE 'Elegir%'
                ORDER BY CASE WHEN id_tipo_proveedor = 'Tour Operador' THEN 0 ELSE 1 END, nombre")).ToList();

        /// <summary>Destinos en español (Valor = id_destino tal cual, Extra = país) para las listas y coincidencias del Excel.</summary>
        public static async Task<List<CargaExcelOpcion>> DestinosAsync(IDbConnection cn) =>
            (await cn.QueryAsync<(string Destino, string? Pais)>(@"SELECT LTRIM(RTRIM(d.id_destino)), ISNULL(p.nombre, d.id_pais) FROM Eurovip.destino d
                LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES' WHERE d.id_idioma = 'ES' ORDER BY d.id_destino"))
            .Where(d => !string.IsNullOrWhiteSpace(d.Destino)).GroupBy(d => d.Destino, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CargaExcelOpcion(g.Key, g.Key, g.First().Pais)).ToList();

        /// <summary>Moneda en mayúsculas (vacía = DOP) y el error si no existe.</summary>
        public static (string Moneda, string? Error) Moneda(string? texto, ISet<string> monedas)
        {
            var m = string.IsNullOrWhiteSpace(texto) ? "DOP" : texto.Trim().ToUpperInvariant();
            if (m is "RD$" or "RD" or "PESOS" or "PESO") m = "DOP";
            else if (m is "US$" or "$" or "DOLAR" or "DOLARES" or "DÓLAR" or "DÓLARES") m = "USD";
            else if (m is "€" or "EURO" or "EUROS") m = "EUR";
            return (m, monedas.Contains(m) ? null : $"La moneda «{texto}» no existe. Usa {string.Join(", ", monedas.OrderBy(x => x))}.");
        }

        /// <summary>Proveedor por código o nombre (sin tildes ni mayúsculas). Vacío = (null, null).</summary>
        public static (string? Id, string? Error) BuscarProveedor(string? texto, IReadOnlyList<Proveedor> lista)
        {
            if (string.IsNullOrWhiteSpace(texto)) return (null, null);
            var t = texto.Trim();
            var p = lista.FirstOrDefault(x => string.Equals(x.Id, t, StringComparison.OrdinalIgnoreCase))
                 ?? lista.FirstOrDefault(x => ExcelTabla.Clave(x.Nombre) == ExcelTabla.Clave(t));
            return p is null ? (null, $"El proveedor «{t}» no existe. Escribe su nombre o código como en la hoja «Proveedores».") : (p.Id, null);
        }

        /// <summary>Error si el proveedor elegido en el formulario no existe (null si está vacío o existe).</summary>
        public static async Task<string?> ErrorProveedorAsync(IDbConnection cn, string? id) =>
            string.IsNullOrWhiteSpace(id) || await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.proveedor WHERE id_proveedor = @id", new { id = id.Trim() }) > 0
                ? null : $"El proveedor «{id}» no existe.";

        /// <summary>Hoja «Proveedores» de las plantillas (código, nombre, tipo).</summary>
        public static ExcelTabla.Hoja HojaProveedores(IEnumerable<Proveedor> lista) =>
            new("Proveedores", new[] { "Código", "Proveedor", "Tipo" }, lista.Select(p => new object?[] { p.Id, p.Nombre, p.Tipo }).ToList(), new double[] { 12, 50, 18 });
    }
}
