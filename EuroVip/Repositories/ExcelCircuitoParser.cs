using Dapper;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using EuroVip.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EuroVip.Repositories
{
    public interface IExcelCircuitoParser
    {
        Task<ExcelCircuito> LeerAsync(Stream archivo);
    }

    /// <summary>
    /// Lee el Excel de circuito igual que BackTiTan (CargarCircuito/LeerExcelCircuito):
    ///   • etiquetas "Aerolínea", "Tasa", "Segmento 1", "Segmento 2" en cualquier lugar (valor hasta 5 celdas
    ///     a la derecha o 3 hacia abajo);
    ///   • hoteles = textos de la columna A que no son etiquetas ni números;
    ///   • fechas = celdas de la columna B en adelante con formato fecha (o número de serie 30000–80000);
    ///     el precio es el número de la fila de abajo, misma columna (solo > 0, sin repetir).
    /// Los hoteles se buscan en Eurovip.alojamiento (ES, activos) y la aerolínea en proveedor.
    /// </summary>
    public class ExcelCircuitoParser : IExcelCircuitoParser
    {
        private static readonly string[] Reservadas =
        {
            "aerolinea", "aerolineas", "tasa", "tasas", "segmento 1", "segmento 2", "segmento", "hotel", "hoteles", "fecha", "fechas",
            "precio", "precios", "noches", "salidas", "salida", "categoria", "circuito", "total", "importe", "precio total", "desde", "hasta"
        };

        private readonly ICircuitoRepository _repo;
        public ExcelCircuitoParser(ICircuitoRepository repo) => _repo = repo;

        internal record Celda(int Fila, int Col, string? Texto, double? Numero, bool EsFecha);

        public async Task<ExcelCircuito> LeerAsync(Stream archivo)
        {
            var r = new ExcelCircuito();
            var celdas = LeerCeldas(archivo);
            var porPos = celdas.ToDictionary(c => (c.Fila, c.Col));

            // ---- etiquetas ----
            string? ValorDe(params string[] etiquetas)
            {
                foreach (var c in celdas.Where(c => c.Texto is not null && etiquetas.Contains(Normalizar(c.Texto).TrimEnd(':'))))
                {
                    for (var d = 1; d <= 5; d++)
                        if (porPos.TryGetValue((c.Fila, c.Col + d), out var v) && Valor(v) is { } s) return s;
                    for (var d = 1; d <= 3; d++)
                        if (porPos.TryGetValue((c.Fila + d, c.Col), out var v) && Valor(v) is { } s) return s;
                }
                return null;
            }

            r.Aerolinea = ValorDe("aerolinea", "aerolineas", "compania aerea");
            var tasa = ValorDe("tasa", "tasas");
            if (tasa is not null && decimal.TryParse(tasa.Replace("€", "").Replace("RD$", "").Replace(",", ".").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var t)) r.Tasa = t;
            r.Segmento1 = ValorDe("segmento 1", "ida");
            r.Segmento2 = ValorDe("segmento 2", "vuelta");

            // ---- fechas + precio de la fila de abajo ----
            var vistas = new HashSet<DateTime>();
            foreach (var c in celdas.Where(c => c.Col >= 2 && EsFecha(c)).OrderBy(c => c.Col).ThenBy(c => c.Fila))
            {
                var fecha = DateTime.FromOADate(c.Numero!.Value).Date;
                if (!porPos.TryGetValue((c.Fila + 1, c.Col), out var pc) || pc.Numero is not > 0) continue;
                // Si la "fecha" es solo un número de serie, el de abajo debe parecer un precio y no otra fecha.
                if (!c.EsFecha && (pc.EsFecha || pc.Numero >= 30000)) continue;
                if (!vistas.Add(fecha)) continue;
                r.Fechas.Add(new ExcelFecha { Fecha = fecha, Precio = Math.Round((decimal)pc.Numero.Value, 2) });
            }
            r.Fechas = r.Fechas.OrderBy(f => f.Fecha).ToList();

            // ---- hoteles (columna A) ----
            var textosHotel = celdas.Where(c => c.Col == 1 && c.Texto is { Length: > 2 } && c.Numero is null)
                                    .Select(c => c.Texto!.Trim())
                                    .Where(s => !Reservadas.Contains(Normalizar(s).TrimEnd(':')) && !Regex.IsMatch(s, @"^\d+([.,]\d+)?$"))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList();

            if (textosHotel.Count > 0)
            {
                using var cn = _repo.Conexion();
                var hoteles = (await cn.QueryAsync<(int Id, string Nombre, string Destino)>(
                    "SELECT id_alojamiento, nombre, id_destino FROM Eurovip.alojamiento WHERE id_idioma = 'ES' AND activo = 1")).ToList();
                var indice = hoteles.Select(h => (h.Id, h.Nombre, h.Destino, N: Normalizar(h.Nombre))).ToList();

                foreach (var texto in textosHotel)
                {
                    var n = Normalizar(texto);
                    var exactos = indice.Where(h => h.N == n).ToList();
                    var candidatos = exactos.Count > 0 ? exactos
                        : indice.Where(h => h.N.Contains(n) || (n.Length >= 6 && n.Contains(h.N) && h.N.Length >= 6)).Take(15).ToList();

                    var item = new ExcelHotel { Texto = texto };
                    if (candidatos.Count == 1)
                    {
                        item.Estado = "found";
                        item.Id_Alojamiento = candidatos[0].Id;
                        item.Nombre = candidatos[0].Nombre;
                    }
                    else if (candidatos.Count > 1)
                    {
                        item.Estado = "multiple";
                        item.Candidatos = candidatos.Select(h => new Opcion { Valor = h.Id.ToString(), Texto = h.Nombre, Extra = h.Destino }).ToList();
                    }
                    r.Hoteles.Add(item);
                }
            }

            // ---- aerolínea ----
            if (!string.IsNullOrWhiteSpace(r.Aerolinea))
            {
                using var cn = _repo.Conexion();
                var aerolineas = (await cn.QueryAsync<(string Id, string Nombre)>(
                    "SELECT id_proveedor, nombre FROM Eurovip.proveedor WHERE id_tipo_proveedor = 'Aerolinea'")).ToList();
                var a = Normalizar(r.Aerolinea);
                var match = aerolineas.FirstOrDefault(x => Normalizar(x.Nombre) == a || x.Id.Equals(r.Aerolinea.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match.Id is null) match = aerolineas.FirstOrDefault(x => Normalizar(x.Nombre).Contains(a) || a.Contains(Normalizar(x.Nombre)));
                if (match.Id is not null) r.AerolineaId = match.Id;
                else r.Avisos.Add($"No encontré la aerolínea \"{r.Aerolinea}\"; elígela a mano.");
            }

            if (r.Fechas.Count == 0) r.Avisos.Add("No encontré fechas con precio. Revisa que las fechas tengan formato fecha y el precio esté justo debajo.");
            if (r.Hoteles.Count == 0) r.Avisos.Add("No encontré hoteles en la columna A.");
            return r;
        }

        internal static List<Celda> LeerCeldas(Stream archivo)
        {
            using var doc = SpreadsheetDocument.Open(archivo, false);
            var wb = doc.WorkbookPart ?? throw new InvalidOperationException("El archivo no es un Excel válido.");
            var hoja = wb.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault() ?? throw new InvalidOperationException("El Excel no tiene hojas.");
            var ws = (WorksheetPart)wb.GetPartById(hoja.Id!);
            var compartidos = wb.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().Select(i => i.InnerText).ToList() ?? new();
            var formatos = wb.WorkbookStylesPart?.Stylesheet;
            var formatosFecha = FormatosFecha(formatos);

            var lista = new List<Celda>();
            foreach (var row in ws.Worksheet.Descendants<Row>())
            {
                foreach (var cell in row.Elements<Cell>())
                {
                    if (cell.CellReference?.Value is not { } refe) continue;
                    var (fila, col) = Posicion(refe);
                    string? texto = null;
                    double? numero = null;
                    var raw = cell.CellValue?.Text;

                    if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(raw, out var idx) && idx < compartidos.Count)
                        texto = compartidos[idx];
                    else if (cell.DataType?.Value == CellValues.InlineString)
                        texto = cell.InlineString?.InnerText;
                    else if (cell.DataType?.Value == CellValues.String)
                        texto = raw;
                    else if (raw is not null && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        numero = d;

                    if (texto is not null && string.IsNullOrWhiteSpace(texto)) texto = null;
                    if (texto is null && numero is null) continue;

                    var esFecha = numero is not null && cell.StyleIndex?.Value is { } si && formatosFecha.Contains((int)si);
                    lista.Add(new Celda(fila, col, texto?.Trim(), numero, esFecha));
                }
            }
            return lista;
        }

        /// <summary>Índices de estilo (cellXfs) cuyo formato numérico es de fecha.</summary>
        private static HashSet<int> FormatosFecha(Stylesheet? ss)
        {
            var res = new HashSet<int>();
            if (ss?.CellFormats is null) return res;
            var personalizados = ss.NumberingFormats?.Elements<NumberingFormat>()
                .Where(n => n.NumberFormatId is not null)
                .ToDictionary(n => (int)n.NumberFormatId!.Value, n => n.FormatCode?.Value ?? "") ?? new();
            var i = 0;
            foreach (var xf in ss.CellFormats.Elements<CellFormat>())
            {
                var id = (int)(xf.NumberFormatId?.Value ?? 0);
                var esFecha = id is >= 14 and <= 22 or >= 45 and <= 47 ||
                              (personalizados.TryGetValue(id, out var code) && Regex.IsMatch(Regex.Replace(code, "\"[^\"]*\"|\\[[^\\]]*\\]", ""), "[dDyY]|m{3,}"));
                if (esFecha) res.Add(i);
                i++;
            }
            return res;
        }

        // Fecha con formato de fecha, o número de serie de Excel entre 30000 (1982) y 80000 (2119) sin decimales.
        internal static bool EsFecha(Celda c) => c.Numero is not null && (c.EsFecha || (c.Numero >= 30000 && c.Numero <= 80000 && c.Numero % 1 == 0));

        private static string? Valor(Celda c)
        {
            if (c.Texto is not null) return c.Texto;
            if (c.Numero is null) return null;
            return c.EsFecha ? DateTime.FromOADate(c.Numero.Value).ToString("dd/MM/yyyy") : c.Numero.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static (int Fila, int Col) Posicion(string referencia)
        {
            var letras = new string(referencia.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
            var col = letras.Aggregate(0, (a, ch) => a * 26 + (ch - 'A' + 1));
            var fila = int.Parse(referencia[letras.Length..], CultureInfo.InvariantCulture);
            return (fila, col);
        }

        /// <summary>Minúsculas, sin acentos y con espacios simples (para comparar nombres).</summary>
        public static string Normalizar(string s)
        {
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var ch in d)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant(), @"\s+", " ").Trim();
        }
    }
}

