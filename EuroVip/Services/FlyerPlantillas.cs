using EuroVip.Models;
using SkiaSharp;
using System.Globalization;

namespace EuroVip.Services
{
    /// <summary>Una plantilla de flyer que el usuario puede elegir.</summary>
    public record FlyerPlantilla(string Clave, string Nombre, string Descripcion, int Ancho, int Alto, string Uso);

    /// <summary>Fuentes ya cargadas (se crean una vez por generación).</summary>
    public sealed class FlyerFuentes : IDisposable
    {
        /// <summary>Titulares grandes y precios: Bahnschrift condensada (estilo cartel) o, si no está, Segoe UI Black.</summary>
        public SKTypeface Display { get; }
        public SKTypeface Titulo { get; }
        public SKTypeface Negrita { get; }
        public SKTypeface Normal { get; }
        /// <summary>Titulares finos (estilo minimalista): Bahnschrift Light o, si no está, Segoe UI Light.</summary>
        public SKTypeface Fina { get; }

        public FlyerFuentes()
        {
            Titulo = Tipo(true, "Segoe UI Black", "Segoe UI", "Arial Black", "Arial");
            Negrita = Tipo(true, "Segoe UI Semibold", "Segoe UI", "Arial");
            Normal = Tipo(false, "Segoe UI", "Arial");
            var l = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyleWeight.Light, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            Fina = l is not null && string.Equals(l.FamilyName, "Bahnschrift", StringComparison.OrdinalIgnoreCase)
                ? l : SKTypeface.FromFamilyName("Segoe UI Light") ?? Tipo(false, "Segoe UI", "Arial");
            var b = SKTypeface.FromFamilyName("Bahnschrift", SKFontStyleWeight.Bold, SKFontStyleWidth.Condensed, SKFontStyleSlant.Upright);
            Display = b is not null && string.Equals(b.FamilyName, "Bahnschrift", StringComparison.OrdinalIgnoreCase)
                ? b : Tipo(true, "Segoe UI Black", "Arial Black", "Arial");
        }

        private static SKTypeface Tipo(bool negrita, params string[] familias)
        {
            foreach (var f in familias)
            {
                var t = SKTypeface.FromFamilyName(f, negrita ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                if (t is not null && string.Equals(t.FamilyName, f, StringComparison.OrdinalIgnoreCase)) return t;
            }
            return SKTypeface.FromFamilyName(null, negrita ? SKFontStyle.Bold : SKFontStyle.Normal);
        }

        public void Dispose() { Display.Dispose(); Titulo.Dispose(); Negrita.Dispose(); Normal.Dispose(); Fina.Dispose(); }
    }

    /// <summary>
    /// Plantillas de flyer minimalistas dibujadas con SkiaSharp: fondo claro, mucho aire, tipografía fina
    /// y precios en pastillas amarillas; el más barato con fondo gris y pastilla gris oscuro. Sin negro, azul, naranja ni beige; marca EUROVIP.
    /// No se muestra el origen de salida: el flyer vende el destino.
    /// </summary>
    public static class FlyerPlantillas
    {
        public static readonly FlyerPlantilla[] Todas =
        {
            new("tropical", "Minimal",          "Foto a un lado y la información al otro. Limpio y elegante.", 1080, 1080, "Cuadrado · Instagram (1:1)"),
            new("ola45",    "Minimal vertical", "Foto enmarcada arriba, destino en letra fina y lista de hoteles.", 1080, 1350, "WhatsApp · Instagram (4:5)"),
            new("postal",   "Centrado",         "Foto a sangre arriba y todo el texto centrado, estilo revista.", 1080, 1350, "WhatsApp · Instagram (4:5)"),
            new("clasica",  "Marco",            "Ficha con marco fino, foto y lista de hoteles.", 900, 1190, "WhatsApp"),
            new("historia", "Story",            "Vertical a pantalla completa: foto oscurecida, tarjetas de cristal y precios en amarillo.", 1080, 1920, "Estado de WhatsApp · Story (9:16)"),
        };

