using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    // ======================= CATÁLOGO DE CIRCUITOS =======================

    /// <summary>Tarjeta de la lista de circuitos.</summary>
    public class CircuitoResumen
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public short Noches { get; set; }
        public string Id_Proveedor { get; set; } = "";
        public string? Proveedor { get; set; }
        public string? Caratula { get; set; }
        public string? Destinos { get; set; }
        public string? Paises { get; set; }
        public int Hoteles { get; set; }
        public int Dias { get; set; }
        public int Salidas { get; set; }
        public int SalidasActivas { get; set; }
        public DateTime? ProximaSalida { get; set; }
        /// <summary>Última salida activa (aunque ya haya pasado): para explicar por qué no sale en la Home.</summary>
        public DateTime? UltimaSalida { get; set; }
        /// <summary>true si una tarjeta visible de la Home de la web es de este circuito.</summary>
        public bool EnHome { get; set; }
        public decimal? PrecioDesde { get; set; }
    }

    public class CircuitoDestinoDto
    {
        [Required, StringLength(30)] public string Id_Destino { get; set; } = "";
        [Required, StringLength(2, MinimumLength = 2)] public string Id_Pais { get; set; } = "";
        public string? Pais { get; set; }
    }

    public class CircuitoHotelDto
    {
        [Required] public int Id_Alojamiento { get; set; }
        [Required, StringLength(20)] public string Categoria { get; set; } = "";
        public string? Nombre { get; set; }
        public string? Destino { get; set; }
        public string? Estrellas { get; set; }
        public string? Caratula { get; set; }
    }

    public class CircuitoDiaDto
    {
        public int Dia { get; set; }
        [Required(ErrorMessage = "Cada día necesita un título"), StringLength(200)] public string Titulo { get; set; } = "";
        public string? Descripcion { get; set; }
        public string? Visitas { get; set; }
        [StringLength(200)] public string? Regimen { get; set; }
        [StringLength(200)] public string? Transporte { get; set; }
        [StringLength(20)] public string? Tipo_Alojamiento { get; set; }
        [StringLength(200)] public string? Caratula { get; set; }
    }

    /// <summary>Formulario completo de un circuito (crear o editar todo de una vez).</summary>
    public class CircuitoGuardarDto
    {
        /// <summary>Código original al editar; null = nuevo.</summary>
        public string? CodigoOriginal { get; set; }

        [Required(ErrorMessage = "Escribe un código para el circuito")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres")]
        [RegularExpression(@"^[A-Za-z0-9 ]+$", ErrorMessage = "Solo letras, números y espacios")]
        public string Codigo { get; set; } = "";

        [Required(ErrorMessage = "Escribe el nombre del circuito")]
        [StringLength(50, ErrorMessage = "Máximo 50 caracteres")]
        [RegularExpression(@"^[\p{L}\p{M}\p{N}' ]+$", ErrorMessage = "Solo letras, números, espacios y apóstrofo (sin guiones ni símbolos)")]
        public string Nombre { get; set; } = "";

        [Required(ErrorMessage = "Elige el tour operador")]
        [StringLength(10)]
        public string Id_Proveedor { get; set; } = "";

        [Range(1, 60, ErrorMessage = "Entre 1 y 60 noches")]
        public short Noches { get; set; } = 7;

        [StringLength(200)] public string? Caratula { get; set; }
        public string? Itinerario { get; set; }

        public List<CircuitoDestinoDto> Destinos { get; set; } = new();
        public List<CircuitoHotelDto> Hoteles { get; set; } = new();
        public List<CircuitoDiaDto> Dias { get; set; } = new();
    }

    public class CircuitoSalida
    {
        public int Id_Servicio { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public short Id_Campanya { get; set; }
        public string? Campanya { get; set; }
        public string Id_Moneda { get; set; } = "";
        public string? Codigo { get; set; }
        public string? Categoria { get; set; }
        public string? Origen { get; set; }
        public decimal Precio_Base { get; set; }
        public decimal Descuento { get; set; }
        public decimal? PrecioWeb { get; set; }
        public int Vuelos { get; set; }
        public bool Activo { get; set; }
        public string? Usuario { get; set; }
    }

    public class CircuitoDetalle
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Id_Proveedor { get; set; } = "";
        public string? Proveedor { get; set; }
        public short Noches { get; set; }
        public string? Caratula { get; set; }
        public string? Itinerario { get; set; }
        public List<CircuitoDestinoDto> Destinos { get; set; } = new();
        public List<CircuitoHotelDto> Hoteles { get; set; } = new();
        public List<CircuitoDiaDto> Dias { get; set; } = new();
        public List<CircuitoSalida> Salidas { get; set; } = new();
    }

    public class CategoriaDto
    {
        [Required(ErrorMessage = "Escribe la categoría"), StringLength(20, ErrorMessage = "Máximo 20 caracteres")]
        public string Categoria { get; set; } = "";
        [StringLength(200)] public string? Descripcion { get; set; }
        public string? Original { get; set; }
    }

    public class Opcion
    {
        public string Valor { get; set; } = "";
        public string Texto { get; set; } = "";
        public string? Extra { get; set; }
        public string? Extra2 { get; set; }
    }

    // ======================= CARGA DE CIRCUITOS =======================

    public class CargaCampanyaDto
    {
        public short Id_Campanya { get; set; }
        [Range(0, 100, ErrorMessage = "El descuento va de 0 a 100 %")] public decimal Descuento { get; set; }
    }

    public class CargaSuplementoDto
    {
        [Required] public string Id_Tasa { get; set; } = "";
        public decimal Valor { get; set; }
    }

    /// <summary>Una salida = una fecha con su precio final (lo que paga el cliente).</summary>
    public class CargaSalidaDto
    {
        public DateTime Desde { get; set; }
        public decimal PrecioTotal { get; set; }
        /// <summary>Tasas incluidas en el precio total. Si es null se usa la suma de suplementos.</summary>
        public decimal? Tasas { get; set; }
    }

    public class CargaSegmentoDto
    {
        [Required] public string Aerolinea { get; set; } = "";
        [Required] public string AeropuertoOrigen { get; set; } = "";
        [Required] public string AeropuertoDestino { get; set; } = "";
        public string? NumeroVuelo { get; set; }
        public string Clase { get; set; } = "T";
        public bool Conexion { get; set; }
        [Required, RegularExpression(@"^\d{2}:\d{2}$")] public string HoraSalida { get; set; } = "";
        [Required, RegularExpression(@"^\d{2}:\d{2}$")] public string HoraLlegada { get; set; } = "";
    }

    public class CargaNotaDto
    {
        [Required] public string Tipo { get; set; } = "";
        [Required] public string Texto { get; set; } = "";
    }

    /// <summary>
    /// Datos de una carga de circuito. Lo usan las DOS versiones (Express y Clásica).
    /// Por cada salida × campaña se crea un "servicio".
    /// </summary>
    public class CargaCircuitoDto
    {
        public string Modo { get; set; } = "clasica";

        /// <summary>Circuito existente (código). Si viene NuevoCircuito se crea primero.</summary>
        public string? CodigoCircuito { get; set; }
        public CircuitoGuardarDto? NuevoCircuito { get; set; }

        [StringLength(30)] public string? CodigoServicio { get; set; }
        [StringLength(10)] public string? Id_Proveedor { get; set; }
        [Range(1, 60)] public short? Noches { get; set; }
        [Required(ErrorMessage = "Elige la categoría de hoteles")] public string Categoria { get; set; } = "";
        [Required] public string Divisa { get; set; } = "DOP";

        [Required(ErrorMessage = "Elige la ciudad de origen")] public string Origen { get; set; } = "";
        [Required(ErrorMessage = "Elige el país de origen")] public string Id_Pais_Origen { get; set; } = "";

        public List<CargaCampanyaDto> Campanyas { get; set; } = new();
        public List<CargaSuplementoDto> Suplementos { get; set; } = new();
        public List<CargaSalidaDto> Salidas { get; set; } = new();
        public List<CircuitoHotelDto> Hoteles { get; set; } = new();

        public bool ConVuelo { get; set; }
        public List<CargaSegmentoDto> VuelosIda { get; set; } = new();
        public List<CargaSegmentoDto> VuelosVuelta { get; set; } = new();

        public List<CargaNotaDto> Notas { get; set; } = new();
        public bool PublicarEnWeb { get; set; } = true;
    }

    public class CargaResultado
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; } = "";
        public string Circuito { get; set; } = "";
        public string Codigo { get; set; } = "";
        public int Servicios { get; set; }
        public List<int> Ids { get; set; } = new();
        public int Vuelos { get; set; }
        public int LogId { get; set; }
    }

    // ======================= EXCEL =======================

    public class ExcelHotel
    {
        public string Texto { get; set; } = "";
        public string Estado { get; set; } = "not_found";   // found | multiple | not_found
        public int? Id_Alojamiento { get; set; }
        public string? Nombre { get; set; }
        public List<Opcion> Candidatos { get; set; } = new();
    }

    public class ExcelFecha
    {
        public DateTime Fecha { get; set; }
        public decimal Precio { get; set; }
    }

    public class ExcelCircuito
    {
        public string? Aerolinea { get; set; }
        public string? AerolineaId { get; set; }
        public decimal? Tasa { get; set; }
        public string? Segmento1 { get; set; }
        public string? Segmento2 { get; set; }
        public List<ExcelHotel> Hoteles { get; set; } = new();
        public List<ExcelFecha> Fechas { get; set; } = new();
        public List<string> Avisos { get; set; } = new();
    }
}
