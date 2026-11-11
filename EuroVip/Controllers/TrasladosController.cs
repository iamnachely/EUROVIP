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
        [HttpGet] public async Task<IActionResult> Hoteles(string? destino, string? q) => Json(await _repo.HotelesAsync(destino, q));

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
            if (await RevisarAsync(dto) is var (campo, mal)) return Mal(campo, mal);

            try
            {
                var antes = dto.Id_Traslado is int idAntes ? await _repo.ObtenerAsync(idAntes) : null;
                if (dto.Id_Traslado is not null && antes is null) return NotFound(new { mensaje = "Ese traslado ya no existe. Recarga la página." });
                var id = await _repo.GuardarAsync(dto, User.Identity?.Name ?? "desconocido");
                await PorDefectoAsync(TipoMm, id, dto.Id_Destino, !string.IsNullOrWhiteSpace(antes?.Archivo) && _img.BuscarArchivo("traslados", antes!.Archivo!) is not null);
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
        private async Task<(string Campo, string Mensaje)?> RevisarAsync(TrasladoGuardarDto dto, ISet<string>? monedas = null)
        {
            dto.Nombre = ReglasDatos.Limpiar(dto.Nombre);
            dto.Tipo = (dto.Tipo ?? "").Trim().ToUpperInvariant();
            dto.Categoria = (dto.Categoria ?? "").Trim().ToUpperInvariant();
            dto.Vehiculo = (dto.Vehiculo ?? "").Trim().ToUpperInvariant();
            dto.Cobro = (dto.Cobro ?? "").Trim().ToUpperInvariant();
            dto.Id_Estacion_Origen = ReglasDatos.Limpiar(dto.Id_Estacion_Origen).ToUpperInvariant();

            if (ReglasDatos.Nombre("nombre del traslado", dto.Nombre, 3) is string malNombre) return ("Nombre", malNombre);
            if (!TrasladoValores.Tipos.ContainsKey(dto.Tipo)) return ("Tipo", "Elige Compartido o Privado.");
            if (!TrasladoValores.Categorias.ContainsKey(dto.Categoria)) return ("Categoria", "Elige la categoría.");
            if (!TrasladoValores.Vehiculos.ContainsKey(dto.Vehiculo)) return ("Vehiculo", "Elige el vehículo.");
            if (!TrasladoValores.Cobros.ContainsKey(dto.Cobro)) return ("Cobro", "Elige cómo se cobra.");
            if (dto.Pasajeros_Max < dto.Pasajeros_Min) return ("Pasajeros_Max", "El máximo de pasajeros no puede ser menor que el mínimo.");
            if (dto.Precio_Ida is null && dto.Precio_Ida_Vuelta is null) return ("Precio_Ida_Vuelta", "Escribe al menos un precio: ida y vuelta o solo ida.");
            if (await _repo.ValidarEstacionAsync(dto.Id_Estacion_Origen) is string malEst) return ("Id_Estacion_Origen", malEst);
            var (malDestino, destino, _) = await _repo.ValidarDestinoAsync(dto.Id_Destino);
            if (malDestino is not null) return ("Id_Destino", malDestino);
            dto.Id_Destino = destino!;
            if (!dto.Todos_Hoteles)
            {
                if (dto.Hoteles.Count == 0) return ("Hoteles", "Elige al menos un hotel o marca «Todos los hoteles del destino».");
                var fuera = await _repo.HotelesQueNoSonDelDestinoAsync(dto.Hoteles, dto.Id_Destino);
                if (fuera.Count > 0) return ("Hoteles", $"Estos hoteles no son de {dto.Id_Destino}: {string.Join(", ", fuera)}. Quítalos de la lista.");
            }
            if (await _repo.RepetidoAsync(dto) is int otro)
                return ("Nombre", $"Ya existe ese traslado (n.º {otro}) con la misma ruta, tipo, categoría y vehículo.");
            // FASE 38: moneda que exista, proveedor que exista y código interno único entre todos los productos
            using var cn = _aud.Conexion();
            var (moneda, malMoneda) = CatalogoCargas.Moneda(dto.Id_Moneda, monedas ?? await CatalogoCargas.MonedasAsync(cn));
            if (malMoneda is not null) return ("Id_Moneda", malMoneda);
            dto.Id_Moneda = moneda;
            if (await CatalogoCargas.ErrorProveedorAsync(cn, dto.Id_Proveedor) is string malProv) return ("Id_Proveedor", malProv);
            dto.Codigo = CodigoInterno.Limpiar(dto.Codigo);
            var anterior = dto.Id_Traslado is int idt ? await cn.ExecuteScalarAsync<string?>("SELECT codigo FROM Eurovip.traslado WHERE id_traslado = @idt", new { idt }) : null;
            if (await CodigoInterno.ErrorAsync(cn, dto.Codigo, "TRASLADO", dto.Id_Traslado?.ToString(), anterior) is string malCodigo) return ("Codigo", malCodigo);
            return null;
        }

        // ================= CARGAR EXCEL (muchos traslados de una vez) =================
        private static readonly string[] Columnas =
        {
            "Nombre", "Tipo", "Categoría", "Vehículo", "Aeropuerto", "Destino", "Maletas", "Duración (min)", "Pasajeros mín", "Pasajeros máx",
            "Cobro", "Moneda", "Precio ida y vuelta", "Precio solo ida", "Cancelable", "Horas cancelación", "Código interno", "Proveedor", "Descripción", "Activo", "Prioridad", "Precio proveedor"
        };

        /// <summary>Columnas del Excel con sus listas (lo que hay en la base AHORA): para la plantilla, las coincidencias y el editor de filas.</summary>
        private async Task<List<CargaExcelColumna>> ColumnasExcelAsync()
        {
            using var cn = _aud.Conexion();
            var aeropuertos = (await cn.QueryAsync<(string Codigo, string? Nombre, string? Destino)>(@"
                SELECT LTRIM(RTRIM(e.id_estacion)), MIN(e.nombre), MIN(LTRIM(RTRIM(e.id_destino))) FROM Eurovip.estacion e
                WHERE e.id_pais = 'DO' OR EXISTS (SELECT 1 FROM Eurovip.traslado t WHERE t.id_estacion_origen = e.id_estacion)
                GROUP BY e.id_estacion ORDER BY 1")).Select(a => new CargaExcelOpcion(a.Codigo, a.Nombre ?? a.Codigo, a.Destino)).ToList();
            var destinos = await CatalogoCargas.DestinosAsync(cn);
            var monedas = await CatalogoCargas.MonedasAsync(cn);
            var proveedores = await CatalogoCargas.ProveedoresAsync(cn);
            CargaExcelColumna Num(string n, string campo, params string[] alias) => new() { Nombre = n, Alias = alias, Campo = campo, Tipo = "numero" };
            return new()
            {
                new() { Nombre = "Nombre", Alias = new[] { "Traslado" }, Campo = "Nombre" },
                new() { Nombre = "Tipo", Campo = "Tipo", Opciones = CargaExcel.DeDiccionario(TrasladoValores.Tipos) },
                new() { Nombre = "Categoría", Alias = new[] { "Categoria" }, Campo = "Categoria", Opciones = CargaExcel.DeDiccionario(TrasladoValores.Categorias) },
                new() { Nombre = "Vehículo", Alias = new[] { "Vehiculo" }, Campo = "Vehiculo", Opciones = CargaExcel.DeDiccionario(TrasladoValores.Vehiculos) },
                new() { Nombre = "Aeropuerto", Alias = new[] { "Aeropuerto de salida", "Desde" }, Campo = "Id_Estacion_Origen", Opciones = aeropuertos },
                new() { Nombre = "Destino", Alias = new[] { "Hasta" }, Campo = "Id_Destino", Opciones = destinos },
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
            };
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
            var destinos = (await cn.QueryAsync<(string Destino, string? Pais)>(@"SELECT d.id_destino, ISNULL(p.nombre, d.id_pais) FROM Eurovip.destino d
                LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES' WHERE d.id_idioma = 'ES' ORDER BY d.id_destino")).ToList();
            static string Lista(Dictionary<string, string> d) => string.Join(" · ", d.Values);
            var monedas = (await CatalogoCargas.MonedasAsync(cn)).OrderBy(m => m).ToList();
            var proveedores = await CatalogoCargas.ProveedoresAsync(cn);
            var datos = new ExcelTabla.Hoja("Traslados", Columnas, new List<object?[]>
            {
                new object?[] { "Aeropuerto Punta Cana → hoteles de Bávaro", "Compartido", "Estándar", "Autobús", "PUJ", "Punta Cana", 2, 30, 1, 1,
                                "Por persona", "USD", 25m, 15m, "Sí", 24, "TRPUJBAV1", proveedores.FirstOrDefault()?.Nombre, "Traslado compartido desde el aeropuerto a tu hotel en Bávaro.", "Sí", 1, 35m },
                new object?[] { "Privado Santo Domingo → La Romana", "Privado", "Premium", "Van", "SDQ", "La Romana", 6, 90, 1, 6,
                                "Para todos los viajeros", "DOP", 9500m, 5500m, "Sí", 48, "", "", "Van privada con aire acondicionado para hasta 6 personas.", "Sí" },
            }, new double[] { 40, 13, 12, 12, 12, 18, 9, 14, 13, 13, 22, 10, 18, 15, 12, 17, 16, 30, 60, 9, 11, 16 })
            {
                Listas = CargaExcel.Listas(await ColumnasExcelAsync())
            };
            var ayuda = new ExcelTabla.Hoja("Instrucciones", new[] { "Columna", "Qué poner", "" }, new List<object?[]>
            {
                new object?[] { "Nombre", "Obligatorio. No se puede repetir la misma ruta con el mismo tipo, categoría y vehículo.", null },
                new object?[] { "Tipo", Lista(TrasladoValores.Tipos), null },
                new object?[] { "Categoría", Lista(TrasladoValores.Categorias), null },
                new object?[] { "Vehículo", Lista(TrasladoValores.Vehiculos), null },
                new object?[] { "Aeropuerto", "Código del aeropuerto de salida (lista abajo).", null },
                new object?[] { "Destino", "Tal cual está en Eurovip (lista abajo). Llega a todos los hoteles del destino.", null },
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
                new object?[] { "", "", null },
                new object?[] { "Aeropuertos válidos", "Nombre", "Destino" },
            }.Concat(aeropuertos.Select(a => new object?[] { a.Codigo, a.Nombre, a.Destino }))
             .Append(new object?[] { "", "", null }).Append(new object?[] { "Destinos válidos", "País", null })
             .Concat(destinos.Select(d => new object?[] { d.Destino, d.Pais, null })).ToList(), new double[] { 30, 80, 24 });
            return File(ExcelTabla.Crear(datos, ayuda, CatalogoCargas.HojaProveedores(proveedores)), CargaExcel.TipoXlsx, "Eurovip-plantilla-traslados.xlsx");
        }

        /// <summary>Cargar Excel: con guardar = false solo revisa; con guardar = true crea los traslados de las filas correctas.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(CargaExcel.MaxBytes + 100_000)]
        public async Task<IActionResult> CargarExcel(IFormFile? archivo, bool guardar, string? cambios, string? omitir, bool descargar = false, bool sinListas = false)
        {
            var (filas, error) = await CargaExcel.LeerAsync(archivo, cambios, omitir);
            if (error is not null) return BadRequest(new { mensaje = error });
            var columnas = await ColumnasExcelAsync();
            var ayuda = CargaExcel.Ayudar(filas!, columnas);
            if (descargar) return File(CargaExcel.Corregido("Traslados", filas!, columnas), CargaExcel.TipoXlsx, "Eurovip-traslados-corregido.xlsx");
            var r = new CargaExcelResultado { Guardado = guardar };
            var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<CatalogoCargas.Proveedor> proveedores; ISet<string> monedas;
            using (var cn = _aud.Conexion()) { proveedores = await CatalogoCargas.ProveedoresAsync(cn); monedas = await CatalogoCargas.MonedasAsync(cn); }
            short Corto(ExcelTabla.Fila f, short defecto, params string[] c) => f.Cifra(c) is decimal d ? (short)Math.Clamp(Math.Round(d), short.MinValue, short.MaxValue) : defecto;
            foreach (var f in filas!)
            {
                string Valor(Dictionary<string, string> d, string col) => CargaExcel.Opcion(d, f.Texto(col)) ?? (f.Texto(col) ?? "");
                var (idProv, malProv) = CatalogoCargas.BuscarProveedor(f.Texto("Proveedor"), proveedores);
                var dto = new TrasladoGuardarDto
                {
                    Id_Moneda = f.Texto("Moneda", "Divisa") ?? "DOP", Codigo = f.Texto("Código interno", "Codigo interno", "Código", "Codigo"), Id_Proveedor = idProv,
                    Nombre = f.Texto("Nombre", "Traslado") ?? "",
                    Tipo = Valor(TrasladoValores.Tipos, "Tipo"), Categoria = Valor(TrasladoValores.Categorias, "Categoría"),
                    Vehiculo = Valor(TrasladoValores.Vehiculos, "Vehículo"), Cobro = Valor(TrasladoValores.Cobros, "Cobro"),
                    Id_Estacion_Origen = f.Texto("Aeropuerto", "Aeropuerto de salida", "Desde") ?? "", Id_Destino = f.Texto("Destino", "Hasta") ?? "",
                    Maletas = Corto(f, 2, "Maletas"), Duracion_Min = f.Cifra("Duración (min)", "Duración", "Minutos") is decimal du ? (short)Math.Round(du) : null,
                    Pasajeros_Min = Corto(f, 1, "Pasajeros mín", "Pasajeros minimo"), Pasajeros_Max = Corto(f, 1, "Pasajeros máx", "Pasajeros maximo"),
                    Precio_Ida_Vuelta = f.Cifra("Precio ida y vuelta"), Precio_Ida = f.Cifra("Precio solo ida", "Precio ida"),
                    Cancelable = f.SiNo(true, "Cancelable"), Horas_Cancelacion = f.Cifra("Horas cancelación", "Horas cancelacion") is decimal h ? (short)Math.Round(h) : null,
                    Descripcion = f.Texto("Descripción", "Descripcion"), Activo = f.SiNo(true, "Activo"), Todos_Hoteles = true,
                    Peso = f.Cifra("Prioridad", "Peso") is decimal pe ? (short)Math.Round(pe) : null, Precio_Proveedor = f.Cifra("Precio proveedor", "Precio del proveedor")
                };
                var fila = new CargaExcelFila { Fila = f.Numero, Nombre = dto.Nombre, Detalle = $"{dto.Id_Estacion_Origen} → {dto.Id_Destino} · {f.Texto("Tipo")} · {f.Texto("Vehículo")}" };
                r.Filas.Add(fila);
                fila.Mensaje = malProv ?? CargaExcel.ErrorModelo(dto);
                if (fila.Mensaje is null && await RevisarAsync(dto, monedas) is var (campo, mal)) { fila.Mensaje = mal; CargaExcel.Marcar(fila, campo, columnas); }
                if (malProv is not null) CargaExcel.Marcar(fila, "Id_Proveedor", columnas);
                if (fila.Mensaje is null && !vistas.Add($"{dto.Id_Estacion_Origen}|{dto.Id_Destino}|{dto.Tipo}|{dto.Categoria}|{dto.Vehiculo}"))
                    fila.Mensaje = "Está repetido en el Excel (misma ruta, tipo, categoría y vehículo).";
                if (fila.Mensaje is null && dto.Codigo is not null && !codigos.Add(dto.Codigo)) fila.Mensaje = $"El código «{dto.Codigo}» está repetido en el Excel.";
                fila.Detalle += $" · {dto.Id_Moneda}" + (dto.Codigo is null ? "" : $" · {dto.Codigo}");
                fila.Nombre = dto.Nombre; fila.Ok = fila.Mensaje is null;
                if (!fila.Ok || !guardar) continue;
                try
                {
                    fila.Id = await _repo.GuardarAsync(dto, User.Identity?.Name ?? "desconocido");
                    await PorDefectoAsync(TipoMm, fila.Id.Value, dto.Id_Destino, false);
                    r.Guardadas++;
                    await Auditar("Traslado_Crear", fila.Id.Value, new { dto, excel = archivo!.FileName, fila = f.Numero });
                }
                catch (Exception ex) { fila.Ok = false; fila.Mensaje = "No se pudo guardar: " + ex.Message; _logger.LogError(ex, "Cargar Excel: traslado fila {Fila}", f.Numero); }
            }
            CargaExcel.Completar(r, filas!, columnas, ayuda, sinListas);
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
                Activo = false // la copia empieza desactivada para que no salga dos veces en la web
            };
            for (var i = 2; await _repo.RepetidoAsync(dto) is not null; i++) dto.Nombre = $"{t.Nombre} (copia {i})";
            var nuevo = await _repo.GuardarAsync(dto, User.Identity?.Name ?? "desconocido");
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
