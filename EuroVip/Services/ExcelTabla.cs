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
        public record Hoja(string Nombre, IReadOnlyList<string> Encabezados, IReadOnlyList<object?[]> Filas, IReadOnlyList<double>? Anchos = null)
        {
            /// <summary>Listas desplegables por columna (encabezado → valores): en Excel la celda muestra la flecha y no deja escribir otra cosa.</summary>
            public IReadOnlyDictionary<string, IReadOnlyList<string>>? Listas { get; init; }
        }

        /// <summary>Listas comunes de las plantillas.</summary>
        public static readonly IReadOnlyList<string> SiNoLista = new[] { "Sí", "No" };

        /// <summary>Una fila leída: número de fila en Excel y sus valores por columna (clave normalizada).</summary>
        public sealed class Fila
        {
            public int Numero { get; init; }
            public Dictionary<string, string> Valores { get; } = new();
            /// <summary>Títulos de las columnas tal como están en el Excel (clave normalizada → texto original). El mismo para todas las filas.</summary>
            public IReadOnlyDictionary<string, string> Encabezados { get; init; } = new Dictionary<string, string>();
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
        // 26/11/2026: diseño unificado de TODOS los Excel que baja el proveedor (y el equipo): colores de Eurovip, encabezado destacado,
        // filas alternadas con bordes suaves, columnas con su formato (fecha, precio), filtros, primera fila fija, hoja «Instrucciones» limpia
        // y listo para imprimir. Solo cambia el ASPECTO: las posiciones (encabezados en la fila 1, datos desde la 2) son las de siempre.

        /// <summary>Índices de estilo de <see cref="Estilos"/> (públicos para las plantillas que arman su propio libro).</summary>
        public static class E
        {
            public const uint Normal = 0, Encabezado = 1, Fecha = 2, Negrita = 3, TextoLargo = 4, Texto = 5, Precio = 6,
                              TextoAlt = 7, PrecioAlt = 8, FechaAlt = 9, TextoLargoAlt = 10, AyudaTitulo = 11, AyudaTexto = 12,
                              FechaEncabezado = 13, Nota = 14, EncabezadoIzq = 15, Titulo = 16;
        }

        const string Navy = "FF0B2E6B", Naranja = "FFF26B1D", Zebra = "FFF3F6FC", Borde = "FFD3DAE6", Suave = "FFE8EEF9", Tinta = "FF1F2937";

        /// <summary>Hoja de estilos común (los índices están en <see cref="E"/>).</summary>
        public static Stylesheet Estilos()
        {
            static Font F(string? color = null, bool negrita = false, bool cursiva = false, double tam = 11)
            {
                var f = new Font();
                if (negrita) f.Append(new Bold());
                if (cursiva) f.Append(new Italic());
                f.Append(new FontSize { Val = tam });
                f.Append(new Color { Rgb = color ?? Tinta });
                f.Append(new FontName { Val = "Calibri" });
                return f;
            }
            static Fill R(string rgb) => new(new PatternFill(new ForegroundColor { Rgb = rgb }) { PatternType = PatternValues.Solid });
            static Border B(bool abajoNaranja = false) =>
                new(new LeftBorder(new Color { Rgb = Borde }) { Style = BorderStyleValues.Thin },
                    new RightBorder(new Color { Rgb = Borde }) { Style = BorderStyleValues.Thin },
                    new TopBorder(new Color { Rgb = Borde }) { Style = BorderStyleValues.Thin },
                    abajoNaranja ? new BottomBorder(new Color { Rgb = Naranja }) { Style = BorderStyleValues.Medium }
                                 : new BottomBorder(new Color { Rgb = Borde }) { Style = BorderStyleValues.Thin },
                    new DiagonalBorder());
            static CellFormat C(uint fuente = 0, uint relleno = 0, uint borde = 0, uint fmt = 0, HorizontalAlignmentValues? h = null, VerticalAlignmentValues? v = null, bool ajustar = false)
            {
                var al = new Alignment { Vertical = v ?? VerticalAlignmentValues.Center, WrapText = ajustar };
                if (h is { } hh) al.Horizontal = hh;
                return new CellFormat(al)
                { FontId = fuente, FillId = relleno, BorderId = borde, NumberFormatId = fmt, ApplyFont = true, ApplyFill = relleno != 0, ApplyBorder = borde != 0, ApplyNumberFormat = fmt != 0, ApplyAlignment = true };
            }

            return new Stylesheet(
                new NumberingFormats(new NumberingFormat { NumberFormatId = 165, FormatCode = "ddd d mmm" }),
                new Fonts(F(), F("FFFFFFFF", true), F(null, true), F("FF6B7280", false, true, 10), F(Navy, true, false, 14)),
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
                          R(Navy), R(Zebra), R("FFFFF1E8"), R(Suave)),
                new Borders(new Border(), B(), B(abajoNaranja: true)),
                new CellFormats(
                    new CellFormat(),                                                                                              // 0 normal
                    C(1, 2, 2, 0, HorizontalAlignmentValues.Center, VerticalAlignmentValues.Center, true),                         // 1 encabezado
                    C(0, 0, 1, 14, HorizontalAlignmentValues.Center),                                                              // 2 fecha
                    C(2),                                                                                                          // 3 negrita
                    C(0, 0, 1, 0, null, VerticalAlignmentValues.Top, true),                                                        // 4 texto largo
                    C(0, 0, 1),                                                                                                    // 5 texto
                    C(0, 0, 1, 4, HorizontalAlignmentValues.Right),                                                                // 6 precio (#,##0.00)
                    C(0, 3, 1),                                                                                                    // 7 texto (fila alterna)
                    C(0, 3, 1, 4, HorizontalAlignmentValues.Right),                                                                // 8 precio alterna
                    C(0, 3, 1, 14, HorizontalAlignmentValues.Center),                                                              // 9 fecha alterna
                    C(0, 3, 1, 0, null, VerticalAlignmentValues.Top, true),                                                        // 10 texto largo alterna
                    C(2, 5, 1, 0, null, VerticalAlignmentValues.Center, true),                                                     // 11 ayuda: título
                    C(0, 0, 1, 0, null, VerticalAlignmentValues.Center, true),                                                     // 12 ayuda: texto
                    C(1, 2, 2, 165, HorizontalAlignmentValues.Center, VerticalAlignmentValues.Center, true),                       // 13 fecha en el encabezado
                    C(3, 0, 0, 0, null, VerticalAlignmentValues.Top, true),                                                        // 14 nota
                    C(1, 2, 2, 0, HorizontalAlignmentValues.Left, VerticalAlignmentValues.Center, true),                           // 15 encabezado a la izquierda
                    C(4, 0, 0, 0, null, VerticalAlignmentValues.Center)));                                                         // 16 título
        }

        /// <summary>Agrega al libro una hoja de ayuda («Instrucciones»): sin cuadrícula, columna de títulos y columna de texto.</summary>
        public static void AgregarAyuda(WorkbookPart wb, Sheets sheets, uint id, string nombre, IReadOnlyList<string> encabezados, IReadOnlyList<object?[]> filas, IReadOnlyList<double> anchos)
        {
            var ws = wb.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            var cols = new Columns();
            for (var i = 0; i < anchos.Count; i++) cols.Append(new Column { Min = (uint)(i + 1), Max = (uint)(i + 1), Width = anchos[i], CustomWidth = true });
            data.Append(FilaEncabezado(1, encabezados));
            for (var f = 0; f < filas.Count; f++)
            {
                var row = new Row { RowIndex = (uint)(f + 2) };
                var vacia = filas[f].All(x => string.IsNullOrWhiteSpace(x?.ToString()));
                for (var i = 0; i < Math.Max(anchos.Count, filas[f].Length); i++)
                {
                    var v = i < filas[f].Length ? filas[f][i]?.ToString() ?? "" : "";
                    if (vacia) { row.Append(new Cell { CellReference = Ref(i, f + 2) }); continue; }
                    row.Append(Texto(Ref(i, f + 2), v, i == 0 ? E.AyudaTitulo : E.AyudaTexto));
                }
                data.Append(row);
            }
            ws.Worksheet = new Worksheet(
                new SheetProperties(new PageSetupProperties { FitToPage = true }) { TabColor = new TabColor { Rgb = Naranja } },
                new SheetViews(new SheetView { ShowGridLines = false, WorkbookViewId = 0 }), cols, data,
                new PageMargins { Left = 0.5, Right = 0.5, Top = 0.6, Bottom = 0.6, Header = 0.3, Footer = 0.3 },
                new PageSetup { Orientation = OrientationValues.Landscape, FitToHeight = 0 });
            sheets.Append(new Sheet { Id = wb.GetIdOfPart(ws), SheetId = id, Name = nombre });
        }

        private static Row FilaEncabezado(uint fila, IReadOnlyList<string> enc)
        {
            var r = new Row { RowIndex = fila, Height = 32, CustomHeight = true };
            for (var i = 0; i < enc.Count; i++) r.Append(Texto(Ref(i, (int)fila), enc[i], i == 0 ? E.EncabezadoIzq : E.Encabezado));
            return r;
        }

        public static byte[] Crear(params Hoja[] hojas)
        {
            using var ms = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
            {
                var wb = doc.AddWorkbookPart();
                wb.Workbook = new Workbook(new BookViews(new WorkbookView { ActiveTab = 0 }));
                var st = wb.AddNewPart<WorkbookStylesPart>();
                st.Stylesheet = Estilos();
                var sheets = wb.Workbook.AppendChild(new Sheets());
                var listas = new List<List<string>>();
                uint n = 1;
                foreach (var h in hojas)
                {
                    // La hoja de ayuda lleva su propio diseño (sin cuadrícula, títulos a un lado)
                    if (h.Encabezados.Count > 0 && Clave(h.Nombre) is "instrucciones" or "ayuda")
                    {
                        AgregarAyuda(wb, sheets, n++, h.Nombre, h.Encabezados, h.Filas, h.Anchos ?? Enumerable.Repeat(40d, h.Encabezados.Count).ToList());
                        continue;
                    }
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
                    var conTabla = h.Encabezados.Count > 0;
                    var primera = conTabla ? 2 : 1;
                    if (conTabla) data.Append(FilaEncabezado(1, h.Encabezados));
                    // Zona de captura: los ejemplos y filas vacías con el mismo diseño hasta tener al menos 30 filas (para que se vea dónde escribir)
                    var totalFilas = conTabla ? Math.Max(h.Filas.Count, 30) : h.Filas.Count;
                    for (var f = 0; f < totalFilas; f++)
                    {
                        var alt = conTabla && f % 2 == 1;
                        var row = new Row { RowIndex = (uint)(f + primera) };
                        var vals = f < h.Filas.Count ? h.Filas[f] : Array.Empty<object?>();
                        for (var i = 0; i < Math.Max(vals.Length, conTabla ? columnas : 0); i++)
                        {
                            var r = Ref(i, f + primera);
                            var v = i < vals.Length ? vals[i] : null;
                            switch (v)
                            {
                                case null: if (conTabla) row.Append(new Cell { CellReference = r, StyleIndex = alt ? E.TextoAlt : E.Texto }); break;
                                case DateTime d: row.Append(new Cell { CellReference = r, StyleIndex = alt ? E.FechaAlt : E.Fecha, CellValue = new CellValue(d.ToOADate().ToString(CultureInfo.InvariantCulture)) }); break;
                                case decimal or double:
                                    row.Append(new Cell { CellReference = r, StyleIndex = conTabla ? (alt ? E.PrecioAlt : E.Precio) : E.Normal, CellValue = new CellValue(Convert.ToString(v, CultureInfo.InvariantCulture)!) }); break;
                                case int or short or long:
                                    row.Append(new Cell { CellReference = r, StyleIndex = conTabla ? (alt ? E.TextoAlt : E.Texto) : E.Normal, CellValue = new CellValue(Convert.ToString(v, CultureInfo.InvariantCulture)!) }); break;
                                case bool b: row.Append(Texto(r, b ? "Sí" : "No", conTabla ? (alt ? E.TextoAlt : E.Texto) : E.Normal)); break;
                                default:
                                    var s = v.ToString()!;
                                    row.Append(Texto(r, s, conTabla ? (s.Length > 60 ? (alt ? E.TextoLargoAlt : E.TextoLargo) : (alt ? E.TextoAlt : E.Texto)) : E.Normal)); break;
                            }
                        }
                        data.Append(row);
                    }
                    var ultimaCol = Ref(Math.Max(columnas, 1) - 1, 1)[..^1];
                    var vistas = conTabla
                        ? new SheetViews(new SheetView(new Pane { VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen }) { WorkbookViewId = 0, TabSelected = n == 1 })
                        : new SheetViews(new SheetView { WorkbookViewId = 0, TabSelected = n == 1 });
                    ws.Worksheet = new Worksheet(new SheetProperties(new PageSetupProperties { FitToPage = true }) { TabColor = new TabColor { Rgb = Navy } }, vistas, cols, data);
                    if (conTabla) ws.Worksheet.Append(new AutoFilter { Reference = $"A1:{ultimaCol}{totalFilas + 1}" });
                    // Listas desplegables (filas 2 a 2000 de cada columna con lista). Los valores van en la hoja oculta «Listas» (sin límite
                    // de largo: destinos, proveedores, aeropuertos…), con lo que hay en la base AL DESCARGAR. Si se escribe algo que no está
                    // (porque se creó después o se escribe parecido), Excel solo AVISA y deja seguir: al subirlo se busca la coincidencia.
                    if (h.Listas is { Count: > 0 })
                    {
                        var dv = new DataValidations();
                        foreach (var (col, vals) in h.Listas)
                        {
                            var i = h.Encabezados.ToList().FindIndex(e => Clave(e) == Clave(col));
                            var limpios = vals.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().ToList();
                            if (i < 0 || limpios.Count == 0) continue;
                            var letraLista = Ref(listas.Count, 1)[..^1];
                            listas.Add(limpios);
                            var letra = Ref(i, 1)[..^1];
                            dv.Append(new DataValidation
                            {
                                Type = DataValidationValues.List, AllowBlank = true, ShowErrorMessage = true, ShowInputMessage = true,
                                ErrorStyle = DataValidationErrorStyleValues.Warning,
                                ErrorTitle = "No está en la lista", Error = "Ese valor no está en la lista. Si sigues, al subir el Excel te muestro las coincidencias para elegir.",
                                PromptTitle = col.Length > 32 ? col[..32] : col, Prompt = "Elige de la lista (flecha) o escribe: al subirlo se busca el parecido.",
                                SequenceOfReferences = new ListValue<StringValue> { InnerText = $"{letra}2:{letra}2000" },
                                Formula1 = new Formula1($"Listas!${letraLista}$1:${letraLista}${limpios.Count}")
                            });
                        }
                        if (dv.HasChildren) { dv.Count = (uint)dv.ChildElements.Count; ws.Worksheet.Append(dv); }
                    }
                    ws.Worksheet.Append(new PageMargins { Left = 0.4, Right = 0.4, Top = 0.6, Bottom = 0.6, Header = 0.3, Footer = 0.3 });
                    ws.Worksheet.Append(new PageSetup { Orientation = OrientationValues.Landscape, FitToHeight = 0 });
                    sheets.Append(new Sheet { Id = wb.GetIdOfPart(ws), SheetId = n++, Name = h.Nombre.Length > 31 ? h.Nombre[..31] : h.Nombre });
                }
                if (listas.Count > 0)
                {
                    // Hoja oculta con los valores de las listas desplegables (una columna por lista)
                    var ws = wb.AddNewPart<WorksheetPart>();
                    var data = new SheetData();
                    for (var f = 0; f < listas.Max(l => l.Count); f++)
                    {
                        var row = new Row { RowIndex = (uint)(f + 1) };
                        for (var c = 0; c < listas.Count; c++)
                            if (f < listas[c].Count) row.Append(Texto(Ref(c, f + 1), listas[c][f], 0));
                        data.Append(row);
                    }
                    ws.Worksheet = new Worksheet(data);
                    sheets.Append(new Sheet { Id = wb.GetIdOfPart(ws), SheetId = n++, Name = "Listas", State = SheetStateValues.Hidden });
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
            // La hoja de datos: la primera visible que no sea de ayuda (si la usuaria movió «Instrucciones» al principio, se salta)
            var hojas = wb.Workbook.Sheets?.Elements<Sheet>().ToList() ?? new();
            if (hojas.Count == 0) throw new InvalidOperationException("El Excel no tiene hojas.");
            static bool DeAyuda(Sheet s) => Clave(s.Name?.Value) is "instrucciones" or "proveedores" or "listas" or "ayuda" || (s.State is not null && s.State.Value != SheetStateValues.Visible);
            var hoja = hojas.FirstOrDefault(s => !DeAyuda(s)) ?? hojas[0];
            var ws = (WorksheetPart)wb.GetPartById(hoja.Id!);
            var compartidos = wb.SharedStringTablePart?.SharedStringTable.Elements<SharedStringItem>().Select(i => i.InnerText).ToList() ?? new();
            var fechas = FormatosFecha(wb.WorkbookStylesPart?.Stylesheet);

            string? Valor(Cell c)
            {
                if (c.DataType?.Value == CellValues.SharedString && int.TryParse(c.CellValue?.Text, out var i)) return i < compartidos.Count ? compartidos[i] : null;
                if (c.DataType?.Value == CellValues.InlineString) return c.InlineString?.InnerText;
                if (c.DataType?.Value == CellValues.Boolean) return c.CellValue?.Text == "1" ? "Sí" : "No";
                if (c.DataType?.Value == CellValues.Error) return null;   // #N/A, #REF!… de una fórmula: como vacío
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
            var titulos = new Dictionary<string, string>();
            foreach (var row in ws.Worksheet.Descendants<Row>())
            {
                var valores = row.Elements<Cell>().Select(c => (Col: Columna(c.CellReference), Val: Valor(c)?.Trim())).Where(x => !string.IsNullOrEmpty(x.Val)).ToList();
                if (valores.Count == 0) continue;
                if (encabezados is null)
                {
                    // Un título arriba («Hoteles de diciembre») tiene una sola celda: se salta y se busca la fila de los nombres de columna
                    if (valores.Count < 2) continue;
                    encabezados = valores.ToDictionary(x => x.Col, x => Clave(x.Val));
                    foreach (var (_, val) in valores) if (Clave(val) is { Length: > 0 } k) titulos.TryAdd(k, val!);
                    continue;
                }
                var f = new Fila { Numero = (int)(row.RowIndex?.Value ?? 0), Encabezados = titulos };
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
