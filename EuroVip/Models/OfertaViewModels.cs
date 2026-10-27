using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    // ======================= CARGA MANUAL DE OFERTAS =======================

    public class OfertaHabitacionDto
    {
        public int Id_Habitacion { get; set; }
        /// <summary>Suplemento de esta habitación sobre el precio (0 = la habitación base).</summary>
        [Range(0, 999999)] public decimal Precio { get; set; }
        public string? Nombre { get; set; }
    }

    /// <summary>Un hotel de la carga con su régimen, sus habitaciones y SUS fechas/precios.</summary>
    public class OfertaHotelDto
    {
        public int Id_Alojamiento { get; set; }
        [Required(ErrorMessage = "Elige el régimen")] public string Id_Regimen { get; set; } = "";
        public List<OfertaHabitacionDto> Habitaciones { get; set; } = new();
        public List<CargaSalidaDto> Salidas { get; set; } = new();
        public string? Nombre { get; set; }
        // Se guardan en todas las ofertas/paquetes que se crean de este hotel
        /// <summary>Prioridad (FASE 29): 1 sale primero en la web, 2 segundo… null = sin prioridad (por precio).</summary>
        [Range(1, 999, ErrorMessage = "La prioridad va de 1 a 999")] public short? Peso { get; set; }
        /// <summary>Precio del proveedor en su web (informativo: nuestro precio no cambia).</summary>
        [Range(0, 100_000_000, ErrorMessage = "El precio del proveedor tiene que ser 0 o más")] public decimal? Precio_Proveedor { get; set; }
    }

    /// <summary>
    /// Datos de una carga manual de ofertas. Lo usan las DOS versiones (Express y Clásica).
    /// Por cada hotel × campaña × fecha se crea un "servicio" de tipo OFERTA.
    /// </summary>
    public class CargaOfertaDto
    {
        public string Modo { get; set; } = "clasica";
        /// <summary>OFERTA (Solo Hotel, por defecto) o PAQUETES (Paquetes): se guarda en servicio.id_tipo_servicio.</summary>
        public string? Tipo { get; set; }
        [StringLength(30)] public string? CodigoServicio { get; set; }
        [Required(ErrorMessage = "Elige el proveedor")] [StringLength(10)] public string Id_Proveedor { get; set; } = "";
        [StringLength(50)] public string? Nombre { get; set; }
        [Range(1, 60, ErrorMessage = "Entre 1 y 60 noches")] public short Noches { get; set; } = 7;
        [Required] public string Divisa { get; set; } = "DOP";
        /// <summary>Ciudad de origen de venta. Opcional en Solo Hotel (vacío = sin origen); obligatoria en Paquetes (lo revisa CargaOfertaService).</summary>
        public string? Origen { get; set; } = "";
        public string? Id_Pais_Origen { get; set; } = "";

        public List<OfertaHotelDto> Hoteles { get; set; } = new();
        public List<CargaCampanyaDto> Campanyas { get; set; } = new();
        public List<CargaSuplementoDto> Suplementos { get; set; } = new();

        public bool ConVuelo { get; set; }
        public List<CargaSegmentoDto> VuelosIda { get; set; } = new();
        public List<CargaSegmentoDto> VuelosVuelta { get; set; } = new();

        public List<CargaNotaDto> Notas { get; set; } = new();
        public bool PublicarEnWeb { get; set; } = true;
    }

    // ======================= LISTA / EDICIÓN =======================

    public class OfertaFiltro
    {
        /// <summary>OFERTA (Solo Hotel, por defecto) o PAQUETES (Paquetes).</summary>
        public string? Tipo { get; set; }
        public string? Q { get; set; }
        public string? Destino { get; set; }
        public short? Campanya { get; set; }
        public int? Estado { get; set; }        // 1 activas, 0 inactivas
        public int? Vuelo { get; set; }         // 1 con vuelo, 0 sin vuelo
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public bool Pasadas { get; set; }
        public string Orden { get; set; } = "fecha";
        public int Pagina { get; set; } = 1;
        public int Tam { get; set; } = 25;
    }

    public class OfertaItem
    {
        public int Id_Servicio { get; set; }
        public string? Nombre { get; set; }
        public int Id_Alojamiento { get; set; }
        public string Hotel { get; set; } = "";
        public string? Caratula { get; set; }
        public string? Destino { get; set; }
        public string? Id_Pais { get; set; }
        public string Id_Regimen { get; set; } = "";
        public string? Regimen { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public short Noches { get; set; }
        public short Id_Campanya { get; set; }
        public string? Campanya { get; set; }
        public string Id_Moneda { get; set; } = "";
        public decimal Precio_Base { get; set; }
        public decimal Descuento { get; set; }
        public decimal Tasas { get; set; }
        public decimal? PrecioWeb { get; set; }
        public string? Origen { get; set; }
        public int Vuelos { get; set; }
        public int Habitaciones { get; set; }
        public bool Activo { get; set; }
        public string? Codigo { get; set; }
        public string? Usuario { get; set; }
        public string? Proveedor { get; set; }
        public short? Peso { get; set; }
        public decimal? Precio_Proveedor { get; set; }
    }

    public class OfertaEstadisticas
    {
        public int Total { get; set; }
        public int Activas { get; set; }
        public int Futuras { get; set; }
        public int Hoteles { get; set; }
        public int ConVuelo { get; set; }
        public decimal? PrecioDesde { get; set; }
    }

    public class OfertaTasa { public string Id_Tasa { get; set; } = ""; public decimal Valor { get; set; } }
    public class OfertaNota { public short Secuencia { get; set; } public string Tipo { get; set; } = ""; public string Nota1 { get; set; } = ""; }

    public class OfertaHabitacionDetalle
    {
        public int Id_Habitacion { get; set; }
        public string Nombre { get; set; } = "";
        public string? Id_Tipo_Habitacion { get; set; }
        public decimal Precio { get; set; }
    }

    public class OfertaVuelo
    {
        public short Segmento { get; set; }
        public string Id_Proveedor { get; set; } = "";
        public string Id_Estacion_Origen { get; set; } = "";
        public string Id_Estacion_Destino { get; set; } = "";
        public string? Vuelo { get; set; }
        public DateTime Fecha_Salida { get; set; }
        public string Hora_Salida { get; set; } = "";
        public DateTime Fecha_Regreso { get; set; }
        public string Hora_Regreso { get; set; } = "";
    }

    public class OfertaDetalle : OfertaItem
    {
        public string Id_Proveedor { get; set; } = "";
        public string? Id_Pais_Origen { get; set; }
        public List<OfertaHabitacionDetalle> HabitacionesDetalle { get; set; } = new();
        public List<OfertaTasa> TasasDetalle { get; set; } = new();
        public List<OfertaNota> Notas { get; set; } = new();
        public List<OfertaVuelo> VuelosDetalle { get; set; } = new();
    }

    /// <summary>Edición de una salida de oferta (lo que se puede cambiar sin recrearla).</summary>
    public class OfertaEditarDto
    {
        public int Id_Servicio { get; set; }
        [Range(1, 999999, ErrorMessage = "Precio inválido")] public decimal PrecioTotal { get; set; }
        [Range(0, 100, ErrorMessage = "Descuento de 0 a 100")] public decimal Descuento { get; set; }
        public short Id_Campanya { get; set; }
        [Required] public string Id_Regimen { get; set; } = "";
        public DateTime Desde { get; set; }
        [Range(1, 60)] public short Noches { get; set; }
        public bool Activo { get; set; } = true;
        public List<OfertaHabitacionDto> Habitaciones { get; set; } = new();
        public List<CargaSuplementoDto> Suplementos { get; set; } = new();
        /// <summary>Prioridad (FASE 29): 1 sale primero en la web, 2 segundo… null = sin prioridad (por precio).</summary>
        [Range(1, 999, ErrorMessage = "La prioridad va de 1 a 999")] public short? Peso { get; set; }
        /// <summary>Precio del proveedor en su web (informativo: nuestro precio no cambia).</summary>
        [Range(0, 100_000_000, ErrorMessage = "El precio del proveedor tiene que ser 0 o más")] public decimal? Precio_Proveedor { get; set; }
        /// <summary>true = la prioridad y el precio del proveedor se ponen también en las demás fechas de este hotel (mismo tipo).</summary>
        public bool PrioridadTodas { get; set; }
    }

    public class OfertaLoteDto
    {
        public List<int> Ids { get; set; } = new();
        public bool? Activo { get; set; }
        public decimal? AjustePrecio { get; set; }   // suma/resta al precio base
        public decimal? Descuento { get; set; }
    }

    // ======================= EXCEL DE OFERTAS =======================

    public class ExcelOfertaHotel
    {
        public string Texto { get; set; } = "";
        public string Estado { get; set; } = "not_found";
        public int? Id_Alojamiento { get; set; }
        public string? Nombre { get; set; }
        public string? Destino { get; set; }
        public List<Opcion> Candidatos { get; set; } = new();
        public List<ExcelFecha> Precios { get; set; } = new();
    }

    public class ExcelOferta
    {
        public List<DateTime> Fechas { get; set; } = new();
        public List<ExcelOfertaHotel> Hoteles { get; set; } = new();
        public List<string> Avisos { get; set; } = new();
    }
}
