using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace EuroVip.Services
{
    /// <summary>
    /// Plantillas y cargas masivas con Excel (Hoteles, Excursiones, Traslados…):
    ///   • Crear: arma un .xlsx con una o varias hojas (fila 1 = encabezados en negrita, luego las filas).
    ///   • Leer: lee la PRIMERA hoja como tabla: la primera fila con texto son los encabezados y cada fila de abajo es un registro,
    ///     con sus valores por nombre de columna (sin mayúsculas, tildes ni espacios: «Precio ida y vuelta» → "precioidayvuelta").
    /// </summary>
    public static class ExcelTabla
    {
        /// <summary>Una hoja: nombre, encabezados, filas (string, número, bool o DateTime) y ancho de cada columna (opcional).</summary>
        public record Hoja(string Nombre, IReadOnlyList<string> Encabezados, IReadOnlyList<object?[]> Filas, IReadOnlyList<double>? Anchos = null);

        /// <summary>Una fila leída: número de fila en Excel y sus valores por columna (clave normalizada).</summary>
        public sealed class Fila
        {
            public int Numero { get; init; }
            public Dictionary<string, string> Valores { get; } = new();
            /// <summary>Valor de la primera columna que exista de las dadas (por si escriben «Aeropuerto» o «Aeropuerto de salida»); null si está vacío.</summary>
            public string? Texto(params string[] columnas)
            {
                foreach (var c in columnas)
                    if (Valores.TryGetValue(Clave(c), out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
                return null;
            }
            /// <summary>Número de la primera columna que exista (precio, noches…); null si está vacía o no es un número.</summary>
            public decimal? Cifra(params string[] columnas) => ExcelTabla.Numero(Texto(columnas));
            /// <summary>Sí / No (también si, x, 1, true, verdadero). Vacío → el valor por defecto.</summary>
            public bool SiNo(bool porDefecto, params string[] columnas)
            {
                var t = Texto(columnas);
                if (t is null) return porDefecto;
                var c = Clave(t);
                return c is "si" or "s" or "x" or "1" or "true" or "verdadero" or "yes";
            }
        }

        public static string Clave(string? texto)
        {
            var sb = new StringBuilder();
            foreach (var ch in (texto ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant())
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        /// <summary>«1.250,50», «1,250.50», «RD$ 1250», «1250» → 1250.50 / 1250. Null si no es un número.</summary>
        public static decimal? Numero(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var t = new string(texto.Where(c => char.IsDigit(c) || c is ',' or '.' or '-').ToArray());
            if (t == "" || t == "-") return null;
            var coma = t.LastIndexOf(','); var punto = t.LastIndexOf('.');
            if (coma >= 0 && punto >= 0) t = coma > punto ? t.Replace(".", "").Replace(',', '.') : t.Replace(",", "");
            else if (coma >= 0) t = t.Length - coma - 1 is 1 or 2 ? t.Replace(',', '.') : t.Replace(",", "");
            return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        // ======================================================================== escribir
        public static byte[] Crear(params Hoja[] hojas)
        {
            using var ms = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
            {
                var wb = doc.AddWorkbookPart();
                wb.Workbook = new Workbook();
                var st = wb.AddNewPart<WorkbookStylesPart>();
                st.Stylesheet = new Stylesheet(
                    new Fonts(new Font(), new Font(new Bold(), new Color { Rgb = "FFFFFFFF" }), new Font(new Bold())),
                    new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                              new Fill(new PatternFill(new ForegroundColor { Rgb = "FF0B3B4F" }) { PatternType = PatternValues.Solid })),
                    new Borders(new Border()),
                    new CellFormats(
                        new CellFormat(),                                                                                    // 0 normal
                        new CellFormat { FontId = 1, FillId = 2, ApplyFont = true, ApplyFill = true },                       // 1 encabezado
                        new CellFormat { NumberFormatId = 14, ApplyNumberFormat = true },                                    // 2 fecha
                        new CellFormat { FontId = 2, ApplyFont = true },                                                     // 3 negrita
                        new CellFormat(new Alignment { WrapText = true, Vertical = VerticalAlignmentValues.Top }) { ApplyAlignment = true })); // 4 texto largo
                var sheets = wb.Workbook.AppendChild(new Sheets());
                uint n = 1;
                foreach (var h in hojas)
                {
                    var ws = wb.AddNewPart<WorksheetPart>();
                    var data = new SheetData();
                    var cols = new Columns();
                    var columnas = Math.Max(h.Encabezados.Count, h.Filas.Count == 0 ? 0 : h.Filas.Max(x => x.Length));
                    for (var i = 0; i < columnas; i++)
                    {
                        var ancho = h.Anchos is not null && i < h.Anchos.Count ? h.Anchos[i] : i < h.Encabezados.Count ? Math.Clamp(h.Encabezados[i].Length + 4, 12, 40) : 13;
                        cols.Append(new Column { Min = (uint)(i + 1), Max = (uint)(i + 1), Width = ancho, CustomWidth = true });
                    }
                    // Sin encabezados (formato libre, p. ej. el Excel de circuitos): las filas empiezan en la 1
                    var primera = h.Encabezados.Count > 0 ? 2 : 1;
                    if (primera == 2)
                    {
                        var enc = new Row { RowIndex = 1 };
                        for (var i = 0; i < h.Encabezados.Count; i++) enc.Append(Texto(Ref(i, 1), h.Encabezados[i], 1));
                        data.Append(enc);
                    }
                    for (var f = 0; f < h.Filas.Count; f++)
                    {
                        var row = new Row { RowIndex = (uint)(f + primera) };
                        for (var i = 0; i < h.Filas[f].Length; i++)
                        {
                            var r = Ref(i, f + primera);
                            switch (h.Filas[f][i])
                            {
                                case null: break;
                                case DateTime d: row.Append(new Cell { CellReference = r, StyleIndex = 2, CellValue = new CellValue(d.ToOADate().ToString(CultureInfo.InvariantCulture)) }); break;
                                case decimal or int or short or long or double:
                                    row.Append(new Cell { CellReference = r, CellValue = new CellValue(Convert.ToString(h.Filas[f][i], CultureInfo.InvariantCulture)!) }); break;
                                case bool b: row.Append(Texto(r, b ? "Sí" : "No", 0)); break;
                                default:
                                    var s = h.Filas[f][i]!.ToString()!;
                                    row.Append(Texto(r, s, s.Length > 60 ? (uint)4 : 0)); break;
                            }
                        }
                        data.Append(row);
                    }
                    ws.Worksheet = primera == 2
                        ? new Worksheet(new SheetViews(new SheetView(new Pane { VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen }) { WorkbookViewId = 0 }), cols, data)
                        : new Worksheet(cols, data);
                    sheets.Append(new Sheet { Id = wb.GetIdOfPart(ws), SheetId = n++, Name = h.Nombre.Length > 31 ? h.Nombre[..31] : h.Nombre });
                }
                wb.Workbook.Save();
            }
            return ms.ToArray();
        }

        private static Cell Texto(string r, string s, uint estilo) =>
            new() { CellReference = r, DataType = CellValues.InlineString, StyleIndex = estilo, InlineString = new InlineString(new Text(s) { Space = SpaceProcessingModeValues.Preserve }) };

        private static string Ref(int col, int fila)
        {
            var s = ""; col++;
            while (col > 0) { var m = (col - 1) % 26; s = (char)('A' + m) + s; col = (col - 1) / 26; }
            return s + fila;
        }

        // ======================================================================== leer
        public static List<Fila> Leer(Stream archivo)
        {
            using var doc = SpreadsheetDocument.Open(archivo, false);
            var wb = doc.WorkbookPart ?? throw new InvalidOperationException("El archivo no es un Excel válido.");
            var hoja = wb.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault() ?? throw new InvalidOperationException("El Excel no tiene hojas.");
            var ws = (WorksheetPart)wb.GetPartById(hoja.Id!);
            var compartidos = wb.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().Select(i => i.InnerText).ToList() ?? new();
            var fechas = FormatosFecha(wb.WorkbookStylesPart?.Stylesheet);

            string? Valor(Cell c)
            {
                if (c.DataType?.Value == CellValues.SharedString && int.TryParse(c.CellValue?.Text, out var i)) return i < compartidos.Count ? compartidos[i] : null;
                if (c.DataType?.Value == CellValues.InlineString) return c.InlineString?.InnerText;
                if (c.DataType?.Value == CellValues.Boolean) return c.CellValue?.Text == "1" ? "Sí" : "No";
                var t = c.CellValue?.Text;
                if (t is null) return null;
                if (c.StyleIndex is { } si && fechas.Contains(si.Value) && double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa))
                    return DateTime.FromOADate(oa).ToString("yyyy-MM-dd");
                return t;
            }
            static int Columna(string? r)
            {
                var n = 0;
                foreach (var ch in r ?? "") { if (!char.IsLetter(ch)) break; n = n * 26 + (char.ToUpperInvariant(ch) - 'A' + 1); }
                return n;
            }

            var filas = new List<Fila>();
            Dictionary<int, string>? encabezados = null;
            foreach (var row in ws.Worksheet.Descendants<Row>())
            {
                var valores = row.Elements<Cell>().Select(c => (Col: Columna(c.CellReference), Val: Valor(c)?.Trim())).Where(x => !string.IsNullOrEmpty(x.Val)).ToList();
                if (valores.Count == 0) continue;
                if (encabezados is null) { encabezados = valores.ToDictionary(x => x.Col, x => Clave(x.Val)); continue; }
                var f = new Fila { Numero = (int)(row.RowIndex?.Value ?? 0) };
                foreach (var (col, val) in valores)
                    if (encabezados.TryGetValue(col, out var k) && k != "") f.Valores[k] = val!;
                if (f.Valores.Count > 0) filas.Add(f);
            }
            if (encabezados is null) throw new InvalidOperationException("El Excel está vacío.");
            return filas;
        }

        private static HashSet<uint> FormatosFecha(Stylesheet? s)
        {
            var r = new HashSet<uint>();
            if (s?.CellFormats is null) return r;
            var propios = s.NumberingFormats?.Elements<NumberingFormat>()
                .Where(n => n.FormatCode?.Value is string f && (f.Contains('d') || f.Contains('y')) && !f.Contains('#'))
                .Select(n => n.NumberFormatId!.Value).ToHashSet() ?? new HashSet<uint>();
            uint i = 0;
            foreach (var cf in s.CellFormats.Elements<CellFormat>())
            {
                var id = cf.NumberFormatId?.Value ?? 0;
                if (id is >= 14 and <= 22 || propios.Contains(id)) r.Add(i);
                i++;
            }
            return r;
        }
    }
}
