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
            if (tasa is not null && (TextoPrecio(tasa) ?? EuroVip.Services.ExcelTabla.Numero(tasa)) is decimal t) r.Tasa = t;   // «US$ 1,250.00» y «1.250,00» también
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
                var opciones = hoteles.Select(h => new EuroVip.Services.CargaExcelOpcion(h.Id.ToString(), h.Nombre, h.Destino)).ToList();
                var porId = hoteles.GroupBy(h => h.Id).ToDictionary(g => g.Key, g => g.First());

                foreach (var texto in textosHotel)
                {
                    // Pedido de la usuaria: «si no reconoces el hotel, busca coincidencias; si son varias, muéstralas todas»
                    var item = new ExcelHotel { Texto = texto };
                    var (elegida, aproximado, cand) = EuroVip.Services.Coincidencias.Clasificar(texto, opciones);
                    if (elegida is not null)
                    {
                        var h = porId[int.Parse(elegida.Valor)];
                        item.Estado = "found"; item.Aproximado = aproximado; item.Id_Alojamiento = h.Id; item.Nombre = h.Nombre; item.Destino = h.Destino;
                    }
                    else if (cand.Count > 0) item.Estado = "multiple";
                    item.Candidatos = cand.Select(c => new Opcion { Valor = c.Valor, Texto = c.Texto, Extra = c.Extra }).ToList();
                    r.Hoteles.Add(item);
                }
            }

            // ---- aerolínea ----
            if (!string.IsNullOrWhiteSpace(r.Aerolinea))
            {
                using var cn = _repo.Conexion();
                var aerolineas = (await cn.QueryAsync<(string Id, string Nombre)>(
                    "SELECT id_proveedor, nombre FROM Eurovip.proveedor WHERE id_tipo_proveedor = 'Aerolinea'")).ToList();
                var porCodigo = aerolineas.FirstOrDefault(x => x.Id.Trim().Equals(r.Aerolinea.Trim(), StringComparison.OrdinalIgnoreCase));
                var (elegida, aproximado, cand) = porCodigo.Id is not null ? (new EuroVip.Services.CargaExcelOpcion(porCodigo.Id, porCodigo.Nombre), false, new())
                    : EuroVip.Services.Coincidencias.Clasificar(r.Aerolinea, aerolineas.Select(x => new EuroVip.Services.CargaExcelOpcion(x.Id, x.Nombre)).ToList());
                if (elegida is not null)
                {
                    r.AerolineaId = elegida.Valor;
                    if (aproximado) r.Avisos.Add($"La aerolínea «{r.Aerolinea}» la reconocí como «{elegida.Texto}». Revísala.");
                }
                else r.Avisos.Add(cand.Count > 0
                    ? $"La aerolínea «{r.Aerolinea}» se parece a: {string.Join(", ", cand.Take(6).Select(c => c.Texto))}. Elígela en la lista."
                    : $"No encontré la aerolínea «{r.Aerolinea}»; búscala y elígela a mano.");
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
                    // Fechas y precios escritos como TEXTO (pegados de un correo o de otra web): «05/12/2026», «5-dic-2026»,
                    // «US$ 1,790», «RD$ 1.250,50». Antes se ignoraban (o «1,290» se leía 1.29) y la fecha quedaba sin precio.
                    // Solo desde la columna B: la A son nombres de hotel.
                    if (texto is not null && col >= 2)
                    {
                        if (TextoFecha(texto) is DateTime tf) { numero = tf.ToOADate(); esFecha = true; texto = null; }
                        else if (TextoPrecio(texto) is decimal tp) { numero = (double)tp; texto = null; }
                    }
                    lista.Add(new Celda(fila, col, texto?.Trim(), numero, esFecha));
                }
            }
            return lista;
        }

        private static readonly string[] Meses = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };
        private static readonly string[] MesesEn = { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

        /// <summary>
        /// Una fecha escrita como texto, día primero como en RD: «28/11/2026», «28-11-26», «28.11.2026», «5-dic-2026»,
        /// «5 de diciembre de 2026», «2026-12-05». Null si no lo es.
        /// </summary>
        internal static DateTime? TextoFecha(string texto)
        {
            var s = texto.Trim().ToLowerInvariant();
            if (s.Length is < 6 or > 30 || !s.Any(char.IsDigit)) return null;
            s = Regex.Replace(s, @"\bde(l)?\b", " ");
            // nombre del mes (español o inglés, completo o abreviado) → número
            s = Regex.Replace(s, @"[a-zñ]+\.?", m =>
            {
                var p = m.Value.TrimEnd('.');
                if (p.Length < 3) return m.Value;
                var k = Array.FindIndex(Meses, x => p.StartsWith(x)); if (k < 0) k = Array.FindIndex(MesesEn, x => p.StartsWith(x));
                if (p == "set" || p.StartsWith("setiem")) k = 8;
                return k < 0 ? m.Value : (k + 1).ToString();
            });
            if (Regex.IsMatch(s, "[a-z]")) return null;   // queda texto: no es una fecha
            s = Regex.Replace(s.Trim(), @"[\s./\-]+", "/");
            if (DateTime.TryParseExact(s, new[] { "d/M/yyyy", "d/M/yy", "yyyy/M/d" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d.Year is >= 2000 and <= 2100)
                return d.Date;
            return null;
        }

        /// <summary>Un precio escrito como texto: solo cifras y, como mucho, un símbolo o código de moneda. Null si no lo es.</summary>
        internal static decimal? TextoPrecio(string texto)
        {
            var s = Regex.Replace(texto, @"(?i)RD\$|US\$|USD|DOP|EUR|€|\$|\s", "");
            if (s.Length == 0 || !Regex.IsMatch(s, @"^-?[\d.,]+$")) return null;
            return EuroVip.Services.ExcelTabla.Numero(s);
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

