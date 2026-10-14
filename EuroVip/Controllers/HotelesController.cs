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
    /// </summary>
    public class HotelesController : Controller
    {
        private const string TipoImagen = "hoteles";
        private readonly IHotelRepository _repo;
        private readonly IImagenesLocales _img;
        private readonly ILogger<HotelesController> _logger;

        public HotelesController(IHotelRepository repo, IImagenesLocales img, ILogger<HotelesController> logger)
        {
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
            foreach (var h in resultado.Items) h.Imagen = _img.Buscar(TipoImagen, h.Id_Destino)?.Url;
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
            await Auditar("Hotel_Eliminar", id, eliminado);
            _logger.LogInformation("Hotel {Id} eliminado por {Usuario}", id, User.Identity?.Name);
            return Json(new { ok = true, mensaje = $"\"{eliminado.Id_Destino}\" se eliminó." });
        }

        // ---------------- imágenes (carpeta wwwroot/imagenes/hoteles, por nombre del hotel) ----------------
        [HttpGet]
        public IActionResult Imagen(string nombre) =>
            Json(new { archivo = _img.NombreArchivo(nombre ?? ""), imagen = _img.Buscar(TipoImagen, nombre ?? "") });

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> SubirImagen([FromForm] string nombre, IFormFile? archivo)
        {
            nombre = (nombre ?? "").Trim();
            if (_img.NombreArchivo(nombre) == "") return BadRequest(new { mensaje = "Primero escribe el nombre del hotel: la foto se guarda con ese nombre." });
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > 10_000_000) return BadRequest(new { mensaje = "La imagen pesa más de 10 MB." });
            if (await ImagenSegura.ExtensionAsync(archivo) is null) return BadRequest(new { mensaje = "El archivo no es una imagen JPG, PNG o WebP." });
            try
            {
                var existia = _img.Buscar(TipoImagen, nombre) is not null;
                await using var st = archivo.OpenReadStream();
                var img = await _img.GuardarAsync(TipoImagen, nombre, st);
                await Auditar(existia ? "Hotel_ImagenCambiar" : "Hotel_ImagenSubir", 0, new { nombre, img.Archivo, img.Bytes, original = archivo.FileName });
                return Json(new { ok = true, imagen = img, mensaje = existia ? "Foto reemplazada." : "Foto guardada." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarImagen(string archivo)
        {
            if (!_img.Eliminar(TipoImagen, archivo ?? "")) return NotFound(new { mensaje = "Esa foto ya no existe." });
            await Auditar("Hotel_ImagenEliminar", 0, new { archivo });
            return Json(new { ok = true, mensaje = "Foto eliminada." });
        }

        /// <summary>Galería: todas las fotos de la carpeta, con los hoteles que la usan, y los hoteles que no tienen foto.</summary>
        [HttpGet]
        public async Task<IActionResult> Imagenes()
        {
            var nombres = (await _repo.NombresAsync()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            var porArchivo = nombres.GroupBy(n => _img.NombreArchivo(n)).ToDictionary(g => g.Key, g => g.ToList());
            var fotos = _img.Listar(TipoImagen).Select(i => new { i.Archivo, i.Url, i.Bytes, i.Fecha, i.Ancho, i.Alto,
                hoteles = porArchivo.TryGetValue(i.Archivo, out var h) ? h : new List<string>() }).ToList();
            var conFoto = fotos.Select(f => f.Archivo).ToHashSet();
            var sinFoto = porArchivo.Where(p => p.Key != "" && !conFoto.Contains(p.Key)).SelectMany(p => p.Value).OrderBy(n => n).ToList();
            return Json(new { fotos, sinFoto });
        }

        private Task Auditar(string tipo, int id, object cambios) =>
            _repo.AuditarAsync(tipo, id, cambios,
                User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
    }
}
