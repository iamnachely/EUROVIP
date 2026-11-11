using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Mantenimiento de excursiones (Eurovip.servicio_excursion, copia de ViajeVDT): buscar, ver, cargar, editar,
    /// duplicar, eliminar y cambiar la foto. Las fotos van en Eurovip.multimedia (tipo EXCURSION, carpeta excursiones/{id}/):
    /// portada aquí y galería con el componente de fotos. Si una excursión aún no tiene portada nueva se muestra la de antes
    /// (wwwroot/imagenes/excursiones/{Nombre}.webp, por nombre como BackTiTan).
    /// </summary>
    public class ExcursionesController : Controller
    {
        private const string TipoImagen = "excursiones";
        private readonly IExcursionRepository _repo;
        private readonly ICircuitoRepository _aud;
        private readonly IImagenesLocales _img;
        private readonly ILogger<ExcursionesController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IMultimedia _mm;
        private const string TipoMm = "EXCURSION";

        public ExcursionesController(IExcursionRepository repo, ICircuitoRepository aud, IImagenesLocales img, ILogger<ExcursionesController> logger, IWebHostEnvironment env, IMultimedia mm)
        {
            _mm = mm;
            _repo = repo;
            _aud = aud;
            _img = img;
            _logger = logger;
            _env = env;
        }

        public IActionResult Index() => View();

        /// <summary>Archivo de la foto a partir del nombre ("Excursión Catamarán (Party Boat)" → "ExcursionCatamaranPartyBoat.webp").</summary>
        private string Archivo(string? nombre)
        {
            var b = _img.NombreCircuito(nombre ?? "");
            if (b.Length > 120) b = b[..120];
            return b == "" ? "" : b + ".webp";
        }

        /// <summary>Portada: la de Eurovip.multimedia; si todavía no tiene, la foto vieja por nombre.</summary>
        private Excursion ConImagen(Excursion e, IReadOnlyDictionary<string, string>? portadas = null)
        {
            e.Imagen = (portadas ?? new Dictionary<string, string>()).TryGetValue(e.Id_Excursion.ToString(), out var u) ? u
                       : _img.BuscarArchivo(TipoImagen, Archivo(e.Nombre))?.Url;
            return e;
        }

        private async Task<Excursion> ConImagenAsync(Excursion e) =>
            ConImagen(e, (await _mm.PortadaAsync(TipoMm, e.Id_Excursion.ToString())) is { } u ? new Dictionary<string, string> { [e.Id_Excursion.ToString()] = u } : null);

        /// <summary>
        /// Portada de la sección de un destino: 1) su foto en imagenes/destinos (o la de destino.caratula si el archivo está);
        /// 2) el fondo de flyer de ese destino (uploads/flyers/fondos). Si no hay, la pantalla usa la foto de una excursión o un degradado.
        /// </summary>
        private string? ImagenDestino(ExcursionDestino d, IReadOnlyDictionary<string, string> portadasDestino)
        {
            if (portadasDestino.TryGetValue(d.Destino, out var mm)) return mm;   // portada del destino en Eurovip.multimedia
            var local = _img.Buscar("destinos", d.Destino)?.Url;
            if (local is not null) return local;
            if (!string.IsNullOrWhiteSpace(d.Caratula))
            {
                if (d.Caratula.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return d.Caratula;
                var rel = d.Caratula.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                if (!rel.Contains("..") && System.IO.File.Exists(Path.Combine(_env.WebRootPath, rel))) return "/" + d.Caratula.TrimStart('/');
            }
            foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp" })
            {
                var f = Path.Combine(_env.WebRootPath, "uploads", "flyers", "fondos", d.Destino + ext);
                if (System.IO.File.Exists(f)) return $"/uploads/flyers/fondos/{Uri.EscapeDataString(d.Destino + ext)}?v={System.IO.File.GetLastWriteTimeUtc(f).Ticks:x}";
            }
            return null;
        }

        /// <summary>Todas las excursiones (con su foto) y los destinos que tienen excursiones (con su portada) para las secciones.</summary>
        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var portadas = await _mm.PortadasAsync(TipoMm);
            var excursiones = (await _repo.ListarAsync()).Select(e => ConImagen(e, portadas)).ToList();
            var destinos = await _repo.DestinosConExcursionesAsync();
            var portadasDestino = await _mm.PortadasAsync("DESTINO");
            foreach (var d in destinos) d.Imagen = ImagenDestino(d, portadasDestino);
            return Json(new { excursiones, destinos });
        }

        [HttpGet]
        public async Task<IActionResult> Destinos(string? q) => Json(await _repo.DestinosAsync(q));

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] ExcursionGuardarDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key, x => x.Value!.Errors.First().ErrorMessage);
                return BadRequest(new { mensaje = errores.Values.First(), errores });
            }
            if (await RevisarAsync(dto) is var (campo, mal))
                return BadRequest(new { mensaje = mal, errores = new Dictionary<string, string> { [campo] = mal } });

            try
            {
                var antes = dto.Id_Excursion is int idAntes ? await _repo.ObtenerAsync(idAntes) : null;
                if (dto.Id_Excursion is not null && antes is null) return NotFound(new { mensaje = "Esa excursión ya no existe. Recarga la página." });
                var id = await _repo.GuardarAsync(dto);
                await PorDefectoAsync(TipoMm, id, dto.Id_Destino, _img.BuscarArchivo(TipoImagen, Archivo(dto.Nombre)) is not null);

                // Si cambió el nombre, la foto pasa al nombre nuevo (se relaciona por nombre).
                var fotoMovida = antes is not null && Archivo(antes.Nombre) != Archivo(dto.Nombre)
                                 && _img.RenombrarArchivo(TipoImagen, Archivo(antes.Nombre), Archivo(dto.Nombre));

                await Auditar(antes is null ? "Excursion_Crear" : "Excursion_Editar", id, new { antes, despues = dto, fotoMovida });
                var e = await ConImagenAsync((await _repo.ObtenerAsync(id))!);
                return Json(new { ok = true, id, excursion = e, mensaje = antes is null ? $"¡Listo! Se creó «{dto.Nombre}»." : "Cambios guardados." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando la excursión {Nombre}", dto.Nombre);
                return StatusCode(500, new { mensaje = "No se pudo guardar: " + ex.Message });
            }
        }

        /// <summary>
        /// Reglas para guardar una excursión (las mismas en el formulario y en Cargar Excel): nombre con letras, destino que exista
        /// (lo deja escrito como en Eurovip.destino) y que no se repita el nombre en ese destino. Devuelve el campo y el error, o null.
        /// </summary>
        private async Task<(string Campo, string Mensaje)?> RevisarAsync(ExcursionGuardarDto dto, ISet<string>? monedas = null)
        {
            dto.Nombre = ReglasDatos.Limpiar(dto.Nombre);
            using var cn = _aud.Conexion();
            // FASE 38: moneda que exista, proveedor que exista y código interno único entre todos los productos
            var (moneda, malMoneda) = CatalogoCargas.Moneda(dto.Id_Moneda, monedas ?? await CatalogoCargas.MonedasAsync(cn));
            if (malMoneda is not null) return ("Id_Moneda", malMoneda);
            dto.Id_Moneda = moneda;
            if (await CatalogoCargas.ErrorProveedorAsync(cn, dto.Id_Proveedor) is string malProv) return ("Id_Proveedor", malProv);
            dto.Codigo = CodigoInterno.Limpiar(dto.Codigo);
            var anterior = dto.Id_Excursion is int ida ? await cn.ExecuteScalarAsync<string?>("SELECT codigo FROM Eurovip.servicio_excursion WHERE id_Excursion = @ida", new { ida }) : null;
            if (await CodigoInterno.ErrorAsync(cn, dto.Codigo, "EXCURSION", dto.Id_Excursion?.ToString(), anterior) is string malCodigo) return ("Codigo", malCodigo);
            if (ReglasDatos.Nombre("nombre de excursión", dto.Nombre, 3) is string malNombre) return ("Nombre", malNombre);
            var (malDestino, destino) = await _repo.ValidarDestinoAsync(dto.Id_Destino);
            if (malDestino is not null) return ("Id_Destino", malDestino);
            dto.Id_Destino = destino!;
            if (Archivo(dto.Nombre) == "") return ("Nombre", "El nombre tiene que llevar letras o números.");
            if (await _repo.RepetidaAsync(dto.Nombre, dto.Id_Destino, dto.Id_Excursion) is int otra)
                return ("Nombre", $"Ya existe la excursión «{dto.Nombre}» en {dto.Id_Destino} (n.º {otra}). No se puede repetir.");
            return null;
        }

        // ================= CARGAR EXCEL (muchas excursiones de una vez) =================
        private static readonly string[] Columnas = { "Nombre", "Destino", "Moneda", "Precio", "Cancelable", "Código interno", "Proveedor", "Descripción", "Prioridad", "Precio proveedor" };

        /// <summary>Columnas del Excel con sus listas (lo que hay en la base AHORA): para la plantilla, las coincidencias y el editor de filas.</summary>
        private async Task<List<CargaExcelColumna>> ColumnasExcelAsync()
        {
            using var cn = _aud.Conexion();
            var destinos = await CatalogoCargas.DestinosAsync(cn);
            var monedas = await CatalogoCargas.MonedasAsync(cn);
            var proveedores = await CatalogoCargas.ProveedoresAsync(cn);
            return new()
            {
                new() { Nombre = "Nombre", Alias = new[] { "Excursión", "Excursion" }, Campo = "Nombre" },
                new() { Nombre = "Destino", Campo = "Id_Destino", Opciones = destinos },
                CargaExcel.ColumnaMoneda(monedas),
                new() { Nombre = "Precio", Alias = new[] { "Precio por persona" }, Campo = "Precio", Tipo = "numero" },
                CargaExcel.ColumnaSiNo("Cancelable"),
                new() { Nombre = "Código interno", Alias = new[] { "Código", "Codigo", "Codigo interno" }, Campo = "Codigo" },
                CargaExcel.ColumnaProveedor(proveedores),
                new() { Nombre = "Descripción", Alias = new[] { "Descripcion" }, Campo = "Descripcion", Tipo = "largo" },
                new() { Nombre = "Prioridad", Alias = new[] { "Peso" }, Campo = "Peso", Tipo = "numero" },
                new() { Nombre = "Precio proveedor", Alias = new[] { "Precio del proveedor" }, Campo = "Precio_Proveedor", Tipo = "numero" },
            };
        }

        /// <summary>Plantilla: hoja «Excursiones» (una fila por excursión) y hoja «Instrucciones» con los destinos que existen.</summary>
        [HttpGet]
        public async Task<IActionResult> Plantilla()
        {
            using var cn = _aud.Conexion();
            var destinos = (await cn.QueryAsync<(string Destino, string? Pais)>(@"SELECT d.id_destino, ISNULL(p.nombre, d.id_pais) FROM Eurovip.destino d
                LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES' WHERE d.id_idioma = 'ES' ORDER BY d.id_destino")).ToList();
            var monedas = (await CatalogoCargas.MonedasAsync(cn)).OrderBy(m => m).ToList();
            var proveedores = await CatalogoCargas.ProveedoresAsync(cn);
            // Ejemplos con destinos que existen (si no, la fila de ejemplo daría error)
            var ej = destinos.Select(d => d.Destino).Where(d => d is "Punta Cana" or "Samaná" or "La Romana").DefaultIfEmpty(destinos.FirstOrDefault().Destino ?? "Punta Cana").ToList();
            var datos = new ExcelTabla.Hoja("Excursiones", Columnas, new List<object?[]>
            {
                new object?[] { "Isla Saona en catamarán", ej[0], "DOP", 4500m, "Sí", "EXCSAONA1", proveedores.FirstOrDefault()?.Nombre, "Día completo en Isla Saona: catamarán, almuerzo buffet, bebidas y piscina natural.", 1, 5200m },
                new object?[] { "Cascada El Limón a caballo", ej[^1], "USD", 55m, "No", "", "", "Paseo a caballo hasta la cascada El Limón con almuerzo típico." },
            }, new double[] { 42, 22, 10, 12, 12, 16, 30, 80, 11, 16 }) { Listas = CargaExcel.Listas(await ColumnasExcelAsync()) };
            var ayuda = new ExcelTabla.Hoja("Instrucciones", new[] { "Columna", "Qué poner" }, new List<object?[]>
            {
                new object?[] { "Nombre", "Obligatorio. Nombre de la excursión (no se puede repetir en el mismo destino)." },
                new object?[] { "Destino", "Obligatorio. Tal cual está en Eurovip (lista abajo)." },
                new object?[] { "Moneda", $"Moneda del precio: {string.Join(", ", monedas)}. Vacío = DOP." },
                new object?[] { "Precio", "Obligatorio. Precio por persona (número, sin símbolo)." },
                new object?[] { "Código interno", "Opcional. Tu código interno: letras y números, sin espacios, tildes ni símbolos. No se puede repetir en ningún producto." },
                new object?[] { "Proveedor", "Opcional. De dónde sale la excursión: nombre o código como en la hoja «Proveedores»." },
                new object?[] { "Cancelable", "Sí o No. Vacío = No." },
                new object?[] { "Descripción", "Opcional." },
                new object?[] { "Prioridad", "Opcional. 1 sale primero en la web, 2 segundo… Vacío = se ordena por precio." },
                new object?[] { "Precio proveedor", "Opcional. Precio del proveedor en su web (nuestro precio no cambia)." },
                new object?[] { "", "" },
                new object?[] { "Destinos válidos", "País" },
            }.Concat(destinos.Select(d => new object?[] { d.Destino, d.Pais })).ToList(), new double[] { 32, 90 });
            return File(ExcelTabla.Crear(datos, ayuda, CatalogoCargas.HojaProveedores(proveedores)), CargaExcel.TipoXlsx, "Eurovip-plantilla-excursiones.xlsx");
        }

        /// <summary>
        /// Cargar Excel: con guardar = false solo revisa (dice fila por fila si está bien o qué falla); con guardar = true crea
        /// las excursiones de las filas correctas (las que fallan no se guardan). Cada una actualiza la tarjeta de su destino en la Home.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(CargaExcel.MaxBytes + 100_000)]
        public async Task<IActionResult> CargarExcel(IFormFile? archivo, bool guardar, string? cambios, string? omitir, bool descargar = false, bool sinListas = false)
        {
            var (filas, error) = await CargaExcel.LeerAsync(archivo, cambios, omitir);
            if (error is not null) return BadRequest(new { mensaje = error });
            var columnas = await ColumnasExcelAsync();
            var ayuda = CargaExcel.Ayudar(filas!, columnas);
            if (descargar) return File(CargaExcel.Corregido("Excursiones", filas!, columnas), CargaExcel.TipoXlsx, "Eurovip-excursiones-corregido.xlsx");
            var r = new CargaExcelResultado { Guardado = guardar };
            var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<CatalogoCargas.Proveedor> proveedores; ISet<string> monedas;
            using (var cn = _aud.Conexion()) { proveedores = await CatalogoCargas.ProveedoresAsync(cn); monedas = await CatalogoCargas.MonedasAsync(cn); }
            foreach (var f in filas!)
            {
                var (idProv, malProv) = CatalogoCargas.BuscarProveedor(f.Texto("Proveedor"), proveedores);
                var dto = new ExcursionGuardarDto
                {
                    Id_Moneda = f.Texto("Moneda", "Divisa") ?? "DOP", Codigo = f.Texto("Código interno", "Codigo interno", "Código", "Codigo"), Id_Proveedor = idProv,
                    Nombre = f.Texto("Nombre", "Excursión", "Excursion") ?? "", Id_Destino = f.Texto("Destino") ?? "",
                    Precio = f.Cifra("Precio", "Precio por persona"), Cancelable = f.SiNo(false, "Cancelable"), Descripcion = f.Texto("Descripción", "Descripcion"),
                    Peso = f.Cifra("Prioridad", "Peso") is decimal pe ? (short)Math.Round(pe) : null, Precio_Proveedor = f.Cifra("Precio proveedor", "Precio del proveedor")
                };
                var fila = new CargaExcelFila { Fila = f.Numero, Nombre = dto.Nombre, Detalle = $"{dto.Id_Destino} · {(dto.Precio is decimal p ? p.ToString("N2") : "sin precio")}" };
                r.Filas.Add(fila);
                fila.Mensaje = malProv ?? CargaExcel.ErrorModelo(dto);
                if (fila.Mensaje is null && f.Texto("Precio", "Precio por persona") is string tp && dto.Precio is null) fila.Mensaje = $"El precio «{tp}» no es un número.";
                if (fila.Mensaje is null && await RevisarAsync(dto, monedas) is var (campo, mal)) { fila.Mensaje = mal; CargaExcel.Marcar(fila, campo, columnas); }
                if (malProv is not null) CargaExcel.Marcar(fila, "Id_Proveedor", columnas);
                if (fila.Mensaje is null && !vistas.Add($"{dto.Id_Destino}|{dto.Nombre}")) fila.Mensaje = "Está repetida en el Excel (mismo nombre y destino).";
                if (fila.Mensaje is null && dto.Codigo is not null && !codigos.Add(dto.Codigo)) fila.Mensaje = $"El código «{dto.Codigo}» está repetido en el Excel.";
                fila.Detalle = $"{dto.Id_Destino} · {(dto.Precio is decimal pr ? $"{dto.Id_Moneda} {pr:N2}" : "sin precio")}" + (dto.Codigo is null ? "" : $" · {dto.Codigo}");
                fila.Nombre = dto.Nombre; fila.Ok = fila.Mensaje is null;
                if (!fila.Ok || !guardar) continue;
                try
                {
                    fila.Id = await _repo.GuardarAsync(dto);
                    await PorDefectoAsync(TipoMm, fila.Id.Value, dto.Id_Destino, _img.BuscarArchivo(TipoImagen, Archivo(dto.Nombre)) is not null);
                    r.Guardadas++;
                    await Auditar("Excursion_Crear", fila.Id.Value, new { dto, excel = archivo!.FileName, fila = f.Numero });
                }
                catch (Exception ex) { fila.Ok = false; fila.Mensaje = "No se pudo guardar: " + ex.Message; _logger.LogError(ex, "Cargar Excel: excursión fila {Fila}", f.Numero); }
            }
            CargaExcel.Completar(r, filas!, columnas, ayuda, sinListas);
            r.Mensaje = CargaExcel.Mensaje(r, "excursión", "excursiones");
            return Json(r);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicar(int id)
        {
            var e = await _repo.ObtenerAsync(id);
            if (e is null) return NotFound(new { mensaje = "Esa excursión ya no existe." });
            var nombre = e.Nombre ?? "Excursión";
            var copia = $"{nombre} (copia)";
            for (var i = 2; await _repo.RepetidaAsync(copia, e.Id_Destino ?? "", null) is not null; i++) copia = $"{nombre} (copia {i})";
            var nuevo = await _repo.GuardarAsync(new ExcursionGuardarDto
            {
                Nombre = copia, Id_Destino = e.Id_Destino ?? "", Precio = e.Precio, Cancelable = e.Cancelable ?? false, Descripcion = e.Descripcion,
                Id_Moneda = e.Id_Moneda, Id_Proveedor = e.Id_Proveedor, Precio_Proveedor = e.Precio_Proveedor   // el código no se copia: es único
            });
            await PorDefectoAsync(TipoMm, nuevo, e.Id_Destino, false);
            await Auditar("Excursion_Duplicar", nuevo, new { de = id, nombre = copia });
            return Json(new { ok = true, id = nuevo, excursion = await ConImagenAsync((await _repo.ObtenerAsync(nuevo))!), mensaje = $"Se duplicó como «{copia}». Cámbiale lo que haga falta." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var e = await _repo.ObtenerAsync(id);
            if (e is null) return NotFound(new { mensaje = "Esa excursión ya no existe." });
            await _repo.EliminarAsync(id);
            // La foto se borra solo si ninguna otra excursión se llama igual (se relaciona por nombre).
            var otras = (await _repo.ListarAsync()).Any(x => Archivo(x.Nombre) == Archivo(e.Nombre));
            var fotoBorrada = !otras && _img.Eliminar(TipoImagen, Archivo(e.Nombre));
            var fotosMultimedia = await _mm.EliminarProductoAsync(TipoMm, id.ToString());   // portada y galería
            await Auditar("Excursion_Eliminar", id, new { antes = e, fotoBorrada, fotosMultimedia });
            return Json(new { ok = true, mensaje = $"Se eliminó «{e.Nombre}»." });
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> SubirImagen([FromForm] int id, IFormFile? archivo)
        {
            var e = await _repo.ObtenerAsync(id);
            if (e is null) return NotFound(new { mensaje = "Guarda primero la excursión: la foto se guarda con su nombre." });
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > 10_000_000) return BadRequest(new { mensaje = "La imagen pesa más de 10 MB." });
            if (await ImagenSegura.ExtensionAsync(archivo) is null) return BadRequest(new { mensaje = "El archivo no es una imagen JPG, PNG o WebP." });
            try
            {
                await using var st = archivo.OpenReadStream();
                var img = await _mm.SubirAsync(TipoMm, id.ToString(), "PORTADA", st, archivo.FileName);
                await Auditar("Excursion_Imagen", id, new { img.Ruta, img.Tamano_Bytes });
                return Json(new { ok = true, imagen = img.Url, mensaje = "Foto guardada." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarImagen(int id)
        {
            var e = await _repo.ObtenerAsync(id);
            if (e is null) return NotFound(new { mensaje = "Esa excursión ya no existe." });
            // Portada nueva (multimedia) y, si quedaba, la foto vieja por nombre (si no, volvería a aparecer)
            var borrada = await _mm.QuitarUsoAsync(TipoMm, id.ToString(), "PORTADA") > 0 | _img.Eliminar(TipoImagen, Archivo(e.Nombre));
            if (borrada) await Auditar("Excursion_QuitarImagen", id, new { archivo = Archivo(e.Nombre) });
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