        /// <summary>Busca la plantilla; las que ya no existen (historia, minimal…) caen en la primera.</summary>
        public static FlyerPlantilla Buscar(string? clave) => Todas.FirstOrDefault(p => p.Clave == clave) ?? Todas[0];

        // ---------------- colores ----------------
        private static readonly SKColor Fondo = SKColors.White, Blanco = SKColors.White,
            Amarillo = SKColor.Parse("FACC15"),         // amarillo llamativo: todos los precios
            Texto2 = SKColor.Parse("374151"),           // gris oscuro (no negro)
            Suave2 = SKColor.Parse("6B7280"), Linea2 = SKColor.Parse("E5E7EB"),
            Resalte = SKColor.Parse("F3F4F6");          // fondo gris claro de la fila más barata

        private static readonly CultureInfo Es = new("es-ES");
        private static string Euros(int v) => v.ToString("N0", Es) + " €";
        private const string Legal = "Precio por persona · sujeto a disponibilidad";

        private enum Estilo { Lado, Vertical, Centrado, Marco }

        public static SKData Dibujar(string? clave, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string marca)
        {
            var p = Buscar(clave);
            using var surf = SKSurface.Create(new SKImageInfo(p.Ancho, p.Alto));
            var c = surf.Canvas;
            c.Clear(Fondo);
            var m = string.IsNullOrWhiteSpace(marca) ? "EUROVIP" : marca.Trim().ToUpperInvariant();
            if (p.Clave == "historia") Story(c, p, d, foto, f, m);
            else
            {
                var e = p.Clave switch { "ola45" => Estilo.Vertical, "postal" => Estilo.Centrado, "clasica" => Estilo.Marco, _ => Estilo.Lado };
                Minimal(c, p, d, foto, f, m, e);
            }
            using var img = surf.Snapshot();
            return img.Encode(SKEncodedImageFormat.Png, 100);
        }

