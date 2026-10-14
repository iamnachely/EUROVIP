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
        public SKTypeface Titulo { get; }
        public SKTypeface Negrita { get; }
        public SKTypeface Normal { get; }

        public FlyerFuentes()
        {
            Titulo = Tipo(true, "Segoe UI Black", "Segoe UI", "Arial Black", "Arial");
            Negrita = Tipo(true, "Segoe UI Semibold", "Segoe UI", "Arial");
            Normal = Tipo(false, "Segoe UI", "Arial");
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

        public void Dispose() { Titulo.Dispose(); Negrita.Dispose(); Normal.Dispose(); }
    }

    /// <summary>
    /// Plantillas modernas de flyer dibujadas con SkiaSharp (colores Eurovip: tinta #0B1215 y aquas).
    /// Cada plantilla recibe el destino con su top de hoteles, la foto de fondo (si hay) y la web del pie.
    /// </summary>
    public static class FlyerPlantillas
    {
        public static readonly FlyerPlantilla[] Todas =
        {
            new("postal",   "Postal",   "Foto grande arriba y los hoteles en una hoja blanca redondeada.", 1080, 1350, "WhatsApp · Instagram (4:5)"),
            new("historia", "Historia", "Foto a pantalla completa con tarjetas de cristal. Para estados y stories.", 1080, 1920, "Estado de WhatsApp · Story (9:16)"),
            new("minimal",  "Minimal",  "Blanco y limpio: foto enmarcada y tipografía grande.", 1080, 1350, "WhatsApp · Instagram (4:5)"),
            new("tropical", "Tropical", "Degradado aqua con la foto en círculo y tarjetas blancas.", 1080, 1080, "Cuadrado · Instagram (1:1)"),
            new("clasica",  "Clásica",  "Portada con sello de precio y lista de hoteles (la de siempre, retocada).", 900, 1190, "WhatsApp"),
        };

        public static FlyerPlantilla Buscar(string? clave) => Todas.FirstOrDefault(p => p.Clave == clave) ?? Todas[0];

        // ---------------- colores ----------------
        private static readonly SKColor Ink = SKColor.Parse("0B1215"), Ink2 = SKColor.Parse("12302F"), Aqua = SKColor.Parse("74F2E6"),
            Mint = SKColor.Parse("B3FFF3"), Teal = SKColor.Parse("2BD4C4"), Deep = SKColor.Parse("0E8F86"), Muted = SKColor.Parse("5B726F"),
            Linea = SKColor.Parse("E2EFED"), Blanco = SKColors.White, Suave = SKColor.Parse("F5FBFA");

        private static readonly CultureInfo Es = new("es-ES");
        private static string Euros(int v) => v.ToString("N0", Es) + " €";
        private static string Hoy => DateTime.Now.ToString("d 'de' MMMM", Es);

        public static SKData Dibujar(string? clave, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            var p = Buscar(clave);
            using var surf = SKSurface.Create(new SKImageInfo(p.Ancho, p.Alto));
            var c = surf.Canvas;
            c.Clear(Blanco);
            switch (p.Clave)
            {
                case "historia": Historia(c, p, d, foto, f, web); break;
                case "minimal": Minimal(c, p, d, foto, f, web); break;
                case "tropical": Tropical(c, p, d, foto, f, web); break;
                case "clasica": Clasica(c, p, d, foto, f, web); break;
                default: Postal(c, p, d, foto, f, web); break;
            }
            using var img = surf.Snapshot();
            return img.Encode(SKEncodedImageFormat.Png, 100);
        }

        // =====================================================================================
        // 1) POSTAL — foto arriba, hoja blanca redondeada con los hoteles
        // =====================================================================================
        private static void Postal(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, foto_h = 640, pad = 64;
            Foto(c, foto, new SKRect(0, 0, W, foto_h + 60));
            Degradado(c, new SKRect(0, 0, W, foto_h + 60), SKColors.Transparent, Ink.WithAlpha(200), vertical: true, desde: .35f);
            Chip(c, f, $"OFERTAS DESDE {d.Origen.ToUpperInvariant()}", pad, 56, Ink.WithAlpha(210), Aqua, 22);
            TextoAjustado(c, d.Nombre_Destino, pad, foto_h - 110, W - 2 * pad - 260, f.Titulo, 96, 60, Blanco);
            Texto(c, $"Precios por persona · {Hoy}", pad, foto_h - 50, f.Normal, 26, Blanco.WithAlpha(225));
            Sello(c, f, W - pad - 120, foto_h - 140, 120, Minimo(d), Aqua, Ink);

            // hoja blanca
            var hoja = new SKRoundRect(new SKRect(0, foto_h, W, H + 60), 56, 56);
            using (var pp = new SKPaint { Color = Blanco, IsAntialias = true, ImageFilter = SKImageFilter.CreateDropShadow(0, -10, 24, 24, Ink.WithAlpha(60)) })
                c.DrawRoundRect(hoja, pp);
            float y = foto_h + 70;
            Texto(c, "LOS HOTELES MÁS BUSCADOS", pad, y, f.Negrita, 22, Deep);
            y += 26;
            var hs = d.Hoteles.Take(5).ToList();
            float fila = Math.Min(104, (H - 150 - y) / Math.Max(1, hs.Count));
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (i == 0) Rect(c, new SKRect(pad - 20, y + 6, W - pad + 20, y + fila - 6), Mint.WithAlpha(110), 22);
                Circulo(c, pad + 26, y + fila / 2, 26, i == 0 ? Ink : Suave);
                Texto(c, (i + 1).ToString(), pad + 26, y + fila / 2 + 10, f.Titulo, 26, i == 0 ? Aqua : Ink, SKTextAlign.Center);
                TextoCorto(c, h.Nombre_Hotel, pad + 72, y + fila / 2 - 4, W - 2 * pad - 72 - 210, f.Negrita, 30, Ink);
                TextoCorto(c, $"{h.Nombre_Habitacion} · {h.Regimen}", pad + 72, y + fila / 2 + 30, W - 2 * pad - 72 - 210, f.Normal, 21, Muted);
                Texto(c, Euros(h.Precio_Final), W - pad, y + fila / 2 + 12, f.Titulo, 36, Ink, SKTextAlign.Right);
                y += fila;
            }
            // pie
            Rect(c, new SKRect(0, H - 110, W, H), Ink);
            Rect(c, new SKRect(0, H - 110, W, H - 104), Aqua);
            Texto(c, web, pad, H - 44, f.Titulo, 34, Blanco);
            Boton(c, f, "Reserva por WhatsApp", W - pad, H - 55, Aqua, Ink, alinearDerecha: true);
        }

        // =====================================================================================
        // 2) HISTORIA — 9:16, foto completa y tarjetas de cristal
        // =====================================================================================
        private static void Historia(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, pad = 72;
            Foto(c, foto, new SKRect(0, 0, W, H));
            Degradado(c, new SKRect(0, 0, W, H), Ink.WithAlpha(90), Ink.WithAlpha(235), vertical: true, desde: .1f);
            Chip(c, f, $"DESDE {d.Origen.ToUpperInvariant()}", pad, 120, Blanco.WithAlpha(40), Blanco, 26, borde: Blanco.WithAlpha(120));
            TextoAjustado(c, d.Nombre_Destino, pad, 330, W - 2 * pad, f.Titulo, 132, 80, Blanco);
            Texto(c, "Ofertas de la semana · precio por persona", pad, 400, f.Normal, 30, Blanco.WithAlpha(220));

            // precio desde grande
            Texto(c, "DESDE", pad, 500, f.Negrita, 28, Aqua);
            Texto(c, Euros(Minimo(d)), pad, 600, f.Titulo, 110, Aqua);

            var hs = d.Hoteles.Take(5).ToList();
            float y = 680, alto = 150, gap = 22;
            foreach (var (h, i) in hs.Select((h, i) => (h, i)))
            {
                var r = new SKRect(pad, y, W - pad, y + alto);
                Rect(c, r, Blanco.WithAlpha((byte)(i == 0 ? 245 : 34)), 30, borde: Blanco.WithAlpha(90));
                var txt = i == 0 ? Ink : Blanco;
                if (i == 0) Chip(c, f, "MEJOR PRECIO", r.Left + 32, r.Top + 22, Teal, Ink, 18);
                TextoCorto(c, h.Nombre_Hotel, r.Left + 32, r.Top + (i == 0 ? 92 : 66), r.Width - 330, f.Negrita, 36, txt);
                TextoCorto(c, $"{h.Nombre_Habitacion} · {h.Regimen}", r.Left + 32, r.Top + (i == 0 ? 128 : 106), r.Width - 330, f.Normal, 24, i == 0 ? Muted : Blanco.WithAlpha(200));
                Texto(c, Euros(h.Precio_Final), r.Right - 32, r.MidY + 18, f.Titulo, 50, i == 0 ? Ink : Aqua, SKTextAlign.Right);
                y += alto + gap;
            }
            // llamada a la acción
            var bt = new SKRect(pad, H - 220, W - pad, H - 120);
            Rect(c, bt, Aqua, 50);
            Texto(c, "Reserva ya · escríbenos", bt.MidX, bt.MidY + 14, f.Titulo, 40, Ink, SKTextAlign.Center);
            Texto(c, web, W / 2, H - 60, f.Negrita, 30, Blanco.WithAlpha(230), SKTextAlign.Center);
        }

        // =====================================================================================
        // 3) MINIMAL — blanco, foto enmarcada, tipografía grande
        // =====================================================================================
        private static void Minimal(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, pad = 72;
            c.Clear(Blanco);
            Texto(c, $"DESDE {d.Origen.ToUpperInvariant()}", pad, 104, f.Negrita, 24, Deep);
            TextoAjustado(c, d.Nombre_Destino, pad, 196, W - 2 * pad, f.Titulo, 100, 64, Ink);
            var marco = new SKRect(pad, 240, W - pad, 640);
            c.Save(); c.ClipRoundRect(new SKRoundRect(marco, 36, 36), antialias: true);
            Foto(c, foto, marco); c.Restore();
            // sello flotante sobre la foto
            var sello = new SKRect(W - pad - 300, 590, W - pad - 30, 690);
            Rect(c, sello, Ink, 26);
            Texto(c, "desde", sello.Left + 28, sello.Top + 38, f.Normal, 22, Aqua);
            Texto(c, Euros(Minimo(d)), sello.Right - 28, sello.Top + 78, f.Titulo, 44, Blanco, SKTextAlign.Right);

            float y = 740;
            var hs = d.Hoteles.Take(5).ToList();
            float fila = Math.Min(96, (H - 130 - y) / Math.Max(1, hs.Count));
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                Texto(c, $"{i + 1:00}", pad, y + 44, f.Titulo, 28, i == 0 ? Teal : SKColor.Parse("A9C3BF"));
                TextoCorto(c, h.Nombre_Hotel, pad + 70, y + 38, W - 2 * pad - 70 - 220, f.Negrita, 30, Ink);
                TextoCorto(c, $"{h.Nombre_Habitacion} · {h.Regimen}", pad + 70, y + 70, W - 2 * pad - 70 - 220, f.Normal, 21, Muted);
                Texto(c, Euros(h.Precio_Final), W - pad, y + 50, f.Titulo, 36, i == 0 ? Deep : Ink, SKTextAlign.Right);
                if (i < hs.Count - 1) Rect(c, new SKRect(pad, y + fila - 2, W - pad, y + fila), Linea);
                y += fila;
            }
            Texto(c, $"Precios por persona, sujetos a disponibilidad · {Hoy}", pad, H - 96, f.Normal, 20, Muted);
            Rect(c, new SKRect(pad, H - 70, W - pad, H - 68), Ink);
            Texto(c, web, pad, H - 28, f.Titulo, 28, Ink);
            Texto(c, "Reserva por WhatsApp →", W - pad, H - 28, f.Negrita, 24, Deep, SKTextAlign.Right);
        }

        // =====================================================================================
        // 4) TROPICAL — cuadrado, degradado aqua, foto en círculo
        // =====================================================================================
        private static void Tropical(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, pad = 60;
            using (var g = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(W, H), new[] { Mint, Aqua, Teal, Deep }, new[] { 0f, .35f, .7f, 1f }, SKShaderTileMode.Clamp) })
                c.DrawRect(0, 0, W, H, g);
            // burbujas decorativas
            Circulo(c, W - 80, 90, 160, Blanco.WithAlpha(40));
            Circulo(c, 70, H - 60, 120, Blanco.WithAlpha(30));
            // foto circular
            float cx = W - 250, cy = 250, r = 190;
            c.Save();
            using (var path = new SKPath()) { path.AddCircle(cx, cy, r); c.ClipPath(path, antialias: true); }
            Foto(c, foto, new SKRect(cx - r, cy - r, cx + r, cy + r)); c.Restore();
            using (var aro = new SKPaint { Color = Blanco, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 12 }) c.DrawCircle(cx, cy, r, aro);
            // precio sobre la foto
            var pr = new SKRect(cx - 150, cy + r - 40, cx + 150, cy + r + 50);
            Rect(c, pr, Ink, 45);
            Texto(c, $"desde {Euros(Minimo(d))}", pr.MidX, pr.MidY + 13, f.Titulo, 34, Aqua, SKTextAlign.Center);

            Chip(c, f, $"DESDE {d.Origen.ToUpperInvariant()}", pad, 70, Ink, Aqua, 22);
            TextoAjustado(c, d.Nombre_Destino, pad, 210, W - 2 * pad - 2 * r - 30, f.Titulo, 84, 52, Ink);
            Texto(c, "Ofertas de la semana", pad, 262, f.Negrita, 28, Ink.WithAlpha(190));

            float y = 520;
            var hs = d.Hoteles.Take(4).ToList();
            float alto = Math.Min(104, (H - 120 - y) / Math.Max(1, hs.Count) - 12);
            foreach (var (h, i) in hs.Select((h, i) => (h, i)))
            {
                var rr = new SKRect(pad, y, W - pad, y + alto);
                using (var sp = new SKPaint { Color = Blanco, IsAntialias = true, ImageFilter = SKImageFilter.CreateDropShadow(0, 6, 12, 12, Ink.WithAlpha(40)) })
                    c.DrawRoundRect(new SKRoundRect(rr, 26, 26), sp);
                TextoCorto(c, h.Nombre_Hotel, rr.Left + 28, rr.MidY - 2, rr.Width - 280, f.Negrita, 30, Ink);
                TextoCorto(c, $"{h.Nombre_Habitacion} · {h.Regimen}", rr.Left + 28, rr.MidY + 30, rr.Width - 280, f.Normal, 20, Muted);
                Texto(c, Euros(h.Precio_Final), rr.Right - 28, rr.MidY + 14, f.Titulo, 38, Deep, SKTextAlign.Right);
                y += alto + 12;
            }
            Texto(c, web, W / 2, H - 40, f.Titulo, 30, Ink, SKTextAlign.Center);
        }

        // =====================================================================================
        // 5) CLÁSICA — la de siempre, retocada
        // =====================================================================================
        private static void Clasica(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, HERO = 470, PAD = 56;
            Foto(c, foto, new SKRect(0, 0, W, HERO));
            Degradado(c, new SKRect(0, 0, W, HERO), SKColors.Transparent, Ink.WithAlpha(230), vertical: true, desde: .3f);
            Chip(c, f, $"OFERTAS DESDE {d.Origen.ToUpperInvariant()}", PAD, 40, Ink.WithAlpha(215), Aqua, 16);
            TextoAjustado(c, d.Nombre_Destino, PAD, HERO - 110, W - 2 * PAD - 230, f.Titulo, 74, 48, Blanco);
            Texto(c, $"Precios por persona · {Hoy}", PAD, HERO - 46, f.Normal, 19, Blanco.WithAlpha(220));
            Sello(c, f, W - PAD - 100, HERO - 150, 100, Minimo(d), Aqua, Ink);

            float y = HERO + 56;
            Texto(c, "LOS HOTELES MÁS BUSCADOS", PAD, y, f.Negrita, 16, Deep);
            y += 30;
            var hs = d.Hoteles.Take(5).ToList();
            const float FILA = 100;
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (i == 0) Rect(c, new SKRect(PAD - 18, y, W - PAD + 18, y + FILA - 10), Mint.WithAlpha(120), 18);
                Circulo(c, PAD + 24, y + 44, 24, i == 0 ? Ink : Linea);
                Texto(c, (i + 1).ToString(), PAD + 24, y + 54, f.Titulo, 24, i == 0 ? Aqua : Ink, SKTextAlign.Center);
                TextoCorto(c, h.Nombre_Hotel, PAD + 68, y + 40, W - 2 * PAD - 68 - 190, f.Negrita, 22, Ink);
                TextoCorto(c, $"{h.Nombre_Habitacion} · {h.Regimen}", PAD + 68, y + 68, W - 2 * PAD - 68 - 190, f.Normal, 16, Muted);
                Texto(c, Euros(h.Precio_Final), W - PAD, y + 54, f.Titulo, 30, Ink, SKTextAlign.Right);
                y += FILA;
            }
            Rect(c, new SKRect(0, H - 110, W, H), Ink);
            Rect(c, new SKRect(0, H - 110, W, H - 104), Aqua);
            Texto(c, web, PAD, H - 56, f.Titulo, 28, Blanco);
            Texto(c, "Reserva ya · República Dominicana a tu manera", PAD, H - 24, f.Normal, 15, Aqua);
        }

        // ---------------- piezas comunes ----------------
        private static int Minimo(FlyerDestino d) => d.Hoteles.Count == 0 ? 0 : d.Hoteles.Min(h => h.Precio_Final);

        /// <summary>Foto "cover" (llena el rectángulo recortando lo que sobra) o, si no hay, un degradado aqua.</summary>
        private static void Foto(SKCanvas c, SKBitmap? bmp, SKRect r)
        {
            if (bmp is null)
            {
                using var g = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(r.Left, r.Top), new SKPoint(r.Right, r.Bottom), new[] { Mint, Aqua, Teal }, null, SKShaderTileMode.Clamp) };
                c.DrawRect(r, g);
                return;
            }
            var esc = Math.Max(r.Width / bmp.Width, r.Height / bmp.Height);
            float bw = bmp.Width * esc, bh = bmp.Height * esc;
            using var img = SKImage.FromBitmap(bmp);
            c.Save(); c.ClipRect(r);
            c.DrawImage(img, new SKRect(r.MidX - bw / 2, r.MidY - bh / 2, r.MidX + bw / 2, r.MidY + bh / 2), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            c.Restore();
        }

        private static void Degradado(SKCanvas c, SKRect r, SKColor a, SKColor b, bool vertical, float desde = 0)
        {
            var p0 = vertical ? new SKPoint(r.Left, r.Top + r.Height * desde) : new SKPoint(r.Left + r.Width * desde, r.Top);
            var p1 = vertical ? new SKPoint(r.Left, r.Bottom) : new SKPoint(r.Right, r.Top);
            using var g = new SKPaint { Shader = SKShader.CreateLinearGradient(p0, p1, new[] { a, b }, null, SKShaderTileMode.Clamp) };
            c.DrawRect(r, g);
        }

        private static void Rect(SKCanvas c, SKRect r, SKColor color, float radio = 0, SKColor? borde = null)
        {
            using var p = new SKPaint { Color = color, IsAntialias = true };
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

        private static void Texto(SKCanvas c, string s, float x, float y, SKTypeface t, float tam, SKColor color, SKTextAlign al = SKTextAlign.Left)
        {
            using var font = new SKFont(t, tam);
            using var p = new SKPaint { Color = color, IsAntialias = true };
            c.DrawText(s ?? "", x, y, al, font, p);
        }

        /// <summary>Una línea que no pasa del ancho: si sobra, corta con "…".</summary>
        private static void TextoCorto(SKCanvas c, string? s, float x, float y, float ancho, SKTypeface t, float tam, SKColor color)
        {
            using var font = new SKFont(t, tam);
            var txt = s ?? "";
            if (font.MeasureText(txt) > ancho)
            {
                while (txt.Length > 1 && font.MeasureText(txt + "…") > ancho) txt = txt[..^1];
                txt = txt.TrimEnd() + "…";
            }
            Texto(c, txt, x, y, t, tam, color);
        }

        /// <summary>Título que se achica para entrar en el ancho (hasta un mínimo) y, si aun así no entra, pasa a dos líneas hacia arriba.</summary>
        private static void TextoAjustado(SKCanvas c, string s, float x, float y, float ancho, SKTypeface t, float tam, float minimo, SKColor color)
        {
            var size = tam;
            using (var font = new SKFont(t, size)) while (size > minimo && font.MeasureText(s) > ancho) font.Size = --size;
            using var f2 = new SKFont(t, size);
            if (f2.MeasureText(s) <= ancho) { Texto(c, s, x, y, t, size, color); return; }
            // dos líneas: la segunda en y, la primera encima
            var palabras = s.Split(' ');
            var corte = palabras.Length / 2;
            for (var i = palabras.Length - 1; i > 0; i--) if (f2.MeasureText(string.Join(' ', palabras.Take(i))) <= ancho) { corte = i; break; }
            Texto(c, string.Join(' ', palabras.Take(corte)), x, y - size * 1.02f, t, size, color);
            Texto(c, string.Join(' ', palabras.Skip(corte)), x, y, t, size, color);
        }

        private static void Chip(SKCanvas c, FlyerFuentes f, string s, float x, float y, SKColor fondo, SKColor texto, float tam, SKColor? borde = null)
        {
            using var font = new SKFont(f.Negrita, tam);
            var w = font.MeasureText(s) + tam * 1.6f;
            var r = new SKRect(x, y, x + w, y + tam * 2.1f);
            Rect(c, r, fondo, r.Height / 2, borde);
            Texto(c, s, x + tam * .8f, r.MidY + tam * .36f, f.Negrita, tam, texto);
        }

        private static void Sello(SKCanvas c, FlyerFuentes f, float cx, float cy, float r, int precio, SKColor fondo, SKColor texto)
        {
            using (var p = new SKPaint { Color = fondo, IsAntialias = true, ImageFilter = SKImageFilter.CreateDropShadow(0, 8, 16, 16, Ink.WithAlpha(90)) })
                c.DrawCircle(cx, cy, r, p);
            using (var b = new SKPaint { Color = Blanco, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = r * .07f }) c.DrawCircle(cx, cy, r, b);
            Texto(c, "DESDE", cx, cy - r * .34f, f.Negrita, r * .17f, texto, SKTextAlign.Center);
            Texto(c, Euros(precio), cx, cy + r * .2f, f.Titulo, precio >= 10000 ? r * .38f : r * .46f, texto, SKTextAlign.Center);
            Texto(c, "por persona", cx, cy + r * .5f, f.Normal, r * .15f, texto, SKTextAlign.Center);
        }

        private static void Boton(SKCanvas c, FlyerFuentes f, string s, float x, float yCentro, SKColor fondo, SKColor texto, bool alinearDerecha = false)
        {
            using var font = new SKFont(f.Negrita, 24);
            var w = font.MeasureText(s) + 56;
            var left = alinearDerecha ? x - w : x;
            var r = new SKRect(left, yCentro - 30, left + w, yCentro + 30);
            Rect(c, r, fondo, 30);
            Texto(c, s, r.MidX, yCentro + 9, f.Negrita, 24, texto, SKTextAlign.Center);
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
