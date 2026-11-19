using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Mantenimiento de traslados (Eurovip.traslado): ver, crear, editar, duplicar, activar/desactivar, eliminar y foto.
    /// Igual que la búsqueda del tour operador: Desde (aeropuerto) → A (destino u hoteles), ida y vuelta o solo ida.
    /// Fotos en Eurovip.multimedia (tipo TRASLADO, carpeta traslados/{id}/): portada aquí y galería con el componente de fotos.
    /// Si un traslado aún no tiene portada nueva se muestra la de antes (wwwroot/imagenes/traslados/{traslado.imagen}).
    /// La web busca con Eurovip.spPortalBuscarTraslados.
    /// </summary>
    public class TrasladosController : Controller
    {
        private const string TipoImagen = "traslados";
        private readonly ITrasladoRepository _repo;
        private readonly ICircuitoRepository _aud;
        private readonly IImagenesLocales _img;
        private readonly ILogger<TrasladosController> _logger;

        private readonly IMultimedia _mm;
        private const string TipoMm = "TRASLADO";

        public TrasladosController(ITrasladoRepository repo, ICircuitoRepository aud, IImagenesLocales img, ILogger<TrasladosController> logger, IMultimedia mm)
        {
            _mm = mm;
            _repo = repo;
            _aud = aud;
            _img = img;
            _logger = logger;
        }

        public IActionResult Index() => View();

        private static string Archivo(int id) => $"traslado{id}.webp";

        private Traslado ConImagen(Traslado t, IReadOnlyDictionary<string, string> portadas)
        {
            t.Imagen = portadas.TryGetValue(t.Id_Traslado.ToString(), out var u) ? u
                       : string.IsNullOrWhiteSpace(t.Archivo) ? null : _img.BuscarArchivo(TipoImagen, t.Archivo)?.Url;
            return t;
        }

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var portadas = await _mm.PortadasAsync(TipoMm);
            return Json(new
            {
                traslados = (await _repo.ListarAsync()).Select(t => ConImagen(t, portadas)),
                valores = new { tipos = TrasladoValores.Tipos, categorias = TrasladoValores.Categorias, vehiculos = TrasladoValores.Vehiculos, cobros = TrasladoValores.Cobros }
            });
        }

        [HttpGet] public async Task<IActionResult> Destinos(string? q) => Json(await _repo.DestinosAsync(q));
        [HttpGet] public async Task<IActionResult> Aeropuertos(string? q) => Json(await _repo.AeropuertosAsync(q));
        [HttpGet] public async Task<IActionResult> Hoteles(string? destino, string? q, string? preferido) => Json(await _repo.HotelesAsync(destino, q, preferido));

        private BadRequestObjectResult Mal(string campo, string mensaje) =>
            BadRequest(new { mensaje, errores = new Dictionary<string, string> { [campo] = mensaje } });

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] TrasladoGuardarDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key, x => x.Value!.Errors.First().ErrorMessage);
                return BadRequest(new { mensaje = errores.Values.First(), errores });
            }
            // 20/11/2026: el traslado va de un aeropuerto a un HOTEL (el destino sale del hotel): ya no se guarda «todos los hoteles del destino»
            if (dto.Todos_Hoteles) return Mal("Hoteles", "Elige el hotel al que llega el traslado.");
            if (await RevisarAsync(dto) is var (campo, mal)) return Mal(campo, mal);
            if (await Guardado.MinimoTrasladoAsync(dto) is { } bajo)   // CAMBIO 139: precio mínimo de la vista Traslados (ida y vuelta y solo ida)
                return BadRequest(bajo.Respuesta());

            try
            {
                var antes = dto.Id_Traslado is int idAntes ? await _repo.ObtenerAsync(idAntes) : null;
                if (dto.Id_Traslado is not null && antes is null) return NotFound(new { mensaje = "Ese traslado ya no existe. Recarga la página." });
                // guardar + información para el cliente (CAMBIO 130) + imagen por defecto: el mismo camino que las cargas de proveedores (CAMBIO 140)
                var id = await Guardado.GuardarTrasladoAsync(dto, User.Identity?.Name ?? "desconocido", antes);
                await Auditar(antes is null ? "Traslado_Crear" : "Traslado_Editar", id, new { antes, despues = dto });
                return Json(new { ok = true, id, traslado = ConImagen((await _repo.ObtenerAsync(id))!, await _mm.PortadasAsync(TipoMm)), mensaje = antes is null ? $"¡Listo! Se creó «{dto.Nombre}»." : "Cambios guardados." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando el traslado {Nombre}", dto.Nombre);
                return StatusCode(500, new { mensaje = "No se pudo guardar: " + ex.Message });
            }
        }

        /// <summary>
        /// Reglas para guardar un traslado (las mismas en el formulario y en Cargar Excel). Deja los códigos en mayúsculas y el destino
        /// como está en Eurovip.destino. Devuelve el campo y el error, o null si está bien.
        /// </summary>
        /// <remarks>CAMBIO 140: las reglas viven en GuardadoProductos (las usa también la publicación de las cargas de proveedores).</remarks>
        private Task<(string Campo, string Mensaje)?> RevisarAsync(TrasladoGuardarDto dto, ISet<string>? monedas = null) =>
            Guardado.RevisarTrasladoAsync(dto, monedas);

        private GuardadoProductos Guardado => HttpContext.RequestServices.GetRequiredService<GuardadoProductos>();

        // ================= CARGAR EXCEL (muchos traslados de una vez) =================
        private static readonly string[] Columnas = new[]
        {
            "Nombre", "Tipo", "Categoría", "Vehículo", "Aeropuerto", "Hotel", "Maletas", "Duración (min)", "Pasajeros mín", "Pasajeros máx",
            "Cobro", "Moneda", "Precio ida y vuelta", "Precio solo ida", "Cancelable", "Horas cancelación", "Código interno", "Proveedor", "Descripción", "Activo", "Prioridad", "Precio proveedor"
        }.Concat(CargaExcel.NombresInfo).ToArray();

        // ---- Hoteles del Excel (20/11/2026: de aeropuerto a HOTEL; ya no hay columna Destino) ----
        private sealed record HotelExcel(int Id, string Nombre, string Destino, string Etiqueta);

        private static string Norm(string? s)
        {
            var d = (s ?? "").Trim().Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in d) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>Hoteles activos con la etiqueta que se escribe en el Excel: el nombre, o «Nombre · Destino» si hay dos con el mismo nombre.</summary>
        private static async Task<List<HotelExcel>> HotelesExcelAsync(System.Data.IDbConnection cn)
        {
            var filas = (await cn.QueryAsync<(int Id, string? Nombre, string? Destino)>(@"SELECT a.id_alojamiento, LTRIM(RTRIM(a.nombre)), LTRIM(RTRIM(a.id_destino))
                FROM Eurovip.alojamiento a WHERE ISNULL(a.activo, 1) = 1 ORDER BY a.nombre")).Where(f => !string.IsNullOrWhiteSpace(f.Nombre)).ToList();
            var repetidos = filas.GroupBy(f => Norm(f.Nombre)).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            return filas.Select(f => new HotelExcel(f.Id, f.Nombre!, f.Destino ?? "", repetidos.Contains(Norm(f.Nombre)) ? $"{f.Nombre} · {f.Destino}" : f.Nombre!)).ToList();
        }

        private static (HotelExcel? Hotel, string? Error) BuscarHotel(string? texto, List<HotelExcel> hoteles)
        {
            var t = (texto ?? "").Trim();
            if (t == "") return (null, "Escribe el hotel al que llega el traslado (columna Hotel).");
            if (int.TryParse(t, out var id) && hoteles.FirstOrDefault(h => h.Id == id) is { } porId) return (porId, null);
            var exacto = hoteles.Where(h => Norm(h.Etiqueta) == Norm(t)).ToList();
            if (exacto.Count == 1) return (exacto[0], null);
            var porNombre = hoteles.Where(h => Norm(h.Nombre) == Norm(t)).ToList();
            if (porNombre.Count == 1) return (porNombre[0], null);
            if (porNombre.Count > 1) return (null, $"Hay {porNombre.Count} hoteles llamados «{t}»: escribe «Nombre · Destino».");
            return (null, $"No encontramos el hotel «{t}». Elígelo de la lista.");
        }

        /// <summary>Columnas del Excel con sus listas (lo que hay en la base AHORA): para la plantilla, las coincidencias y el editor de filas.</summary>
        private async Task<List<CargaExcelColumna>> ColumnasExcelAsync()
        {
            using var cn = _aud.Conexion();
            var aeropuertos = (await cn.QueryAsync<(string Codigo, string? Nombre, string? Destino)>(@"
                SELECT LTRIM(RTRIM(e.id_estacion)), MIN(e.nombre), MIN(LTRIM(RTRIM(e.id_destino))) FROM Eurovip.estacion e
                WHERE e.id_pais = 'DO' OR EXISTS (SELECT 1 FROM Eurovip.traslado t WHERE t.id_estacion_origen = e.id_estacion)
                GROUP BY e.id_estacion ORDER BY 1")).Select(a => new CargaExcelOpcion(a.Codigo, a.Nombre ?? a.Codigo, a.Destino)).ToList();
            var hotelesX = await HotelesExcelAsync(cn);
            var monedas = await CatalogoCargas.MonedasAsync(cn);
            var proveedores = await CatalogoCargas.ProveedoresAsync(cn);
            CargaExcelColumna Num(string n, string campo, params string[] alias) => new() { Nombre = n, Alias = alias, Campo = campo, Tipo = "numero" };
            return new List<CargaExcelColumna>
            {
                new() { Nombre = "Nombre", Alias = new[] { "Traslado" }, Campo = "Nombre", Obligatoria = true },
                new() { Nombre = "Tipo", Campo = "Tipo", Obligatoria = true, Opciones = CargaExcel.DeDiccionario(TrasladoValores.Tipos) },
                new() { Nombre = "Categoría", Alias = new[] { "Categoria" }, Campo = "Categoria", Opciones = CargaExcel.DeDiccionario(TrasladoValores.Categorias) },
                new() { Nombre = "Vehículo", Alias = new[] { "Vehiculo" }, Campo = "Vehiculo", Opciones = CargaExcel.DeDiccionario(TrasladoValores.Vehiculos) },
                new() { Nombre = "Aeropuerto", Alias = new[] { "Aeropuerto de salida", "Desde" }, Campo = "Id_Estacion_Origen", Opciones = aeropuertos, Obligatoria = true },
                new() { Nombre = "Hotel", Alias = new[] { "Hotel de destino", "A", "Hasta" }, Campo = "Hoteles", Obligatoria = true,
                        Opciones = hotelesX.Select(h => new CargaExcelOpcion(h.Etiqueta, h.Etiqueta, h.Destino)).ToList() },
                Num("Maletas", "Maletas"), Num("Duración (min)", "Duracion_Min", "Duración", "Minutos"),
                Num("Pasajeros mín", "Pasajeros_Min", "Pasajeros minimo"), Num("Pasajeros máx", "Pasajeros_Max", "Pasajeros maximo"),
                new() { Nombre = "Cobro", Campo = "Cobro", Opciones = CargaExcel.DeDiccionario(TrasladoValores.Cobros) },
                CargaExcel.ColumnaMoneda(monedas),
                Num("Precio ida y vuelta", "Precio_Ida_Vuelta"), Num("Precio solo ida", "Precio_Ida", "Precio ida"),
                CargaExcel.ColumnaSiNo("Cancelable"), Num("Horas cancelación", "Horas_Cancelacion", "Horas cancelacion"),
                new() { Nombre = "Código interno", Alias = new[] { "Código", "Codigo", "Codigo interno" }, Campo = "Codigo" },
                CargaExcel.ColumnaProveedor(proveedores),
                new() { Nombre = "Descripción", Alias = new[] { "Descripcion" }, Campo = "Descripcion", Tipo = "largo" },
                CargaExcel.ColumnaSiNo("Activo"),
                Num("Prioridad", "Peso", "Peso"), Num("Precio proveedor", "Precio_Proveedor", "Precio del proveedor"),
            }.Concat(CargaExcel.ColumnasInfo()).ToList();   // + Incluye … Oferta especial (opcionales)
        }

        /// <summary>Plantilla: hoja «Traslados» (una fila por traslado) y hoja «Instrucciones» con los valores válidos.</summary>
        [HttpGet]
        public async Task<IActionResult> Plantilla()
        {
            using var cn = _aud.Conexion();
            var aeropuertos = (await cn.QueryAsync<(string Codigo, string? Nombre, string? Destino)>(@"
                SELECT e.id_estacion, MIN(e.nombre), MIN(LTRIM(RTRIM(e.id_destino))) FROM Eurovip.estacion e
                WHERE e.id_pais = 'DO' OR EXISTS (SELECT 1 FROM Eurovip.traslado t WHERE t.id_estacion_origen = e.id_estacion)
                GROUP BY e.id_estacion ORDER BY e.id_estacion")).ToList();
            var hotelesX = await HotelesExcelAsync(cn);
            string Ejemplo(int n, string destino) => (hotelesX.Where(h => h.Destino.Equals(destino, StringComparison.OrdinalIgnoreCase)).Skip(n).FirstOrDefault() ?? hotelesX.Skip(n).FirstOrDefault())?.Etiqueta ?? "";
            static string Lista(Dictionary<string, string> d) => string.Join(" · ", d.Values);
            var monedas = (await CatalogoCargas.MonedasAsync(cn)).OrderBy(m => m).ToList();
            var proveedores = await CatalogoCargas.ProveedoresAsync(cn);
            var datos = new ExcelTabla.Hoja("Traslados", Columnas, new List<object?[]>
            {
                new object?[] { "Aeropuerto Punta Cana → hotel", "Compartido", "Estándar", "Autobús", "PUJ", Ejemplo(0, "Punta Cana"), 2, 30, 1, 1,
                                "Por persona", "USD", 25m, 15m, "Sí", 24, "TRPUJBAV1", proveedores.FirstOrDefault()?.Nombre, "Traslado compartido desde el aeropuerto a tu hotel en Bávaro.", "Sí", 1, 35m },
                new object?[] { "Privado Santo Domingo → hotel", "Privado", "Premium", "Van", "SDQ", Ejemplo(0, "La Romana"), 6, 90, 1, 6,
                                "Para todos los viajeros", "DOP", 9500m, 5500m, "Sí", 48, "", "", "Van privada con aire acondicionado para hasta 6 personas.", "Sí" },
            }, new double[] { 40, 13, 12, 12, 12, 18, 9, 14, 13, 13, 22, 10, 18, 15, 12, 17, 16, 30, 60, 9, 11, 16 })
            {
                Listas = CargaExcel.Listas(await ColumnasExcelAsync())
            };
            var ayuda = new ExcelTabla.Hoja("Instrucciones", new[] { "Columna", "Qué poner", "" }, new List<object?[]>
            {
                new object?[] { "Nombre", "Obligatorio. No se puede repetir el mismo aeropuerto y hotel con el mismo tipo, categoría y vehículo.", null },
                new object?[] { "Tipo", Lista(TrasladoValores.Tipos), null },
                new object?[] { "Categoría", Lista(TrasladoValores.Categorias), null },
                new object?[] { "Vehículo", Lista(TrasladoValores.Vehiculos), null },
                new object?[] { "Aeropuerto", "Código del aeropuerto de salida (lista abajo).", null },
                new object?[] { "Hotel", "Obligatorio. El hotel al que llega el traslado, tal cual está en Eurovip (lista abajo; en la revisión se elige de un desplegable con buscador). Una fila por hotel. Si hay dos hoteles con el mismo nombre, escribe «Nombre · Destino».", null },
                new object?[] { "Maletas, Duración, Pasajeros", "Números. Vacío: 2 maletas, 1 a 1 pasajeros.", null },
                new object?[] { "Cobro", Lista(TrasladoValores.Cobros), null },
                new object?[] { "Moneda", $"Moneda de los precios: {string.Join(", ", monedas)}. Vacío = DOP.", null },
                new object?[] { "Precios", "Al menos uno de los dos: ida y vuelta o solo ida.", null },
                new object?[] { "Código interno", "Opcional. Tu código interno: letras y números, sin espacios, tildes ni símbolos. No se puede repetir en ningún producto.", null },
                new object?[] { "Proveedor", "Opcional. De dónde sale el traslado: nombre o código como en la hoja «Proveedores».", null },
                new object?[] { "Cancelable / Activo", "Sí o No. Vacío = Sí.", null },
                new object?[] { "Horas cancelación", "Hasta cuántas horas antes se puede cancelar (opcional).", null },
                new object?[] { "Prioridad", "Opcional. 1 sale primero en la web, 2 segundo… Vacío = se ordena por precio.", null },
                new object?[] { "Precio proveedor", "Opcional. Precio del proveedor en su web (nuestro precio no cambia).", null },
                CargaExcel.AyudaInfo(3),
                new object?[] { "", "", null },
                new object?[] { "Aeropuertos válidos", "Nombre", "Destino" },
            }.Concat(aeropuertos.Select(a => new object?[] { a.Codigo, a.Nombre, a.Destino }))
             .Append(new object?[] { "", "", null }).Append(new object?[] { "Hoteles válidos", "Destino", null })
             .Concat(hotelesX.Select(h => new object?[] { h.Etiqueta, h.Destino, null })).ToList(), new double[] { 30, 80, 24 });
            return File(ExcelTabla.Crear(datos, ayuda, CatalogoCargas.HojaProveedores(proveedores)), CargaExcel.TipoXlsx, "Eurovip-plantilla-traslados.xlsx");
        }

        /// <summary>Cargar Excel: con guardar = false solo revisa; con guardar = true crea los traslados de las filas correctas.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(CargaExcel.MaxBytes + 100_000)]
        public async Task<IActionResult> CargarExcel(IFormFile? archivo, bool guardar, string? cambios, string? omitir, bool descargar = false, bool sinListas = false, string? mapa = null)
        {
            var (filas, error) = await CargaExcel.LeerAsync(archivo, cambios, omitir);
            if (error is not null) return BadRequest(new { mensaje = error });
            var columnas = await ColumnasExcelAsync();
            var plantilla = CargaExcel.Encabezados(filas!, columnas, mapa);   // columnas con otro nombre o plantilla distinta: se pregunta, no falla
            var ayuda = CargaExcel.Ayudar(filas!, columnas);
            if (descargar) return File(CargaExcel.Corregido("Traslados", filas!, columnas), CargaExcel.TipoXlsx, "Eurovip-traslados-corregido.xlsx");
            var r = new CargaExcelResultado { Guardado = guardar };
            var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<CatalogoCargas.Proveedor> proveedores; ISet<string> monedas; List<HotelExcel> hotelesX;
            decimal? minimo;   // CAMBIO 139
            using (var cn = _aud.Conexion()) { hotelesX = await HotelesExcelAsync(cn); proveedores = await CatalogoCargas.ProveedoresAsync(cn); monedas = await CatalogoCargas.MonedasAsync(cn); minimo = await PreciosMinimos.ObtenerAsync(cn, PreciosMinimos.Traslados); }
            short Corto(ExcelTabla.Fila f, short defecto, params string[] c) => f.Cifra(c) is decimal d ? (short)Math.Clamp(Math.Round(d), short.MinValue, short.MaxValue) : defecto;
            foreach (var f in filas!)
            {
                string Valor(Dictionary<string, string> d, string col) => CargaExcel.Opcion(d, f.Texto(col)) ?? (f.Texto(col) ?? "");
                var (idProv, malProv) = CatalogoCargas.BuscarProveedor(f.Texto("Proveedor"), proveedores);
                var (hotel, malHotel) = BuscarHotel(f.Texto("Hotel", "Hotel de destino", "Hasta"), hotelesX);   // de aeropuerto a HOTEL: el destino sale del hotel
                var dto = new TrasladoGuardarDto
                {
                    Id_Moneda = f.Texto("Moneda", "Divisa") ?? "DOP", Codigo = f.Texto("Código interno", "Codigo interno", "Código", "Codigo"), Id_Proveedor = idProv,
                    Nombre = f.Texto("Nombre", "Traslado") ?? "",
                    Tipo = Valor(TrasladoValores.Tipos, "Tipo"), Categoria = Valor(TrasladoValores.Categorias, "Categoría"),
                    Vehiculo = Valor(TrasladoValores.Vehiculos, "Vehículo"), Cobro = Valor(TrasladoValores.Cobros, "Cobro"),
                    Id_Estacion_Origen = f.Texto("Aeropuerto", "Aeropuerto de salida", "Desde") ?? "", Id_Destino = hotel?.Destino ?? "", Hoteles = hotel is null ? new List<int>() : new List<int> { hotel.Id },
                    Maletas = Corto(f, 2, "Maletas"), Duracion_Min = f.Cifra("Duración (min)", "Duración", "Minutos") is decimal du ? (short)Math.Round(du) : null,
                    Pasajeros_Min = Corto(f, 1, "Pasajeros mín", "Pasajeros minimo"), Pasajeros_Max = Corto(f, 1, "Pasajeros máx", "Pasajeros maximo"),
                    Precio_Ida_Vuelta = f.Cifra("Precio ida y vuelta"), Precio_Ida = f.Cifra("Precio solo ida", "Precio ida"),
                    Cancelable = f.SiNo(true, "Cancelable"), Horas_Cancelacion = f.Cifra("Horas cancelación", "Horas cancelacion") is decimal h ? (short)Math.Round(h) : null,
                    Descripcion = f.Texto("Descripción", "Descripcion"), Activo = f.SiNo(true, "Activo"), Todos_Hoteles = false,
                    Peso = f.Cifra("Prioridad", "Peso") is decimal pe ? (short)Math.Round(pe) : null, Precio_Proveedor = f.Cifra("Precio proveedor", "Precio del proveedor"),
                    Info = CargaExcel.InfoDeFila(f)
                };
                var fila = new CargaExcelFila { Fila = f.Numero, Nombre = dto.Nombre, Detalle = $"{dto.Id_Estacion_Origen} → {hotel?.Nombre ?? f.Texto("Hotel")} · {f.Texto("Tipo")} · {f.Texto("Vehículo")}" };
                r.Filas.Add(fila);
                fila.Mensaje = malProv ?? CargaExcel.ErrorModelo(dto);
                if (fila.Mensaje is null && malHotel is not null) { fila.Mensaje = malHotel; CargaExcel.Marcar(fila, "Hoteles", columnas); }
                if (fila.Mensaje is null && await RevisarAsync(dto, monedas) is var (campo, mal)) { fila.Mensaje = mal; CargaExcel.Marcar(fila, campo, columnas); }
                if (malProv is not null) CargaExcel.Marcar(fila, "Id_Proveedor", columnas);
                if (fila.Mensaje is null && !vistas.Add($"{dto.Id_Estacion_Origen}|{hotel?.Id}|{dto.Tipo}|{dto.Categoria}|{dto.Vehiculo}"))
                    fila.Mensaje = "Está repetido en el Excel (mismo aeropuerto y hotel, tipo, categoría y vehículo).";
                if (fila.Mensaje is null && dto.Codigo is not null && !codigos.Add(dto.Codigo)) fila.Mensaje = $"El código «{dto.Codigo}» está repetido en el Excel.";
                foreach (var (col, precio) in new[] { ("Precio ida y vuelta", dto.Precio_Ida_Vuelta), ("Precio solo ida", dto.Precio_Ida) })
                    if (CargaExcel.RevisarMinimo(r, fila, PreciosMinimos.Traslados, minimo, precio, col, $"{dto.Nombre} ({col.ToLowerInvariant()})") is string mMin) { if (fila.Mensaje is null) fila.Columna = col; fila.Mensaje ??= mMin; }
                fila.Detalle += $" · {dto.Id_Moneda}" + (dto.Codigo is null ? "" : $" · {dto.Codigo}");
                fila.Nombre = dto.Nombre; fila.Ok = fila.Mensaje is null;
                if (!fila.Ok || !guardar) continue;
                try
                {
                    fila.Id = await _repo.GuardarAsync(dto, User.Identity?.Name ?? "desconocido");
                    if (dto.Info is { Vacia: false }) using (var cnInfo = _aud.Conexion()) await InfoCliente.GuardarAsync(cnInfo, "Eurovip.traslado", fila.Id.Value, dto.Info);
                    await PorDefectoAsync(TipoMm, fila.Id.Value, dto.Id_Destino, false);
                    r.Guardadas++;
                    await Auditar("Traslado_Crear", fila.Id.Value, new { dto, excel = archivo!.FileName, fila = f.Numero });
                }
                catch (Exception ex) { fila.Ok = false; fila.Mensaje = "No se pudo guardar: " + ex.Message; _logger.LogError(ex, "Cargar Excel: traslado fila {Fila}", f.Numero); }
            }
            CargaExcel.Completar(r, filas!, columnas, ayuda, sinListas, plantilla);
            r.Mensaje = CargaExcel.Mensaje(r, "traslado", "traslados");
            return Json(r);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicar(int id)
        {
            var t = await _repo.ObtenerAsync(id);
            if (t is null) return NotFound(new { mensaje = "Ese traslado ya no existe." });
            var dto = new TrasladoGuardarDto
            {
                Nombre = t.Nombre + " (copia)", Tipo = t.Tipo, Categoria = t.Categoria, Vehiculo = t.Vehiculo, Id_Estacion_Origen = t.Id_Estacion_Origen,
                Id_Destino = t.Id_Destino, Maletas = t.Maletas, Duracion_Min = t.Duracion_Min, Pasajeros_Min = t.Pasajeros_Min, Pasajeros_Max = t.Pasajeros_Max,
                Cobro = t.Cobro, Precio_Ida = t.Precio_Ida, Precio_Ida_Vuelta = t.Precio_Ida_Vuelta, Cancelable = t.Cancelable, Horas_Cancelacion = t.Horas_Cancelacion,
                Descripcion = t.Descripcion, Todos_Hoteles = t.Todos_Hoteles, Hoteles = t.Hoteles.Select(h => int.Parse(h.Valor)).ToList(),
                Id_Moneda = t.Id_Moneda, Id_Proveedor = t.Id_Proveedor, Precio_Proveedor = t.Precio_Proveedor,   // el código no se copia: es único
                Info = new InfoClienteDto { Incluye = t.Incluye, No_Incluye = t.No_Incluye, Condiciones = t.Condiciones, Documentacion = t.Documentacion, Importante = t.Importante, Oferta_Especial = t.Oferta_Especial },
                Activo = false // la copia empieza desactivada para que no salga dos veces en la web
            };
            for (var i = 2; await _repo.RepetidoAsync(dto) is not null; i++) dto.Nombre = $"{t.Nombre} (copia {i})";
            var nuevo = await _repo.GuardarAsync(dto, User.Identity?.Name ?? "desconocido");
            using (var cnInfo = _aud.Conexion()) await InfoCliente.GuardarAsync(cnInfo, "Eurovip.traslado", nuevo, dto.Info);
            await PorDefectoAsync(TipoMm, nuevo, dto.Id_Destino, false);
            await Auditar("Traslado_Duplicar", nuevo, new { de = id, dto.Nombre });
            return Json(new { ok = true, id = nuevo, mensaje = $"Se duplicó como «{dto.Nombre}» (desactivado). Cámbiale lo que haga falta y actívalo." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Activar(int id, bool activo)
        {
            var t = await _repo.ObtenerAsync(id);
            if (t is null) return NotFound(new { mensaje = "Ese traslado ya no existe." });
            await _repo.ActivarAsync(id, activo);
            await Auditar(activo ? "Traslado_Activar" : "Traslado_Desactivar", id, new { t.Nombre });
            return Json(new { ok = true, mensaje = activo ? $"«{t.Nombre}» está activo: sale en la web." : $"«{t.Nombre}» está desactivado: ya no sale en la web." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var t = await _repo.ObtenerAsync(id);
            if (t is null) return NotFound(new { mensaje = "Ese traslado ya no existe." });
            await _repo.EliminarAsync(id);
            var fotoBorrada = !string.IsNullOrWhiteSpace(t.Archivo) && _img.Eliminar(TipoImagen, t.Archivo);
            await _mm.EliminarProductoAsync(TipoMm, id.ToString());   // portada y galería
            await Auditar("Traslado_Eliminar", id, new { antes = t, fotoBorrada });
            return Json(new { ok = true, mensaje = $"Se eliminó «{t.Nombre}»." });
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> SubirImagen([FromForm] int id, IFormFile? archivo)
        {
            var t = await _repo.ObtenerAsync(id);
            if (t is null) return NotFound(new { mensaje = "Guarda primero el traslado." });
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > 10_000_000) return BadRequest(new { mensaje = "La imagen pesa más de 10 MB." });
            if (await ImagenSegura.ExtensionAsync(archivo) is null) return BadRequest(new { mensaje = "El archivo no es una imagen JPG, PNG o WebP." });
            try
            {
                await using var st = archivo.OpenReadStream();
                var img = await _mm.SubirAsync(TipoMm, id.ToString(), "PORTADA", st, archivo.FileName);   // también vacía traslado.imagen
                if (!string.IsNullOrWhiteSpace(t.Archivo)) _img.Eliminar(TipoImagen, t.Archivo);           // la foto vieja ya no se usa
                await Auditar("Traslado_Imagen", id, new { img.Ruta, img.Tamano_Bytes });
                return Json(new { ok = true, imagen = img.Url, mensaje = "Foto guardada." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarImagen(int id)
        {
            var t = await _repo.ObtenerAsync(id);
            if (t is null) return NotFound(new { mensaje = "Ese traslado ya no existe." });
            var borrada = await _mm.QuitarUsoAsync(TipoMm, id.ToString(), "PORTADA") > 0
                          | (!string.IsNullOrWhiteSpace(t.Archivo) && _img.Eliminar(TipoImagen, t.Archivo));
            await _repo.PonerImagenAsync(id, null);
            if (borrada) await Auditar("Traslado_QuitarImagen", id, new { t.Archivo });
            return Json(new { ok = true, mensaje = borrada ? "Foto quitada." : "No tenía foto." });
        }

        private Task Auditar(string tipo, int id, object cambios) =>
            _aud.AuditarAsync(tipo, new[] { id }, cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

        /// <summary>Punto 2: si no tiene foto (ni nueva ni la de antes), se le pone la imagen por defecto (la de su destino o la general).</summary>
        private Task PorDefectoAsync(string tipo, object clave, string? destino, bool yaTieneFoto) =>
            HttpContext.RequestServices.GetRequiredService<IImagenesPorDefecto>().AsegurarAsync(tipo, clave.ToString()!, destino, yaTieneFoto);
    }
}
