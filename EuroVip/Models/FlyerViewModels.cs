using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    // ======================= FLYERS (equivale a /Flyer/Hub de BackTiTan) =======================

    public class FlyerConfig
    {
        public int Id { get; set; }
        [StringLength(100)] public string? Nombre_Config { get; set; }
        [Required] public string Id_Tipo_Servicio { get; set; } = "SOLO_HOTEL";
        [Range(1, int.MaxValue, ErrorMessage = "Elige la campaña")] public int Id_Campanya { get; set; }
        public string Id_Idioma { get; set; } = "ES";
        [Required] public string Id_Moneda { get; set; } = "DOP";
        public int Activo_Servicio { get; set; } = 1;
        [Required(ErrorMessage = "Elige el origen")] [StringLength(100)] public string Origen { get; set; } = "";
        [Required(ErrorMessage = "Elige el régimen")] [StringLength(20)] public string Id_Regimen { get; set; } = "TI";
        [Required(ErrorMessage = "Elige el destino")] [StringLength(100)] public string Id_Destino { get; set; } = "";
        [StringLength(200)] public string? Nombre_Destino { get; set; }
        public DateTime? Fecha_Creacion { get; set; }
        [StringLength(500)] public string? Url_Destino { get; set; }
        [StringLength(20)] public string? Tipo { get; set; }
        // calculado
        public string? Campanya { get; set; }
        public int Hoteles { get; set; }
        public int? PrecioDesde { get; set; }
        public string? Fondo { get; set; }
    }

    public class FlyerHotel
    {
        public string? Id_Destino { get; set; }
        public string? Nombre_Destino { get; set; }
        public string? Id_Pais { get; set; }
        public string Nombre_Hotel { get; set; } = "";
        public string? Nombre_Habitacion { get; set; }
        public string? Regimen { get; set; }
        public int Precio_Final { get; set; }
        public int Posicion { get; set; }
        public DateTime? Fecha_Actualizacion { get; set; }
    }

    public class FlyerDestino
    {
        public int IdConfig { get; set; }
        public string Id_Destino { get; set; } = "";
        public string Nombre_Destino { get; set; } = "";
        public string Origen { get; set; } = "";
        public string? Fondo { get; set; }
        public DateTime? Fecha_Actualizacion { get; set; }
        public List<FlyerHotel> Hoteles { get; set; } = new();
    }

    public class FlyerContacto
    {
        public int Id { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime Created_At { get; set; }
        [StringLength(200)] public string? Nombre_Completo { get; set; }
        [Required(ErrorMessage = "Escribe el número")] [StringLength(30)] public string Numero { get; set; } = "";
        [StringLength(200)] [EmailAddress(ErrorMessage = "Correo inválido")] public string? Email { get; set; }
    }

    public class FlyerEnvio
    {
        public int Id { get; set; }
        public DateTime Fecha_Envio { get; set; }
        public string Carpeta_Fecha { get; set; } = "";
        public int Total_Contactos { get; set; }
        public int Enviados_Ok { get; set; }
        public int Errores { get; set; }
        public int Imagenes_Count { get; set; }
        public string? Texto_Mensaje { get; set; }
        public string? Estado { get; set; }
    }

    /// <summary>Hotel con su precio más barato a la venta (buscador de hoteles del editor de Flyer Editar).</summary>
    public class FlyerHotelPrecio
    {
        public int Id_Alojamiento { get; set; }
        public string Nombre { get; set; } = "";
        public string? Caratula { get; set; }
        public string? Destino { get; set; }
        public string? Regimen { get; set; }
        public string? Habitacion { get; set; }
        public DateTime? Desde { get; set; }
        public int? Precio { get; set; }
    }

    /// <summary>Lo que necesita la pantalla del editor (copia del editor de Flyer Cliente de BackTiTan).</summary>
    public class FlyerEditorModelo
    {
        public List<FlyerDestino> Destinos { get; set; } = new();
        public List<FlyerConfig> Configs { get; set; } = new();
        public DateTime? UltimaActualizacion { get; set; }
        public Dictionary<string, string> Fondos { get; set; } = new();
        public string Logo { get; set; } = "/img/flyer/logo-eurovip-travel.png";
        public string Web { get; set; } = "http://eurovip.com.do";
        public string WebTexto { get; set; } = "eurovip.com.do";
    }

    public class FlyerGuardarPngDto
    {
        public string ImageData { get; set; } = "";
        public string? NombreDestino { get; set; }
        public string? NombreArchivo { get; set; }
        public string? LinkOferta { get; set; }
        public int? Plantilla { get; set; }
    }

    public class FlyerImagen
    {
        public string Carpeta { get; set; } = "";
        public string Archivo { get; set; } = "";
        public string Url { get; set; } = "";
        public long Bytes { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class FlyerEnviarDto
    {
        /// <summary>vacío = a todos los contactos activos</summary>
        public List<int> Contactos { get; set; } = new();
        public string? NumeroPrueba { get; set; }
        public List<string> Imagenes { get; set; } = new();   // "carpeta/archivo.png"
        [StringLength(1000)] public string? Texto { get; set; }
        /// <summary>Con botón a la web (plantilla «cliente_info»): cada flyer lleva un botón a la página de su destino.</summary>
        public bool ConLink { get; set; }
    }

    public class FlyerEnvioEstado
    {
        public bool EnCurso { get; set; }
        public bool Cancelado { get; set; }
        public int Total { get; set; }
        public int Enviados { get; set; }
        public int Errores { get; set; }
        public string? Actual { get; set; }
        public DateTime? Inicio { get; set; }
        public DateTime? Fin { get; set; }
        public List<string> Registro { get; set; } = new();
    }
}