        // =====================================================================================
        // Una sola composición minimalista con cuatro encuadres
        // =====================================================================================
        private static void Minimal(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string marca, Estilo e)
        {
            float W = p.Ancho, H = p.Alto;
            float x0, x1, y;            // columna de texto
            bool centro = e == Estilo.Centrado;
            int maxHoteles = 5;

            switch (e)
            {
                case Estilo.Lado:
                    FotoRedondeada(c, foto, new SKRect(48, 48, 450, H - 48), 18);
                    x0 = 510; x1 = W - 56; y = 112;
                    break;
                case Estilo.Vertical:
                    FotoRedondeada(c, foto, new SKRect(60, 60, W - 60, 540), 18);
                    x0 = 72; x1 = W - 72; y = 618;
                    break;
                case Estilo.Centrado:
                    Foto(c, foto, new SKRect(0, 0, W, 490));
                    x0 = 96; x1 = W - 96; y = 566;
                    break;
                default: // Marco
                    using (var st = new SKPaint { Color = Linea2, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 })
                        c.DrawRect(new SKRect(22, 22, W - 22, H - 22), st);
                    Foto(c, foto, new SKRect(48, 48, W - 48, 460));
                    x0 = 76; x1 = W - 76; y = 524;
                    break;
            }
            float ancho = x1 - x0, xc = (x0 + x1) / 2;
            var esc = e == Estilo.Marco ? .84f : 1f;   // la plantilla de 900 px va un poco más pequeña

            // gancho + destino
            var gancho = Gancho(d);
            var tg = 15 * esc;
            var wg = Ancho(f.Negrita, gancho, tg, .3f);
            var xg = centro ? xc - wg / 2 : x0;
            Espaciado(c, gancho, xg, y, f.Negrita, tg, Texto2, .3f);
            Rect(c, new SKRect(xg, y + 8, xg + (centro ? wg : 44), y + 13), Amarillo, 2);
            var (lin, tam) = Medir(d.Nombre_Destino, e == Estilo.Vertical ? ancho - 260 : ancho, f.Fina, (e == Estilo.Lado ? 92 : 104) * esc, 52 * esc, 2);
            var yLinea = y + 24 + tam * .82f;
            foreach (var l in lin)
            {
                Texto(c, l, centro ? xc : x0, yLinea, f.Fina, tam, Texto2, centro ? SKTextAlign.Center : SKTextAlign.Left);
                yLinea += tam * .98f;
            }
            y = yLinea - tam * .98f;

            // "desde" — a la derecha del título en la vertical; debajo en el resto
            var min = Minimo(d);
            if (e == Estilo.Vertical)
            {
                Texto(c, "desde", x1, y - 72, f.Normal, 22, Suave2, SKTextAlign.Right);
                Pastilla(c, Euros(min), f.Negrita, 50, x1, y - 4, Amarillo, Texto2, derecha: true);
                y += 44;
            }
            else
            {
                y += 50 * esc;
                var txt = "desde ";
                using var fn = new SKFont(f.Normal, 24 * esc);
                using var fp = new SKFont(f.Negrita, 30 * esc);
                using var fr = new SKFont(f.Normal, 24 * esc);
                float w1 = fn.MeasureText(txt), w2 = fp.MeasureText(Euros(min)) + 30 * esc * .9f, w3 = fr.MeasureText("  por persona");
                float xi = centro ? xc - (w1 + w2 + w3) / 2 : x0;
                Texto(c, txt, xi, y, f.Normal, 24 * esc, Suave2);
                Pastilla(c, Euros(min), f.Negrita, 30 * esc, xi + w1, y, Amarillo, Texto2);
                Texto(c, "  por persona", xi + w1 + w2, y, f.Normal, 24 * esc, Suave2);
                y += 40 * esc;
            }

            // hoteles
            Rect(c, new SKRect(x0, y, x1, y + 1.5f), Linea2);
            var hs = d.Hoteles.Take(maxHoteles).ToList();
            var barato = Barato(hs);
            float pie = H - (e == Estilo.Lado ? 130 : e == Estilo.Marco ? 116 : 128);
            float fila = Math.Min((e == Estilo.Lado ? 120 : 104) * esc, (pie - 20 - y) / Math.Max(1, hs.Count));
            for (var i = 0; i < hs.Count; i++)
            {
                FilaHotel(c, f, hs[i], x0, x1, y, fila, i == barato, esc * (e == Estilo.Lado ? .92f : 1f));
                y += fila;
                Rect(c, new SKRect(x0, y - 1.5f, x1, y), Linea2);
            }

            // pie: marca + leyenda
            float yp = H - (e == Estilo.Marco ? 58 : 64);
            if (e == Estilo.Lado)
            {
                Espaciado(c, marca, x0, yp - 10, f.Display, 26, Texto2, .3f);
                Texto(c, Legal, x0, yp + 22, f.Normal, 15, Suave2);
            }
            else if (centro)
            {
                Espaciado(c, marca, xc - Ancho(f.Display, marca, 28, .3f) / 2, yp - 4, f.Display, 28, Texto2, .3f);
                Texto(c, Legal, xc, yp + 28, f.Normal, 15, Suave2, SKTextAlign.Center);
            }
            else
            {
                var tm = 28 * esc;
                Espaciado(c, marca, x0, yp + 6, f.Display, tm, Texto2, .3f);
                Texto(c, Legal, x1, yp + 4, f.Normal, 15 * esc, Suave2, SKTextAlign.Right);
            }
        }

