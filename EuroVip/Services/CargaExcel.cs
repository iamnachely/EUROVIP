using System.ComponentModel.DataAnnotations;
using System.Text.Json;

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
        /// <summary>Lo que dice cada columna (ya con los cambios de la pantalla y las coincidencias aplicadas): para verla y editarla.</summary>
        public Dictionary<string, string?> Valores { get; set; } = new();
        /// <summary>Datos de lista que no reconocí: lo escrito y las coincidencias para elegir.</summary>
        public List<CargaExcelDuda> Dudas { get; set; } = new();
        /// <summary>Lo que se arregló solo («punta kana» → «Punta Cana»).</summary>
        public List<string> Avisos { get; set; } = new();
        /// <summary>Columna del error (para marcarla en la pantalla), si se sabe.</summary>
        public string? Columna { get; set; }
    }

    public sealed record CargaExcelOpcion(string Valor, string Texto, string? Extra = null);
    public sealed record CargaExcelDuda(string Columna, string Escrito, List<CargaExcelOpcion> Sugerencias);

    /// <summary>Una columna de la plantilla: nombres que acepta y, si es de lista, las opciones válidas (se eligen con Tom Select).</summary>
    public sealed class CargaExcelColumna
    {
        public string Nombre { get; init; } = "";
        [System.Text.Json.Serialization.JsonIgnore] public string[] Alias { get; init; } = Array.Empty<string>();
        /// <summary>Opciones válidas (Valor = lo que se escribe en el Excel). Null = texto libre.</summary>
        public List<CargaExcelOpcion>? Opciones { get; init; }
        /// <summary>Campo del formulario (Id_Destino…) para saber en qué columna está el error.</summary>
        [System.Text.Json.Serialization.JsonIgnore] public string? Campo { get; init; }
        /// <summary>Tipo de dato para el editor: texto, numero, largo.</summary>
        public string Tipo { get; init; } = "texto";
        /// <summary>La plantilla la necesita (si el Excel no trae esa columna se avisa y se puede asignar o escribir fila por fila).</summary>
        public bool Obligatoria { get; init; }
        /// <summary>Atajos que se entienden antes de buscar (RD$ → DOP, x → Sí). Devuelve el texto tal cual si no es un atajo.</summary>
        [System.Text.Json.Serialization.JsonIgnore] public Func<string, string>? Traducir { get; init; }
        [System.Text.Json.Serialization.JsonIgnore] public string[] Nombres => new[] { Nombre }.Concat(Alias).ToArray();
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
        /// <summary>Columnas de la plantilla con sus listas (para ver y editar cada dato en la pantalla).</summary>
        public List<CargaExcelColumna> Columnas { get; set; } = new();
        /// <summary>Datos que se reconocieron por parecido (para avisar que se revisen).</summary>
        public int Arreglados => Filas.Sum(f => f.Avisos.Count);
        public int Dudas => Filas.Sum(f => f.Dudas.Count);
        /// <summary>Cómo se entendieron las columnas del Excel (plantilla distinta, columnas con otro nombre, columnas que faltan).</summary>
        public CargaExcelPlantilla? Plantilla { get; set; }
    }

    /// <summary>Una columna del Excel que no es de la plantilla: su título y a qué columna de la plantilla se parece.</summary>
    public sealed record CargaExcelEncabezado(string Clave, string Titulo, string? Asignada, List<string> Sugerencias, string? Ejemplo);

    /// <summary>
    /// Pedido de la usuaria: «si se carga una plantilla diferente, habla con el usuario, indícale; nunca dar error».
    /// Columnas del Excel que no se reconocieron (para que la usuaria diga cuál es cuál), las que se reconocieron por
    /// parecido y las de la plantilla que faltan.
    /// </summary>
    public sealed class CargaExcelPlantilla
    {
        public List<CargaExcelEncabezado> SinReconocer { get; set; } = new();
        public List<string> Reconocidas { get; set; } = new();
        public List<string> Faltan { get; set; } = new();
        /// <summary>Ninguna columna se parece a la plantilla: probablemente es otro Excel.</summary>
        public bool Distinta { get; set; }
        public string? Mensaje { get; set; }
    }

    /// <summary>
    /// Lo común de las cargas masivas con Excel (Hoteles, Excursiones, Traslados).
    /// Pedido de la usuaria: «la carga de Excel nunca puede fallar; si no reconoces algo, busca coincidencias y deja elegir
    /// con Tom Select; ayuda al usuario, no va a escribir las cosas tal cual». Por eso:
    ///   1) Ayudar(): antes de revisar, cada columna de lista (destino, proveedor, moneda, campaña…) se compara con lo que
    ///      existe en la base EN ESE MOMENTO. Igual → bien. Un solo parecido claro → se cambia solo y se avisa. Varios o
    ///      ninguno → «duda» con las coincidencias para elegir.
    ///   2) La pantalla manda «cambios» (fila → columna → valor) y «omitir» (filas que no se cargan): se aplican sobre el Excel
    ///      sin tener que editarlo y volver a subirlo.
    ///   3) Corregido(): el mismo Excel con todo lo arreglado, para guardarlo.
    /// </summary>
    public static class CargaExcel
    {
        public const long MaxBytes = 10_000_000;
        public const int MaxFilas = 2000;
        public const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>Lee el archivo subido como tabla (ExcelTabla.Leer) y le aplica lo corregido en la pantalla. Devuelve el error para el usuario si no sirve.</summary>
        public static async Task<(List<ExcelTabla.Fila>? Filas, string? Error)> LeerAsync(Microsoft.AspNetCore.Http.IFormFile? archivo, string? cambios = null, string? omitir = null)
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
                return (AplicarCambios(filas, cambios, omitir), null);
            }
            catch (Exception ex) { return (null, "No pude leer el Excel: " + ex.Message); }
        }

        /// <summary>Aplica lo que la usuaria corrigió en la pantalla: cambios por fila/columna y filas que no quiere cargar.</summary>
        public static List<ExcelTabla.Fila> AplicarCambios(List<ExcelTabla.Fila> filas, string? cambiosJson, string? omitirJson)
        {
            var omitir = new HashSet<int>();
            try { if (!string.IsNullOrWhiteSpace(omitirJson)) omitir = JsonSerializer.Deserialize<HashSet<int>>(omitirJson) ?? new(); } catch (JsonException) { }
            Dictionary<string, Dictionary<string, string?>>? cambios = null;
            try { if (!string.IsNullOrWhiteSpace(cambiosJson)) cambios = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string?>>>(cambiosJson); } catch (JsonException) { }
            if (cambios is not null)
                foreach (var f in filas)
                    if (cambios.TryGetValue(f.Numero.ToString(), out var c))
                        foreach (var (col, val) in c)
                        {
                            var k = ExcelTabla.Clave(col);
                            if (k == "") continue;
                            if (string.IsNullOrWhiteSpace(val)) f.Valores.Remove(k); else f.Valores[k] = val.Trim();
                        }
            return filas.Where(f => !omitir.Contains(f.Numero)).ToList();
        }

        /// <summary>
        /// Paso 0: entender las columnas del Excel aunque no sea la plantilla.
        ///   • «mapa» (de la pantalla): { claveColumnaExcel: "Nombre de la plantilla" | "" (no usar) } → se renombran.
        ///   • Columnas con otro nombre («Precio x persona», «Hotel Nombre») → se busca a qué columna de la plantilla se parece;
        ///     si es clara, se usa sola y se avisa; si no, queda en SinReconocer con sugerencias para que la usuaria elija.
        ///   • Columnas obligatorias que no están → Faltan (se puede asignar otra columna o escribir el dato fila por fila).
        /// Nunca falla: si el Excel es otro, lo dice y deja asignar las columnas.
        /// </summary>
        public static CargaExcelPlantilla Encabezados(List<ExcelTabla.Fila> filas, IReadOnlyList<CargaExcelColumna> columnas, string? mapaJson)
        {
            var p = new CargaExcelPlantilla();
            var titulos = filas.FirstOrDefault()?.Encabezados ?? new Dictionary<string, string>();
            var conocidas = columnas.SelectMany(c => c.Nombres.Select(n => (Clave: ExcelTabla.Clave(n), Col: c))).GroupBy(x => x.Clave).ToDictionary(g => g.Key, g => g.First().Col);
            Dictionary<string, string>? mapa = null;
            try { if (!string.IsNullOrWhiteSpace(mapaJson)) mapa = JsonSerializer.Deserialize<Dictionary<string, string>>(mapaJson); } catch (JsonException) { }

            void Renombrar(string de, string? a)
            {
                foreach (var f in filas)
                {
                    if (!f.Valores.Remove(de, out var v)) continue;
                    if (a is not null && ExcelTabla.Clave(a) is { Length: > 0 } k && !f.Valores.ContainsKey(k)) f.Valores[k] = v;
                }
            }

            // Opciones para comparar títulos: el nombre de cada columna y sus otros nombres
            var opciones = columnas.SelectMany(c => c.Nombres.Select(n => new CargaExcelOpcion(c.Nombre, n))).ToList();
            var usadas = new HashSet<string>(titulos.Keys.Where(conocidas.ContainsKey).Select(k => conocidas[k].Nombre));
            foreach (var (clave, titulo) in titulos)
            {
                if (conocidas.ContainsKey(clave)) continue;
                var ejemplo = filas.Select(f => f.Valores.GetValueOrDefault(clave)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (mapa is not null && mapa.TryGetValue(clave, out var elegida))
                {
                    var col = columnas.FirstOrDefault(c => c.Nombre == elegida);
                    Renombrar(clave, col?.Nombre);
                    if (col is not null) { usadas.Add(col.Nombre); p.Reconocidas.Add($"«{titulo}» → {col.Nombre}"); }
                    p.SinReconocer.Add(new CargaExcelEncabezado(clave, titulo, col?.Nombre ?? "", new(), ejemplo));
                    continue;
                }
                var (sola, sug) = Coincidencias.Elegir(titulo, opciones.Where(o => !usadas.Contains(o.Valor)).ToList());
                if (sola is not null)
                {
                    Renombrar(clave, sola.Valor); usadas.Add(sola.Valor);
                    p.Reconocidas.Add($"«{titulo}» → {sola.Valor}");
                    p.SinReconocer.Add(new CargaExcelEncabezado(clave, titulo, sola.Valor, new(), ejemplo));
                }
                else p.SinReconocer.Add(new CargaExcelEncabezado(clave, titulo, null, sug.Select(s => s.Valor).Distinct().ToList(), ejemplo));
            }
            p.Faltan = columnas.Where(c => c.Obligatoria && !usadas.Contains(c.Nombre)).Select(c => c.Nombre).ToList();
            var obligatorias = columnas.Count(c => c.Obligatoria);
            p.Distinta = usadas.Count == 0 || (obligatorias > 0 && p.Faltan.Count == obligatorias);
            var porAsignar = p.SinReconocer.Count(s => s.Asignada is null);
            p.Mensaje = p.Distinta
                ? $"Este Excel no parece la plantilla: sus columnas ({string.Join(", ", titulos.Values.Take(8))}{(titulos.Count > 8 ? "…" : "")}) no son las que espero. Dime abajo qué es cada columna, o descarga la plantilla y copia ahí tus datos."
                : porAsignar > 0 || p.Faltan.Count > 0
                    ? (porAsignar > 0 ? $"Hay {porAsignar} columna(s) que no reconozco: dime abajo qué es cada una (o «No usar»). " : "")
                      + (p.Faltan.Count > 0 ? $"Faltan columnas de la plantilla: {string.Join(", ", p.Faltan)}. Si están con otro nombre, asígnalas abajo; si no, escribe ese dato en cada fila con «Corregir»." : "")
                    : null;
            return p;
        }

        /// <summary>
        /// Paso 1 de la ayuda: para cada fila y cada columna de lista, deja el valor tal cual está en Eurovip si lo reconoce
        /// (igual o un solo parecido claro) y si no, apunta la duda con las coincidencias. Devuelve la info por número de fila.
        /// </summary>
        public static Dictionary<int, CargaExcelFila> Ayudar(List<ExcelTabla.Fila> filas, IReadOnlyList<CargaExcelColumna> columnas)
        {
            var r = new Dictionary<int, CargaExcelFila>();
            // Lo mismo escrito en la misma columna se busca una sola vez (hay listas de miles: destinos, proveedores)
            var memoria = new Dictionary<(string, string), (CargaExcelOpcion?, List<CargaExcelOpcion>)>();
            foreach (var f in filas)
            {
                var info = new CargaExcelFila { Fila = f.Numero };
                foreach (var c in columnas.Where(c => c.Opciones is { Count: > 0 }))
                {
                    var escrito = f.Texto(c.Nombres);
                    if (escrito is null) continue;
                    var buscado = c.Traducir?.Invoke(escrito) ?? escrito;
                    var llave = (c.Nombre, Parecidos.Compacto(buscado));
                    if (!memoria.TryGetValue(llave, out var hallado)) memoria[llave] = hallado = Coincidencias.Elegir(buscado, c.Opciones!);
                    var (opcion, sugerencias) = hallado;
                    if (opcion is not null)
                    {
                        Poner(f, c, opcion.Valor);
                        if (Parecidos.Compacto(escrito) is var ec && ec != Parecidos.Compacto(opcion.Valor) && ec != Parecidos.Compacto(opcion.Texto))
                            info.Avisos.Add($"{c.Nombre}: «{escrito}» → «{Mostrar(opcion)}»");
                    }
                    else info.Dudas.Add(new CargaExcelDuda(c.Nombre, escrito, sugerencias));
                }
                r[f.Numero] = info;
            }
            return r;
        }

        /// <summary>Escribe el valor en la columna (en el nombre principal; quita los alias para que no se lea el viejo).</summary>
        private static void Poner(ExcelTabla.Fila f, CargaExcelColumna c, string valor)
        {
            foreach (var n in c.Alias) f.Valores.Remove(ExcelTabla.Clave(n));
            f.Valores[ExcelTabla.Clave(c.Nombre)] = valor;
        }

        private static string Mostrar(CargaExcelOpcion o) => ExcelTabla.Clave(o.Valor) == ExcelTabla.Clave(o.Texto) ? o.Texto : $"{o.Valor} · {o.Texto}";

        /// <summary>
        /// Paso final: pasa a cada fila del resultado sus valores (para verlos y editarlos), sus dudas y avisos, y la columna
        /// del error. Si una fila tiene dudas, el mensaje dice qué elegir en vez del error técnico.
        /// </summary>
        public static void Completar(CargaExcelResultado r, List<ExcelTabla.Fila> filas, IReadOnlyList<CargaExcelColumna> columnas, Dictionary<int, CargaExcelFila> ayuda, bool sinListas = false, CargaExcelPlantilla? plantilla = null)
        {
            r.Plantilla = plantilla;
            // Las listas (miles de destinos) se mandan solo la primera vez: la pantalla las guarda
            r.Columnas = sinListas ? columnas.Select(c => new CargaExcelColumna { Nombre = c.Nombre, Tipo = c.Tipo }).ToList() : columnas.ToList();
            var porNumero = filas.ToDictionary(f => f.Numero);
            foreach (var fila in r.Filas)
            {
                if (porNumero.TryGetValue(fila.Fila, out var f))
                    foreach (var c in columnas) fila.Valores[c.Nombre] = f.Texto(c.Nombres);
                if (!ayuda.TryGetValue(fila.Fila, out var a)) continue;
                fila.Avisos = a.Avisos;
                fila.Dudas = a.Dudas;
                if (fila.Ok || a.Dudas.Count == 0) continue;
                var d = a.Dudas[0];
                fila.Columna = d.Columna;
                fila.Mensaje = d.Sugerencias.Count switch
                {
                    0 => $"No encontré «{d.Escrito}» en {d.Columna}. Búscalo y elígelo en la lista.",
                    1 => $"{d.Columna}: ¿«{d.Escrito}» es «{Mostrar(d.Sugerencias[0])}»? Confírmalo o elige otro.",
                    _ => $"{d.Columna}: «{d.Escrito}» se parece a {d.Sugerencias.Count} opciones. Elige la correcta."
                } + (a.Dudas.Count > 1 ? $" (y {a.Dudas.Count - 1} dato(s) más por elegir)" : "");
            }
        }

        /// <summary>Marca la columna del error a partir del campo del formulario que devolvió la revisión (Id_Destino → Destino).</summary>
        public static void Marcar(CargaExcelFila fila, string? campo, IReadOnlyList<CargaExcelColumna> columnas) =>
            fila.Columna ??= campo is null ? null : columnas.FirstOrDefault(c => string.Equals(c.Campo, campo, StringComparison.OrdinalIgnoreCase))?.Nombre;

        /// <summary>El Excel con todo lo arreglado (cambios de la pantalla y coincidencias), en la forma de la plantilla.</summary>
        public static byte[] Corregido(string hoja, List<ExcelTabla.Fila> filas, IReadOnlyList<CargaExcelColumna> columnas, params ExcelTabla.Hoja[] otras)
        {
            var datos = new ExcelTabla.Hoja(hoja, columnas.Select(c => c.Nombre).ToList(),
                filas.Select(f => columnas.Select(c => (object?)f.Texto(c.Nombres)).ToArray()).ToList())
            {
                Listas = Listas(columnas)
            };
            return ExcelTabla.Crear(new[] { datos }.Concat(otras).ToArray());
        }

        /// <summary>Listas desplegables de la plantilla a partir de las columnas de lista (con lo que hay en la base al descargarla).</summary>
        public static Dictionary<string, IReadOnlyList<string>> Listas(IEnumerable<CargaExcelColumna> columnas) =>
            columnas.Where(c => c.Opciones is { Count: > 0 }).ToDictionary(c => c.Nombre, c => (IReadOnlyList<string>)c.Opciones!.Select(o => o.Valor).Distinct().ToList());

        /// <summary>Las 6 columnas opcionales de información para el cliente (CAMBIO 130), al final de las plantillas.</summary>
        public static readonly string[] NombresInfo = { "Incluye", "No incluye", "Condiciones", "Documentación", "Importante", "Oferta especial" };

        public static IEnumerable<CargaExcelColumna> ColumnasInfo() => new CargaExcelColumna[]
        {
            new() { Nombre = "Incluye", Tipo = "largo" }, new() { Nombre = "No incluye", Tipo = "largo" },
            new() { Nombre = "Condiciones", Tipo = "largo" }, new() { Nombre = "Documentación", Alias = new[] { "Documentacion" }, Tipo = "largo" },
            new() { Nombre = "Importante", Tipo = "largo" }, new() { Nombre = "Oferta especial", Alias = new[] { "Oferta" }, Tipo = "largo" },
        };

        /// <summary>La información para el cliente de una fila del Excel (columnas vacías = sin texto).</summary>
        public static InfoClienteDto InfoDeFila(ExcelTabla.Fila f) => new()
        {
            Incluye = f.Texto("Incluye"), No_Incluye = f.Texto("No incluye"), Condiciones = f.Texto("Condiciones"),
            Documentacion = f.Texto("Documentación", "Documentacion"), Importante = f.Texto("Importante"), Oferta_Especial = f.Texto("Oferta especial", "Oferta")
        };

        /// <summary>Fila de la hoja Instrucciones que explica las columnas de información.</summary>
        public static object?[] AyudaInfo(int columnas) =>
            new object?[] { "Incluye … Oferta especial", "Opcionales. Información para el cliente que se ve en la web (Incluye y No incluye: una cosa por línea)." }
                .Concat(Enumerable.Repeat<object?>(null, Math.Max(0, columnas - 2))).ToArray();

        /// <summary>Opciones Sí / No.</summary>
        public static List<CargaExcelOpcion> SiNo() => new() { new("Sí", "Sí"), new("No", "No") };

        /// <summary>x, 1, s, si, true, verdadero, yes → Sí; 0, n, false, falso → No.</summary>
        public static string TraducirSiNo(string t) => ExcelTabla.Clave(t) switch
        {
            "si" or "s" or "x" or "1" or "true" or "verdadero" or "yes" or "y" => "Sí",
            "no" or "n" or "0" or "false" or "falso" => "No",
            _ => t
        };

        /// <summary>Columna Sí / No.</summary>
        public static CargaExcelColumna ColumnaSiNo(string nombre, params string[] alias) =>
            new() { Nombre = nombre, Alias = alias, Opciones = SiNo(), Traducir = TraducirSiNo };

        /// <summary>Columna Moneda con las monedas de la base (RD$, US$, € y nombres se entienden).</summary>
        public static CargaExcelColumna ColumnaMoneda(IEnumerable<string> monedas, string campo = "Id_Moneda") => new()
        {
            Nombre = "Moneda", Alias = new[] { "Divisa" }, Campo = campo,
            Opciones = monedas.OrderBy(m => m).Select(m => new CargaExcelOpcion(m, m)).ToList(),
            Traducir = t => CatalogoCargas.Moneda(t, new HashSet<string>(monedas)).Moneda
        };

        /// <summary>Columna Proveedor (se escribe el nombre; se entiende también el código).</summary>
        public static CargaExcelColumna ColumnaProveedor(IEnumerable<CatalogoCargas.Proveedor> lista) => new()
        {
            Nombre = "Proveedor", Campo = "Id_Proveedor",
            Opciones = lista.Select(p => new CargaExcelOpcion(p.Nombre, p.Nombre, p.Id)).ToList()
        };

        /// <summary>Opciones de un diccionario código → texto (Tipo, Categoría…): en el Excel se escribe el texto.</summary>
        public static List<CargaExcelOpcion> DeDiccionario(IReadOnlyDictionary<string, string> d) => d.Select(x => new CargaExcelOpcion(x.Value, x.Value, x.Key)).ToList();

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
            (r.Guardado
                ? $"¡Listo! Se guardaron {r.Guardadas} {(r.Guardadas == 1 ? uno : varios)}" + (r.ConError > 0 ? $". {r.ConError} fila(s) no se guardaron: corrígelas aquí mismo y vuelve a guardar." : ".")
                : r.ConError == 0 ? $"Todo correcto: {r.Correctas} {(r.Correctas == 1 ? uno : varios)} para guardar."
                : $"{r.Correctas} fila(s) correctas y {r.ConError} por corregir. Corrígelas aquí mismo (botón «Corregir») y pulsa «Revisar de nuevo»: no hace falta volver a subir el Excel.")
            + (r.Arreglados > 0 && !r.Guardado ? $" Reconocí {r.Arreglados} dato(s) por parecido: revísalos (en amarillo)." : "");
    }
}
