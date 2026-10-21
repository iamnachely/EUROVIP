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
            dto.Id_Destino = dto.Id_Destino?.Trim() ?? "";
            dto.CaratulaDestino = dto.CaratulaDestino?.Trim();
            var d = System.Text.RegularExpressions.Regex.Match(dto.Descripcion ?? "", @"^(.+?)\s*-\s*(\d+)\s*$");
            if (d.Success) dto.Descripcion = $"{d.Groups[1].Value.Trim()} -{d.Groups[2].Value}";
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(x => x.Key, x => x.Value!.Errors.First().ErrorMessage);
                return BadRequest(new { mensaje = "Revisa los campos marcados.", errores });
            }
            // Nada de nombres basura ("222") ni destinos que no existen o están mal escritos.
            dto.Id_Destino = ReglasDatos.Limpiar(dto.Id_Destino);
            dto.CaratulaDestino = ReglasDatos.Limpiar(dto.CaratulaDestino);
            if (ReglasDatos.Nombre("nombre de hotel", dto.Id_Destino, 3) is string malHotel)
                return BadRequest(new { mensaje = malHotel, errores = new Dictionary<string, string> { ["Id_Destino"] = malHotel } });
            if (await _repo.ErrorDestinoAsync(dto.CaratulaDestino) is string malDestino)
                return BadRequest(new { mensaje = malDestino, errores = new Dictionary<string, string> { ["CaratulaDestino"] = malDestino } });

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
