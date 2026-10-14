using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Catálogo de circuitos (equivale a /Circuito/Index de BackTiTan con todos sus formularios:
    /// crear, destinos, hoteles por categoría, itinerario día a día, categorías, salidas cargadas y eliminar).
    /// También expone las búsquedas que usan las pantallas de carga.
    /// </summary>
    public class CircuitosController : Controller
    {
        private readonly ICircuitoRepository _repo;
        private readonly IImagenesLocales _img;
        private readonly ILogger<CircuitosController> _logger;

        public CircuitosController(ICircuitoRepository repo, IImagenesLocales img, ILogger<CircuitosController> logger)
        {
            _repo = repo;
            _img = img;
            _logger = logger;
        }

        public IActionResult Index() => View();

        // ------------------------------------------------------------ catálogo

        [HttpGet] public async Task<IActionResult> Listar(string? q, string? pais) => Json(await _repo.ListarAsync(q, pais));

        [HttpGet]
        public async Task<IActionResult> Obtener(string codigo)
        {
            var c = await _repo.ObtenerAsync(codigo);
            return c is null ? NotFound(new { mensaje = "No encontramos ese circuito." }) : Json(c);
        }

        [HttpGet]
        public async Task<IActionResult> Disponible(string? codigo, string? nombre, string? excepto)
        {
            var codigoOcupado = !string.IsNullOrWhiteSpace(codigo) && await _repo.CodigoExisteAsync(codigo, excepto);
            var nombreOcupado = !string.IsNullOrWhiteSpace(nombre) && await _repo.NombreExisteAsync(nombre, excepto);
            return Json(new { codigoOcupado, nombreOcupado });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] CircuitoGuardarDto dto)
        {
            if (!ModelState.IsValid) return Errores();

            var errores = new List<string>();
            var excepto = string.IsNullOrWhiteSpace(dto.CodigoOriginal) ? null : dto.CodigoOriginal.Trim();
            if (await _repo.CodigoExisteAsync(dto.Codigo, excepto)) errores.Add($"Ya existe un circuito con el código \"{dto.Codigo}\".");
            if (await _repo.NombreExisteAsync(dto.Nombre, excepto)) errores.Add($"Ya existe un circuito llamado \"{dto.Nombre}\".");
            if (dto.Destinos.Count == 0) errores.Add("Agrega al menos un destino.");
            // Cada destino tiene que existir en Herramientas › Destinos y ser del país indicado (nada de "-pego", "65401"…).
            using (var cn = _repo.Conexion())
                foreach (var d in dto.Destinos)
                    if (await ReglasDatos.ErrorDestinoAsync(cn, d.Id_Destino, d.Id_Pais) is string e) errores.Add(e);
            if (errores.Count > 0) return BadRequest(new { mensaje = errores[0], lista = errores });

            try
            {
                await _repo.GuardarAsync(dto);
                await Auditar(excepto is null ? "Circuito_Crear" : "Circuito_Editar", dto);
                return Json(new { ok = true, codigo = dto.Codigo.Trim(), mensaje = excepto is null ? $"Circuito \"{dto.Nombre}\" creado." : "Cambios guardados." });
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                _logger.LogWarning(ex, "FK al guardar circuito {Codigo}", dto.Codigo);
                return BadRequest(new { mensaje = "Algún destino, hotel o proveedor no existe en los catálogos. Revisa los datos." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando circuito {Codigo}", dto.Codigo);
                return StatusCode(500, new { mensaje = "No se pudo guardar el circuito. " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(string codigo)
        {
            var c = await _repo.ObtenerAsync(codigo);
            if (c is null) return NotFound(new { mensaje = "No encontramos ese circuito." });
            await _repo.EliminarAsync(codigo);
            await Auditar("Circuito_Eliminar", new { c.Codigo, c.Nombre, Salidas = c.Salidas.Count });
            return Json(new { ok = true, mensaje = $"\"{c.Nombre}\" se eliminó" + (c.Salidas.Count > 0 ? $" junto con {c.Salidas.Count} salidas." : ".") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EstadoSalida(int id, bool activo)
        {
            var n = await _repo.CambiarEstadoSalidaAsync(id, activo);
            if (n == 0) return NotFound(new { mensaje = "No encontramos esa salida." });
            await Auditar("Circuito_SalidaEstado", new { id, activo }, id);
            return Json(new { ok = true, mensaje = activo ? "Salida activada." : "Salida desactivada." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSalida(int id)
        {
            await _repo.EliminarSalidaAsync(id);
            await Auditar("Circuito_SalidaEliminar", new { id }, id);
            return Json(new { ok = true, mensaje = $"Salida #{id} eliminada." });
        }

        // ------------------------------------------------------------ categorías

        [HttpGet] public async Task<IActionResult> Categorias() => Json(await _repo.CategoriasAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarCategoria([FromBody] CategoriaDto dto)
        {
            if (!ModelState.IsValid) return Errores();
            var error = await _repo.GuardarCategoriaAsync(dto);
            if (error is not null) return BadRequest(new { mensaje = error });
            await Auditar("Categoria_Guardar", dto);
            return Json(new { ok = true, mensaje = "Categoría guardada." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCategoria(string categoria)
        {
            var error = await _repo.EliminarCategoriaAsync(categoria);
            if (error is not null) return BadRequest(new { mensaje = error });
            await Auditar("Categoria_Eliminar", new { categoria });
            return Json(new { ok = true, mensaje = $"Categoría \"{categoria}\" eliminada." });
        }

        // ------------------------------------------------------------ búsquedas (también para las cargas)

        [HttpGet] public async Task<IActionResult> Proveedores(string? tipo, string? q) => Json(await _repo.ProveedoresAsync(tipo, q));
        [HttpGet] public async Task<IActionResult> Paises() => Json(await _repo.PaisesAsync());
        [HttpGet] public async Task<IActionResult> Destinos(string? q, string? pais) => Json(await _repo.DestinosAsync(q, pais));
        [HttpGet] public async Task<IActionResult> Hoteles(string? q, string? destino, string? pais) => Json(await _repo.HotelesAsync(q, destino, pais));
        [HttpGet] public async Task<IActionResult> Aeropuertos(string? q, string? pais) => Json(await _repo.AeropuertosAsync(q, pais));
        [HttpGet] public async Task<IActionResult> Campanyas() => Json(await _repo.CampanyasAsync());
        [HttpGet] public async Task<IActionResult> Tasas() => Json(await _repo.TasasAsync());
        [HttpGet] public async Task<IActionResult> Regimenes() => Json(await _repo.RegimenesAsync());
        [HttpGet] public async Task<IActionResult> Monedas() => Json(await _repo.MonedasAsync());
        [HttpGet] public async Task<IActionResult> PrecioMinimo() => Json(await _repo.PrecioBaseMinimoAsync());

        private IActionResult Errores()
        {
            var lista = ModelState.Where(x => x.Value?.Errors.Count > 0)
                                  .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                                  .Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
            return BadRequest(new { mensaje = lista.FirstOrDefault() ?? "Revisa los datos.", lista });
        }

        // ------------------------------------------------------------ imágenes (carpetas del proyecto, como BackTiTan)
        // Portada:  wwwroot/imagenes/circuitos/{NombreSinEspacios}.webp              → circuito.caratula = "{NombreSinEspacios}.webp"
        // Día:      wwwroot/imagenes/circuitos/itinerario/es/{Nombre}-itinerario-dia-{n}.webp → circuito_itinerario.caratula = "/imagenes/circuitos/itinerario/es/…"
        // La ruta se guarda en el circuito al pulsar "Guardar circuito".

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> SubirPortada([FromForm] string nombre, IFormFile? archivo)
        {
            var baseNombre = _img.NombreCircuito(nombre ?? "");
            if (baseNombre == "") return BadRequest(new { mensaje = "Primero escribe el nombre del circuito: la imagen se guarda con ese nombre." });
            var mal = await ValidarImagen(archivo);
            if (mal is not null) return BadRequest(new { mensaje = mal });
            try
            {
                await using var st = archivo!.OpenReadStream();
                var img = await _img.GuardarComoAsync("circuitos", null, baseNombre + ".webp", st);
                await Auditar("Circuito_ImagenPortada", new { nombre, img.Archivo, img.Bytes });
                return Json(new { ok = true, caratula = img.Archivo, url = img.Url, imagen = img, mensaje = "Portada cargada. Se guarda en el circuito al pulsar Guardar." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> SubirImagenDia([FromForm] string nombre, [FromForm] int dia, IFormFile? archivo)
        {
            var baseNombre = _img.NombreCircuito(nombre ?? "");
            if (baseNombre == "") return BadRequest(new { mensaje = "Primero escribe el nombre del circuito: la imagen se guarda con ese nombre." });
            if (dia is < 1 or > 99) return BadRequest(new { mensaje = "Día inválido." });
            var mal = await ValidarImagen(archivo);
            if (mal is not null) return BadRequest(new { mensaje = mal });
            try
            {
                const string sub = "itinerario/es";
                await using var st = archivo!.OpenReadStream();
                var img = await _img.GuardarComoAsync("circuitos", sub, $"{baseNombre}-itinerario-dia-{dia}.webp", st);
                await Auditar("Circuito_ImagenDia", new { nombre, dia, img.Archivo, img.Bytes });
                return Json(new { ok = true, caratula = $"/imagenes/circuitos/{sub}/{img.Archivo}", url = img.Url, imagen = img, mensaje = $"Imagen del día {dia} cargada. Se guarda al pulsar Guardar." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        private static async Task<string?> ValidarImagen(IFormFile? archivo)
        {
            if (archivo is null || archivo.Length == 0) return "Elige una imagen.";
            if (archivo.Length > 10_000_000) return "La imagen pesa más de 10 MB.";
            return await ImagenSegura.ExtensionAsync(archivo) is null ? "El archivo no es una imagen JPG, PNG o WebP." : null;
        }

        private Task Auditar(string tipo, object cambios, int? id = null) =>
            _repo.AuditarAsync(tipo, id is null ? Array.Empty<int>() : new[] { id.Value }, cambios,
                User.Identity?.Name ?? "desconocido", HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
