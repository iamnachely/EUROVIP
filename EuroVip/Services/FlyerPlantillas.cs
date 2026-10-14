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
    /// Plantillas de flyer de marketing dibujadas con SkiaSharp (colores Eurovip: tinta #0B1215 y aquas).
    /// Cada plantilla recibe el destino con su top de hoteles, la foto de fondo (si hay) y la web del pie.
    /// No se muestra el origen de salida: el flyer vende el destino.
    /// </summary>
    public static class FlyerPlantillas
    {
        public static readonly FlyerPlantilla[] Todas =
        {
            new("postal",   "Editorial", "Foto a sangre, titular gigante y los hoteles en un panel de cristal.", 1080, 1350, "WhatsApp · Instagram (4:5)"),
            new("historia", "Story",     "Vertical a pantalla completa: titular apilado, sticker de precio y tarjetas.", 1080, 1920, "Estado de WhatsApp · Story (9:16)"),
            new("minimal",  "Arco",      "Fondo arena, foto en arco y tipografía de revista. Elegante y limpio.", 1080, 1350, "WhatsApp · Instagram (4:5)"),
            new("tropical", "Ola",       "Foto con borde de ola, fondo oscuro y hoteles en cuadrícula.", 1080, 1080, "Cuadrado · Instagram (1:1)"),
            new("clasica",  "Póster",    "Foto enmarcada con sticker de precio y lista de hoteles en tarjetas.", 900, 1190, "WhatsApp"),
        };

        public static FlyerPlantilla Buscar(string? clave) => Todas.FirstOrDefault(p => p.Clave == clave) ?? Todas[0];

        // ---------------- colores ----------------
        private static readonly SKColor Ink = SKColor.Parse("0B1215"), Aqua = SKColor.Parse("74F2E6"),
            Mint = SKColor.Parse("B3FFF3"), Teal = SKColor.Parse("2BD4C4"), Deep = SKColor.Parse("0E8F86"), Muted = SKColor.Parse("5B726F"),
            Linea = SKColor.Parse("E2EFED"), Blanco = SKColors.White, Suave = SKColor.Parse("F3F8F7"), Arena = SKColor.Parse("F5F1E8");

        private static readonly CultureInfo Es = new("es-ES");
        private static string Euros(int v) => v.ToString("N0", Es) + " €";
        private const string Legal = "Precio por persona · sujeto a disponibilidad";

        public static SKData Dibujar(string? clave, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            var p = Buscar(clave);
            using var surf = SKSurface.Create(new SKImageInfo(p.Ancho, p.Alto));
            var c = surf.Canvas;
            c.Clear(Blanco);
            switch (p.Clave)
            {
                case "historia": Story(c, p, d, foto, f, web); break;
                case "minimal": Arco(c, p, d, foto, f, web); break;
                case "tropical": Ola(c, p, d, foto, f, web); break;
                case "clasica": Poster(c, p, d, foto, f, web); break;
                default: Editorial(c, p, d, foto, f, web); break;
            }
            using var img = surf.Snapshot();
            return img.Encode(SKEncodedImageFormat.Png, 100);
        }

        // =====================================================================================
        // 1) EDITORIAL — foto a sangre, titular gigante, panel de cristal con los hoteles
        // =====================================================================================
        private static void Editorial(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, pad = 72;
            var lienzo = new SKRect(0, 0, W, H);
            Foto(c, foto, lienzo);
            Degradado(c, new SKRect(0, 0, W, 320), Ink.WithAlpha(170), SKColors.Transparent, vertical: true);
            Degradado(c, lienzo, SKColors.Transparent, Ink.WithAlpha(250), vertical: true, desde: .28f);

            Rect(c, new SKRect(pad, 90, pad + 44, 96), Aqua, 3);
            Espaciado(c, Gancho(d), pad + 60, 102, f.Negrita, 24, Blanco, .18f);

            var (lin, tam) = Medir(d.Nombre_Destino, W - 2 * pad, f.Display, 170, 96, 2);
            Lineas(c, lin, pad, 584 - (lin.Count - 1) * tam * .92f, f.Display, tam, Blanco, .92f, sombra: true);

            Chip(c, f, "DESDE", pad, 646, Aqua, Ink, 18);
            var w = PrecioGrande(c, f, pad, 798, Minimo(d), 140, Blanco, Aqua);
            Texto(c, "por persona", pad + w + 22, 796, f.Negrita, 28, Blanco, sombra: true);

            // panel de cristal con los hoteles
            var panel = new SKRect(pad - 16, 836, W - pad + 16, H - 146);
            Cristal(c, foto, lienzo, panel, 36, Ink.WithAlpha(120));
            var hs = d.Hoteles.Take(5).ToList();
            float fila = Math.Min(88, (panel.Height - 28) / Math.Max(1, hs.Count)), y = panel.Top + 14;
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                float x = panel.Left + 30, ancho = panel.Width - 60 - 64 - 200;
                Texto(c, $"{i + 1:00}", x, y + fila / 2 + 9, f.Display, 26, i == 0 ? Aqua : Blanco.WithAlpha(150));
                TextoCorto(c, h.Nombre_Hotel, x + 64, y + fila / 2 - 2, ancho, f.Negrita, 28, Blanco);
                TextoCorto(c, Detalle(h), x + 64, y + fila / 2 + 25, ancho, f.Normal, 19, Blanco.WithAlpha(165));
                Texto(c, Euros(h.Precio_Final), panel.Right - 30, y + fila / 2 + 14, f.Display, 42, i == 0 ? Aqua : Blanco, SKTextAlign.Right);
                if (i < hs.Count - 1) Rect(c, new SKRect(x, y + fila - 1, panel.Right - 30, y + fila), Blanco.WithAlpha(38));
                y += fila;
            }

            Boton(c, f, "Reserva ahora  →", pad, H - 74, Aqua, Ink, tam: 28, alto: 80);
            Texto(c, web, W - pad, H - 82, f.Titulo, 28, Blanco, SKTextAlign.Right);
            Texto(c, Legal, W - pad, H - 48, f.Normal, 18, Blanco.WithAlpha(150), SKTextAlign.Right);
        }

        // =====================================================================================
        // 2) STORY — 9:16, titular apilado, sticker de precio y tarjetas
        // =====================================================================================
        private static void Story(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, pad = 72;
            Rect(c, new SKRect(0, 0, W, H), Ink);
            Foto(c, foto, new SKRect(0, 0, W, 1240));
            Degradado(c, new SKRect(0, 0, W, 420), Ink.WithAlpha(170), SKColors.Transparent, vertical: true);
            Degradado(c, new SKRect(0, 640, W, 1241), SKColors.Transparent, Ink, vertical: true);

            Chip(c, f, Gancho(d), pad, 100, Blanco.WithAlpha(36), Blanco, 24, borde: Blanco.WithAlpha(110));
            // una palabra por línea, enorme
            var (lin, tam) = Medir(d.Nombre_Destino.Replace(' ', '\n'), W - 2 * pad, f.Display, 230, 120, 3, saltos: true);
            Lineas(c, lin, pad, 230 + tam * .8f, f.Display, tam, Blanco, .88f, sombra: true);

            Sello(c, f, W - 210, 1000, 150, Minimo(d), Aqua, Ink, grados: -10);

            var hs = d.Hoteles.Take(5).ToList();
            float y = 1170, alto = 96, gap = 12;
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                var r = new SKRect(pad, y, W - pad, y + alto);
                if (i == 0) Rect(c, r, Blanco, 28); else Rect(c, r, Blanco.WithAlpha(20), 28, borde: Blanco.WithAlpha(55));
                var txt = i == 0 ? Ink : Blanco;
                Circulo(c, r.Left + 48, r.MidY, 26, i == 0 ? Ink : Aqua);
                Texto(c, (i + 1).ToString(), r.Left + 48, r.MidY + 10, f.Display, 28, i == 0 ? Aqua : Ink, SKTextAlign.Center);
                var precio = Euros(h.Precio_Final);
                var wp = Pildora(c, precio, f.Display, 36, r.Right - 18, r.MidY, i == 0 ? Ink : Aqua, i == 0 ? Aqua : Ink, 62);
                TextoCorto(c, h.Nombre_Hotel, r.Left + 92, r.MidY - 4, r.Width - 92 - wp - 40, f.Negrita, 31, txt);
                TextoCorto(c, Detalle(h), r.Left + 92, r.MidY + 27, r.Width - 92 - wp - 40, f.Normal, 20, i == 0 ? Muted : Blanco.WithAlpha(180));
                y += alto + gap;
            }

            var bt = new SKRect(pad, 1744, W - pad, 1834);
            Rect(c, bt, Aqua, 45);
            Texto(c, "Reserva ya por WhatsApp  →", bt.MidX, bt.MidY + 13, f.Titulo, 36, Ink, SKTextAlign.Center);
            Texto(c, $"{web}  ·  {Legal}", W / 2, 1884, f.Normal, 22, Blanco.WithAlpha(180), SKTextAlign.Center);
        }

        // =====================================================================================
        // 3) ARCO — fondo arena, foto en arco, tipografía de revista
        // =====================================================================================
        private static void Arco(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, pad = 72;
            c.Clear(Arena);
            Circulo(c, W - 70, 120, 150, Aqua.WithAlpha(110));
            var arco = new SKRect(560, 80, W - pad, 760);
            using (var contorno = ArcoForma(new SKRect(arco.Left - 22, arco.Top + 22, arco.Right - 22, arco.Bottom + 22)))
            using (var st = new SKPaint { Color = Deep, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 })
                c.DrawRoundRect(contorno, st);
            using (var forma = ArcoForma(arco))
            {
                c.Save(); c.ClipRoundRect(forma, SKClipOperation.Intersect, antialias: true);
                Foto(c, foto, arco); c.Restore();
            }

            Rect(c, new SKRect(pad, 118, pad + 36, 123), Deep, 3);
            Espaciado(c, Gancho(d), pad + 50, 128, f.Negrita, 19, Deep, .16f);
            var (lin, tam) = Medir(d.Nombre_Destino, 560 - pad - 36, f.Display, 150, 72, 3);
            Lineas(c, lin, pad, 175 + tam * .8f, f.Display, tam, Ink, .9f);
            var finTitulo = 175 + tam * .8f + (lin.Count - 1) * tam * .9f;
            if (finTitulo < 470)
            {
                var (sub, ts) = Medir("Los hoteles más buscados al mejor precio. ¡Reserva antes de que se agoten!", 560 - pad - 36, f.Normal, 30, 24, 3);
                Lineas(c, sub, pad, finTitulo + 70, f.Normal, ts, Muted, 1.3f);
            }

            Espaciado(c, "DESDE", pad, 640, f.Negrita, 20, Muted, .2f);
            PrecioGrande(c, f, pad, 740, Minimo(d), 120, Ink, Deep);
            Texto(c, "por persona", pad, 780, f.Normal, 22, Muted);

            var hs = d.Hoteles.Take(5).ToList();
            float y = 826, fila = Math.Min(80, (H - 150 - y) / Math.Max(1, hs.Count));
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                float ancho = W - 2 * pad - 70 - 220;
                Texto(c, $"{i + 1:00}", pad, y + fila / 2 + 11, f.Display, 30, i == 0 ? Deep : Muted.WithAlpha(150));
                TextoCorto(c, h.Nombre_Hotel, pad + 70, y + fila / 2 - 2, ancho, f.Negrita, 29, Ink);
                TextoCorto(c, Detalle(h), pad + 70, y + fila / 2 + 24, ancho, f.Normal, 19, Muted);
                Texto(c, Euros(h.Precio_Final), W - pad, y + fila / 2 + 14, f.Display, 40, i == 0 ? Deep : Ink, SKTextAlign.Right);
                Rect(c, new SKRect(pad, y + fila - 1.5f, W - pad, y + fila), Ink.WithAlpha(30));
                y += fila;
            }

            Texto(c, web, pad, H - 70, f.Titulo, 28, Ink);
            Texto(c, Legal, pad, H - 38, f.Normal, 18, Muted);
            Boton(c, f, "Reserva por WhatsApp  →", W - pad, H - 64, Ink, Aqua, alinearDerecha: true, tam: 24, alto: 68);
        }

        // =====================================================================================
        // 4) OLA — cuadrado, foto con borde de ola, fondo tinta y cuadrícula de hoteles
        // =====================================================================================
        private static void Ola(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, pad = 60;
            c.Clear(Ink);
            using var ola = new SKPath();
            ola.MoveTo(0, 0); ola.LineTo(W, 0); ola.LineTo(W, 470);
            ola.CubicTo(840, 570, 620, 410, 400, 500);
            ola.CubicTo(230, 570, 110, 520, 0, 480);
            ola.Close();
            // ola aqua por detrás, un poco más abajo
            c.Save(); c.Translate(0, 26);
            using (var pa = new SKPaint { Color = Aqua, IsAntialias = true }) c.DrawPath(ola, pa);
            c.Restore();
            c.Save(); c.ClipPath(ola, SKClipOperation.Intersect, antialias: true);
            Foto(c, foto, new SKRect(0, 0, W, 580));
            Degradado(c, new SKRect(0, 0, W, 220), Ink.WithAlpha(150), SKColors.Transparent, vertical: true);
            c.Restore();

            Chip(c, f, Gancho(d), pad, 54, Ink.WithAlpha(200), Aqua, 21);
            var (lin, tam) = Medir(d.Nombre_Destino, W - 2 * pad - 290, f.Display, 118, 64, 2);
            Lineas(c, lin, pad, 700 - (lin.Count - 1) * tam * .9f, f.Display, tam, Blanco, .9f, sombra: true);
            Sello(c, f, W - 175, 610, 118, Minimo(d), Aqua, Ink, grados: 8);

            var hs = d.Hoteles.Take(4).ToList();
            float top = 744, alto = 96, gap = 14, col = (W - 2 * pad - gap) / 2;
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                float x = pad + (i % 2) * (col + gap), y = top + (i / 2) * (alto + gap);
                var r = new SKRect(x, y, x + col, y + alto);
                Rect(c, r, Blanco.WithAlpha((byte)(i == 0 ? 30 : 14)), 22, borde: Blanco.WithAlpha(i == 0 ? (byte)130 : (byte)40));
                var precio = Euros(h.Precio_Final);
                using var fp = new SKFont(f.Display, 36);
                var wp = fp.MeasureText(precio);
                TextoCorto(c, h.Nombre_Hotel, r.Left + 22, r.MidY - 4, col - 44 - wp - 16, f.Negrita, 25, Blanco);
                TextoCorto(c, Detalle(h), r.Left + 22, r.MidY + 25, col - 44 - wp - 16, f.Normal, 17, Blanco.WithAlpha(165));
                Texto(c, precio, r.Right - 22, r.MidY + 13, f.Display, 36, Aqua, SKTextAlign.Right);
            }

            Texto(c, web, pad, H - 52, f.Titulo, 28, Blanco);
            Texto(c, Legal, pad, H - 24, f.Normal, 16, Blanco.WithAlpha(140));
            Boton(c, f, "Reserva ahora  →", W - pad, H - 52, Aqua, Ink, alinearDerecha: true, tam: 25, alto: 70);
        }

        // =====================================================================================
        // 5) PÓSTER — foto enmarcada, sticker y hoteles en tarjetas
        // =====================================================================================
        private static void Poster(SKCanvas c, FlyerPlantilla p, FlyerDestino d, SKBitmap? foto, FlyerFuentes f, string web)
        {
            float W = p.Ancho, H = p.Alto, PAD = 56;
            c.Clear(Suave);
            var hero = new SKRect(24, 24, W - 24, 620);
            c.Save(); c.ClipRoundRect(new SKRoundRect(hero, 40, 40), SKClipOperation.Intersect, antialias: true);
            Foto(c, foto, hero);
            Degradado(c, hero, SKColors.Transparent, Ink.WithAlpha(235), vertical: true, desde: .35f);
            c.Restore();

            Chip(c, f, Gancho(d), PAD, 56, Ink.WithAlpha(200), Aqua, 17);
            var (lin, tam) = Medir(d.Nombre_Destino, W - 2 * PAD - 210, f.Display, 124, 64, 2);
            Lineas(c, lin, PAD, 566 - (lin.Count - 1) * tam * .9f, f.Display, tam, Blanco, .9f, sombra: true);
            Sello(c, f, W - 140, 590, 100, Minimo(d), Aqua, Ink, grados: -8);

            Espaciado(c, "HOTELES DESTACADOS", PAD, 708, f.Negrita, 16, Deep, .2f);
            var hs = d.Hoteles.Take(5).ToList();
            float y = 728, fila = Math.Min(72, (H - 140 - y) / Math.Max(1, hs.Count));
            for (var i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                var r = new SKRect(PAD - 14, y, W - PAD + 14, y + fila - 8);
                Rect(c, r, i == 0 ? Mint.WithAlpha(150) : Blanco, 18, borde: i == 0 ? null : Linea);
                float ancho = r.Width - 60 - 200;
                Texto(c, $"{i + 1:00}", r.Left + 20, r.MidY + 10, f.Display, 26, i == 0 ? Deep : Muted.WithAlpha(150));
                TextoCorto(c, h.Nombre_Hotel, r.Left + 66, r.MidY - 2, ancho, f.Negrita, 22, Ink);
                TextoCorto(c, Detalle(h), r.Left + 66, r.MidY + 20, ancho, f.Normal, 15, Muted);
                Texto(c, Euros(h.Precio_Final), r.Right - 20, r.MidY + 12, f.Display, 34, Ink, SKTextAlign.Right);
                y += fila;
            }

            var pie = new SKRect(24, H - 112, W - 24, H - 24);
            Rect(c, pie, Ink, 32);
            Texto(c, web, pie.Left + 32, pie.MidY - 2, f.Titulo, 25, Blanco);
            Texto(c, Legal, pie.Left + 32, pie.MidY + 24, f.Normal, 14, Blanco.WithAlpha(150));
            Boton(c, f, "Reserva ya  →", pie.Right - 18, pie.MidY, Aqua, Ink, alinearDerecha: true, tam: 22, alto: 58);
        }

        // ---------------- textos comunes ----------------
        private static int Minimo(FlyerDestino d) => d.Hoteles.Count == 0 ? 0 : d.Hoteles.Min(h => h.Precio_Final);

        private static string Detalle(FlyerHotel h) =>
            string.Join(" · ", new[] { h.Nombre_Habitacion, h.Regimen }.Where(x => !string.IsNullOrWhiteSpace(x)));

        /// <summary>Frase gancho de la parte de arriba (no se usa el origen: el flyer vende el destino).</summary>
        private static string Gancho(FlyerDestino d) =>
            d.Hoteles.Count > 0 && d.Hoteles.All(h => (h.Regimen ?? "").Contains("incluido", StringComparison.OrdinalIgnoreCase) || string.Equals(h.Regimen, "TI", StringComparison.OrdinalIgnoreCase))
                ? "VACACIONES TODO INCLUIDO" : "OFERTAS DE LA SEMANA";

        // ---------------- piezas de dibujo ----------------
        /// <summary>Foto "cover" (llena el rectángulo recortando lo que sobra) o, si no hay, un degradado aqua.</summary>
        private static void Foto(SKCanvas c, SKBitmap? bmp, SKRect r, SKPaint? paint = null)
        {
            if (bmp is null)
            {
                using var g = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(r.Left, r.Top), new SKPoint(r.Right, r.Bottom), new[] { Mint, Aqua, Teal, Deep }, null, SKShaderTileMode.Clamp) };
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

        /// <summary>Panel de "cristal esmerilado": la foto de detrás desenfocada, teñida y con borde fino.</summary>
        private static void Cristal(SKCanvas c, SKBitmap? foto, SKRect fotoRect, SKRect r, float radio, SKColor tinte)
        {
            var rr = new SKRoundRect(r, radio, radio);
            c.Save(); c.ClipRoundRect(rr, SKClipOperation.Intersect, antialias: true);
            using (var blur = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(34, 34) }) Foto(c, foto, fotoRect, blur);
            Rect(c, r, tinte);
            Degradado(c, r, Blanco.WithAlpha(26), SKColors.Transparent, vertical: true);
            c.Restore();
            using var b = new SKPaint { Color = Blanco.WithAlpha(70), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            c.DrawRoundRect(rr, b);
        }

        /// <summary>Rectángulo con la parte de arriba en medio punto (arco).</summary>
        private static SKRoundRect ArcoForma(SKRect r)
        {
            var rr = new SKRoundRect();
            float a = r.Width / 2;
            rr.SetRectRadii(r, new[] { new SKPoint(a, a), new SKPoint(a, a), new SKPoint(28, 28), new SKPoint(28, 28) });
            return rr;
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

        private static void Texto(SKCanvas c, string s, float x, float y, SKTypeface t, float tam, SKColor color, SKTextAlign al = SKTextAlign.Left, bool sombra = false)
        {
            using var font = new SKFont(t, tam);
            using var p = new SKPaint { Color = color, IsAntialias = true };
            if (sombra) p.ImageFilter = SKImageFilter.CreateDropShadow(0, tam * .03f, tam * .08f, tam * .08f, Ink.WithAlpha(110));
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

        /// <summary>Sticker circular de precio "desde", con sombra y algo girado para darle ritmo.</summary>
        private static void Sello(SKCanvas c, FlyerFuentes f, float cx, float cy, float r, int precio, SKColor fondo, SKColor texto, float grados = 0)
        {
            c.Save();
            c.RotateDegrees(grados, cx, cy);
            using (var p = new SKPaint { Color = fondo, IsAntialias = true, ImageFilter = SKImageFilter.CreateDropShadow(0, 10, 18, 18, Ink.WithAlpha(110)) })
                c.DrawCircle(cx, cy, r, p);
            using (var b = new SKPaint { Color = texto.WithAlpha(60), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = r * .02f })
                c.DrawCircle(cx, cy, r * .88f, b);
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

        private static void Boton(SKCanvas c, FlyerFuentes f, string s, float x, float yCentro, SKColor fondo, SKColor texto, bool alinearDerecha = false, float tam = 24, float alto = 60)
        {
            using var font = new SKFont(f.Titulo, tam);
            var w = font.MeasureText(s) + alto * .9f;
            var left = alinearDerecha ? x - w : x;
            var r = new SKRect(left, yCentro - alto / 2, left + w, yCentro + alto / 2);
            Rect(c, r, fondo, alto / 2);
            Texto(c, s, r.MidX, yCentro + tam * .36f, f.Titulo, tam, texto, SKTextAlign.Center);
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
