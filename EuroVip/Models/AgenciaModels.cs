using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    /// <summary>Una agencia a la que se envían los flyers (Eurovip.flyer_agencia).</summary>
    public class FlyerAgencia
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Escribe el nombre de la agencia"), StringLength(200)] public string Nombre { get; set; } = "";
        [Required(ErrorMessage = "Escribe el WhatsApp de la agencia"), StringLength(30)] public string Telefono { get; set; } = "";
        [StringLength(200), EmailAddress(ErrorMessage = "Correo inválido")] public string? Email { get; set; }
        public bool Activo { get; set; } = true;
        public string? Origen { get; set; }
        public DateTime Fecha_Creacion { get; set; }
    }

    /// <summary>Link único del editor de la agencia (Eurovip.flyer_agencia_link): /agencia/editor/{token}.</summary>
    public class FlyerAgenciaLink
    {
        public int Id { get; set; }
        public string Token { get; set; } = "";
        public string Id_Destino { get; set; } = "";
        public string? Nombre_Destino { get; set; }
        public int? Id_Agencia { get; set; }
        public string? Nombre_Agencia { get; set; }
        public string? Email_Agencia { get; set; }
        public string? Nombre_Imagen { get; set; }
        public int? Id_Envio { get; set; }
        public DateTime Fecha_Creacion { get; set; }
        public DateTime? Fecha_Expiracion { get; set; }
        public bool Activo { get; set; }
        public int Visitas { get; set; }
        public DateTime? Ultima_Visita { get; set; }
        public string? Creado_Por { get; set; }
        public bool Vigente => Activo && (Fecha_Expiracion is null || Fecha_Expiracion > DateTime.Now);
        /// <summary>Dirección completa (la arma el controlador con el dominio de la pantalla).</summary>
        public string? Url { get; set; }
    }

    public class AgenciaLinkDto
    {
        [Required(ErrorMessage = "Elige el destino")] public string Id_Destino { get; set; } = "";
        public int? Id_Agencia { get; set; }
        [StringLength(200)] public string? Nombre_Agencia { get; set; }
        [StringLength(200), EmailAddress(ErrorMessage = "Correo inválido")] public string? Email_Agencia { get; set; }
        /// <summary>Días que dura el link; vacío = no vence.</summary>
        [Range(1, 3650, ErrorMessage = "De 1 a 3650 días")] public int? Dias { get; set; }
    }

    /// <summary>Un envío masivo por WhatsApp (Eurovip.flyer_envio_masivo).</summary>
    public class FlyerEnvioMasivo
    {
        public int Id { get; set; }
        public string Modulo { get; set; } = "";
        public string? Subtipo { get; set; }
        public string Estado { get; set; } = "";
        public string? Plantilla { get; set; }
        public int Total_Mensajes { get; set; }
        public int Total_Contactos { get; set; }
        public int Total_Imagenes { get; set; }
        public int Enviados_Ok { get; set; }
        public int Enviados_Error { get; set; }
        public string? Contacto_Actual { get; set; }
        public string? Imagen_Actual { get; set; }
        public decimal? Costo_Unitario { get; set; }
        public string? Usuario { get; set; }
        public DateTime Fecha_Creacion { get; set; }
        public DateTime? Fecha_Inicio { get; set; }
        public DateTime? Fecha_Fin { get; set; }
        public string? Mensaje_Error { get; set; }
        public decimal? Costo => Costo_Unitario is decimal c ? Math.Round(c * Enviados_Ok, 2) : null;
        public int Porcentaje => Total_Mensajes <= 0 ? 0 : Math.Min(100, (int)Math.Round(100.0 * (Enviados_Ok + Enviados_Error) / Total_Mensajes));
    }

    public class FlyerEnvioMasivoDetalle
    {
        public int Id { get; set; }
        public int Id_Envio { get; set; }
        public string? Nombre_Contacto { get; set; }
        public string? Telefono { get; set; }
        public string? Nombre_Imagen { get; set; }
        public string Estado { get; set; } = "";
        public string? Mensaje_Error { get; set; }
        public string? Token { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class AgenciaEnviarDto
    {
        /// <summary>Imágenes de la galería de agencias (nombre del archivo). Vacío = todas.</summary>
        public List<string> Imagenes { get; set; } = new();
        /// <summary>Agencias elegidas. Vacío = todas las activas.</summary>
        public List<int> Agencias { get; set; } = new();
        /// <summary>Si viene, se envía SOLO a este número (prueba).</summary>
        [StringLength(30)] public string? Prueba { get; set; }
    }

    public class GuardarImagenDto
    {
        [Required] public string ImageData { get; set; } = "";
        [Required] public string Id_Destino { get; set; } = "";
    }

    /// <summary>Lo que ve la agencia al abrir su link (/agencia/editor/{token}).</summary>
    public class AgenciaPublicoModelo
    {
        public string Token { get; set; } = "";
        public string? Nombre_Agencia { get; set; }
        public FlyerDestino Destino { get; set; } = new();
        public string Logo { get; set; } = "";
        public string Web { get; set; } = "";
        public string WebTexto { get; set; } = "";
        public string Simbolo { get; set; } = "RD$";
        public string Subtitulo { get; set; } = "";
    }
}
