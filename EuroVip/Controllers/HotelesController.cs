using EuroVip.Models;
using EuroVip.Helpers;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Fase 2 — Carga de Hoteles. Equivale a /Inicio2/Index?tipoServicio=HOTEL + modal "Nueva Oferta" de BackTiTan,
    /// guardando en [Eurovip].[inicio] con tipo_servicio = 'HOTEL'.
    /// Foto: Eurovip.multimedia tipo HOTEL (id_hotel = id_servicio de la tarjeta) → multimedia/hoteles/{id}/. Va con el hotel,
    /// no con el nombre. Si un hotel aún no tiene foto nueva se muestra la de antes (wwwroot/imagenes/hoteles/{nombre}.webp).
    /// </summary>
    public class HotelesController : Controller
    {
        private const string TipoImagen = "hoteles";
        private readonly IHotelRepository _repo;
        private readonly IImagenesLocales _img;
        private readonly ILogger<HotelesController> _logger;
        private readonly IMultimedia _mm;
        private const string TipoMm = "HOTEL";

        public HotelesController(IHotelRepository repo, IImagenesLocales img, ILogger<HotelesController> logger, IMultimedia mm)
        {
            _mm = mm;
            _repo = repo;
            _img = img;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var modelo = new HotelesPaginaViewModel
            {
                Monedas = await _repo.MonedasAsync(),
                Estadisticas = await _repo.EstadisticasAsync(),
                Destinos = await _repo.DestinosAsync()
            };
            return View(modelo);
        }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] HotelFiltro filtro)
        {
            var resultado = await _repo.ListarAsync(filtro);
            var portadas = await _mm.PortadasAsync(TipoMm);
            foreach (var h in resultado.Items)
                h.Imagen = portadas.TryGetValue(h.Id_Servicio.ToString(), out var u) ? u : _img.Buscar(TipoImagen, h.Id_Destino)?.Url;
            var stats = await _repo.EstadisticasAsync();
            return Json(new { resultado.Items, resultado.Total, resultado.Pagina, resultado.Tam, resultado.Paginas, stats });
        }

        [HttpGet]
        public async Task<IActionResult> Estadisticas() => Json(await _repo.EstadisticasAsync());

        [HttpGet]
        public async Task<IActionResult> Obtener(int id)
        {
            var hotel = await _repo.ObtenerAsync(id);
            return hotel is null ? NotFound(new { mensaje = "No encontramos ese hotel." }) : Json(hotel);
        }

        // Hoteles solo en ES: campañas y secciones siempre del idioma español.
        [HttpGet]
        public async Task<IActionResult> Campanyas() => Json(await _repo.CampanyasAsync(HotelRepository.IdiomaHotel));

        [HttpGet]
        public async Task<IActionResult> Contenedores() => Json(await _repo.ContenedoresAsync(HotelRepository.IdiomaHotel));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] HotelGuardarDto dto)
        {
            // Limpia espacios y deja la descripción con el formato de ViajeVDT: "Régimen -Código"
            Preparar(dto);
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(x => x.Key, x => x.Value!.Errors.First().ErrorMessage);
                return BadRequest(new { mensaje = "Revisa los campos marcados.", errores });
            }
            // Nada de nombres basura ("222") ni destinos que no existen o están mal escritos.
            if (await RevisarAsync(dto) is var (campo, mal))
                return BadRequest(new { mensaje = mal, errores = new Dictionary<string, string> { [campo] = mal } });

            try
            {
                if (dto.Id_Servicio is null)
                {
                    var id = await _repo.CrearAsync(dto);
                    await Auditar("Hotel_Crear", id, dto);
                    _logger.LogInformation("Hotel {Id} creado por {Usuario}", id, User.Identity?.Name);
                    return Json(new { ok = true, id, mensaje = $"¡Listo! \"{dto.Id_Destino}\" se agregó a la vitrina." });
                }

                var antes = await _repo.ObtenerAsync(dto.Id_Servicio.Value);
                if (antes is null)
                    return NotFound(new { mensaje = "Ese hotel ya no existe. Recarga la página." });

                await _repo.EditarAsync(dto);
                await Auditar("Hotel_Editar", dto.Id_Servicio.Value, new { antes, despues = dto });

                // La imagen va por nombre: si cambió el nombre del hotel, la imagen lo sigue.
                // Si otro hotel aún usa el nombre viejo, se copia en vez de mover.
                var movida = false;
                if (_img.NombreArchivo(antes.Id_Destino ?? "") != _img.NombreArchivo(dto.Id_Destino))
                {
                    var siguenUsandola = (await _repo.NombresAsync()).Any(n => _img.NombreArchivo(n) == _img.NombreArchivo(antes.Id_Destino ?? ""));
                    movida = _img.Renombrar(TipoImagen, antes.Id_Destino ?? "", dto.Id_Destino, copiar: siguenUsandola);
                }
                return Json(new { ok = true, id = dto.Id_Servicio, mensaje = movida ? "Cambios guardados. La foto pasó al nombre nuevo." : "Cambios guardados." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando hotel {@Dto}", dto);
                return StatusCode(500, new { mensaje = "No se pudo guardar. Intenta de nuevo en unos segundos." });
            }
        }

        /// <summary>Limpia espacios y deja la descripción con el formato de ViajeVDT: "Régimen -Código".</summary>
        private static void Preparar(HotelGuardarDto dto)
        {
            dto.Id_Destino = dto.Id_Destino?.Trim() ?? "";
            dto.CaratulaDestino = dto.CaratulaDestino?.Trim();
            var d = System.Text.RegularExpressions.Regex.Match(dto.Descripcion ?? "", @"^(.+?)\s*-\s*(\d+)\s*$");
            if (d.Success) dto.Descripcion = $"{d.Groups[1].Value.Trim()} -{d.Groups[2].Value}";
        }

        /// <summary>
        /// Reglas para guardar un hotel (las mismas en el formulario y en Cargar Excel): nada de nombres basura ("222")
        /// ni destinos que no existen o están mal escritos. Devuelve el campo y el error, o null.
        /// </summary>
        private async Task<(string Campo, string Mensaje)?> RevisarAsync(HotelGuardarDto dto)
        {
            dto.Id_Destino = ReglasDatos.Limpiar(dto.Id_Destino);
            dto.CaratulaDestino = ReglasDatos.Limpiar(dto.CaratulaDestino);
            if (ReglasDatos.Nombre("nombre de hotel", dto.Id_Destino, 3) is string malHotel) return ("Id_Destino", malHotel);
            if (await _repo.ErrorDestinoAsync(dto.CaratulaDestino) is string malDestino) return ("CaratulaDestino", malDestino);
            return null;
        }

        // ================= CARGAR EXCEL (muchos hoteles de una vez) =================
        private static readonly string[] Columnas =
        {
            "Hotel", "Destino", "Régimen", "Código hotel", "Precio", "Noches", "Moneda", "Campaña", "País salida", "Ciudad salida",
            "País destino", "Latitud", "Longitud", "Sección de la web", "Visible", "Notas"
        };
        private static readonly string[] Regimenes = { "Solo Habitación", "Alojamiento y Desayuno", "Media Pensión", "Pensión Completa", "Todo Incluido", "Room Only" };

        /// <summary>Plantilla: hoja «Hoteles» (una fila por hotel) y hoja «Instrucciones» con campañas, secciones, monedas y destinos.</summary>
        [HttpGet]
        public async Task<IActionResult> Plantilla()
        {
            var camps = (await _repo.CampanyasAsync(HotelRepository.IdiomaHotel)).ToList();
            var conts = (await _repo.ContenedoresAsync(HotelRepository.IdiomaHotel)).ToList();
            var monedas = (await _repo.MonedasAsync()).ToList();
            var destinos = (await _repo.DestinosAsync()).ToList();
            var camp = CampanyaPorDefecto(camps)?.Id_Campanya.ToString() ?? camps.FirstOrDefault()?.Id_Campanya.ToString();
            // Destinos de ejemplo que SÍ existen (si no, la fila de ejemplo saldría con error al subirla)
            string Dest(string quiero, int i) => destinos.FirstOrDefault(x => ExcelTabla.Clave(x) == ExcelTabla.Clave(quiero)) ?? destinos.ElementAtOrDefault(i) ?? quiero;
            var seccion = conts.FirstOrDefault(c => ExcelTabla.Clave(c.Nombre).Contains("hotel"))?.Nombre ?? conts.FirstOrDefault()?.Nombre;
            var datos = new ExcelTabla.Hoja("Hoteles", Columnas, new List<object?[]>
            {
                new object?[] { "Riu Palace Bavaro", Dest("Punta Cana", 0), "Todo Incluido", "603258", 1290m, 7, "DOP", camp, "ES", "Madrid", "DO", "18.7357", "-68.4447", seccion, "Sí", null },
                new object?[] { "Iberostar Selection Hacienda Dominicus", Dest("La Romana", 1), "Todo Incluido", "702115", 1450m, 7, "DOP", camp, "ES", "Madrid", "DO", null, null, seccion, "Sí", "Ejemplo: borra estas filas y pon tus hoteles." },
            }, new double[] { 38, 18, 20, 13, 11, 9, 9, 11, 11, 14, 12, 11, 11, 22, 9, 40 });
            var ayuda = new List<object?[]>
            {
                new object?[] { "Hotel", "Obligatorio. Nombre del hotel (no debe existir ya en la vitrina).", null },
                new object?[] { "Destino", "Obligatorio. Tal cual está en Eurovip (lista abajo).", null },
                new object?[] { "Régimen", "Obligatorio. Ej.: " + string.Join(" · ", Regimenes), null },
                new object?[] { "Código hotel", "Obligatorio. Código numérico del hotel (3 a 12 cifras).", null },
                new object?[] { "Precio / Noches", "Precio «desde» y noches (opcionales).", null },
                new object?[] { "Moneda", "Código (lista abajo). Vacío = DOP.", null },
                new object?[] { "Campaña", "Número o nombre (lista abajo)." + (camp is null ? "" : $" Vacío = {camp}."), null },
                new object?[] { "País salida / Ciudad salida", "Obligatorios. País en 2 letras (ES, DO…) y ciudad (Madrid…).", null },
                new object?[] { "País destino", "2 letras. Vacío = DO.", null },
                new object?[] { "Latitud / Longitud", "Opcionales (para el mapa).", null },
                new object?[] { "Sección de la web", "Nombre de la sección (lista abajo). Sin sección el hotel queda oculto.", null },
                new object?[] { "Visible", "Sí o No. Vacío = Sí.", null },
                new object?[] { "", "", null },
                new object?[] { "Campañas", "Nombre", "Tipo" },
            };
            ayuda.AddRange(camps.Select(c => new object?[] { c.Id_Campanya.ToString(), c.Nombre, c.Id_Tipo_Servicio }));
            ayuda.Add(new object?[] { "", "", null }); ayuda.Add(new object?[] { "Secciones de la web", "Posición", null });
            ayuda.AddRange(conts.Select(c => new object?[] { c.Nombre, c.Posicion, null }));
            ayuda.Add(new object?[] { "", "", null }); ayuda.Add(new object?[] { "Monedas", "Nombre", null });
            ayuda.AddRange(monedas.Select(m => new object?[] { m.Codigo, m.Nombre, null }));
            ayuda.Add(new object?[] { "", "", null }); ayuda.Add(new object?[] { "Destinos válidos", "", null });
            ayuda.AddRange(destinos.Select(x => new object?[] { x, null, null }));
            return File(ExcelTabla.Crear(datos, new ExcelTabla.Hoja("Instrucciones", new[] { "Columna", "Qué poner", "" }, ayuda, new double[] { 30, 80, 14 })),
                        CargaExcel.TipoXlsx, "Eurovip-plantilla-hoteles.xlsx");
        }

        /// <summary>La campaña si el Excel no la trae: la única de tipo Oferta (si hay una sola).</summary>
        private static Common.Data.Entities.Campanya? CampanyaPorDefecto(List<Common.Data.Entities.Campanya> camps)
        {
            var ofertas = camps.Where(c => string.Equals(c.Id_Tipo_Servicio?.Trim(), "Oferta", StringComparison.OrdinalIgnoreCase)).ToList();
            return ofertas.Count == 1 ? ofertas[0] : camps.Count == 1 ? camps[0] : null;
        }

        /// <summary>Cargar Excel: con guardar = false solo revisa; con guardar = true crea los hoteles de las filas correctas.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(CargaExcel.MaxBytes + 100_000)]
        public async Task<IActionResult> CargarExcel(IFormFile? archivo, bool guardar)
        {
            var (filas, error) = await CargaExcel.LeerAsync(archivo);
            if (error is not null) return BadRequest(new { mensaje = error });
            var camps = (await _repo.CampanyasAsync(HotelRepository.IdiomaHotel)).ToList();
            var conts = (await _repo.ContenedoresAsync(HotelRepository.IdiomaHotel)).ToList();
            var monedas = (await _repo.MonedasAsync()).Select(m => m.Codigo.Trim().ToUpperInvariant()).ToHashSet();
            var existentes = new HashSet<string>((await _repo.NombresAsync()).Select(ExcelTabla.Clave));
            var porDefecto = CampanyaPorDefecto(camps);
            var r = new CargaExcelResultado { Guardado = guardar };
            var vistas = new HashSet<string>();
            foreach (var f in filas!)
            {
                var fila = new CargaExcelFila { Fila = f.Numero, Nombre = f.Texto("Hotel", "Nombre", "Nombre del hotel") ?? "" };
                r.Filas.Add(fila);
                string? mal = null;
                // Campaña: número o nombre; vacía = la de por defecto
                var tc = f.Texto("Campaña", "Campanya");
                var camp = tc is null ? porDefecto
                    : camps.FirstOrDefault(c => c.Id_Campanya.ToString() == tc.Trim() || ExcelTabla.Clave(c.Nombre) == ExcelTabla.Clave(tc));
                if (camp is null) mal = tc is null ? "Escribe la campaña (número o nombre, mira la hoja Instrucciones)." : $"La campaña «{tc}» no existe.";
                // Sección: por nombre; vacía = sin sección (queda oculto)
                var ts = f.Texto("Sección de la web", "Sección", "Seccion");
                var cont = ts is null ? null : conts.FirstOrDefault(c => ExcelTabla.Clave(c.Nombre) == ExcelTabla.Clave(ts));
                if (mal is null && ts is not null && cont is null) mal = $"La sección «{ts}» no existe. Mira la hoja Instrucciones.";
                var moneda = (f.Texto("Moneda") ?? "DOP").Trim().ToUpperInvariant();
                if (mal is null && !monedas.Contains(moneda)) mal = $"La moneda «{moneda}» no existe.";
                var regimen = f.Texto("Régimen", "Regimen"); var codigo = f.Texto("Código hotel", "Codigo hotel", "Código", "Codigo");
                if (mal is null && regimen is null) mal = "Escribe el régimen (ej. Todo Incluido).";
                if (mal is null && codigo is null) mal = "Escribe el código del hotel.";

                var dto = new HotelGuardarDto
                {
                    Id_Destino = fila.Nombre, CaratulaDestino = f.Texto("Destino"), Descripcion = $"{regimen} -{codigo}", Id_Idioma = HotelRepository.IdiomaHotel,
                    Id_Campanya = camp?.Id_Campanya, Id_Moneda = moneda, PrecioWeb = f.Cifra("Precio", "Precio web", "Precio desde"),
                    Noches = f.Cifra("Noches") is decimal n ? (int)Math.Round(n) : null,
                    Id_Pais_Origen = (f.Texto("País salida", "Pais salida", "País de salida", "País origen") ?? "").ToUpperInvariant(),
                    Origen = f.Texto("Ciudad salida", "Ciudad de salida", "Origen") ?? "",
                    Id_PaisDestino = (f.Texto("País destino", "Pais destino") ?? "DO").ToUpperInvariant(),
                    Latitud = f.Texto("Latitud"), Longitud = f.Texto("Longitud"), Id_Contenedor = cont?.Id_Contenedor,
                    Estado = f.SiNo(true, "Visible", "Activo"), Notas = f.Texto("Notas")
                };
                Preparar(dto);
                mal ??= CargaExcel.ErrorModelo(dto);
                if (mal is null && await RevisarAsync(dto) is var (_, m)) mal = m;
                if (mal is null && existentes.Contains(ExcelTabla.Clave(dto.Id_Destino))) mal = $"Ya hay un hotel «{dto.Id_Destino}» en la vitrina: edítalo en la lista.";
                if (mal is null && !vistas.Add(ExcelTabla.Clave(dto.Id_Destino))) mal = "Está repetido en el Excel.";
                fila.Nombre = dto.Id_Destino;
                fila.Detalle = $"{dto.CaratulaDestino} · {dto.Descripcion} · {(dto.PrecioWeb is decimal p ? p.ToString("N2") + " " + moneda : "sin precio")}";
                fila.Mensaje = mal; fila.Ok = mal is null;
                if (!fila.Ok || !guardar) continue;
                try
                {
                    fila.Id = await _repo.CrearAsync(dto);
                    r.Guardadas++;
                    await Auditar("Hotel_Crear", fila.Id.Value, new { dto, excel = archivo!.FileName, fila = f.Numero });
                }
                catch (Exception ex) { fila.Ok = false; fila.Mensaje = "No se pudo guardar: " + ex.Message; _logger.LogError(ex, "Cargar Excel: hotel fila {Fila}", f.Numero); }
            }
            r.Mensaje = CargaExcel.Mensaje(r, "hotel", "hoteles");
            return Json(r);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id)
        {
            var nuevo = await _repo.CambiarEstadoAsync(id);
            if (nuevo is null) return NotFound(new { mensaje = "No encontramos ese hotel." });
            await Auditar("Hotel_Estado", id, new { estado_nuevo = nuevo });
            return Json(new { ok = true, estado = nuevo, mensaje = nuevo == true ? "Ahora se ve en la web." : "Oculto de la web." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var eliminado = await _repo.EliminarAsync(id);
            if (eliminado is null) return NotFound(new { mensaje = "No encontramos ese hotel." });
            await _mm.EliminarProductoAsync(TipoMm, id.ToString());   // su foto (Eurovip.multimedia)
            await Auditar("Hotel_Eliminar", id, eliminado);
            _logger.LogInformation("Hotel {Id} eliminado por {Usuario}", id, User.Identity?.Name);
            return Json(new { ok = true, mensaje = $"\"{eliminado.Id_Destino}\" se eliminó." });
        }

        // ---------------- foto (Eurovip.multimedia, tipo HOTEL, por número de hotel) ----------------
        /// <summary>La foto de un hotel: la de Eurovip.multimedia o, si aún no tiene, la de antes por nombre.</summary>
        private async Task<object?> FotoAsync(int id, string? nombre)
        {
            var mm = (await _mm.ListarAsync(TipoMm, id.ToString())).FirstOrDefault(x => x.Uso == "PORTADA");
            if (mm is not null) return new { archivo = mm.Ruta, url = mm.Url, bytes = mm.Tamano_Bytes, ancho = mm.Ancho, alto = mm.Alto, nueva = true };
            var vieja = string.IsNullOrWhiteSpace(nombre) ? null : _img.Buscar(TipoImagen, nombre);
            return vieja is null ? null : new { archivo = vieja.Archivo, url = vieja.Url, bytes = vieja.Bytes, ancho = vieja.Ancho, alto = vieja.Alto, nueva = false };
        }

        [HttpGet]
        public async Task<IActionResult> Imagen(int? id, string? nombre) =>
            Json(new { carpeta = id is int n ? $"multimedia/hoteles/{n}/" : null, imagen = id is int i ? await FotoAsync(i, nombre) : null });

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> SubirImagen([FromForm] int id, IFormFile? archivo)
        {
            var h = await _repo.ObtenerAsync(id);
            if (h is null) return NotFound(new { mensaje = "Guarda primero el hotel: la foto va con él." });
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > 10_000_000) return BadRequest(new { mensaje = "La imagen pesa más de 10 MB." });
            if (await ImagenSegura.ExtensionAsync(archivo) is null) return BadRequest(new { mensaje = "El archivo no es una imagen JPG, PNG o WebP." });
            try
            {
                var existia = await FotoAsync(id, h.Id_Destino) is not null;
                await using var st = archivo.OpenReadStream();
                var img = await _mm.SubirAsync(TipoMm, id.ToString(), "PORTADA", st, archivo.FileName);
                await Auditar(existia ? "Hotel_ImagenCambiar" : "Hotel_ImagenSubir", id, new { img.Ruta, img.Tamano_Bytes, original = archivo.FileName });
                return Json(new { ok = true, imagen = new { archivo = img.Ruta, url = img.Url, bytes = img.Tamano_Bytes, ancho = img.Ancho, alto = img.Alto, nueva = true },
                                  mensaje = existia ? "Foto reemplazada." : "Foto guardada." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        /// <summary>Quita la foto del hotel (la nueva y, si quedaba, la de antes por nombre cuando ningún otro hotel se llama igual).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarImagen(int id)
        {
            var h = await _repo.ObtenerAsync(id);
            if (h is null) return NotFound(new { mensaje = "No encontramos ese hotel." });
            var quitada = await _mm.QuitarUsoAsync(TipoMm, id.ToString(), "PORTADA") > 0;
            var nombre = (h.Id_Destino ?? "").Trim();
            var mismoNombre = (await _repo.NombresAsync()).Count(n => _img.NombreArchivo(n) == _img.NombreArchivo(nombre));
            if (mismoNombre <= 1 && _img.Eliminar(TipoImagen, _img.NombreArchivo(nombre))) quitada = true;
            if (!quitada) return NotFound(new { mensaje = "Ese hotel no tenía foto." });
            await Auditar("Hotel_ImagenEliminar", id, new { nombre });
            return Json(new { ok = true, mensaje = "Foto eliminada." });
        }

        /// <summary>Galería: cada hotel con su foto (nueva o la de antes) y los hoteles que no tienen foto.</summary>
        [HttpGet]
        public async Task<IActionResult> Imagenes()
        {
            var tarjetas = await _repo.TarjetasAsync();
            var fotos = new List<object>();
            var sinFoto = new List<object>();
            foreach (var (id, nombre) in tarjetas)
            {
                var f = await FotoAsync(id, nombre);
                if (f is null) sinFoto.Add(new { id, nombre });
                else fotos.Add(new { id, nombre, foto = f });
            }
            return Json(new { fotos, sinFoto });
        }

        private Task Auditar(string tipo, int id, object cambios) =>
            _repo.AuditarAsync(tipo, id, cambios,
                User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
    }
}
