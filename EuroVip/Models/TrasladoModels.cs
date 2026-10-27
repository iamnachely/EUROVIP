using System.ComponentModel.DataAnnotations;

namespace EuroVip.Models
{
    // ======================= TRASLADOS (Eurovip.traslado + Eurovip.traslado_alojamiento) =======================

    /// <summary>Valores permitidos (los mismos CHECK de la tabla) y su texto para la pantalla.</summary>
    public static class TrasladoValores
    {
        public static readonly Dictionary<string, string> Tipos = new() { ["COMPARTIDO"] = "Compartido", ["PRIVADO"] = "Privado" };
        public static readonly Dictionary<string, string> Categorias = new() { ["ESTANDAR"] = "Estándar", ["PREMIUM"] = "Premium", ["LUJO"] = "Lujo" };
        public static readonly Dictionary<string, string> Vehiculos = new() { ["BUS"] = "Autobús", ["MINIBUS"] = "Minibús", ["VAN"] = "Van", ["COCHE"] = "Coche", ["LIMUSINA"] = "Limusina" };
        public static readonly Dictionary<string, string> Cobros = new() { ["VEHICULO"] = "Para todos los viajeros", ["PERSONA"] = "Por persona" };
    }

    /// <summary>Un traslado tal como se lista en el mantenimiento.</summary>
    public class Traslado
    {
        public int Id_Traslado { get; set; }
        public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string Vehiculo { get; set; } = "";
        public string Id_Estacion_Origen { get; set; } = "";
        public string? Origen_Nombre { get; set; }
        public string Id_Destino { get; set; } = "";
        public string? Id_Pais { get; set; }
        public short Maletas { get; set; }
        public short? Duracion_Min { get; set; }
        public short Pasajeros_Min { get; set; }
        public short Pasajeros_Max { get; set; }
        public string Cobro { get; set; } = "VEHICULO";
        public decimal? Precio_Ida { get; set; }
        public decimal? Precio_Ida_Vuelta { get; set; }
        /// <summary>Prioridad (FASE 29): 1 sale primero en la web, 2 segundo… null = sin prioridad (por precio).</summary>
        [Range(1, 999, ErrorMessage = "La prioridad va de 1 a 999")] public short? Peso { get; set; }
        /// <summary>Precio del proveedor en su web (informativo: nuestro precio no cambia).</summary>
        [Range(0, 100_000_000, ErrorMessage = "El precio del proveedor tiene que ser 0 o más")] public decimal? Precio_Proveedor { get; set; }
        public string Id_Moneda { get; set; } = "DOP";
        public bool Cancelable { get; set; }
        public short? Horas_Cancelacion { get; set; }
        public string? Descripcion { get; set; }
        /// <summary>Archivo en wwwroot/imagenes/traslados (columna imagen).</summary>
        public string? Archivo { get; set; }
        /// <summary>URL de la foto si el archivo existe.</summary>
        public string? Imagen { get; set; }
        public bool Todos_Hoteles { get; set; }
        public bool Activo { get; set; }
        public string? Usuario { get; set; }
        public DateTime Fecha_Alta { get; set; }
        public DateTime? Fecha_Modificacion { get; set; }
        /// <summary>Hoteles (solo si Todos_Hoteles = false).</summary>
        public List<Opcion> Hoteles { get; set; } = new();
    }

    /// <summary>Lo que manda la pantalla al crear o editar.</summary>
    public class TrasladoGuardarDto
    {
        public int? Id_Traslado { get; set; }

        [Required(ErrorMessage = "Escribe el nombre del traslado"), StringLength(150, ErrorMessage = "Máximo 150 caracteres")]
        public string Nombre { get; set; } = "";

        [Required(ErrorMessage = "Elige el tipo")] public string Tipo { get; set; } = "COMPARTIDO";
        [Required(ErrorMessage = "Elige la categoría")] public string Categoria { get; set; } = "ESTANDAR";
        [Required(ErrorMessage = "Elige el vehículo")] public string Vehiculo { get; set; } = "BUS";

        [Required(ErrorMessage = "Elige el aeropuerto de salida"), StringLength(10)]
        public string Id_Estacion_Origen { get; set; } = "";

        [Required(ErrorMessage = "Elige el destino"), StringLength(50)]
        public string Id_Destino { get; set; } = "";

        [Range(0, 50, ErrorMessage = "Maletas: de 0 a 50")] public short Maletas { get; set; } = 2;
        [Range(1, 1440, ErrorMessage = "Duración: de 1 a 1440 minutos")] public short? Duracion_Min { get; set; }
        [Range(1, 999, ErrorMessage = "Mínimo de pasajeros: 1 o más")] public short Pasajeros_Min { get; set; } = 1;
        [Range(1, 999, ErrorMessage = "Máximo de pasajeros: 1 o más")] public short Pasajeros_Max { get; set; } = 1;

        [Required] public string Cobro { get; set; } = "VEHICULO";
        [Range(0, 10_000_000, ErrorMessage = "El precio tiene que ser 0 o más")] public decimal? Precio_Ida { get; set; }
        [Range(0, 10_000_000, ErrorMessage = "El precio tiene que ser 0 o más")] public decimal? Precio_Ida_Vuelta { get; set; }

        public bool Cancelable { get; set; } = true;
        [Range(0, 720, ErrorMessage = "Horas: de 0 a 720")] public short? Horas_Cancelacion { get; set; }
        public string? Descripcion { get; set; }
        public bool Todos_Hoteles { get; set; } = true;
        public List<int> Hoteles { get; set; } = new();
        public bool Activo { get; set; } = true;
        /// <summary>Prioridad (FASE 29): 1 sale primero en la web, 2 segundo… null = sin prioridad (por precio).</summary>
        [Range(1, 999, ErrorMessage = "La prioridad va de 1 a 999")] public short? Peso { get; set; }
        /// <summary>Precio del proveedor en su web (informativo: nuestro precio no cambia).</summary>
        [Range(0, 100_000_000, ErrorMessage = "El precio del proveedor tiene que ser 0 o más")] public decimal? Precio_Proveedor { get; set; }
    }
}
