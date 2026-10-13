namespace EuroVip.Helpers
{
    /// <summary>Reconoce la imagen por su contenido real (no por la extensión que manda el navegador).</summary>
    public static class ImagenSegura
    {
        public static async Task<string?> ExtensionAsync(IFormFile archivo)
        {
            var cab = new byte[12];
            await using var s = archivo.OpenReadStream();
            var n = await s.ReadAsync(cab);
            if (n < 12) return null;
            return cab switch
            {
                [0xFF, 0xD8, 0xFF, ..] => "jpg",
                [0x89, 0x50, 0x4E, 0x47, ..] => "png",
                [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50] => "webp",
                _ => null
            };
        }
    }
}
