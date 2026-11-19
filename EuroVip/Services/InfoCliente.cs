using System.Data;
using System.Net;
using System.Text.RegularExpressions;
using Dapper;
using Ganss.Xss;

namespace EuroVip.Services
{
    /// <summary>
    /// Información para el cliente (CAMBIO 130), igual en todos los productos y siempre OPCIONAL:
    /// Incluye · No incluye · Condiciones · Documentación · Importante · Oferta especial.
    /// Pedido de la usuaria: «agrégalo donde falte; siempre opcional, pero si un usuario carga información, que pueda editarla siempre».
    /// </summary>
    public class InfoClienteDto
    {
        public string? Incluye { get; set; }
        public string? No_Incluye { get; set; }
        public string? Condiciones { get; set; }
        public string? Documentacion { get; set; }
        public string? Importante { get; set; }
        public string? Oferta_Especial { get; set; }

        public bool Vacia => new[] { Incluye, No_Incluye, Condiciones, Documentacion, Importante, Oferta_Especial }.All(string.IsNullOrWhiteSpace);
    }

    public static class InfoCliente
    {
        // Pedido de la usuaria: «que cada sección tenga barra de formato (tamaño, negrita, colores, viñetas, numeración); se guarda
        // como HTML, pero siempre se ve con sus efectos en el BackOffice y en la web». Todo lo que se guarda pasa por Limpiar.
        private static string? N(string? x) => Limpiar(x);

        // ---------------- HTML de «Información para el cliente» ----------------
        // ¿Es HTML? Una etiqueta de verdad (<p>, </li>, <br/>, <!-- …). «niños < 12» o «<100 €» siguen siendo texto plano.
        private static readonly Regex Etiqueta = new(@"<\s*/?\s*[a-zA-Z][a-zA-Z0-9]*(\s[^<>]*)?/?\s*>|<!--", RegexOptions.Compiled);
        public static bool EsHtml(string? x) => !string.IsNullOrEmpty(x) && Etiqueta.IsMatch(x);

        // Lista blanca: lo que produce la barra de formato y nada más. Sin enlaces, imágenes, iframes, scripts ni on*.
        private static readonly string[] EtiquetasPermitidas = { "p", "br", "strong", "b", "em", "i", "u", "span", "ul", "ol", "li", "div" };
        private static readonly string[] CssPermitido = { "color", "background-color", "font-size", "font-weight" };
        // Estas se borran CON su contenido (el texto de un <script> no debe quedar como texto visible).
        private static readonly HashSet<string> ConContenido = new(StringComparer.OrdinalIgnoreCase)
            { "script", "style", "iframe", "object", "embed", "noscript", "template", "svg", "math", "textarea", "select", "button", "title", "head", "img", "video", "audio", "form", "input", "link", "meta" };
        private static readonly ThreadLocal<HtmlSanitizer> Saneador = new(() =>
        {
            var h = new HtmlSanitizer(new HtmlSanitizerOptions
            {
                AllowedTags = new HashSet<string>(EtiquetasPermitidas, StringComparer.OrdinalIgnoreCase),
                AllowedAttributes = new HashSet<string>(new[] { "style" }, StringComparer.OrdinalIgnoreCase),
                AllowedCssProperties = new HashSet<string>(CssPermitido, StringComparer.OrdinalIgnoreCase),
                AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            })
            { KeepChildNodes = true };   // <font>, <a>, <h2>… se quitan pero su texto queda
            h.RemovingTag += (_, e) => { if (ConContenido.Contains(e.Tag.LocalName)) e.Tag.InnerHtml = ""; };
            return h;
        });

        /// <summary>
        /// Lo que se guarda de cada texto: vacío → null; texto plano (Excel, cargas viejas) → tal cual; HTML → saneado con la lista
        /// blanca y en una sola línea (los saltos los dan &lt;p&gt;, &lt;br&gt; y &lt;li&gt;; así «nota» se sigue separando por línea en blanco).
        /// </summary>
        public static string? Limpiar(string? x)
        {
            if (string.IsNullOrWhiteSpace(x)) return null;
            if (!EsHtml(x)) return x.Trim();
            var html = Regex.Replace(Saneador.Value!.Sanitize(x), @"[\r\n]+", " ").Trim();
            return string.IsNullOrWhiteSpace(TextoPlano(html)) ? null : html;
        }