        /// <summary>
        /// Fila de hotel: nombre y detalle a la izquierda, precio en pastilla amarilla a la derecha.
        /// El más barato: fila con fondo gris claro, pastilla gris oscuro con el precio en amarillo y "MEJOR PRECIO".
        /// </summary>
        private static void FilaHotel(SKCanvas c, FlyerFuentes f, FlyerHotel h, float x0, float x1, float y, float fila, bool barato, float esc)
        {
            float tn = 27 * esc, td = 18 * esc, tp = 30 * esc;
            // nombre + detalle centrados en la fila
            float bloque = tn * .72f + td + 10 * esc, base1 = y + (fila - bloque) / 2 + tn * .72f, base2 = base1 + td + 10 * esc;
            var precio = Euros(h.Precio_Final);
            using var fp = new SKFont(f.Negrita, tp);
            var wp = fp.MeasureText(precio) + tp * .9f;
            var wl = barato ? Ancho(f.Negrita, "MEJOR PRECIO", 12 * esc, .25f) : 0;
            var ancho = x1 - x0 - Math.Max(wp, wl) - 28;
            if (barato) Rect(c, new SKRect(x0 - 16, y + 5, x1 + 16, y + fila - 6), Resalte, 14);
            TextoCorto(c, h.Nombre_Hotel, x0, base1, ancho, f.Negrita, tn, Texto2);
            TextoCorto(c, Detalle(h), x0, base2, ancho, f.Normal, td, Suave2);
            float bp = barato ? base1 : y + fila / 2 + tp * .35f;
            Pastilla(c, precio, f.Negrita, tp, x1, bp, barato ? Texto2 : Amarillo, barato ? Amarillo : Texto2, derecha: true);
            if (barato) Espaciado(c, "MEJOR PRECIO", x1 - wl, base2 + 8 * esc, f.Negrita, 12 * esc, Texto2, .25f);
        }

        /// <summary>Precio dentro de una pastilla de color. y = línea base del texto. Devuelve el ancho.</summary>
        private static float Pastilla(SKCanvas c, string s, SKTypeface t, float tam, float x, float y, SKColor fondo, SKColor texto, bool derecha = false)
        {
            using var font = new SKFont(t, tam);
            float pad = tam * .45f, w = font.MeasureText(s) + 2 * pad, left = derecha ? x - w : x;
            Rect(c, new SKRect(left, y - tam * .98f, left + w, y + tam * .34f), fondo, tam * .3f);
            Texto(c, s, left + pad, y, t, tam, texto);
            return w;
        }

