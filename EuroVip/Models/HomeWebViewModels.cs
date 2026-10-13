using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    // ======================= HOME DE LA WEB (Inicio + Carrusel) =======================

    /// <summary>Una tarjeta de la Home = una fila de [Eurovip].[inicio].</summary>
    public class HwTarjeta
    {
        public int Id { get; set; }
        public int Id_Servicio { get; set; }
        public string Tipo_Servicio { get; set; } = "";
        public short Id_Campanya { get; set; }
        public string? Campanya { get; set; }
        public string Id_Moneda { get; set; } = "";
        public string Id_Pais_Origen { get; set; } = "";
        public string Origen { get; set; } = "";
        public string Id_PaisDestino { get; set; } = "";
        public string Id_Destino { get; set; } = "";
        public string? CaratulaDestino { get; set; }
        public string? Descripcion { get; set; }
        public decimal? PrecioWeb { get; set; }
        public int? Id_Contenedor { get; set; }
        public string? Contenedor { get; set; }
        public int? Posicion_Destino { get; set; }
        public bool Estado { get; set; }
        public int? Noches { get; set; }
        public string? Notas { get; set; }
        /// <summary>null = tarjeta manual (hotel); true/false = la oferta/circuito sigue a la venta.</summary>
        public bool? Vigente { get; set; }
    }

    public class HwContenedor
    {
        public int Id_Contenedor { get; set; }
        public string Nombre { get; set; } = "";
        public int Posicion { get; set; }
        public bool Estado { get; set; }
        public List<HwTarjeta> Tarjetas { get; set; } = new();
    }

    public class HwSlideDestino
    {
        public int Id { get; set; }
        public int Id_Carrusel { get; set; }
        public string Origen { get; set; } = "";
        public string Destino { get; set; } = "";
        public decimal Precio { get; set; }
        public int Id_Campanya { get; set; }
        public string? Regimen { get; set; }
    }

    /// <summary>Un slide = la fila formato 1 (web) de [Eurovip].[carrusel]; tablet (2) y móvil (3) comparten el nombre de archivo.</summary>
    public class HwSlide
    {
        public int Id_Carrusel { get; set; }
        public int Orden { get; set; }
        public string Orientacion { get; set; } = "H";
        public string Ruta { get; set; } = "";
        public bool Activo { get; set; }
        public string? Url { get; set; }
        public bool TieneTablet { get; set; }
        public bool TieneMovil { get; set; }
        public List<HwSlideDestino> Destinos { get; set; } = new();
    }

    public class HwImagenes
    {
        public string Web { get; set; } = "";
        public string Tablet { get; set; } = "";
        public string Movil { get; set; } = "";
    }

    public class HwDatos
    {
        public List<HwContenedor> Contenedores { get; set; } = new();
        public List<HwTarjeta> SinContenedor { get; set; } = new();
        public List<HwSlide> Slides { get; set; } = new();
        public HwImagenes Imagenes { get; set; } = new();
    }

    // ---------- DTOs ----------
    public class HwOrdenDto
    {
        public int? Id_Contenedor { get; set; }
        public List<int> Ids { get; set; } = new();
    }

    public class HwContenedorDto
    {
        public int? Id_Contenedor { get; set; }
        [Required(ErrorMessage = "Escribe el nombre de la sección")] [StringLength(30, ErrorMessage = "Máximo 30 letras")]
        public string Nombre { get; set; } = "";
        public bool Estado { get; set; } = true;
    }

    public class HwTarjetaDto
    {
        public int? Id { get; set; }
        [Required] public string Tipo_Servicio { get; set; } = "OFERTA";
        [Range(1, short.MaxValue, ErrorMessage = "Elige la campaña")] public short Id_Campanya { get; set; }
        [Required(ErrorMessage = "Elige el país de origen")] public string Id_Pais_Origen { get; set; } = "ES";
        [Required(ErrorMessage = "Elige el origen")] [StringLength(30)] public string Origen { get; set; } = "";
        [Required(ErrorMessage = "Elige el país de destino")] public string Id_PaisDestino { get; set; } = "";
        [Required(ErrorMessage = "Elige el destino")] [StringLength(1500)] public string Id_Destino { get; set; } = "";
        [StringLength(300)] public string? CaratulaDestino { get; set; }
        public string? Descripcion { get; set; }
        [Range(0, 999999, ErrorMessage = "Precio inválido")] public decimal? PrecioWeb { get; set; }
        public int? Id_Contenedor { get; set; }
        [Range(0, 60)] public int? Noches { get; set; }
        [StringLength(1000)] public string? Notas { get; set; }
        public bool Estado { get; set; } = true;
    }

    public class HwActualizarDto
    {
        public string Tipo_Servicio { get; set; } = "OFERTA";
        public short Id_Campanya { get; set; }
        public string Id_Destino { get; set; } = "";
        public string Origen { get; set; } = "";
        public string Id_Pais_Origen { get; set; } = "ES";
        public string Id_Moneda { get; set; } = "EUR";
    }

    public class HwSlideDto
    {
        public int Id_Carrusel { get; set; }
        public string Orientacion { get; set; } = "H";
        [StringLength(500)] public string? Url { get; set; }
        public bool Activo { get; set; }
    }

    public class HwSlideDestinoDto
    {
        public int? Id { get; set; }
        public int Id_Carrusel { get; set; }
        [Required(ErrorMessage = "Elige el origen")] [StringLength(50)] public string Origen { get; set; } = "";
        [Required(ErrorMessage = "Elige el destino")] [StringLength(50)] public string Destino { get; set; } = "";
        [Range(0, 999999)] public decimal Precio { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Elige la campaña")] public int Id_Campanya { get; set; }
        [StringLength(100)] public string? Regimen { get; set; }
    }
}