        /// <summary>HTML → texto plano con saltos de línea (para Excel, WhatsApp o donde se lea como texto). Texto plano → igual.</summary>
        public static string? TextoPlano(string? x)
        {
            if (string.IsNullOrWhiteSpace(x)) return null;
            if (!EsHtml(x)) return x.Trim();
            var t = Regex.Replace(x, @"[\r\n]+", " ");
            t = Regex.Replace(t, @"<\s*li\b[^>]*>", "\n• ", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"<\s*(br|/p|/div|/ul|/ol)\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
            t = WebUtility.HtmlDecode(Regex.Replace(t, @"<[^>]*>", ""));
            t = Regex.Replace(t, @"[ \t ]+", " ");
            t = Regex.Replace(string.Join("\n", t.Split('\n').Select(l => l.Trim())), @"\n{3,}", "\n\n").Trim();
            return t.Length == 0 ? null : t;
        }

        /// <summary>Tablas con las 6 columnas (nombre → tabla y condición por la clave @id). Solo estas: van en el SQL.</summary>
        private static readonly IReadOnlyDictionary<string, (string Tabla, string Donde)> Tablas = new Dictionary<string, (string, string)>
        {
            ["Eurovip.servicio_excursion"] = ("Eurovip.servicio_excursion", "id_Excursion = @id"),
            ["Eurovip.traslado"] = ("Eurovip.traslado", "id_traslado = @id"),
            ["Eurovip.inicio"] = ("Eurovip.inicio", "id = @id"),
            // Hoteles de la vitrina: se identifican por id_servicio (como en HotelRepository)
            ["HOTEL"] = ("Eurovip.inicio", "id_servicio = @id AND tipo_servicio = 'HOTEL' AND id_idioma = 'ES'"),
            ["Eurovip.crucero"] = ("Eurovip.crucero", "id_crucero = @id"),
        };

        public static async Task<InfoClienteDto> LeerAsync(IDbConnection cn, string tabla, int id)
        {
            var (t, donde) = Tablas[tabla];
            return await cn.QueryFirstOrDefaultAsync<InfoClienteDto>($@"SELECT incluye AS Incluye, no_incluye AS No_Incluye, condiciones AS Condiciones,
                documentacion AS Documentacion, importante AS Importante, oferta_especial AS Oferta_Especial FROM {t} WHERE {donde}", new { id })
                ?? new InfoClienteDto();
        }

        /// <summary>Guarda los 6 textos (vacío = NULL). null = la pantalla no los mandó: no se toca nada.</summary>
        public static Task GuardarAsync(IDbConnection cn, string tabla, int id, InfoClienteDto? info, IDbTransaction? tx = null)
        {
            if (info is null) return Task.CompletedTask;
            var (t, donde) = Tablas[tabla];
            return cn.ExecuteAsync($@"UPDATE {t} SET incluye = @inc, no_incluye = @noinc, condiciones = @cond, documentacion = @docs,
                importante = @imp, oferta_especial = @ofe WHERE {donde}",
                new { id, inc = N(info.Incluye), noinc = N(info.No_Incluye), cond = N(info.Condiciones), docs = N(info.Documentacion), imp = N(info.Importante), ofe = N(info.Oferta_Especial) }, tx);
        }

        // ---------------- Solo Hotel, Paquetes y Circuitos: Eurovip.servicio_descripcion (lo que ya lee la web) ----------------
        // incluye / no_incluye / oferta_especial van en su columna; en «nota» las condiciones y luego «Documentación: …» e
        // «Importante: …», separados por una línea en blanco (igual que CargaCircuitoService.GuardarDescripcionAsync).

        /// <summary>Separa la «nota» de servicio_descripcion en condiciones, documentación e importante.</summary>
        public static (string? Condiciones, string? Documentacion, string? Importante) Desempacar(string? nota)
        {
            var cond = new List<string>(); var docs = new List<string>(); var imp = new List<string>();
            foreach (var b in (nota ?? "").Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                if (b.StartsWith("Documentación:", StringComparison.OrdinalIgnoreCase)) docs.Add(b["Documentación:".Length..].Trim());
                else if (b.StartsWith("Importante:", StringComparison.OrdinalIgnoreCase)) imp.Add(b["Importante:".Length..].Trim());
                else if (b.StartsWith("Condiciones:", StringComparison.OrdinalIgnoreCase)) cond.Add(b["Condiciones:".Length..].Trim());
                else if (b.Trim() != "") cond.Add(b.Trim());
            }
            static string? J(List<string> l) => l.Count == 0 ? null : string.Join("\n\n", l);
            return (J(cond), J(docs), J(imp));
        }

        /// <summary>Junta condiciones, documentación e importante en la «nota» de servicio_descripcion.</summary>
        public static string? Empacar(string? condiciones, string? documentacion, string? importante)
        {
            var partes = new[] { N(condiciones), N(documentacion) is string d ? "Documentación: " + d : null, N(importante) is string i ? "Importante: " + i : null }
                .Where(x => x is not null).ToList();
            return partes.Count == 0 ? null : string.Join("\n\n", partes);
        }
    }
}
