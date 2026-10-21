using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EuroVip.Controllers
{
    /// <summary>
    /// "Herramientas" del menú: un solo lugar con todos los mantenimientos básicos
    /// (destinos, monedas, campañas, países, idiomas, tasas, regímenes, estaciones, proveedores,
    /// alojamientos y habitaciones). Index = tablero con tarjetas; Catalogo/{id} = la vista de cada uno.
    /// </summary>
    public class HerramientasController : Controller
    {
        private readonly IHerramientasRepository _repo;
        private readonly ICircuitoRepository _aud;
        private readonly IImagenesLocales _img;
        private readonly IMultimedia _mm;
        private readonly ILogger<HerramientasController> _logger;

        public HerramientasController(IHerramientasRepository repo, ICircuitoRepository aud, IImagenesLocales img, ILogger<HerramientasController> logger, IMultimedia mm)
        {
            _mm = mm;
            _repo = repo;
            _aud = aud;
            _img = img;
            _logger = logger;
        }

        /// <summary>
        /// Subir la imagen de un campo tipo "imagen" de cualquier catálogo (Destinos, Alojamientos…). Se guarda en
        /// wwwroot/imagenes/{carpeta del campo}/{nombre}.webp y devuelve la ruta para ese campo. El registro se actualiza
        /// cuando el usuario pulsa Guardar (aquí solo se guarda el archivo).
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> SubirImagen(string id, string campo, [FromForm] string? nombre, IFormFile? archivo)
        {
            var c = HrCatalogos.Buscar(id);
            var f = c?.Campo(campo ?? "");
            if (c is null || f is null || f.Tipo != "imagen" || string.IsNullOrEmpty(f.Carpeta)) return NotFound(new { mensaje = "Ese campo no admite imágenes." });
            var archivoNombre = _img.NombreArchivo(nombre ?? "");
            if (archivoNombre == "") return BadRequest(new { mensaje = $"Primero completa «{c.Campo(f.NombreDe ?? "")?.Etiqueta ?? "el nombre"}»: la imagen se guarda con ese nombre." });
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > 10_000_000) return BadRequest(new { mensaje = "La imagen pesa más de 10 MB." });
            if (await ImagenSegura.ExtensionAsync(archivo) is null) return BadRequest(new { mensaje = "El archivo no es una imagen JPG, PNG o WebP." });
            try
            {
                await using var st = archivo.OpenReadStream();
                // Destinos y Alojamientos: la portada va en Eurovip.multimedia (multimedia/destinos/{slug}/, multimedia/alojamientos/{id}/)
                if (HrCatalogos.Fotos(c) is { } mmc)
                {
                    var mm = await _mm.SubirAsync(mmc.Tipo, ReglasDatos.Limpiar(nombre), "PORTADA", st, archivo.FileName);
                    await Auditar($"Herramienta_{c.Clave}_Imagen", new { campo = f.Col, nombre, mm.Ruta, mm.Tamano_Bytes });
                    return Json(new { ok = true, ruta = mm.Url, url = mm.Url, archivo = mm.Ruta, mensaje = "Imagen guardada." });
                }
                var img = await _img.GuardarComoAsync(f.Carpeta, null, archivoNombre, st);
                await Auditar($"Herramienta_{c.Clave}_Imagen", new { campo = f.Col, nombre, img.Archivo, img.Bytes });
                return Json(new { ok = true, ruta = $"/imagenes/{f.Carpeta}/{img.Archivo}", url = img.Url, archivo = img.Archivo,
                                  mensaje = "Imagen subida. Pulsa Guardar para dejarla en el registro." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        public IActionResult Index() => View();

        public IActionResult Catalogo(string id)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return RedirectToAction(nameof(Index));
            return View(c);
        }

        [HttpGet] public async Task<IActionResult> Resumen() => Json(await _repo.ResumenAsync());
        [HttpGet] public async Task<IActionResult> Actividad() => Json(await _repo.ActividadAsync(8));

        /// <summary>Datos inconsistentes ya guardados en un mantenimiento (nombres basura, repetidos, país que no concuerda…).</summary>
        [HttpGet]
        public async Task<IActionResult> Revisar(string id)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            return Json(await _repo.RevisarAsync(c));
        }

        /// <summary>Cuántos datos a revisar tiene cada mantenimiento (tablero de Herramientas).</summary>
        [HttpGet] public async Task<IActionResult> RevisarResumen() => Json(await _repo.RevisarResumenAsync());

        [HttpGet]
        public async Task<IActionResult> Buscar(string? q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Json(Array.Empty<object>());
            return Json(await _repo.BuscarGlobalAsync(q));
        }

        [HttpGet]
        public async Task<IActionResult> Listar(string id, string? q, int pagina = 1, int tam = 25)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            var filtros = Request.Query.Where(x => x.Key.StartsWith("f_")).ToDictionary(x => x.Key[2..], x => x.Value.ToString());
            var (filas, total) = await _repo.ListarAsync(c, q, filtros, pagina, tam);
            return Json(new { filas, total, pagina = Math.Max(1, pagina), tam = Math.Clamp(tam, 10, 100) });
        }

        [HttpGet]
        public async Task<IActionResult> Lista(string nombre, string? q)
        {
            try { return Json(await _repo.ListaAsync(nombre, q)); }
            catch (ArgumentException) { return BadRequest(new { mensaje = "Lista desconocida." }); }
        }

        public class PeticionDto
        {
            public Dictionary<string, JsonElement> Clave { get; set; } = new();
            public Dictionary<string, JsonElement> Datos { get; set; } = new();
            public bool Nuevo { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Obtener(string id, [FromBody] PeticionDto dto)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            var fila = await _repo.ObtenerAsync(c, dto.Clave);
            if (fila is null) return NotFound(new { mensaje = $"No encontramos {c.Ese} {c.Singular}." });
            var usos = await _repo.UsosAsync(c, dto.Clave);
            return Json(new { fila, usos = usos.Select(u => new { u.Etiqueta, u.N }) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar(string id, [FromBody] PeticionDto dto)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            try
            {
                var (clave, error) = await _repo.GuardarAsync(c, dto.Datos, dto.Nuevo);
                if (error is not null) return BadRequest(new { mensaje = error });
                await Auditar($"Herramienta_{c.Clave}_{(dto.Nuevo ? "Crear" : "Editar")}", new { clave, datos = dto.Datos });
                return Json(new { ok = true, clave, mensaje = dto.Nuevo ? $"Se creó {c.El} {c.Singular}." : $"Se guardaron los cambios de {c.El} {c.Singular}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando {Catalogo}", c.Clave);
                return StatusCode(500, new { mensaje = "No se pudo guardar: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(string id, [FromBody] PeticionDto dto)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            var antes = await _repo.ObtenerAsync(c, dto.Clave);
            var error = await _repo.EliminarAsync(c, dto.Clave);
            if (error is not null) return BadRequest(new { mensaje = error });
            // Sus fotos de Eurovip.multimedia también se van (portada y galería)
            if (HrCatalogos.Fotos(c) is { } mmc && dto.Clave.TryGetValue(mmc.Col, out var k) && k.ToString() is { Length: > 0 } valor)
                await _mm.EliminarProductoAsync(mmc.Tipo, valor);
            await Auditar($"Herramienta_{c.Clave}_Eliminar", new { antes });
            return Json(new { ok = true, mensaje = $"Se eliminó {c.El} {c.Singular}." });
        }

        /// <summary>Cuántas fotos hay en Eurovip.multimedia por tipo y uso (panel "Fotos" de Herramientas).</summary>
        [HttpGet]
        public async Task<IActionResult> MultimediaResumen()
        {
            using var cn = _aud.Conexion();
            var filas = await Dapper.SqlMapper.QueryAsync<(string Tipo, string Uso, int N)>(cn, "SELECT tipo, uso, COUNT(*) FROM Eurovip.multimedia GROUP BY tipo, uso");
            return Json(filas.Select(f => new { f.Tipo, f.Uso, f.N }));
        }

        /// <summary>
        /// Pasa a Eurovip.multimedia las fotos de antes (wwwroot/imagenes): excursiones, traslados, portadas de circuito, destinos,
        /// alojamientos y hoteles. No toca el itinerario ni el slider, no borra nada y se puede repetir. Solo Superadministrador.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> MigrarImagenes()
        {
            if ((HttpContext.Items[AccesoModuloFilter.ClavePermisos] as PermisosUsuario)?.EsSuper != true)
                return StatusCode(403, new { mensaje = "Solo un Superadministrador puede pasar las fotos." });
            var r = await _mm.MigrarImagenesViejasAsync();
            await Auditar("Herramienta_MigrarImagenes", new { r.Pasadas, r.YaEstaban, sinArchivo = r.SinArchivo.Count, r.Errores });
            var total = r.Pasadas.Values.Sum();
            return Json(new { ok = true, pasadas = r.Pasadas, yaEstaban = r.YaEstaban, sinArchivo = r.SinArchivo, errores = r.Errores,
                              mensaje = total == 0 ? "No había fotos nuevas que pasar." : $"Se pasaron {total} fotos a multimedia." });
        }

        private Task Auditar(string tipo, object cambios) =>
            _aud.AuditarAsync(tipo, Array.Empty<int>(), cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
