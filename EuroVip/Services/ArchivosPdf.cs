namespace EuroVip.Services
{
    /// <summary>
    /// Archivos PDF de los productos (FASE 30: PDF de cada crucero). Van al lado de las imágenes, en la carpeta «PDF»:
    ///   {Multimedia:CarpetaFisica}\..\PDF\cruceros\{crucero}\{nombre}.pdf  →  {Multimedia:Url}/../PDF/cruceros/{crucero}/{nombre}.pdf
    /// (C:\inetpub\wwwroot\photosVacacionalparce\eurovip\PDF\… → https://fotos.eurovip.com.do/eurovip/PDF/…).
    /// Se pueden fijar aparte con Multimedia:CarpetaPdf y Multimedia:UrlPdf. En la tabla solo se guarda la ruta relativa.
    /// </summary>
    public static class ArchivosPdf
    {
        public const long Maximo = 25_000_000;
        /// <summary>Ruta local donde Program.cs publica la carpeta (para ver los PDF al trabajar en local).</summary>
        public const string RutaLocal = "/pdf-locales";

        public static string CarpetaDe(IConfiguration cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg["Multimedia:CarpetaPdf"])) return cfg["Multimedia:CarpetaPdf"]!.Trim();
            var imagenes = Multimedia.CarpetaDe(cfg).TrimEnd('\\', '/');
            return Path.Combine(Path.GetDirectoryName(imagenes) ?? imagenes, "PDF");
        }

        public static string UrlDe(IConfiguration cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg["Multimedia:UrlPdf"])) return cfg["Multimedia:UrlPdf"]!.Trim().TrimEnd('/');
            var imagenes = Multimedia.UrlDe(cfg);
            if (imagenes.StartsWith('/')) return RutaLocal;          // fotos servidas por el propio back
            var i = imagenes.LastIndexOf('/');
            return (i > "https://".Length ? imagenes[..i] : imagenes) + "/PDF";
        }

        /// <summary>¿Empieza como un PDF de verdad («%PDF-»)? No se fía de la extensión ni del tipo que manda el navegador.</summary>
        public static async Task<bool> EsPdfAsync(IFormFile archivo)
        {
            var cab = new byte[5];
            await using var st = archivo.OpenReadStream();
            var n = await st.ReadAsync(cab);
            return n == 5 && cab[0] == '%' && cab[1] == 'P' && cab[2] == 'D' && cab[3] == 'F' && cab[4] == '-';
        }

        /// <summary>Guarda el PDF en {carpeta}/{nombre}.pdf sin pisar otro (nombre-2, nombre-3…). Devuelve la ruta relativa.</summary>
        public static async Task<string> GuardarAsync(IConfiguration cfg, string carpeta, string nombre, IFormFile archivo)
        {
            var baseFisica = CarpetaDe(cfg);
            var dir = Path.Combine(baseFisica, carpeta.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(dir);
            var final = nombre;
            for (var i = 2; File.Exists(Path.Combine(dir, final + ".pdf")); i++) final = $"{nombre}-{i}";
            var fisica = Path.Combine(dir, final + ".pdf");
            var temporal = fisica + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (var destino = File.Create(temporal))
                await archivo.CopyToAsync(destino);
            File.Move(temporal, fisica);
            return $"{carpeta}/{final}.pdf";
        }

        /// <summary>Borra el archivo (si la ruta es de la carpeta PDF). No falla si ya no estaba.</summary>
        public static void Borrar(IConfiguration cfg, string? ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta) || ruta.Contains("..")) return;
            try
            {
                var f = Path.Combine(CarpetaDe(cfg), ruta.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(f)) File.Delete(f);
                var dir = Path.GetDirectoryName(f);
                if (dir is not null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            catch (IOException) { /* lo borra la próxima limpieza */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
