using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    public class PerfilDto
    {
        public int CodigoUsuario { get; set; }
        public string Nombre { get; set; } = "";
        public string Apellido { get; set; } = "";
        public string? Telefono { get; set; }
        public string? Imagen { get; set; }
        public string? Rol { get; set; }
        public string Email { get; set; } = "";
        public string UserName { get; set; } = "";
        public string? CorreoAD { get; set; }
        public bool Activo { get; set; }
        public int Cargas { get; set; }
        public int Registros { get; set; }
        public int Cambios { get; set; }
        public DateTime? UltimaCarga { get; set; }
        /// <summary>URL usable de la foto (solo las subidas desde Eurovip o URLs completas).</summary>
        public string? Foto => PerfilFoto.Url(Imagen);
    }

    /// <summary>Lo único que el usuario puede cambiar de sí mismo (no correo, usuario ni contraseña).</summary>
    public class PerfilEditarDto
    {
        [Required(ErrorMessage = "Escribe tu nombre")] [StringLength(50, ErrorMessage = "El nombre admite 50 letras")]
        public string Nombre { get; set; } = "";
        [Required(ErrorMessage = "Escribe tu apellido")] [StringLength(50, ErrorMessage = "El apellido admite 50 letras")]
        public string Apellido { get; set; } = "";
        [StringLength(15, ErrorMessage = "El teléfono admite 15 caracteres")] [RegularExpression(@"^[0-9+\-\s()]*$", ErrorMessage = "El teléfono solo puede tener números, espacios, +, - y paréntesis")]
        public string? Telefono { get; set; }
    }

    public static class PerfilFoto
    {
        public const string Carpeta = "uploads/usuarios";
        public static string? Url(string? imagen) =>
            string.IsNullOrWhiteSpace(imagen) ? null
            : imagen.StartsWith("/" + Carpeta + "/") || imagen.StartsWith("http://") || imagen.StartsWith("https://") ? imagen
            : null;   // fotos antiguas de ViajeVDT (solo nombre de archivo): no hay ruta pública
    }

    public class NotificacionDto
    {
        public string Id { get; set; } = "";
        /// <summary>carga · proceso · error · cambio · alerta</summary>
        public string Tipo { get; set; } = "";
        public string Icono { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Texto { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Url { get; set; } = "/";
        public bool Leida { get; set; }
    }

    public class NotificacionesDto
    {
        public List<NotificacionDto> Items { get; set; } = new();
        public int NoLeidas { get; set; }
        public DateTime? LeidasHasta { get; set; }
    }
}