        // =====================================================================================
        // STORY — 9:16, foto a pantalla completa oscurecida, tarjetas de cristal y precios en amarillo
        // =====================================================================================
        private static void Story(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string marca)
        {
            float W = p.Ancho, H = p.Alto, pad = 72;
            var oscuro = SKColor.Parse("1F2937");
            var lienzo = new SKRect(0, 0, W, H);
            Foto(c, foto, lienzo);
            Degradado(c, lienzo, oscuro.WithAlpha(110), oscuro.WithAlpha(240), vertical: true, desde: .05f);

            // frase gancho en cápsula de cristal
            var gancho = Gancho(d);
            var wg = Ancho(f.Negrita, gancho, 24, .12f);
            Rect(c, new SKRect(pad, 120, pad + wg + 46, 172), Blanco.WithAlpha(40), 26, borde: Blanco.WithAlpha(130));
            Espaciado(c, gancho, pad + 23, 155, f.Negrita, 24, Blanco, .12f);

            // destino (hasta 2 líneas) y precio "desde"
            var (lin, tam) = Medir(d.Nombre_Destino, W - 2 * pad, f.Titulo, 130, 78, 2);
            var y = 238 + tam * .8f;
            for (var i = 0; i < lin.Count; i++) { Texto(c, lin[i], pad, y, f.Titulo, tam, Blanco, sombra: true); if (i < lin.Count - 1) y += tam * .98f; }
            Texto(c, "Ofertas de la semana · precio por persona", pad, y + 66, f.Normal, 30, Blanco.WithAlpha(220));
            Espaciado(c, "DESDE", pad, y + 168, f.Negrita, 28, Amarillo, .08f);
            Texto(c, Euros(Minimo(d)), pad, y + 270, f.Titulo, 110, Amarillo, sombra: true);

            // tarjetas de hoteles
            var hs = d.Hoteles.Take(5).ToList();
            var barato = Barato(hs);
            float top = y + 340, gap = 22, alto = Math.Min(150, (1660 - top) / Math.Max(1, hs.Count) - gap);
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                var r = new SKRect(pad, top, W - pad, top + alto);
                bool b = i == barato;
                if (b) Rect(c, r, Blanco.WithAlpha(248), 30); else Rect(c, r, Blanco.WithAlpha(34), 30, borde: Blanco.WithAlpha(95));
                var precio = Euros(h.Precio_Final);
                using var fp = new SKFont(f.Titulo, 50);
                var anchoTxt = r.Width - 64 - fp.MeasureText(precio) - 30;
                float yn = b ? r.MidY + 6 : r.MidY - 4, yd = yn + 40;
                if (b)
                {
                    var we = Ancho(f.Negrita, "MEJOR PRECIO", 17, .06f) + 28;
                    var et = new SKRect(r.Left + 32, yn - 64, r.Left + 32 + we, yn - 30);
                    Rect(c, et, Amarillo, 17);
                    Espaciado(c, "MEJOR PRECIO", et.Left + 14, et.MidY + 6, f.Negrita, 17, oscuro, .06f);
                }
                TextoCorto(c, h.Nombre_Hotel, r.Left + 32, yn, anchoTxt, f.Negrita, 36, b ? oscuro : Blanco);
                TextoCorto(c, Detalle(h), r.Left + 32, yd, anchoTxt, f.Normal, 24, b ? Suave2 : Blanco.WithAlpha(200));
                Texto(c, precio, r.Right - 32, r.MidY + 18, f.Titulo, 50, b ? oscuro : Amarillo, SKTextAlign.Right);
                top += alto + gap;
            }

            // llamada a la acción + marca
            var bt = new SKRect(pad, 1700, W - pad, 1800);
            Rect(c, bt, Amarillo, 50);
            Texto(c, "Reserva ya · escríbenos", bt.MidX, bt.MidY + 14, f.Titulo, 40, oscuro, SKTextAlign.Center);
            Espaciado(c, marca, W / 2 - Ancho(f.Display, marca, 34, .3f) / 2, 1862, f.Display, 34, Blanco, .3f);
            Texto(c, Legal, W / 2, 1898, f.Normal, 18, Blanco.WithAlpha(170), SKTextAlign.Center);
        }

        private static void FotoRedondeada(SKCanvas c, SKBitmap? foto, SKRect r, float radio)
        {
            c.Save(); c.ClipRoundRect(new SKRoundRect(r, radio, radio), SKClipOperation.Intersect, antialias: true);
            Foto(c, foto, r); c.Restore();
        }

        // ---------------- textos comunes ----------------
        private static int Minimo(FlyerDestino d) => d.Hoteles.Count == 0 ? 0 : d.Hoteles.Min(h => h.Precio_Final);

        /// <summary>Índice del hotel más barato de la lista mostrada (el que se resalta).</summary>
        private static int Barato(List<FlyerHotel> hs) => hs.Count == 0 ? -1 : hs.IndexOf(hs.MinBy(h => h.Precio_Final)!);

        private static string Detalle(FlyerHotel h) =>
            string.Join(" · ", new[] { h.Nombre_Habitacion, h.Regimen }.Where(x => !string.IsNullOrWhiteSpace(x)));

        /// <summary>Frase gancho de la parte de arriba (no se usa el origen: el flyer vende el destino).</summary>
        private static string Gancho(FlyerDestino d) =>
            d.Hoteles.Count > 0 && d.Hoteles.All(h => (h.Regimen ?? "").Contains("incluido", StringComparison.OrdinalIgnoreCase) || string.Equals(h.Regimen, "TI", StringComparison.OrdinalIgnoreCase))
                ? "VACACIONES TODO INCLUIDO" : "OFERTAS DE LA SEMANA";

