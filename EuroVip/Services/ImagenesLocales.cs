using System.Globalization;
using System.Text;
using SkiaSharp;

namespace EuroVip.Services
{
    /// <summary>Una imagen guardada en las carpetas del proyecto (wwwroot/imagenes/{tipo}).</summary>
    public class ImagenLocal
    {
        public string Archivo { get; set; } = "";
        public string Url { get; set; } = "";
        public long Bytes { get; set; }
        public DateTime Fecha { get; set; }
        public int Ancho { get; set; }
        public int Alto { get; set; }
    }

    public interface IImagenesLocales
    {
        /// <summary>Nombre del archivo a partir del nombre (hotel, circuito…): igual que BackTiTan, minúsculas, sin acentos ni espacios + ".webp".</summary>
        string NombreArchivo(string nombre);
        /// <summary>Nombre para circuitos, igual que BackTiTan: sin espacios y respetando mayúsculas ("Triángulo Colonial" → "TrianguloColonial"),
        /// sin acentos ni signos para que el archivo sea seguro.</summary>
        string NombreCircuito(string nombre);
        ImagenLocal? Buscar(string tipo, string nombre);
        /// <summary>Busca por nombre de archivo exacto (p. ej. la portada guardada en circuito.caratula).</summary>
        ImagenLocal? BuscarArchivo(string tipo, string archivo, string? sub = null);
        /// <summary>Guarda con un nombre de archivo exacto, opcionalmente en una subcarpeta permitida (p. ej. "itinerario/es").</summary>
        Task<ImagenLocal> GuardarComoAsync(string tipo, string? sub, string archivo, Stream origen);
        List<ImagenLocal> Listar(string tipo);
        Task<ImagenLocal> GuardarAsync(string tipo, string nombre, Stream origen);
        bool Eliminar(string tipo, string archivo);
        /// <summary>Si el nombre cambió (p. ej. se renombró el hotel), pasa la imagen al nombre nuevo. Con copiar = true deja también la vieja
        /// (otro registro la sigue usando). Devuelve true si hizo algo.</summary>
        bool Renombrar(string tipo, string nombreViejo, string nombreNuevo, bool copiar = false);
        /// <summary>Igual que Renombrar pero con los nombres de archivo exactos (p. ej. excursiones: "CatamaranPartyBoat.webp").</summary>
        bool RenombrarArchivo(string tipo, string archivoViejo, string archivoNuevo);
    }

    /// <summary>
    /// Imágenes en carpetas internas del proyecto: wwwroot/imagenes/hoteles, wwwroot/imagenes/circuitos…
    /// No se guarda nada en la base: la imagen se relaciona por NOMBRE (como BackTiTan con /imagenes/destinos),
    /// así "Hotel Kristal" → /imagenes/hoteles/hotelkristal.webp.
    /// Al subir, la imagen se convierte a WebP (máx. 1600 px de lado, calidad 82) para que pese poco.
    /// </summary>
    public class ImagenesLocales : IImagenesLocales
    {
        public static readonly string[] Tipos = { "hoteles", "circuitos", "destinos", "alojamientos", "excursiones" };
        /// <summary>Subcarpetas permitidas (como BackTiTan: circuitos/itinerario/es).</summary>
        public static readonly string[] Subcarpetas = { "itinerario/es" };
        private const int LadoMaximo = 1600;
        private const int Calidad = 82;
        private readonly IWebHostEnvironment _env;

        public ImagenesLocales(IWebHostEnvironment env) => _env = env;

