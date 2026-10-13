using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    public static class Roles
    {
        public const string Super = "Superadministrador";
        public static bool EsSuper(string? rol) => string.Equals(rol, Super, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Claves fijas de [Eurovip].[UsuarioAcceso].clave que el programa consulta directamente.</summary>
    public static class Modulos
    {
        public const string Usuarios = "usuarios";
        public const string FlyersEnviar = "flyers_enviar";
    }

    public class ModuloDto
    {
        public int Id_Modulo { get; set; }
        public string Nombre_Modulo { get; set; } = "";
        public string? Categoria { get; set; }
        public string? Clave { get; set; }
        public string? Controladores { get; set; }
        public string? Icono { get; set; }
        public string? Descripcion { get; set; }
        public int? Orden { get; set; }
        public int Usuarios { get; set; }
    }

    public class ModuloEditarDto
    {
        public int Id_Modulo { get; set; }
        [Required(ErrorMessage = "Escribe el nombre del módulo"), StringLength(200)] public string Nombre_Modulo { get; set; } = "";
        [StringLength(200)] public string? Categoria { get; set; }
        [StringLength(300)] public string? Descripcion { get; set; }
        [StringLength(40)] public string? Icono { get; set; }
    }

    public class UsuarioFila
    {
        public int CodigoUsuario { get; set; }
        public string Nombre { get; set; } = "";
        public string Apellido { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Telefono { get; set; }
        public string? Rol { get; set; }
        public bool Activo { get; set; }
        public string? Imagen { get; set; }
        public string? Foto { get; set; }
        public int Modulos { get; set; }
        public DateTime? UltimaActividad { get; set; }
    }

    public class UsuarioEditarDto
    {
        public int CodigoUsuario { get; set; }
        [Required(ErrorMessage = "Escribe el nombre"), StringLength(50)] public string Nombre { get; set; } = "";
        [Required(ErrorMessage = "Escribe el apellido"), StringLength(50)] public string Apellido { get; set; } = "";
        [Required(ErrorMessage = "Escribe el usuario"), StringLength(15, ErrorMessage = "El usuario admite hasta 15 caracteres")]
        [RegularExpression(@"^[A-Za-z0-9_.\-]+$", ErrorMessage = "El usuario solo puede tener letras, números, punto, guion y guion bajo")]
        public string UserName { get; set; } = "";
        [Required(ErrorMessage = "Escribe el correo"), StringLength(50), EmailAddress(ErrorMessage = "Correo inválido")] public string Email { get; set; } = "";
        [StringLength(15)] public string? Telefono { get; set; }
        [Required(ErrorMessage = "Elige el rol"), StringLength(30)] public string Rol { get; set; } = "Usuario";
        public bool Activo { get; set; } = true;
        /// <summary>Solo al crear. Si viene vacía se genera una segura.</summary>
        public string? Password { get; set; }
        /// <summary>Solo al crear: módulos marcados.</summary>
        public List<int>? Modulos { get; set; }
    }

    public class AccesosDto
    {
        [Required] public string Usuario { get; set; } = "";
        public List<int> Modulos { get; set; } = new();
    }

    public class ClaveDto
    {
        public int CodigoUsuario { get; set; }
        /// <summary>Vacía = generar una segura.</summary>
        public string? Password { get; set; }
    }

    /// <summary>Lo que el filtro de accesos necesita saber del usuario conectado (se guarda un minuto en memoria).</summary>
    public class PermisosUsuario
    {
        public bool Existe { get; set; }
        public bool Activo { get; set; }
        public string? Rol { get; set; }
        public HashSet<string> Claves { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool EsSuper => Roles.EsSuper(Rol);
        public bool Puede(string clave) => EsSuper || Claves.Contains(clave);
    }
}
