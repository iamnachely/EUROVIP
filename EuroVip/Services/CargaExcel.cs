using System.ComponentModel.DataAnnotations;

namespace EuroVip.Services
{
    /// <summary>Una fila del Excel en la revisión: si está bien o qué le falta, y el número que tomó al guardarse.</summary>
    public class CargaExcelFila
    {
        public int Fila { get; set; }
        public string Nombre { get; set; } = "";
        public string? Detalle { get; set; }
        public bool Ok { get; set; }
        public string? Mensaje { get; set; }
        public int? Id { get; set; }
    }

    /// <summary>Resultado de «Cargar Excel»: primero se revisa (guardar = false) y luego se guardan las filas correctas.</summary>
    public class CargaExcelResultado
    {
        public List<CargaExcelFila> Filas { get; set; } = new();
        public int Correctas => Filas.Count(f => f.Ok);
        public int ConError => Filas.Count(f => !f.Ok);
        public int Guardadas { get; set; }
        public bool Guardado { get; set; }
        public string Mensaje { get; set; } = "";
    }

    /// <summary>Lo común de las cargas masivas con Excel (Hoteles, Excursiones, Traslados).</summary>
    public static class CargaExcel
    {
        public const long MaxBytes = 10_000_000;
        public const int MaxFilas = 2000;
        public const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>Lee el archivo subido como tabla (ExcelTabla.Leer). Devuelve el error para el usuario si no sirve.</summary>
        public static async Task<(List<ExcelTabla.Fila>? Filas, string? Error)> LeerAsync(Microsoft.AspNetCore.Http.IFormFile? archivo)
        {
            if (archivo is null || archivo.Length == 0) return (null, "Elige el archivo Excel.");
            if (archivo.Length > MaxBytes) return (null, "El Excel pesa más de 10 MB.");
            if (!archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) && !archivo.FileName.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase))
                return (null, "El archivo debe ser .xlsx (Excel moderno). Si es .xls o .csv, ábrelo y guárdalo como .xlsx.");
            try
            {
                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms);
                ms.Position = 0;
                var filas = ExcelTabla.Leer(ms);
                if (filas.Count == 0) return (null, "El Excel no trae filas debajo de los encabezados. Usa la plantilla.");
                if (filas.Count > MaxFilas) return (null, $"El Excel trae {filas.Count} filas: el máximo por carga es {MaxFilas}. Divídelo en varios.");
                return (filas, null);
            }
            catch (Exception ex) { return (null, "No pude leer el Excel: " + ex.Message); }
        }

        /// <summary>Las mismas reglas del formulario ([Required], [Range], [StringLength]…): el primer error o null.</summary>
        public static string? ErrorModelo(object dto)
        {
            var errores = new List<ValidationResult>();
            return Validator.TryValidateObject(dto, new ValidationContext(dto), errores, validateAllProperties: true) ? null : errores.First().ErrorMessage;
        }

        /// <summary>Busca el valor en un diccionario por clave o por su texto («Compartido» o "COMPARTIDO"); null si no está.</summary>
        public static string? Opcion(IReadOnlyDictionary<string, string> opciones, string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            var c = ExcelTabla.Clave(texto);
            foreach (var (k, v) in opciones)
                if (ExcelTabla.Clave(k) == c || ExcelTabla.Clave(v) == c) return k;
            return null;
        }

        public static string Mensaje(CargaExcelResultado r, string uno, string varios) =>
            r.Guardado
                ? $"¡Listo! Se guardaron {r.Guardadas} {(r.Guardadas == 1 ? uno : varios)}" + (r.ConError > 0 ? $". {r.ConError} fila(s) con error no se guardaron." : ".")
                : r.ConError == 0 ? $"Todo correcto: {r.Correctas} {(r.Correctas == 1 ? uno : varios)} para guardar."
                : $"{r.Correctas} fila(s) correctas y {r.ConError} con error. Corrige el Excel y vuelve a subirlo, o guarda solo las correctas.";
    }
}
