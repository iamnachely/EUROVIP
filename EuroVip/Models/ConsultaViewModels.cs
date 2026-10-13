namespace EuroVip.Models
{
    // ======================= CONSULTAR OFERTAS (equivale a /ConsultarOferta/Index de BackTiTan) =======================

    public class ConsultaFiltro
    {
        public string? Q { get; set; }                 // texto libre: destino, hotel, circuito, código o nº
        public int? Id_Servicio { get; set; }
        public string? Tipo { get; set; }              // OFERTA · CIRCUITO
        public string? Moneda { get; set; }
        public List<short> Campanyas { get; set; } = new();
        public string? Proveedor { get; set; }
        public string? PaisOrigen { get; set; }
        public string? Origen { get; set; }
        public string? PaisDestino { get; set; }
        public string? Destino { get; set; }
        public DateTime? InicioDesde { get; set; }
        public DateTime? InicioHasta { get; set; }
        public DateTime? AperturaDesde { get; set; }
        public DateTime? AperturaHasta { get; set; }
        public string? Usuario { get; set; }
        public decimal? PrecioDesde { get; set; }      // precio base
        public decimal? PrecioHasta { get; set; }
        public decimal? DescuentoDesde { get; set; }
        public decimal? DescuentoHasta { get; set; }
        public int? Id_Alojamiento { get; set; }
        public short? Noches { get; set; }
        public bool? Activo { get; set; } = true;      // null = activas e inactivas
        public string? Codigo { get; set; }
        public string? Regimen { get; set; }
        public int? Mes { get; set; }                  // mes de entrada 1-12
        public List<int> Dias { get; set; } = new();   // día de la semana de entrada 0=domingo … 6=sábado
        public bool? ConVuelo { get; set; }
        public bool Pasadas { get; set; }              // incluir entradas ya pasadas
        public string Orden { get; set; } = "inicio";  // inicio · precio · destino · recientes
        public int Pagina { get; set; } = 1;
        public int Tam { get; set; } = 50;
    }

    public class ConsultaFila
    {
        public int Id_Servicio { get; set; }
        public string Id_Tipo_Servicio { get; set; } = "";
        public string Id_Moneda { get; set; } = "";
        public short Id_Campanya { get; set; }
        public string? Codigo { get; set; }
        public string? NombreCampanya { get; set; }
        public string? Origen { get; set; }
        public string? Id_Pais_Origen { get; set; }
        public string? Destino { get; set; }
        public string? Id_Pais_Destino { get; set; }
        public DateTime? Inicio { get; set; }
        public DateTime? Regreso { get; set; }
        public short? Noches { get; set; }
        public string? CodigoOperador { get; set; }
        public string? NombreProveedor { get; set; }
        public DateTime? Apertura { get; set; }
        public string? Usuario { get; set; }
        public string? Nota { get; set; }
        public bool Activo { get; set; }
        public int? Id_Alojamiento { get; set; }
        public string? NombreAlojamiento { get; set; }
        public string? Regimen { get; set; }
        public string? Id_Regimen { get; set; }
        public string? Habitacion { get; set; }
        public string? Nombre_Circuito { get; set; }
        public bool ConVuelo { get; set; }
        public string? CodigoAerolinea { get; set; }
        public string? NombreAerolinea { get; set; }
        public string? Vuelo { get; set; }
        public string? Id_Estacion_Origen { get; set; }
        public string? Id_Estacion_Destino { get; set; }
        public decimal Precio_Base { get; set; }
        public decimal Descuento { get; set; }
        public decimal Precio_Mas_Menos { get; set; }
        public decimal TotalTasa { get; set; }
        public decimal PrecioWeb { get; set; }
    }

    public class ConsultaResumen
    {
        public int Total { get; set; }
        public int Ofertas { get; set; }
        public int Circuitos { get; set; }
        public int Activas { get; set; }
        public int ConVuelo { get; set; }
        public decimal? PrecioMin { get; set; }
        public decimal? PrecioMax { get; set; }
        public DateTime? PrimeraEntrada { get; set; }
        public DateTime? UltimaEntrada { get; set; }
    }

    /// <summary>Edición en masa: lo que viene vacío no se toca.</summary>
    public class ConsultaEditarDto
    {
        public List<int> Ids { get; set; } = new();
        public string? Moneda { get; set; }
        public short? Id_Campanya { get; set; }
        public string? Proveedor { get; set; }
        public decimal? PrecioBase { get; set; }       // fija el precio base…
        public decimal? SumarPrecio { get; set; }      // …o le suma/resta (no ambos)
        public decimal? Descuento { get; set; }
        public decimal? PrecioMasMenos { get; set; }
        public bool? Activo { get; set; }
        public string? Aerolinea { get; set; }
    }

    public class ConsultaDuplicarDto
    {
        public List<int> Ids { get; set; } = new();
        public short? Id_Campanya { get; set; }
        public int Dias { get; set; }                  // mover las fechas N días (0 = mismas fechas)
        public int Veces { get; set; } = 1;            // cuántas copias (cada una Dias más tarde)
        public string? PaisOrigen { get; set; }
        public string? Origen { get; set; }
        public string? EstacionOrigen { get; set; }
    }

    public class ConsultaTasaDto
    {
        public List<int> Ids { get; set; } = new();
        public string Id_Tasa { get; set; } = "";
        public decimal Valor { get; set; }
        public bool Quitar { get; set; }
    }

    public class ConsultaDetalle
    {
        public ConsultaFila Fila { get; set; } = new();
        public List<OfertaHabitacionDetalle> Habitaciones { get; set; } = new();
        public List<OfertaTasa> Tasas { get; set; } = new();
        public List<OfertaNota> Notas { get; set; } = new();
        public List<OfertaVuelo> Vuelos { get; set; } = new();
    }
}