        private string Carpeta(string tipo, string? sub = null)
        {
            if (!Tipos.Contains(tipo)) throw new ArgumentException("Tipo de imagen no válido.", nameof(tipo));
            if (sub is not null && !Subcarpetas.Contains(sub)) throw new ArgumentException("Subcarpeta no válida.", nameof(sub));
            var c = Path.Combine(_env.WebRootPath, "imagenes", tipo);
            if (sub is not null) c = Path.Combine(c, sub.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(c);
            return c;
        }

        public string NombreArchivo(string nombre)
        {
            var sb = new StringBuilder();
            foreach (var ch in (nombre ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant())
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && (ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9'))
                    sb.Append(ch);
            return sb.Length == 0 ? "" : sb + ".webp";
        }

        public string NombreCircuito(string nombre)
        {
            var sb = new StringBuilder();
            foreach (var ch in (nombre ?? "").Normalize(NormalizationForm.FormD))
                if (ch is >= 'a' and <= 'z' || ch is >= 'A' and <= 'Z' || ch is >= '0' and <= '9')
                    sb.Append(ch);
            return sb.ToString();
        }

        /// <summary>Ruta física segura: solo nombres simples dentro de la carpeta del tipo.</summary>
        private string? Ruta(string tipo, string archivo, string? sub = null)
        {
            if (string.IsNullOrWhiteSpace(archivo) || archivo != Path.GetFileName(archivo) || !archivo.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) return null;
            return Path.Combine(Carpeta(tipo, sub), archivo);
        }

        private static ImagenLocal Info(string tipo, string ruta, string? sub = null)
        {
            var fi = new FileInfo(ruta);
            int w = 0, h = 0;
            try { using var c = SKCodec.Create(ruta); if (c is not null) { w = c.Info.Width; h = c.Info.Height; } } catch { }
            // ?v= cambia cuando se reemplaza la imagen, así el navegador no muestra la vieja
            return new ImagenLocal { Archivo = fi.Name, Url = $"/imagenes/{tipo}/{(sub is null ? "" : sub + "/")}{fi.Name}?v={fi.LastWriteTimeUtc.Ticks:x}", Bytes = fi.Length, Fecha = fi.LastWriteTime, Ancho = w, Alto = h };
        }

        public ImagenLocal? BuscarArchivo(string tipo, string archivo, string? sub = null)
        {
            var ruta = Ruta(tipo, archivo ?? "", sub);
            return ruta is not null && File.Exists(ruta) ? Info(tipo, ruta, sub) : null;
        }

        public ImagenLocal? Buscar(string tipo, string nombre)
        {
            var ruta = Ruta(tipo, NombreArchivo(nombre));
            return ruta is not null && File.Exists(ruta) ? Info(tipo, ruta) : null;
        }

        public List<ImagenLocal> Listar(string tipo) =>
            Directory.GetFiles(Carpeta(tipo), "*.webp").Select(r => Info(tipo, r)).OrderBy(i => i.Archivo).ToList();

        public Task<ImagenLocal> GuardarAsync(string tipo, string nombre, Stream origen) => GuardarComoAsync(tipo, null, NombreArchivo(nombre), origen);

        public async Task<ImagenLocal> GuardarComoAsync(string tipo, string? sub, string archivo, Stream origen)
        {
            var ruta = Ruta(tipo, archivo, sub) ?? throw new InvalidOperationException("El nombre no sirve para el archivo: escribe letras o números.");
            using var ms = new MemoryStream();
            await origen.CopyToAsync(ms);
            ms.Position = 0;
            using var codec = SKCodec.Create(ms) ?? throw new InvalidOperationException("No se pudo leer la imagen. Usa JPG, PNG o WebP.");
            using var leida = SKBitmap.Decode(codec) ?? throw new InvalidOperationException("No se pudo leer la imagen. Usa JPG, PNG o WebP.");
            using var bmp = Enderezar(leida, codec.EncodedOrigin);   // fotos de móvil giradas

            var escala = Math.Min(1f, LadoMaximo / (float)Math.Max(bmp.Width, bmp.Height));
            using var final = escala < 1f
                ? bmp.Resize(new SKImageInfo((int)(bmp.Width * escala), (int)(bmp.Height * escala)), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                : bmp.Copy();
            using var img = SKImage.FromBitmap(final);
            using var datos = img.Encode(SKEncodedImageFormat.Webp, Calidad);

            // Se escribe en un temporal y se reemplaza: si falla a mitad, la imagen anterior sigue intacta.
            var tmp = ruta + ".tmp";
            await using (var fs = File.Create(tmp)) datos.SaveTo(fs);
            File.Move(tmp, ruta, overwrite: true);
            return Info(tipo, ruta, sub);
        }

        private static SKBitmap Enderezar(SKBitmap b, SKEncodedOrigin origen)
        {
            var grados = origen switch { SKEncodedOrigin.BottomRight => 180, SKEncodedOrigin.RightTop => 90, SKEncodedOrigin.LeftBottom => 270, _ => 0 };
            if (grados == 0) return b.Copy();
            var giro = grados != 180;
            var r = new SKBitmap(giro ? b.Height : b.Width, giro ? b.Width : b.Height);
            using var c = new SKCanvas(r);
            c.Translate(r.Width / 2f, r.Height / 2f);
            c.RotateDegrees(grados);
            c.Translate(-b.Width / 2f, -b.Height / 2f);
            c.DrawBitmap(b, 0, 0);
            return r;
        }

        public bool Eliminar(string tipo, string archivo)
        {
            var ruta = Ruta(tipo, archivo);
            if (ruta is null || !File.Exists(ruta)) return false;
            File.Delete(ruta);
            return true;
        }

        public bool Renombrar(string tipo, string nombreViejo, string nombreNuevo, bool copiar = false) =>
            RenombrarRutas(Ruta(tipo, NombreArchivo(nombreViejo)), Ruta(tipo, NombreArchivo(nombreNuevo)), copiar);

        public bool RenombrarArchivo(string tipo, string archivoViejo, string archivoNuevo) =>
            RenombrarRutas(Ruta(tipo, archivoViejo), Ruta(tipo, archivoNuevo), false);

        private static bool RenombrarRutas(string? viejo, string? nuevo, bool copiar)
        {
            if (viejo is null || nuevo is null || viejo == nuevo || !File.Exists(viejo) || File.Exists(nuevo)) return false;
            if (copiar) File.Copy(viejo, nuevo); else File.Move(viejo, nuevo);
            return true;
        }
    }
}
