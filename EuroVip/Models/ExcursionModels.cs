using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    // ======================= EXCURSIONES (Eurovip.servicio_excursion, copia de ViajeVDT) =======================

    /// <summary>Una excursión tal como se lista en el mantenimiento (con su foto, si tiene).</summary>
    public class Excursion
    {
        public int Id_Excursion { get; set; }
        public string? Id_Destino { get; set; }
        public string? Id_Tipo_Servicio { get; set; }
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
        public bool? Cancelable { get; set; }
        public decimal? Precio { get; set; }
        /// <summary>URL de la foto (wwwroot/imagenes/excursiones/{Nombre sin espacios}.webp) o null.</summary>
        public string? Imagen { get; set; }
    }

    /// <summary>Un destino con excursiones: la cabecera de su sección en la pantalla.</summary>
    public class ExcursionDestino
    {
        public string Destino { get; set; } = "";
        public string? Pais { get; set; }
        public string? Caratula { get; set; }
        /// <summary>Foto de portada de la sección (foto del destino o fondo de flyer); null = degradado o foto de una excursión.</summary>
        public string? Imagen { get; set; }
    }

    /// <summary>Lo que manda la pantalla al crear o editar.</summary>
    public class ExcursionGuardarDto
    {
        public int? Id_Excursion { get; set; }

        [Required(ErrorMessage = "Escribe el nombre de la excursión")]
        [StringLength(500, ErrorMessage = "El nombre admite como máximo 500 caracteres")]
        public string Nombre { get; set; } = "";

        [Required(ErrorMessage = "Elige el destino")]
        [StringLength(50)]
        public string Id_Destino { get; set; } = "";

        [Required(ErrorMessage = "Escribe el precio")]
        [Range(0, 1_000_000, ErrorMessage = "El precio tiene que ser 0 o más")]
        public decimal? Precio { get; set; }

        public bool Cancelable { get; set; }

        public string? Descripcion { get; set; }
    }
}
