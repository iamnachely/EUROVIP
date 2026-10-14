using EuroVip.Models;
using SkiaSharp;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace EuroVip.Services
{
    public interface IFlyerImagenService
    {
        Task<List<FlyerImagen>> GenerarAsync(IEnumerable<FlyerDestino> destinos, string? plantilla = null);
        byte[] Muestra(string plantilla, FlyerDestino? destino);
        List<FlyerImagen> Galeria();
        bool Eliminar(string carpeta, string archivo);
        string? Fisico(string carpeta, string archivo);
        byte[] Zip(IEnumerable<string> rutas);
        string? FondoUrl(string nombreDestino);
        Task<string> GuardarFondoAsync(string nombreDestino, IFormFile archivo, string ext);
    }

    /// <summary>
    /// Genera los flyers (PNG) con SkiaSharp usando la plantilla elegida (FlyerPlantillas: Postal, Historia, Minimal, Tropical, Clásica) (MIT; ImageSharp 3.x tiene vulnerabilidades y la 4.x exige licencia de pago),
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

        // ---------------- dibujo: las plantillas están en FlyerPlantillas.cs ----------------
        private SKBitmap? FotoDe(string destino) { var f = FondoFisico(destino); return f is null ? null : SKBitmap.Decode(f); }
        private string Web => _cfg["Eurovip:Flyer:Web"] ?? "www.dominicanatours.com";

        public async Task<List<FlyerImagen>> GenerarAsync(IEnumerable<FlyerDestino> destinos, string? plantilla = null)
        {
            var dia = DateTime.Now.ToString("yyyy-MM-dd");
            var carpeta = System.IO.Path.Combine(Raiz, dia);
            Directory.CreateDirectory(carpeta);
            var p = FlyerPlantillas.Buscar(plantilla);
            using var fuentes = new FlyerFuentes();
            var res = new List<FlyerImagen>();
            foreach (var d in destinos.Where(x => x.Hoteles.Count > 0))
            {
                var nombre = $"flyer_{Seguro(d.Nombre_Destino)}_{p.Clave}_{DateTime.Now:HHmmss}.png";
                var ruta = System.IO.Path.Combine(carpeta, nombre);
                using (var foto = FotoDe(d.Nombre_Destino))
                using (var png = FlyerPlantillas.Dibujar(p.Clave, d, foto, fuentes, Web))
                await using (var fs = File.Create(ruta))
                    png.SaveTo(fs);
                res.Add(new FlyerImagen { Carpeta = dia, Archivo = nombre, Url = $"/{Carpeta}/{dia}/{Uri.EscapeDataString(nombre)}", Bytes = new FileInfo(ruta).Length, Fecha = DateTime.Now });
            }
            return res;
        }

        /// <summary>Vista previa de una plantilla (PNG) con un destino real si tiene precios, o con datos de ejemplo.</summary>
        public byte[] Muestra(string plantilla, FlyerDestino? destino)
        {
            var d = destino is { Hoteles.Count: > 0 } ? destino : FlyerPlantillas.Ejemplo(destino?.Nombre_Destino);
            using var fuentes = new FlyerFuentes();
            using var foto = FotoDe(d.Nombre_Destino) ?? FotoDe("Punta Cana");
            using var png = FlyerPlantillas.Dibujar(plantilla, d, foto, fuentes, Web);
            return png.ToArray();
        }

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
