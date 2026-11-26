using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;

namespace EuroVip.Controllers
{
    /// <summary>
    /// 26/11/2026: miniaturas livianas para las pantallas del backoffice (HomeWeb: tarjetas y lista). La foto original pesa 200-300 KB y
    /// se cargaba entera en cada tarjeta; aquí se baja una vez, se reduce a un ancho pequeño en WebP (~15-40 KB) y se guarda en memoria.
    /// Solo acepta fotos de nuestros dominios de imágenes (nada de URLs arbitrarias). Si algo falla, redirige a la foto original.
    /// </summary>
    public class MiniController : Controller
    {
        private static readonly string[] Dominios = { "fotos.eurovip.com.do", "fotos.grupovdt.com", "fotos.tropitours.com",
            // fotos de proveedores que salen en las tarjetas (circuitos del mundo, hoteles): pesan 1-2 MB cada una
            "res.cloudinary.com", "www.yourjumbo.com", "bokun.s3.amazonaws.com", "d2poxrheyfxwbo.cloudfront.net" };
        // 26/11/2026: además de la memoria, cada miniatura se guarda en disco (carpeta temporal del servidor): se genera UNA vez y sobrevive a
        // reinicios del sitio y al reciclaje del grupo de aplicaciones, que vaciaban la memoria y obligaban a regenerar las ~100 fotos.
        private static readonly string Carpeta = Path.Combine(Path.GetTempPath(), "eurovip-mini");
        private static readonly SemaphoreSlim Cupo = new(8, 8);   // máximo 8 reducciones a la vez (no saturar el servidor)
        private readonly IHttpClientFactory _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MiniController> _logger;

        public MiniController(IHttpClientFactory http, IMemoryCache cache, ILogger<MiniController> logger)
        { _http = http; _cache = cache; _logger = logger; }

        // Ruta explícita: la ruta por defecto del sitio es Auth/Login, así que «/Mini» a secas daba 404 (solo «/Mini/Index» funcionaba)
        [HttpGet("/Mini")]
        public async Task<IActionResult> Index(string? u, int w = 480, CancellationToken ct = default)
        {
            if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                || !Dominios.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)) return BadRequest();
            w = Math.Clamp(w, 96, 1200);
            var clave = $"mini:{w}:{uri.AbsoluteUri}";
            var archivo = Path.Combine(Carpeta, Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(clave))) + ".webp");
            try
            {
                if (!_cache.TryGetValue(clave, out byte[]? bytes) || bytes is null)
                {
                    if (System.IO.File.Exists(archivo))
                    {
                        try { bytes = await System.IO.File.ReadAllBytesAsync(archivo, ct); } catch (IOException) { bytes = null; }
                    }
                    if (bytes is null)
                    {
                        await Cupo.WaitAsync(ct);
                        try
                        {
                            var origen = await _http.CreateClient("fotos").GetByteArrayAsync(uri, ct);
                            bytes = Reducir(origen, w);
                        }
                        finally { Cupo.Release(); }
                        try   // guardar en disco sin que un fallo de escritura afecte a la respuesta
                        {
                            Directory.CreateDirectory(Carpeta);
                            var tmp = archivo + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            await System.IO.File.WriteAllBytesAsync(tmp, bytes, ct);
                            System.IO.File.Move(tmp, archivo, overwrite: true);
                        }
                        catch (Exception ex) { _logger.LogDebug(ex, "No se pudo guardar la miniatura en disco"); }
                    }
                    _cache.Set(clave, bytes, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12), Size = null });
                }
                Response.Headers.CacheControl = "private, max-age=604800";   // 7 días en el navegador
                return File(bytes, "image/webp");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Miniatura no generada para {Url}; se usa la original", uri);
                return Redirect(uri.AbsoluteUri);
            }
        }

        private static byte[] Reducir(byte[] origen, int ancho)
        {
            using var bmp = SKBitmap.Decode(origen);
            if (bmp is null || bmp.Width <= ancho) return origen;            // ya es pequeña (o no se pudo leer): tal cual
            var alto = Math.Max(1, (int)Math.Round(bmp.Height * (ancho / (double)bmp.Width)));
            using var chica = bmp.Resize(new SKImageInfo(ancho, alto), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            if (chica is null) return origen;
            using var img = SKImage.FromBitmap(chica);
            using var datos = img.Encode(SKEncodedImageFormat.Webp, 72);
            var r = datos.ToArray();
            return r.Length < origen.Length ? r : origen;
        }
    }
}
