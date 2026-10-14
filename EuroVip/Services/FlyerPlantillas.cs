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

        public FlyerFuentes()
        {
            Titulo = Tipo(true, "Segoe UI Black", "Segoe UI", "Arial Black", "Arial");
            Negrita = Tipo(true, "Segoe UI Semibold", "Segoe UI", "Arial");
            Normal = Tipo(false, "Segoe UI", "Arial");
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

        public void Dispose() { Display.Dispose(); Titulo.Dispose(); Negrita.Dispose(); Normal.Dispose(); }
    }

    /// <summary>
    /// Plantillas de flyer de marketing dibujadas con SkiaSharp, con colores cálidos de atardecer
    /// (naranjas, ámbar y crema; sin negro ni azules) y la marca EUROVIP.
    /// El hotel más barato siempre se destaca. No se muestra el origen de salida: el flyer vende el destino.
    /// </summary>
    public static class FlyerPlantillas
    {
        public static readonly FlyerPlantilla[] Todas =
        {
            new("tropical", "Ola",           "Foto con borde de ola, fondo crema y hoteles en tarjetas. El más barato resaltado.", 1080, 1080, "Cuadrado · Instagram (1:1)"),
            new("ola45",    "Ola vertical",  "La Ola en formato alto, con los 5 hoteles en tarjetas.", 1080, 1350, "WhatsApp · Instagram (4:5)"),
            new("postal",   "Editorial",     "Foto arriba con el destino en grande y ranking de hoteles al mejor precio.", 1080, 1350, "WhatsApp · Instagram (4:5)"),
            new("clasica",  "Póster",        "Foto enmarcada con sticker de precio y hoteles en tarjetas.", 900, 1190, "WhatsApp"),
        };

        /// <summary>Busca la plantilla; las que ya no existen (historia, minimal) caen en la primera.</summary>
        public static FlyerPlantilla Buscar(string? clave) => Todas.FirstOrDefault(p => p.Clave == clave) ?? Todas[0];

        // ---------------- colores (atardecer) ----------------
        private static readonly SKColor Naranja = SKColor.Parse("F97316"), NaranjaOsc = SKColor.Parse("EA580C"), Ambar = SKColor.Parse("F59E0B"),
            Melocoton = SKColor.Parse("FED7AA"), Crema = SKColor.Parse("FFF7ED"), Arena = SKColor.Parse("FFEDD5"),
            Texto2 = SKColor.Parse("3F2D23"), Suave2 = SKColor.Parse("8C7466"), Linea2 = SKColor.Parse("F3E3D3"),
            Tostado = SKColor.Parse("431407"), Blanco = SKColors.White;
        private static readonly SKColor[] Atardecer = { SKColor.Parse("FBBF24"), SKColor.Parse("F97316"), SKColor.Parse("EA580C") };

        private static readonly CultureInfo Es = new("es-ES");
        private static string Euros(int v) => v.ToString("N0", Es) + " €";
        private const string Legal = "Precio por persona · sujeto a disponibilidad";
        private const string Lema = "TU AGENCIA DE CONFIANZA";

        public static SKData Dibujar(string? clave, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string marca)
        {
            var p = Buscar(clave);
            using var surf = SKSurface.Create(new SKImageInfo(p.Ancho, p.Alto));
            var c = surf.Canvas;
            c.Clear(Crema);
            var m = string.IsNullOrWhiteSpace(marca) ? "EUROVIP" : marca.Trim().ToUpperInvariant();
            switch (p.Clave)
            {
                case "ola45": Ola(c, p, d, foto, f, m, vertical: true); break;
                case "postal": Editorial(c, p, d, foto, f, m); break;
                case "clasica": Poster(c, p, d, foto, f, m); break;
                default: Ola(c, p, d, foto, f, m, vertical: false); break;
            }
            using var img = surf.Snapshot();
            return img.Encode(SKEncodedImageFormat.Png, 100);
        }

        // =====================================================================================
        // 1) OLA — foto con borde de ola, fondo crema, tarjetas blancas; el más barato en naranja
        // =====================================================================================
        private static void Ola(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string marca, bool vertical)
        {
            float W = p.Ancho, H = p.Alto, pad = 60, yw = vertical ? 520 : 460;
            c.Clear(Crema);
            using var ola = new SKPath();
            ola.MoveTo(0, 0); ola.LineTo(W, 0); ola.LineTo(W, yw);
            ola.CubicTo(W * .78f, yw + 100, W * .57f, yw - 60, W * .37f, yw + 30);
            ola.CubicTo(W * .21f, yw + 100, W * .1f, yw + 50, 0, yw + 10);
            ola.Close();
            // ola de color por detrás, un poco más abajo
            c.Save(); c.Translate(0, 24);
            using (var pa = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(W, 0), Atardecer, null, SKShaderTileMode.Clamp) })
                c.DrawPath(ola, pa);
            c.Restore();
            c.Save(); c.ClipPath(ola, SKClipOperation.Intersect, antialias: true);
            Foto(c, foto, new SKRect(0, 0, W, yw + 110));
            Degradado(c, new SKRect(0, 0, W, 220), Tostado.WithAlpha(120), SKColors.Transparent, vertical: true);
            c.Restore();

            Chip(c, f, Gancho(d), pad, 50, Blanco.WithAlpha(235), NaranjaOsc, 20);
            Marca(c, f, marca, W - pad, 86, 40, Blanco, derecha: true, sombra: true);

            var (lin, tam) = Medir(d.Nombre_Destino, W - 2 * pad - 280, f.Display, vertical ? 118 : 104, 60, 2);
            var yTit = vertical ? yw + 220 : yw + 236;
            Lineas(c, lin, pad, yTit - (lin.Count - 1) * tam * .9f, f.Display, tam, Texto2, .9f);
            Sello(c, f, W - 170, yw + 150, 116, Minimo(d), grados: 8);

            var hs = d.Hoteles.Take(vertical ? 5 : 4).ToList();
            var barato = Barato(hs);
            if (!vertical)
            {
                float top = 748, alto = 100, gap = 16, col = (W - 2 * pad - gap) / 2;
                for (var i = 0; i < hs.Count; i++)
                {
                    float x = pad + (i % 2) * (col + gap), y = top + (i / 2) * (alto + gap);
                    TarjetaHotel(c, f, hs[i], new SKRect(x, y, x + col, y + alto), i == barato, 25, 17, 38);
                }
            }
            else
            {
                float top = 818, alto = 74, gap = 12;
                for (var i = 0; i < hs.Count; i++)
                    TarjetaHotel(c, f, hs[i], new SKRect(pad, top + i * (alto + gap), W - pad, top + i * (alto + gap) + alto), i == barato, 26, 17, 40);
            }

            var yPie = H - 64;
            Marca(c, f, marca, pad, yPie, 40, Naranja);
            Espaciado(c, Lema, pad, yPie + 26, f.Negrita, 14, Suave2, .2f);
            Texto(c, Legal, pad, yPie + 48, f.Normal, 14, Suave2);
            Boton(c, f, "Reserva ahora  →", W - pad, yPie + 4, alinearDerecha: true, tam: 25, alto: 70);
        }

        // =====================================================================================
        // 2) EDITORIAL — foto arriba con el destino, ranking de hoteles en blanco (como el flyer de siempre, renovado)
        // =====================================================================================
        private static void Editorial(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string marca)
        {
            float W = p.Ancho, H = p.Alto, pad = 72, hero = 600;
            c.Clear(Blanco);
            Foto(c, foto, new SKRect(0, 0, W, hero));
            Degradado(c, new SKRect(0, 0, W, hero), SKColors.Transparent, Tostado.WithAlpha(210), vertical: true, desde: .35f);
            Rect(c, new SKRect(0, 0, W, 10), Naranja);
            using (var g = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(W, 0), Atardecer, null, SKShaderTileMode.Clamp) })
                c.DrawRect(0, 0, W, 10, g);

            // pestaña de marca arriba al centro
            using (var fm = new SKFont(f.Display, 38))
            {
                var wm = Ancho(f.Display, marca, 38, .08f) + 70;
                var tab = new SKRect(W / 2 - wm / 2, -30, W / 2 + wm / 2, 92);
                Rect(c, tab, Blanco, 26, sombra: true);
                Marca(c, f, marca, W / 2 - (wm - 70) / 2, 70, 38, Naranja);
            }

            var (lin, tam) = Medir(d.Nombre_Destino, W - 2 * pad - 250, f.Display, 150, 84, 2);
            Lineas(c, lin, pad, 500 - (lin.Count - 1) * tam * .92f, f.Display, tam, Blanco, .92f, sombra: true);
            Espaciado(c, Gancho(d), pad, 556, f.Negrita, 22, Melocoton, .16f);
            Sello(c, f, W - 170, hero - 60, 116, Minimo(d), grados: -8);

            Espaciado(c, "TOP HOTELES AL MEJOR PRECIO", pad, 690, f.Negrita, 20, NaranjaOsc, .2f);
            Rect(c, new SKRect(pad + Ancho(f.Negrita, "TOP HOTELES AL MEJOR PRECIO", 20, .2f) + 24, 682, W - pad - 250, 684), Linea2);

            var hs = d.Hoteles.Take(5).ToList();
            var barato = Barato(hs);
            float y = 716, fila = Math.Min(100, (H - 150 - y) / Math.Max(1, hs.Count));
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                var r = new SKRect(0, y, W, y + fila);
                bool top = i == barato;
                if (top)
                {
                    using (var g = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(W, 0), new[] { Arena, Blanco }, null, SKShaderTileMode.Clamp) })
                        c.DrawRect(r, g);
                    Rect(c, new SKRect(0, r.Top, 8, r.Bottom), Naranja);
                }
                Texto(c, (i + 1).ToString(), pad, r.MidY + 16, f.Display, 46, top ? Naranja : Melocoton);
                float x = pad + 64, ancho = W - pad - x - 230;
                TextoCorto(c, h.Nombre_Hotel, x, r.MidY - (top ? 10 : 2), ancho, f.Negrita, 31, Texto2);
                TextoCorto(c, Detalle(h), x, r.MidY + (top ? 18 : 28), ancho, f.Normal, 20, Suave2);
                if (top) Espaciado(c, "MEJOR PRECIO", x, r.MidY + 42, f.Titulo, 15, Ambar, .2f);
                Texto(c, Euros(h.Precio_Final), W - pad, r.MidY + 18, f.Display, top ? 56 : 46, top ? Naranja : Texto2, SKTextAlign.Right);
                if (i < hs.Count - 1) Rect(c, new SKRect(pad, r.Bottom - 1, W - pad, r.Bottom), Linea2);
                y += fila;
            }

            var pie = new SKRect(0, H - 120, W, H);
            Rect(c, pie, Crema);
            Marca(c, f, marca, pad, H - 64, 40, Naranja);
            Espaciado(c, Lema, pad, H - 38, f.Negrita, 14, Suave2, .2f);
            Texto(c, Legal, pad, H - 16, f.Normal, 13, Suave2);
            Boton(c, f, "Reserva ahora  →", W - pad, H - 60, alinearDerecha: true, tam: 26, alto: 72);
        }

        // =====================================================================================
        // 3) PÓSTER — foto enmarcada, sticker y hoteles en tarjetas sobre crema
        // =====================================================================================
        private static void Poster(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string marca)
        {
            float W = p.Ancho, H = p.Alto, PAD = 56;
            c.Clear(Crema);
            var hero = new SKRect(24, 24, W - 24, 600);
            c.Save(); c.ClipRoundRect(new SKRoundRect(hero, 40, 40), SKClipOperation.Intersect, antialias: true);
            Foto(c, foto, hero);
            Degradado(c, hero, SKColors.Transparent, Tostado.WithAlpha(215), vertical: true, desde: .4f);
            c.Restore();

            Chip(c, f, Gancho(d), PAD, 56, Blanco.WithAlpha(235), NaranjaOsc, 16);
            Marca(c, f, marca, W - PAD, 86, 32, Blanco, derecha: true, sombra: true);
            var (lin, tam) = Medir(d.Nombre_Destino, W - 2 * PAD - 210, f.Display, 124, 64, 2);
            Lineas(c, lin, PAD, 548 - (lin.Count - 1) * tam * .9f, f.Display, tam, Blanco, .9f, sombra: true);
            Sello(c, f, W - 140, 580, 100, Minimo(d), grados: -8);

            Espaciado(c, "HOTELES DESTACADOS", PAD, 690, f.Negrita, 16, NaranjaOsc, .2f);
            var hs = d.Hoteles.Take(5).ToList();
            var barato = Barato(hs);
            float y = 718, fila = Math.Min(70, (H - 136 - y) / Math.Max(1, hs.Count));
            for (var i = 0; i < hs.Count; i++)
            {
                TarjetaHotel(c, f, hs[i], new SKRect(PAD - 14, y, W - PAD + 14, y + fila - 10), i == barato, 22, 15, 32);
                y += fila;
            }

            var pie = new SKRect(24, H - 112, W - 24, H - 24);
            Rect(c, pie, Blanco, 32, sombra: true);
            Marca(c, f, marca, pie.Left + 32, pie.MidY + 4, 34, Naranja);
            Espaciado(c, Lema, pie.Left + 32, pie.MidY + 28, f.Negrita, 12, Suave2, .2f);
            Boton(c, f, "Reserva ya  →", pie.Right - 18, pie.MidY, alinearDerecha: true, tam: 22, alto: 58);
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
        /// <summary>Marca EUROVIP en letra de cartel espaciada.</summary>
        private static void Marca(SKCanvas c, FlyerFuentes f, string marca, float x, float y, float tam, SKColor color, bool derecha = false, bool sombra = false)
        {
            if (derecha) x -= Ancho(f.Display, marca, tam, .08f);
            if (sombra) Espaciado(c, marca, x + 1, y + 2, f.Display, tam, Tostado.WithAlpha(90), .08f);
            Espaciado(c, marca, x, y, f.Display, tam, color, .08f);
        }

        /// <summary>Tarjeta de hotel: blanca con sombra suave; la del más barato en degradado naranja con la etiqueta "MEJOR PRECIO".</summary>
        private static void TarjetaHotel(SKCanvas c, FlyerFuentes f, FlyerHotel h, SKRect r, bool barato, float tNombre, float tDetalle, float tPrecio)
        {
            if (barato) RectDegradado(c, r, 22); else Rect(c, r, Blanco, 22, sombra: true);
            var precio = Euros(h.Precio_Final);
            using var fp = new SKFont(f.Display, tPrecio);
            var wp = fp.MeasureText(precio);
            float ancho = r.Width - 44 - wp - 18;
            TextoCorto(c, h.Nombre_Hotel, r.Left + 22, r.MidY - 3, ancho, f.Negrita, tNombre, barato ? Blanco : Texto2);
            TextoCorto(c, Detalle(h), r.Left + 22, r.MidY + tDetalle + 7, ancho, f.Normal, tDetalle, barato ? Blanco.WithAlpha(225) : Suave2);
            Texto(c, precio, r.Right - 22, r.MidY + tPrecio * .36f, f.Display, tPrecio, barato ? Blanco : NaranjaOsc, SKTextAlign.Right);
            if (barato)
            {
                // etiqueta pegada al borde de arriba, como una pegatina
                using var fe = new SKFont(f.Titulo, 13);
                var we = Ancho(f.Titulo, "MEJOR PRECIO", 13, .16f) + 26;
                var et = new SKRect(r.Right - 22 - we, r.Top - 17, r.Right - 22, r.Top + 9);
                Rect(c, et, Blanco, 13, sombra: true);
                Espaciado(c, "MEJOR PRECIO", et.Left + 13, et.MidY + 5, f.Titulo, 13, NaranjaOsc, .16f);
            }
        }

        /// <summary>Foto "cover" (llena el rectángulo recortando lo que sobra) o, si no hay, un degradado de atardecer.</summary>
        private static void Foto(SKCanvas c, SKBitmap? bmp, SKRect r, SKPaint? paint = null)
        {
            if (bmp is null)
            {
                using var g = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(r.Left, r.Top), new SKPoint(r.Right, r.Bottom), new[] { Melocoton, Ambar, Naranja, NaranjaOsc }, null, SKShaderTileMode.Clamp) };
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

        private static void RectDegradado(SKCanvas c, SKRect r, float radio)
        {
            using var p = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(new SKPoint(r.Left, r.Top), new SKPoint(r.Right, r.Bottom), Atardecer, null, SKShaderTileMode.Clamp),
                ImageFilter = SKImageFilter.CreateDropShadow(0, 8, 14, 14, NaranjaOsc.WithAlpha(90))
            };
            c.DrawRoundRect(r, radio, radio, p);
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
            if (sombra) p.ImageFilter = SKImageFilter.CreateDropShadow(0, 6, 14, 14, Tostado.WithAlpha(34));
            if (radio > 0) c.DrawRoundRect(r, radio, radio, p); else c.DrawRect(r, p);
            if (borde is not null)
            {
                using var b = new SKPaint { Color = borde.Value, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
                c.DrawRoundRect(r, radio, radio, b);
            }
        }

        private static void Circulo(SKCanvas c, float cx, float cy, float r, SKColor color)
        {
            using var p = new SKPaint { Color = color, IsAntialias = true };
            c.DrawCircle(cx, cy, r, p);
        }

        private static void Texto(SKCanvas c, string s, float x, float y, SKTypeface t, float tam, SKColor color, SKTextAlign al = SKTextAlign.Left, bool sombra = false)
        {
            using var font = new SKFont(t, tam);
            using var p = new SKPaint { Color = color, IsAntialias = true };
            if (sombra) p.ImageFilter = SKImageFilter.CreateDropShadow(0, tam * .03f, tam * .08f, tam * .08f, Tostado.WithAlpha(120));
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

        private static void Lineas(SKCanvas c, List<string> lineas, float x, float yPrimera, SKTypeface t, float tam, SKColor color, float interlineado, bool sombra = false)
        {
            for (var i = 0; i < lineas.Count; i++) Texto(c, lineas[i], x, yPrimera + i * tam * interlineado, t, tam, color, sombra: sombra);
        }

        /// <summary>Precio grande con el "€" pequeño arriba. Devuelve el ancho dibujado.</summary>
        private static float PrecioGrande(SKCanvas c, FlyerFuentes f, float x, float y, int precio, float tam, SKColor numero, SKColor simbolo)
        {
            var n = precio.ToString("N0", Es);
            using var fn = new SKFont(f.Display, tam);
            using var fe = new SKFont(f.Display, tam * .5f);
            float wn = fn.MeasureText(n), sep = tam * .05f;
            Texto(c, n, x, y, f.Display, tam, numero, sombra: true);
            Texto(c, "€", x + wn + sep, y - tam * .34f, f.Display, tam * .5f, simbolo);
            return wn + sep + fe.MeasureText("€");
        }

        /// <summary>Pastilla de precio alineada a la derecha. Devuelve su ancho.</summary>
        private static float Pildora(SKCanvas c, string s, SKTypeface t, float tam, float xDerecha, float yCentro, SKColor fondo, SKColor texto, float alto)
        {
            using var font = new SKFont(t, tam);
            var w = font.MeasureText(s) + 40;
            var r = new SKRect(xDerecha - w, yCentro - alto / 2, xDerecha, yCentro + alto / 2);
            Rect(c, r, fondo, alto / 2);
            Texto(c, s, r.MidX, yCentro + tam * .35f, t, tam, texto, SKTextAlign.Center);
            return w;
        }

        private static void Chip(SKCanvas c, FlyerFuentes f, string s, float x, float y, SKColor fondo, SKColor texto, float tam, SKColor? borde = null)
        {
            using var font = new SKFont(f.Negrita, tam);
            float tr = tam * .12f, wTxt = s.Sum(ch => font.MeasureText(ch.ToString()) + tr) - tr;
            var r = new SKRect(x, y, x + wTxt + tam * 1.8f, y + tam * 2.1f);
            Rect(c, r, fondo, r.Height / 2, borde);
            Espaciado(c, s, x + tam * .9f, r.MidY + tam * .36f, f.Negrita, tam, texto, .12f);
        }

        /// <summary>Sticker circular de precio "desde" en degradado de atardecer, con aro blanco, sombra y algo girado.</summary>
        private static void Sello(SKCanvas c, FlyerFuentes f, float cx, float cy, float r, int precio, float grados = 0)
        {
            var texto = Blanco;
            c.Save();
            c.RotateDegrees(grados, cx, cy);
            using (var p = new SKPaint
            {
                IsAntialias = true,
                Shader = SKShader.CreateLinearGradient(new SKPoint(cx - r, cy - r), new SKPoint(cx + r, cy + r), Atardecer, null, SKShaderTileMode.Clamp),
                ImageFilter = SKImageFilter.CreateDropShadow(0, 10, 18, 18, NaranjaOsc.WithAlpha(110))
            })
                c.DrawCircle(cx, cy, r, p);
            using (var b = new SKPaint { Color = Blanco, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = r * .06f })
                c.DrawCircle(cx, cy, r, b);
            using (var b = new SKPaint { Color = Blanco.WithAlpha(110), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = r * .02f })
                c.DrawCircle(cx, cy, r * .86f, b);
            Espaciado(c, "DESDE", cx - Ancho(f.Negrita, "DESDE", r * .15f, .2f) / 2, cy - r * .32f, f.Negrita, r * .15f, texto, .2f);
            var n = Euros(precio);
            using var font = new SKFont(f.Display, r * .5f);
            while (font.MeasureText(n) > r * 1.5f && font.Size > 10) font.Size -= 2;
            Texto(c, n, cx, cy + font.Size * .3f, f.Display, font.Size, texto, SKTextAlign.Center);
            Texto(c, "por persona", cx, cy + r * .52f, f.Normal, r * .14f, texto, SKTextAlign.Center);
            c.Restore();
        }

        private static float Ancho(SKTypeface t, string s, float tam, float tracking)
        {
            using var font = new SKFont(t, tam);
            return s.Sum(ch => font.MeasureText(ch.ToString()) + tam * tracking) - tam * tracking;
        }

        /// <summary>Botón de llamada a la acción en degradado de atardecer con texto blanco.</summary>
        private static void Boton(SKCanvas c, FlyerFuentes f, string s, float x, float yCentro, bool alinearDerecha = false, float tam = 24, float alto = 60)
        {
            using var font = new SKFont(f.Titulo, tam);
            var w = font.MeasureText(s) + alto * .9f;
            var left = alinearDerecha ? x - w : x;
            var r = new SKRect(left, yCentro - alto / 2, left + w, yCentro + alto / 2);
            RectDegradado(c, r, alto / 2);
            Texto(c, s, r.MidX, yCentro + tam * .36f, f.Titulo, tam, Blanco, SKTextAlign.Center);
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
