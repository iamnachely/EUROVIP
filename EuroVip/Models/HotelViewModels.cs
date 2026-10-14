using Common.Data.Entities;
using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    public class HotelFiltro
    {
        public string? Q { get; set; }
        public int? Estado { get; set; }          // 1 visibles, 0 ocultos, null todos
        public int? Contenedor { get; set; }
        public short? Campanya { get; set; }
        public string Orden { get; set; } = "recientes";
        public int Pagina { get; set; } = 1;
        public int Tam { get; set; } = 12;
    }

    public class HotelListaItem
    {
        public int Id_Servicio { get; set; }
        public string Id_Idioma { get; set; } = "";
        public string Id_Destino { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string? CaratulaDestino { get; set; }
        public decimal? PrecioWeb { get; set; }
        public string Id_Moneda { get; set; } = "";
        public string Origen { get; set; } = "";
        public string Id_Pais_Origen { get; set; } = "";
        public string Id_PaisDestino { get; set; } = "";
        public short Id_Campanya { get; set; }
        public string? Nombre_Campanya { get; set; }
        public int? Id_Contenedor { get; set; }
        public string? Contenedor { get; set; }
        public int? Posicion_Destino { get; set; }
        public int? Noches { get; set; }
        public bool Estado { get; set; }
        /// <summary>URL de /imagenes/hoteles/{nombre}.webp si existe (la calcula el controlador).</summary>
        public string? Imagen { get; set; }
    }

    public class PaginaResultado<T>
    {
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
        public int Total { get; set; }
        public int Pagina { get; set; }
        public int Tam { get; set; }
        public int Paginas => Tam <= 0 ? 0 : (int)Math.Ceiling(Total / (double)Tam);
    }

    public class HotelEstadisticas
    {
        public int Total { get; set; }
        public int Visibles { get; set; }
        public int Ocultos { get; set; }
        public decimal? PrecioPromedio { get; set; }
    }

    /// <summary>Datos del formulario "Nuevo / Editar hotel".</summary>
    public class HotelGuardarDto
    {
        public int? Id_Servicio { get; set; }   // null = nuevo

        [Required(ErrorMessage = "Escribe el nombre del hotel")]
        [StringLength(150, ErrorMessage = "Máximo 150 caracteres")]
        public string Id_Destino { get; set; } = "";

        // Igual que ViajeVDT.dbo.inicio (tipo_servicio = Hotel):
        //   Id_Destino = nombre del hotel · CaratulaDestino = nombre del destino · Descripcion = "Régimen -CódigoHotel"
        [Required(ErrorMessage = "Elige el régimen y escribe el código del hotel")]
        [StringLength(2000, ErrorMessage = "Máximo 2000 caracteres")]
        [RegularExpression(@"^.+?\s*-\s*\d{3,12}\s*$", ErrorMessage = "La descripción debe ser «Régimen -Código» (ej. Solo Habitación -603258)")]
        public string Descripcion { get; set; } = "";

        [Required(ErrorMessage = "Escribe el destino (ej. Costa del Sol)")]
        [StringLength(300, ErrorMessage = "Máximo 300 caracteres")]
        [RegularExpression(@"^(?!https?://).+$", ErrorMessage = "Aquí va el nombre del destino, no una dirección web")]
        public string? CaratulaDestino { get; set; }

        // Solo informativo: el servidor siempre guarda ES (HotelRepository.IdiomaHotel).
        public string? Id_Idioma { get; set; }

        [Required(ErrorMessage = "Elige la campaña")]
        public short? Id_Campanya { get; set; }

        [Required(ErrorMessage = "Elige la moneda")]
        [StringLength(3, MinimumLength = 3)]
        public string Id_Moneda { get; set; } = "";

        [Range(0, 999999, ErrorMessage = "Precio inválido")]
        public decimal? PrecioWeb { get; set; }

        [Range(1, 60, ErrorMessage = "Entre 1 y 60 noches")]
        public int? Noches { get; set; }

        [Required(ErrorMessage = "Elige el país de origen")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Código de 2 letras")]
        public string Id_Pais_Origen { get; set; } = "";

        [Required(ErrorMessage = "Escribe la ciudad de origen")]
        [StringLength(30, ErrorMessage = "Máximo 30 caracteres")]
        public string Origen { get; set; } = "";

        [Required(ErrorMessage = "Elige el país de destino")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Código de 2 letras")]
        public string Id_PaisDestino { get; set; } = "DO";

        [StringLength(50)] public string? Latitud { get; set; }
        [StringLength(50)] public string? Longitud { get; set; }

        public int? Id_Contenedor { get; set; }

        [Range(0, 9999, ErrorMessage = "Posición inválida")]
        public int? Posicion_Destino { get; set; }

        public bool Estado { get; set; } = true;

        [StringLength(1000, ErrorMessage = "Máximo 1000 caracteres")]
        public string? Notas { get; set; }
    }

    public class HotelesPaginaViewModel
    {
        public IEnumerable<CodigoNombre> Monedas { get; set; } = Enumerable.Empty<CodigoNombre>();
        public HotelEstadisticas Estadisticas { get; set; } = new();
        public IEnumerable<string> Destinos { get; set; } = Enumerable.Empty<string>();
    }
}
