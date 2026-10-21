using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
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
            dto.Nombre = ReglasDatos.Limpiar(dto.Nombre);
            dto.Tipo = (dto.Tipo ?? "").Trim().ToUpperInvariant();
            dto.Categoria = (dto.Categoria ?? "").Trim().ToUpperInvariant();
            dto.Vehiculo = (dto.Vehiculo ?? "").Trim().ToUpperInvariant();
            dto.Cobro = (dto.Cobro ?? "").Trim().ToUpperInvariant();
            dto.Id_Estacion_Origen = ReglasDatos.Limpiar(dto.Id_Estacion_Origen).ToUpperInvariant();

            if (ReglasDatos.Nombre("nombre del traslado", dto.Nombre, 3) is string malNombre) return Mal("Nombre", malNombre);
            if (!TrasladoValores.Tipos.ContainsKey(dto.Tipo)) return Mal("Tipo", "Elige Compartido o Privado.");
            if (!TrasladoValores.Categorias.ContainsKey(dto.Categoria)) return Mal("Categoria", "Elige la categoría.");
            if (!TrasladoValores.Vehiculos.ContainsKey(dto.Vehiculo)) return Mal("Vehiculo", "Elige el vehículo.");
            if (!TrasladoValores.Cobros.ContainsKey(dto.Cobro)) return Mal("Cobro", "Elige cómo se cobra.");
            if (dto.Pasajeros_Max < dto.Pasajeros_Min) return Mal("Pasajeros_Max", "El máximo de pasajeros no puede ser menor que el mínimo.");
            if (dto.Precio_Ida is null && dto.Precio_Ida_Vuelta is null) return Mal("Precio_Ida_Vuelta", "Escribe al menos un precio: ida y vuelta o solo ida.");
            if (await _repo.ValidarEstacionAsync(dto.Id_Estacion_Origen) is string malEst) return Mal("Id_Estacion_Origen", malEst);
            var (malDestino, destino, _) = await _repo.ValidarDestinoAsync(dto.Id_Destino);
            if (malDestino is not null) return Mal("Id_Destino", malDestino);
            dto.Id_Destino = destino!;
            if (!dto.Todos_Hoteles)
            {
                if (dto.Hoteles.Count == 0) return Mal("Hoteles", "Elige al menos un hotel o marca «Todos los hoteles del destino».");
                var fuera = await _repo.HotelesQueNoSonDelDestinoAsync(dto.Hoteles, dto.Id_Destino);
                if (fuera.Count > 0) return Mal("Hoteles", $"Estos hoteles no son de {dto.Id_Destino}: {string.Join(", ", fuera)}. Quítalos de la lista.");
            }
            if (await _repo.RepetidoAsync(dto) is int otro)
                return Mal("Nombre", $"Ya existe ese traslado (n.º {otro}) con la misma ruta, tipo, categoría y vehículo.");

            try
            {
                var antes = dto.Id_Traslado is int idAntes ? await _repo.ObtenerAsync(idAntes) : null;
                if (dto.Id_Traslado is not null && antes is null) return NotFound(new { mensaje = "Ese traslado ya no existe. Recarga la página." });
                var id = await _repo.GuardarAsync(dto, User.Identity?.Name ?? "desconocido");
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
                Activo = false // la copia empieza desactivada para que no salga dos veces en la web
            };
            for (var i = 2; await _repo.RepetidoAsync(dto) is not null; i++) dto.Nombre = $"{t.Nombre} (copia {i})";
            var nuevo = await _repo.GuardarAsync(dto, User.Identity?.Name ?? "desconocido");
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
    }
}
