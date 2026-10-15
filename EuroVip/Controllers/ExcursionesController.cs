using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Mantenimiento de excursiones (Eurovip.servicio_excursion, copia de ViajeVDT): buscar, ver, cargar, editar,
    /// duplicar, eliminar y cambiar la foto. La foto no va en la base: como BackTiTan se relaciona por el nombre
    /// de la excursión sin espacios ni acentos → wwwroot/imagenes/excursiones/{Nombre}.webp.
    /// </summary>
    public class ExcursionesController : Controller
    {
        private const string TipoImagen = "excursiones";
        private readonly IExcursionRepository _repo;
        private readonly ICircuitoRepository _aud;
        private readonly IImagenesLocales _img;
        private readonly ILogger<ExcursionesController> _logger;

        public ExcursionesController(IExcursionRepository repo, ICircuitoRepository aud, IImagenesLocales img, ILogger<ExcursionesController> logger)
        {
            _repo = repo;
            _aud = aud;
            _img = img;
            _logger = logger;
        }

        public IActionResult Index() => View();

        /// <summary>Archivo de la foto a partir del nombre ("Excursión Catamarán (Party Boat)" → "ExcursionCatamaranPartyBoat.webp").</summary>
        private string Archivo(string? nombre)
        {
            var b = _img.NombreCircuito(nombre ?? "");
            if (b.Length > 120) b = b[..120];
            return b == "" ? "" : b + ".webp";
        }

        private Excursion ConImagen(Excursion e)
        {
            e.Imagen = _img.BuscarArchivo(TipoImagen, Archivo(e.Nombre))?.Url;
            return e;
        }

        [HttpGet]
        public async Task<IActionResult> Listar() => Json((await _repo.ListarAsync()).Select(ConImagen));

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
            dto.Nombre = ReglasDatos.Limpiar(dto.Nombre);
            if (ReglasDatos.Nombre("nombre de excursión", dto.Nombre, 3) is string malNombre)
                return BadRequest(new { mensaje = malNombre, errores = new Dictionary<string, string> { ["Nombre"] = malNombre } });
            var (malDestino, destino) = await _repo.ValidarDestinoAsync(dto.Id_Destino);
            if (malDestino is not null)
                return BadRequest(new { mensaje = malDestino, errores = new Dictionary<string, string> { ["Id_Destino"] = malDestino } });
            dto.Id_Destino = destino!;
            if (Archivo(dto.Nombre) == "")
                return BadRequest(new { mensaje = "El nombre tiene que llevar letras o números.", errores = new Dictionary<string, string> { ["Nombre"] = "Escribe un nombre con letras" } });
            if (await _repo.RepetidaAsync(dto.Nombre, dto.Id_Destino, dto.Id_Excursion) is int otra)
                return BadRequest(new { mensaje = $"Ya existe la excursión «{dto.Nombre}» en {dto.Id_Destino} (n.º {otra}). No se puede repetir.",
                                        errores = new Dictionary<string, string> { ["Nombre"] = "Ya existe en ese destino" } });

            try
            {
                var antes = dto.Id_Excursion is int idAntes ? await _repo.ObtenerAsync(idAntes) : null;
                if (dto.Id_Excursion is not null && antes is null) return NotFound(new { mensaje = "Esa excursión ya no existe. Recarga la página." });
                var id = await _repo.GuardarAsync(dto);

                // Si cambió el nombre, la foto pasa al nombre nuevo (se relaciona por nombre).
                var fotoMovida = antes is not null && Archivo(antes.Nombre) != Archivo(dto.Nombre)
                                 && _img.RenombrarArchivo(TipoImagen, Archivo(antes.Nombre), Archivo(dto.Nombre));

                await Auditar(antes is null ? "Excursion_Crear" : "Excursion_Editar", id, new { antes, despues = dto, fotoMovida });
                var e = ConImagen((await _repo.ObtenerAsync(id))!);
                return Json(new { ok = true, id, excursion = e, mensaje = antes is null ? $"¡Listo! Se creó «{dto.Nombre}»." : "Cambios guardados." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando la excursión {Nombre}", dto.Nombre);
                return StatusCode(500, new { mensaje = "No se pudo guardar: " + ex.Message });
            }
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
                Nombre = copia, Id_Destino = e.Id_Destino ?? "", Precio = e.Precio, Cancelable = e.Cancelable ?? false, Descripcion = e.Descripcion
            });
            await Auditar("Excursion_Duplicar", nuevo, new { de = id, nombre = copia });
            return Json(new { ok = true, id = nuevo, excursion = ConImagen((await _repo.ObtenerAsync(nuevo))!), mensaje = $"Se duplicó como «{copia}». Cámbiale lo que haga falta." });
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
            await Auditar("Excursion_Eliminar", id, new { antes = e, fotoBorrada });
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
                var img = await _img.GuardarComoAsync(TipoImagen, null, Archivo(e.Nombre), st);
                await Auditar("Excursion_Imagen", id, new { img.Archivo, img.Bytes });
                return Json(new { ok = true, imagen = img.Url, mensaje = "Foto guardada." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarImagen(int id)
        {
            var e = await _repo.ObtenerAsync(id);
            if (e is null) return NotFound(new { mensaje = "Esa excursión ya no existe." });
            var borrada = _img.Eliminar(TipoImagen, Archivo(e.Nombre));
            if (borrada) await Auditar("Excursion_QuitarImagen", id, new { archivo = Archivo(e.Nombre) });
            return Json(new { ok = true, mensaje = borrada ? "Foto quitada." : "No tenía foto." });
        }

        private Task Auditar(string tipo, int id, object cambios) =>
            _aud.AuditarAsync(tipo, new[] { id }, cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
