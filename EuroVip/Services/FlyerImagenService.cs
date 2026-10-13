using EuroVip.Models;
using SkiaSharp;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace EuroVip.Services
{
    public interface IFlyerImagenService
    {
        Task<List<FlyerImagen>> GenerarAsync(IEnumerable<FlyerDestino> destinos);
        List<FlyerImagen> Galeria();
        bool Eliminar(string carpeta, string archivo);
        string? Fisico(string carpeta, string archivo);
        byte[] Zip(IEnumerable<string> rutas);
        string? FondoUrl(string nombreDestino);
        Task<string> GuardarFondoAsync(string nombreDestino, IFormFile archivo, string ext);
    }

    /// <summary>
    /// Dibuja los flyers (PNG 900 px) con SkiaSharp (MIT; ImageSharp 3.x tiene vulnerabilidades y la 4.x exige licencia de pago),
    /// como FlyerService de BackTiTan pero con el estilo Eurovip:
    /// portada con la foto del destino y el precio "desde", top 5 hoteles y pie con la web.
    /// Se guardan en wwwroot/uploads/flyers/{yyyy-MM-dd}/ (carpeta ignorada por git); los fondos en uploads/flyers/fondos/.
    /// </summary>
    public class FlyerImagenService : IFlyerImagenService
    {
        private const string Carpeta = "uploads/flyers";
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _cfg;

        public FlyerImagenService(IWebHostEnvironment env, IConfiguration cfg)
        {
            _env = env;
            _cfg = cfg;
        }

        private string Raiz => System.IO.Path.Combine(_env.WebRootPath, Carpeta.Replace('/', System.IO.Path.DirectorySeparatorChar));
        private string Fondos => System.IO.Path.Combine(Raiz, "fondos");

        public static string Seguro(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in (s ?? "").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ch == ' ' || ch == '-' ? '-' : '\0');
            }
            return sb.ToString().Replace("\0", "").Trim('-');
        }

        private string? FondoFisico(string nombreDestino)
        {
            if (!Directory.Exists(Fondos)) return null;
            var clave = Seguro(nombreDestino).ToLowerInvariant();
            return Directory.EnumerateFiles(Fondos).FirstOrDefault(f => Seguro(System.IO.Path.GetFileNameWithoutExtension(f)).ToLowerInvariant() == clave);
        }

        public string? FondoUrl(string nombreDestino)
        {
            var f = FondoFisico(nombreDestino);
            return f is null ? null : $"/{Carpeta}/fondos/{Uri.EscapeDataString(System.IO.Path.GetFileName(f))}?v={File.GetLastWriteTime(f).Ticks}";
        }

        public async Task<string> GuardarFondoAsync(string nombreDestino, IFormFile archivo, string ext)
        {
            Directory.CreateDirectory(Fondos);
            var viejo = FondoFisico(nombreDestino);
            if (viejo is not null) File.Delete(viejo);
            var ruta = System.IO.Path.Combine(Fondos, $"{nombreDestino.Trim()}.{ext}");
            await using var fs = new FileStream(ruta, FileMode.Create);
            await archivo.CopyToAsync(fs);
            return FondoUrl(nombreDestino)!;
        }

        // ---------------- dibujo con SkiaSharp (MIT) ----------------
        private static readonly SKColor Ink = SKColor.Parse("0B1215"), Aqua = SKColor.Parse("74F2E6"), Mint = SKColor.Parse("B3FFF3"),
                                        Teal = SKColor.Parse("2BD4C4"), Deep = SKColor.Parse("0E8F86"), Muted = SKColor.Parse("5B726F"), Linea = SKColor.Parse("DCEBE8");

        private static SKTypeface Tipo(bool negrita, params string[] familias)
        {
            foreach (var f in familias)
            {
                var t = SKTypeface.FromFamilyName(f, negrita ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                if (t is not null && string.Equals(t.FamilyName, f, StringComparison.OrdinalIgnoreCase)) return t;
            }
            return SKTypeface.FromFamilyName(null, negrita ? SKFontStyle.Bold : SKFontStyle.Normal);
        }

        public async Task<List<FlyerImagen>> GenerarAsync(IEnumerable<FlyerDestino> destinos)
        {
            var dia = DateTime.Now.ToString("yyyy-MM-dd");
            var carpeta = System.IO.Path.Combine(Raiz, dia);
            Directory.CreateDirectory(carpeta);
            var web = _cfg["Eurovip:Flyer:Web"] ?? "www.dominicanatours.com";
            using var titulo = Tipo(true, "Segoe UI Black", "Segoe UI", "Arial Black", "Arial");
            using var negrita = Tipo(true, "Segoe UI", "Arial");
            using var normal = Tipo(false, "Segoe UI", "Arial");
            var res = new List<FlyerImagen>();
            foreach (var d in destinos.Where(x => x.Hoteles.Count > 0))
            {
                var nombre = $"flyer_{Seguro(d.Nombre_Destino)}_{DateTime.Now:HHmmss}.png";
                var ruta = System.IO.Path.Combine(carpeta, nombre);
                using (var png = Dibujar(d, titulo, negrita, normal, web))
                await using (var fs = File.Create(ruta))
                    png.SaveTo(fs);
                res.Add(new FlyerImagen { Carpeta = dia, Archivo = nombre, Url = $"/{Carpeta}/{dia}/{Uri.EscapeDataString(nombre)}", Bytes = new FileInfo(ruta).Length, Fecha = DateTime.Now });
            }
            return res;
        }

        private static void Texto(SKCanvas c, string s, float x, float y, SKTypeface t, float tam, SKColor color, SKTextAlign al = SKTextAlign.Left, float ancho = 0)
        {
            using var font = new SKFont(t, tam);
            using var p = new SKPaint { Color = color, IsAntialias = true };
            if (ancho <= 0 || font.MeasureText(s) <= ancho) { c.DrawText(s, x, y, al, font, p); return; }
            // ajusta en varias líneas
            var linea = "";
            foreach (var palabra in s.Split(' '))
            {
                var prueba = linea.Length == 0 ? palabra : linea + " " + palabra;
                if (font.MeasureText(prueba) > ancho && linea.Length > 0) { c.DrawText(linea, x, y, al, font, p); y += tam * 1.05f; linea = palabra; }
                else linea = prueba;
            }
            c.DrawText(linea, x, y, al, font, p);
        }

        private static void Rect(SKCanvas c, float x, float y, float w, float h, SKColor color, float radio = 0)
        {
            using var p = new SKPaint { Color = color, IsAntialias = true };
            if (radio > 0) c.DrawRoundRect(x, y, w, h, radio, radio, p); else c.DrawRect(x, y, w, h, p);
        }

        private static void Circulo(SKCanvas c, float cx, float cy, float r, SKColor color, float borde = 0, SKColor? colorBorde = null)
        {
            using var p = new SKPaint { Color = color, IsAntialias = true };
            c.DrawCircle(cx, cy, r, p);
            if (borde > 0) { using var b = new SKPaint { Color = colorBorde ?? SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = borde }; c.DrawCircle(cx, cy, r, b); }
        }

        private SKData Dibujar(FlyerDestino d, SKTypeface ft, SKTypeface fb, SKTypeface fx, string web)
        {
            const int W = 900, HERO = 440, PAD = 56, FILA = 96, PIE = 110;
            var n = d.Hoteles.Count;
            var H = HERO + 100 + n * FILA + 60 + PIE;
            var desde = d.Hoteles.Min(h => h.Precio_Final);
            var es = new CultureInfo("es-ES");
            string Euros(int v) => v.ToString("N0", es) + " €";

            using var surf = SKSurface.Create(new SKImageInfo(W, H));
            var c = surf.Canvas;
            c.Clear(SKColors.White);

            // ---- portada: foto del destino (o degradado) + velo para leer el texto ----
            var fondo = FondoFisico(d.Nombre_Destino);
            using (var bmp = fondo is null ? null : SKBitmap.Decode(fondo))
            {
                if (bmp is not null)
                {
                    var esc = Math.Max(W / (float)bmp.Width, HERO / (float)bmp.Height);
                    float bw = bmp.Width * esc, bh = bmp.Height * esc;
                    using var img = SKImage.FromBitmap(bmp);
                    c.Save(); c.ClipRect(new SKRect(0, 0, W, HERO));
                    c.DrawImage(img, new SKRect((W - bw) / 2, (HERO - bh) / 2, (W + bw) / 2, (HERO + bh) / 2), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                    c.Restore();
                }
                else
                {
                    using var g = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(W, HERO), new[] { Mint, Aqua, Teal }, null, SKShaderTileMode.Clamp) };
                    c.DrawRect(0, 0, W, HERO, g);
                }
            }
            using (var velo = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, HERO * .3f), new SKPoint(0, HERO), new[] { SKColors.Transparent, Ink.WithAlpha(230) }, null, SKShaderTileMode.Clamp) })
                c.DrawRect(0, 0, W, HERO, velo);

            Rect(c, PAD, 36, 270, 40, Ink.WithAlpha(215), 20);
            Texto(c, $"OFERTAS DESDE {d.Origen.ToUpperInvariant()}", PAD + 20, 62, fb, 15, Aqua);
            Texto(c, d.Nombre_Destino, PAD, HERO - 110, ft, 74, SKColors.White, ancho: W - 2 * PAD - 230);
            Texto(c, $"Precios por persona · actualizados el {DateTime.Now.ToString("d 'de' MMMM", es)}", PAD, HERO - 46, fx, 19, SKColors.White.WithAlpha(220));

            // sello "desde"
            float sx = W - PAD - 100, sy = HERO - 150;
            Circulo(c, sx, sy, 100, Aqua, 7, SKColors.White);
            Texto(c, "DESDE", sx, sy - 34, fb, 17, Ink, SKTextAlign.Center);
            Texto(c, Euros(desde), sx, sy + 20, ft, desde >= 10000 ? 38 : 46, Ink, SKTextAlign.Center);
            Texto(c, "por persona", sx, sy + 50, fx, 15, Ink, SKTextAlign.Center);

            // ---- top hoteles ----
            float y = HERO + 56;
            Texto(c, "LOS HOTELES MÁS BUSCADOS", PAD, y, fb, 16, Deep);
            y += 30;
            for (var i = 0; i < n; i++)
            {
                var h = d.Hoteles[i];
                if (i == 0) Rect(c, PAD - 18, y, W - 2 * PAD + 36, FILA - 10, Mint.WithAlpha(120), 18);
                Circulo(c, PAD + 24, y + 44, 24, i == 0 ? Ink : Linea);
                Texto(c, (i + 1).ToString(), PAD + 24, y + 54, ft, 24, i == 0 ? Aqua : Ink, SKTextAlign.Center);
                Texto(c, Corta(h.Nombre_Hotel, 38), PAD + 68, y + 40, fb, 22, Ink);
                Texto(c, Corta($"{h.Nombre_Habitacion} · {h.Regimen}", 60), PAD + 68, y + 68, fx, 16, Muted);
                if (i == 0) { Rect(c, W - PAD - 158, y + 10, 118, 24, Teal, 12); Texto(c, "MEJOR PRECIO", W - PAD - 99, y + 27, fb, 12, Ink, SKTextAlign.Center); }
                Texto(c, Euros(h.Precio_Final), W - PAD, i == 0 ? y + 70 : y + 54, ft, 30, Ink, SKTextAlign.Right);
                if (i < n - 1) Rect(c, PAD, y + FILA - 4, W - 2 * PAD, 1, Linea);
                y += FILA;
            }
            Texto(c, "Precios por persona, sujetos a disponibilidad. Incluyen tasas y suplementos indicados.", PAD, y + 26, fx, 13, Muted, ancho: W - 2 * PAD);

            // ---- pie ----
            using (var g = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, H - PIE), new SKPoint(W, H), new[] { Ink, SKColor.Parse("12302F") }, null, SKShaderTileMode.Clamp) })
                c.DrawRect(0, H - PIE, W, PIE, g);
            Rect(c, 0, H - PIE, W, 6, Aqua);
            Texto(c, web, PAD, H - PIE + 54, ft, 28, SKColors.White);
            Texto(c, "Reserva ya · República Dominicana a tu manera", PAD, H - PIE + 86, fx, 15, Aqua);

            using var imagen = surf.Snapshot();
            return imagen.Encode(SKEncodedImageFormat.Png, 100);
        }

        private static string Corta(string? s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..(n - 1)] + "…";

        public List<FlyerImagen> Galeria()
        {
            if (!Directory.Exists(Raiz)) return new();
            return Directory.EnumerateDirectories(Raiz).Where(d => System.IO.Path.GetFileName(d) != "fondos")
                .SelectMany(d => Directory.EnumerateFiles(d, "*.png").Select(f => new FileInfo(f)))
                .OrderByDescending(f => f.LastWriteTime).Take(300)
                .Select(f => new FlyerImagen
                {
                    Carpeta = f.Directory!.Name, Archivo = f.Name, Bytes = f.Length, Fecha = f.LastWriteTime,
                    Url = $"/{Carpeta}/{f.Directory.Name}/{Uri.EscapeDataString(f.Name)}"
                }).ToList();
        }

        /// <summary>Ruta física validada (evita salir de la carpeta de flyers).</summary>
        public string? Fisico(string carpeta, string archivo)
        {
            if (string.IsNullOrWhiteSpace(carpeta) || string.IsNullOrWhiteSpace(archivo)) return null;
            if (carpeta.Contains("..") || archivo.Contains("..") || carpeta.IndexOfAny(new[] { '/', '\\' }) >= 0 || archivo.IndexOfAny(new[] { '/', '\\' }) >= 0) return null;
            var ruta = System.IO.Path.GetFullPath(System.IO.Path.Combine(Raiz, carpeta, archivo));
            return ruta.StartsWith(System.IO.Path.GetFullPath(Raiz)) && File.Exists(ruta) ? ruta : null;
        }

        public bool Eliminar(string carpeta, string archivo)
        {
            var f = Fisico(carpeta, archivo);
            if (f is null) return false;
            File.Delete(f);
            return true;
        }

        public byte[] Zip(IEnumerable<string> rutas)
        {
            using var ms = new MemoryStream();
            using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
                foreach (var r in rutas) z.CreateEntryFromFile(r, System.IO.Path.GetFileName(r), CompressionLevel.Fastest);
            return ms.ToArray();
        }
    }
}
