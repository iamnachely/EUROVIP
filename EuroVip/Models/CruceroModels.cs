using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    // ======================= CRUCEROS (Eurovip.crucero* — documento Database/esquema-cruceros.md) =======================

    public static class CruceroValores
    {
        /// <summary>Categorías de cabina (CHECK de crucero_cabina_tipo) y su texto.</summary>
        public static readonly Dictionary<string, string> CategoriasCabina = new()
        {
            ["INTERIOR"] = "Interior", ["EXTERIOR"] = "Exterior (vista al mar)", ["BALCON"] = "Balcón", ["SUITE"] = "Suite"
        };
    }

    // ---------- Catálogos ----------
    public class CruceroNaviera
    {
        public int Id_Naviera { get; set; }
        [Required(ErrorMessage = "Escribe el nombre de la naviera"), StringLength(100)] public string Nombre { get; set; } = "";
        [StringLength(500)] public string? Logo { get; set; }
        public bool Activo { get; set; } = true;
        public int Barcos { get; set; }
    }

    public class CruceroBarco
    {
        public int Id_Barco { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Elige la naviera")] public int Id_Naviera { get; set; }
        public string? Naviera { get; set; }
        [Required(ErrorMessage = "Escribe el nombre del barco"), StringLength(100)] public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        [StringLength(500)] public string? Imagen { get; set; }
        public bool Activo { get; set; } = true;
        public int Cruceros { get; set; }
    }

    public class CruceroCategoria
    {
        public int Id_Categoria { get; set; }
        [Required(ErrorMessage = "Escribe el nombre de la categoría"), StringLength(100)] public string Nombre { get; set; } = "";
        [StringLength(300)] public string? Descripcion { get; set; }
        [StringLength(500)] public string? Imagen { get; set; }
        public short Orden { get; set; } = 1;
        public bool Activo { get; set; } = true;
        public int Cruceros { get; set; }
    }

    // ---------- Lista ----------
    public class CruceroLista
    {
        public int Id_Crucero { get; set; }
        public string Titulo { get; set; } = "";
        public string? Codigo { get; set; }
        public int Id_Barco { get; set; }
        public string Barco { get; set; } = "";
        public int Id_Naviera { get; set; }
        public string Naviera { get; set; } = "";
        public string? Naviera_Logo { get; set; }
        public short Noches { get; set; }
        public string Puerto_Salida { get; set; } = "";
        public string Puerto_Llegada { get; set; } = "";
        public bool Activo { get; set; }
        public string? Itinerario { get; set; }
        public int Salidas { get; set; }
        public int Salidas_Futuras { get; set; }
        public DateTime? Proxima_Salida { get; set; }
        public decimal? Desde_Pp { get; set; }
        public decimal? Interior_Pp { get; set; }
        public decimal? Exterior_Pp { get; set; }
        public decimal? Balcon_Pp { get; set; }
        public decimal? Suite_Pp { get; set; }
        public string? Categorias { get; set; }
        public string? Ids_Categoria { get; set; }
        public int Cabinas { get; set; }
        public int Actividades { get; set; }
        public string? Usuario { get; set; }
        public DateTime? Fecha_Modificacion { get; set; }
        public DateTime Fecha_Alta { get; set; }
        /// <summary>Portada (Eurovip.multimedia, tipo CRUCERO).</summary>
        public string? Imagen { get; set; }
    }

    // ---------- Crucero completo (lo que carga y guarda el editor) ----------
    // Las partes que se relacionan entre sí (salidas, tarifas, tipos de cabina, cubiertas) llevan una "Clave" de texto que pone
    // la pantalla: al guardar se reemplazan todas las filas hijas y se vuelven a enlazar por esa clave.
    public class CruceroDto
    {
        public int? Id_Crucero { get; set; }

        [Required(ErrorMessage = "Escribe el título del crucero"), StringLength(200, ErrorMessage = "Máximo 200 caracteres")]
        public string Titulo { get; set; } = "";
        [StringLength(300)] public string? Subtitulo { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Elige el barco")] public int Id_Barco { get; set; }
        [Required(ErrorMessage = "Escribe el puerto de salida"), StringLength(100)] public string Puerto_Salida { get; set; } = "";
        [Required(ErrorMessage = "Escribe el puerto de llegada"), StringLength(100)] public string Puerto_Llegada { get; set; } = "";
        [Range(1, 180, ErrorMessage = "Noches: de 1 a 180")] public short Noches { get; set; } = 7;
        [StringLength(50)] public string? Codigo { get; set; }
        public string? Descripcion { get; set; }
        public string? Incluye { get; set; }
        public string? No_Incluye { get; set; }
        [StringLength(150)] public string? Titulo_Seccion { get; set; }
        public bool Activo { get; set; } = true;
        public List<int> Categorias { get; set; } = new();

        public List<CruceroDiaDto> Itinerario { get; set; } = new();
        public List<CruceroSeccionDto> Secciones { get; set; } = new();
        public List<CruceroCubiertaDto> Cubiertas { get; set; } = new();
        public List<CruceroCabinaTipoDto> Cabinas { get; set; } = new();
        public List<CruceroSalidaDto> Salidas { get; set; } = new();
        public List<CruceroTarifaDto> Tarifas { get; set; } = new();
        public List<CruceroPrecioDto> Precios { get; set; } = new();

        // Solo lectura (para la pantalla)
        public string? Usuario { get; set; }
        public DateTime? Fecha_Alta { get; set; }
        public DateTime? Fecha_Modificacion { get; set; }
    }

    public class CruceroDiaDto
    {
        public short Dia { get; set; }
        [StringLength(150)] public string Puerto { get; set; } = "";
        public bool En_Navegacion { get; set; }
        /// <summary>"HH:mm" o vacío.</summary>
        public string? Hora_Llegada { get; set; }
        public string? Hora_Salida { get; set; }
        public string? Descripcion { get; set; }
        [StringLength(500)] public string? Imagen { get; set; }
    }

    public class CruceroSeccionDto
    {
        [StringLength(100)] public string Titulo { get; set; } = "";
        public List<CruceroActividadDto> Actividades { get; set; } = new();
    }

    public class CruceroActividadDto
    {
        [StringLength(150)] public string Titulo { get; set; } = "";
        public string? Descripcion { get; set; }
        [StringLength(500)] public string? Imagen { get; set; }
    }

    public class CruceroCubiertaDto
    {
        public short Numero { get; set; }
        [StringLength(100)] public string? Nombre { get; set; }
        [StringLength(500)] public string? Plano { get; set; }
    }

    public class CruceroCabinaTipoDto
    {
        public string Clave { get; set; } = "";
        public string Categoria { get; set; } = "INTERIOR";
        [StringLength(20)] public string? Codigo { get; set; }
        [StringLength(150)] public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public byte? Capacidad_Min { get; set; }
        public byte? Capacidad_Max { get; set; }
        public decimal? Metros { get; set; }
        [StringLength(500)] public string? Imagen { get; set; }
        public bool Permite_Garantizada { get; set; } = true;
        public List<CruceroCabinaDto> Cabinas { get; set; } = new();
    }

    public class CruceroCabinaDto
    {
        [StringLength(10)] public string Numero { get; set; } = "";
        /// <summary>Número de la cubierta (de la lista Cubiertas) o null.</summary>
        public short? Cubierta { get; set; }
        public bool Disponible { get; set; } = true;
    }

    public class CruceroSalidaDto
    {
        public string Clave { get; set; } = "";
        public DateTime Fecha { get; set; }
        public bool Activo { get; set; } = true;
    }

    public class CruceroTarifaDto
    {
        public string Clave { get; set; } = "";
        [StringLength(50)] public string Codigo { get; set; } = "";
        [StringLength(100)] public string Nombre { get; set; } = "";
        [StringLength(300)] public string? Descripcion { get; set; }
    }

    public class CruceroPrecioDto
    {
        public string Salida { get; set; } = "";
        public string Tarifa { get; set; } = "";
        public string Cabina { get; set; } = "";
        public decimal Precio_Pp { get; set; }
    }
}
