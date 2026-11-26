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
        private static readonly SemaphoreSlim Cupo = new(8, 8);   // máximo 8 reducciones a la vez (no saturar el servidor)
        private readonly IHttpClientFactory _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MiniController> _logger;

        public MiniController(IHttpClientFactory http, IMemoryCache cache, ILogger<MiniController> logger)
        { _http = http; _cache = cache; _logger = logger; }

        [HttpGet]
        public async Task<IActionResult> Index(string? u, int w = 480, CancellationToken ct = default)
        {
            if (!Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                || !Dominios.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)) return BadRequest();
            w = Math.Clamp(w, 96, 1200);
            var clave = $"mini:{w}:{uri.AbsoluteUri}";
            try
            {
                if (!_cache.TryGetValue(clave, out byte[]? bytes) || bytes is null)
                {
                    await Cupo.WaitAsync(ct);
                    try
                    {
                        var origen = await _http.CreateClient("fotos").GetByteArrayAsync(uri, ct);
                        bytes = Reducir(origen, w);
                    }
                    finally { Cupo.Release(); }
                    _cache.Set(clave, bytes, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6), Size = null });
                }
                Response.Headers.CacheControl = "private, max-age=86400";
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
