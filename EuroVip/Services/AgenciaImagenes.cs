using EuroVip.Models;

namespace EuroVip.Services
{
    /// <summary>
    /// Archivos de Flyer Agencia en wwwroot/uploads/flyers-agencia (ignorada por git y no se publica: vive en el servidor):
    ///   • agencia_{destino}_{yyyyMMdd_HHmmss}.png → flyers listos para enviar (guardados desde el editor o subidos);
    ///   • logos/{agencia}.{ext} → el logo que cada agencia sube en su editor público.
    /// Va aparte de uploads/flyers (Flyer Editar) para que cada galería tenga solo lo suyo.
    /// </summary>
    public class AgenciaImagenes
    {
        public const string Carpeta = "uploads/flyers-agencia";
        public const int MaxBytes = 15 * 1024 * 1024;
        private readonly IWebHostEnvironment _env;
        public AgenciaImagenes(IWebHostEnvironment env) => _env = env;

        private string Raiz => Path.Combine(_env.WebRootPath, Carpeta.Replace('/', Path.DirectorySeparatorChar));
        private string Logos => Path.Combine(Raiz, "logos");

        /// <summary>Destino → parte del nombre del archivo ("Punta Cana" → "Punta_Cana").</summary>
        public static string Clave(string texto) => FlyerImagenService.Seguro(texto).Replace(' ', '_');

        /// <summary>De "agencia_Punta_Cana_20261026_101500.png" saca "Punta_Cana" (para saber de qué destino es).</summary>
        public static string? ClaveDe(string archivo)
        {
            var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileNameWithoutExtension(archivo), @"^agencia_(.+?)(_\d{8}_\d{6}(_\d+)?)?$");
            return m.Success ? m.Groups[1].Value : null;
        }

        public List<FlyerImagen> Listar() =>
            !Directory.Exists(Raiz) ? new()
            : Directory.EnumerateFiles(Raiz, "*.*").Where(EsImagen).Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => new FlyerImagen { Carpeta = "agencia", Archivo = f.Name, Url = $"/{Carpeta}/{Uri.EscapeDataString(f.Name)}?v={f.LastWriteTime.Ticks}", Bytes = f.Length, Fecha = f.LastWriteTime })
                .ToList();

        private static bool EsImagen(string f) => Path.GetExtension(f).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp";

        /// <summary>Ruta física de un archivo de la galería (null si el nombre no es seguro o no existe).</summary>
        public string? Fisico(string archivo)
        {
            if (string.IsNullOrWhiteSpace(archivo) || archivo != Path.GetFileName(archivo) || !EsImagen(archivo)) return null;
            var f = Path.Combine(Raiz, archivo);
            return File.Exists(f) ? f : null;
        }

        /// <summary>PNG que llega del navegador (data:image/png;base64,…) → agencia_{destino}_{fecha}.png.</summary>
        public async Task<FlyerImagen> GuardarPngAsync(string dataUrl, string destino)
        {
            var coma = dataUrl.IndexOf(',');
            var b64 = coma >= 0 ? dataUrl[(coma + 1)..] : dataUrl;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(b64); } catch { throw new InvalidOperationException("La imagen llegó dañada."); }
            if (bytes.Length > MaxBytes) throw new InvalidOperationException("La imagen pesa demasiado.");
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47) throw new InvalidOperationException("La imagen tiene que ser PNG.");
            Directory.CreateDirectory(Raiz);
            var nombre = $"agencia_{Clave(destino)}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            for (var i = 2; File.Exists(Path.Combine(Raiz, nombre)); i++) nombre = $"agencia_{Clave(destino)}_{DateTime.Now:yyyyMMdd_HHmmss}_{i}.png";
            await File.WriteAllBytesAsync(Path.Combine(Raiz, nombre), bytes);
            return Listar().First(x => x.Archivo == nombre);
        }

        /// <summary>Imagen subida a mano (JPG/PNG/WebP, revisada por su contenido). Nombre: agencia_{destino o nombre}_{fecha}.{ext}.</summary>
        public async Task<string> SubirAsync(IFormFile archivo, string? destino)
        {
            if (archivo.Length > MaxBytes) throw new InvalidOperationException($"{archivo.FileName}: pesa más de 15 MB.");
            var ext = await EuroVip.Helpers.ImagenSegura.ExtensionAsync(archivo) ?? throw new InvalidOperationException($"{archivo.FileName}: no es una imagen JPG, PNG o WebP.");
            Directory.CreateDirectory(Raiz);
            var baseNom = Clave(string.IsNullOrWhiteSpace(destino) ? Path.GetFileNameWithoutExtension(archivo.FileName) : destino);
            var nombre = $"agencia_{baseNom}_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";
            for (var i = 2; File.Exists(Path.Combine(Raiz, nombre)); i++) nombre = $"agencia_{baseNom}_{DateTime.Now:yyyyMMdd_HHmmss}_{i}.{ext}";
            await using var fs = File.Create(Path.Combine(Raiz, nombre));
            await archivo.CopyToAsync(fs);
            return nombre;
        }

        public bool Eliminar(string archivo)
        {
            var f = Fisico(archivo);
            if (f is null) return false;
            File.Delete(f);
            return true;
        }

        /// <summary>Antes de «Guardar imágenes» de todos los destinos: se borran los flyers anteriores (como BackTiTan).</summary>
        public int LimpiarFlyers()
        {
            if (!Directory.Exists(Raiz)) return 0;
            var n = 0;
            foreach (var f in Directory.EnumerateFiles(Raiz, "agencia_*.png")) { File.Delete(f); n++; }
            return n;
        }

        // ---------------- logo de cada agencia (editor público) ----------------
        public static string ClaveAgencia(string? nombre)
        {
            var s = System.Text.RegularExpressions.Regex.Replace(FlyerImagenService.Seguro(nombre ?? "").ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
            return s == "" ? "sin_nombre" : s.Length > 60 ? s[..60] : s;
        }

        public string? LogoUrl(string clave)
        {
            if (!Directory.Exists(Logos)) return null;
            var f = Directory.EnumerateFiles(Logos, clave + ".*").FirstOrDefault();
            return f is null ? null : $"/{Carpeta}/logos/{Path.GetFileName(f)}?v={File.GetLastWriteTime(f).Ticks}";
        }

        public async Task<string> GuardarLogoAsync(string clave, IFormFile archivo)
        {
            if (archivo.Length > 5 * 1024 * 1024) throw new InvalidOperationException("El logo pesa más de 5 MB.");
            var ext = await EuroVip.Helpers.ImagenSegura.ExtensionAsync(archivo) ?? throw new InvalidOperationException("El logo tiene que ser JPG, PNG o WebP.");
            Directory.CreateDirectory(Logos);
            foreach (var viejo in Directory.EnumerateFiles(Logos, clave + ".*")) File.Delete(viejo);
            await using (var fs = File.Create(Path.Combine(Logos, $"{clave}.{ext}"))) await archivo.CopyToAsync(fs);
            return LogoUrl(clave)!;
        }
    }
}