        // ---------------- piezas de dibujo ----------------
        /// <summary>Foto "cover" (llena el rectángulo recortando lo que sobra) o, si no hay, un fondo neutro.</summary>
        private static void Foto(SKCanvas c, SKBitmap? bmp, SKRect r, SKPaint? paint = null)
        {
            if (bmp is null)
            {
                using var g = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(r.Left, r.Top), new SKPoint(r.Right, r.Bottom), new[] { Linea2, Fondo }, null, SKShaderTileMode.Clamp) };
                c.DrawRect(r, g);
                return;
            }
            var esc = Math.Max(r.Width / bmp.Width, r.Height / bmp.Height);
            float bw = bmp.Width * esc, bh = bmp.Height * esc;
            using var img = SKImage.FromBitmap(bmp);
            c.Save(); c.ClipRect(r);
            c.DrawImage(img, new SKRect(r.MidX - bw / 2, r.MidY - bh / 2, r.MidX + bw / 2, r.MidY + bh / 2), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
            c.Restore();
        }

        private static void Degradado(SKCanvas c, SKRect r, SKColor a, SKColor b, bool vertical, float desde = 0)
        {
            var p0 = vertical ? new SKPoint(r.Left, r.Top + r.Height * desde) : new SKPoint(r.Left + r.Width * desde, r.Top);
            var p1 = vertical ? new SKPoint(r.Left, r.Bottom) : new SKPoint(r.Right, r.Top);
            using var g = new SKPaint { Shader = SKShader.CreateLinearGradient(p0, p1, new[] { a, b }, null, SKShaderTileMode.Clamp) };
            c.DrawRect(r, g);
        }

