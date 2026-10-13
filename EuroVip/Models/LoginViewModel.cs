using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Ingresa tu correo")]
        [EmailAddress(ErrorMessage = "Correo inválido")]
        public string User { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu contraseña")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool Remember { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Ingresa tu nombre"), StringLength(50)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu apellido"), StringLength(50)]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu correo"), EmailAddress(ErrorMessage = "Correo inválido"), StringLength(50)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa un usuario"), StringLength(15, ErrorMessage = "Máximo 15 caracteres")]
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Solo letras, números, punto, guion y guion bajo")]
        public string UserName { get; set; } = string.Empty;

        [StringLength(15, ErrorMessage = "Máximo 15 caracteres")]
        public string? Telefono { get; set; }

        [Required(ErrorMessage = "Ingresa una contraseña"), DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mínimo 8 caracteres")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirma la contraseña"), DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmarPassword { get; set; } = string.Empty;
    }

    public class ModuloHome
    {
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;
        public string Url { get; set; } = "#";
        public bool Disponible { get; set; }
        public string? Etiqueta { get; set; }
    }

    public class HomeViewModel
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string? Rol { get; set; }
        public int UsuariosActivos { get; set; }
        public int ModulosConAcceso { get; set; }
        public int TablasEurovip { get; set; }
        public List<ModuloHome> ModulosCarga { get; set; } = new();
        public List<ModuloHome> ModulosAdministracion { get; set; } = new();
    }
}