        private static void Rect(SKCanvas c, SKRect r, SKColor color, float radio = 0, SKColor? borde = null, bool sombra = false)
        {
            using var p = new SKPaint { Color = color, IsAntialias = true };
            if (sombra) p.ImageFilter = SKImageFilter.CreateDropShadow(0, 6, 14, 14, Texto2.WithAlpha(30));
            if (radio > 0) c.DrawRoundRect(r, radio, radio, p); else c.DrawRect(r, p);
            if (borde is not null)
            {
                using var b = new SKPaint { Color = borde.Value, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                c.DrawRoundRect(r, radio, radio, b);
            }
        }

        private static void Texto(SKCanvas c, string s, float x, float y, SKTypeface t, float tam, SKColor color, SKTextAlign al = SKTextAlign.Left, bool sombra = false)
        {
            using var font = new SKFont(t, tam);
            using var p = new SKPaint { Color = color, IsAntialias = true };
            if (sombra) p.ImageFilter = SKImageFilter.CreateDropShadow(0, tam * .03f, tam * .08f, tam * .08f, Texto2.WithAlpha(110));
            c.DrawText(s ?? "", x, y, al, font, p);
        }

        /// <summary>Texto con espaciado entre letras (para los "kickers" en mayúsculas).</summary>
        private static void Espaciado(SKCanvas c, string s, float x, float y, SKTypeface t, float tam, SKColor color, float tracking)
        {
            using var font = new SKFont(t, tam);
            using var p = new SKPaint { Color = color, IsAntialias = true };
            foreach (var ch in s)
            {
                var txt = ch.ToString();
                c.DrawText(txt, x, y, SKTextAlign.Left, font, p);
                x += font.MeasureText(txt) + tam * tracking;
            }
        }

        /// <summary>Una línea que no pasa del ancho: si sobra, corta con "…".</summary>
        private static void TextoCorto(SKCanvas c, string? s, float x, float y, float ancho, SKTypeface t, float tam, SKColor color)
        {
            using var font = new SKFont(t, tam);
            Texto(c, Cortar(font, s ?? "", ancho), x, y, t, tam, color);
        }

        private static string Cortar(SKFont font, string txt, float ancho)
        {
            if (font.MeasureText(txt) <= ancho) return txt;
            while (txt.Length > 1 && font.MeasureText(txt + "…") > ancho) txt = txt[..^1];
            return txt.TrimEnd() + "…";
        }

        /// <summary>
        /// Reparte un titular en como mucho <paramref name="maxLineas"/> líneas, achicando la letra hasta <paramref name="min"/>.
        /// Con <paramref name="saltos"/> respeta los '\n' (una palabra por línea) mientras quepan.
        /// </summary>
        private static (List<string> lineas, float tam) Medir(string s, float ancho, SKTypeface t, float max, float min, int maxLineas, bool saltos = false)
        {
            s ??= "";
            if (saltos && s.Split('\n').Length > maxLineas) s = s.Replace('\n', ' ');
            for (var tam = max; ; tam -= 2)
            {
                using var font = new SKFont(t, tam);
                var l = saltos && s.Contains('\n')
                    ? s.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList()
                    : Envolver(s.Split(' ', StringSplitOptions.RemoveEmptyEntries), font, ancho);
                var cabe = l.Count <= maxLineas && l.All(x => font.MeasureText(x) <= ancho);
                if (cabe) return (l, tam);
                if (tam <= min)
                {
                    if (saltos && s.Contains('\n')) return Medir(s.Replace('\n', ' '), ancho, t, max, min, maxLineas);
                    if (l.Count > maxLineas) l = l.Take(maxLineas - 1).Append(string.Join(' ', l.Skip(maxLineas - 1))).ToList();
                    return (l.Select(x => Cortar(font, x, ancho)).ToList(), tam);
                }
            }
        }

        private static List<string> Envolver(string[] palabras, SKFont font, float ancho)
        {
            var res = new List<string>();
            var actual = "";
            foreach (var w in palabras)
            {
                var prueba = actual.Length == 0 ? w : actual + " " + w;
                if (actual.Length > 0 && font.MeasureText(prueba) > ancho) { res.Add(actual); actual = w; }
                else actual = prueba;
            }
            if (actual.Length > 0) res.Add(actual);
            return res;
        }

        private static float Ancho(SKTypeface t, string s, float tam, float tracking)
        {
            using var font = new SKFont(t, tam);
            return s.Sum(ch => font.MeasureText(ch.ToString()) + tam * tracking) - tam * tracking;
        }

        /// <summary>Destino de ejemplo para la vista previa de las plantillas (cuando todavía no hay precios cargados).</summary>
        public static FlyerDestino Ejemplo(string? destino = null) => new()
        {
            Id_Destino = destino ?? "Punta Cana", Nombre_Destino = destino ?? "Punta Cana", Origen = "Madrid",
            Hoteles = new()
            {
                new() { Nombre_Hotel = "Iberostar Selection Bávaro", Nombre_Habitacion = "Doble Superior", Regimen = "Todo Incluido", Precio_Final = 1089, Posicion = 1 },
                new() { Nombre_Hotel = "Riu Palace Punta Cana", Nombre_Habitacion = "Junior Suite", Regimen = "Todo Incluido", Precio_Final = 1195, Posicion = 2 },
                new() { Nombre_Hotel = "Barceló Bávaro Palace", Nombre_Habitacion = "Deluxe", Regimen = "Todo Incluido", Precio_Final = 1240, Posicion = 3 },
                new() { Nombre_Hotel = "Majestic Elegance Punta Cana", Nombre_Habitacion = "Junior Suite", Regimen = "Todo Incluido", Precio_Final = 1310, Posicion = 4 },
                new() { Nombre_Hotel = "Dreams Royal Beach", Nombre_Habitacion = "Preferred Club", Regimen = "Todo Incluido", Precio_Final = 1395, Posicion = 5 },
            }
        };
    }
}
