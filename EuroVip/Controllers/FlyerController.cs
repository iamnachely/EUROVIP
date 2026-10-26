using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Flyers (equivale a /Flyer/Hub de BackTiTan, sus 11 pantallas en una): destinos y precios, generar imágenes,
    /// galería, contactos y envío por WhatsApp (cuenta de BackTiTan) con progreso e historial.
    /// </summary>
    [AccesoEstricto]   // tiene teléfonos de clientes: sin el módulo no se lee nada
    public class FlyerController : Controller
    {
        private readonly IFlyerRepository _repo;
        private readonly IFlyerImagenService _img;
        private readonly WhatsAppService _wa;
        private readonly FlyerEnvioJob _job;
        private readonly ICircuitoRepository _db;
        private readonly ILogger<FlyerController> _logger;

        public FlyerController(IFlyerRepository repo, IFlyerImagenService img, WhatsAppService wa, FlyerEnvioJob job, ICircuitoRepository db, ILogger<FlyerController> logger)
        {
            _repo = repo;
            _img = img;
            _wa = wa;
            _job = job;
            _db = db;
            _logger = logger;
        }

        /// <summary>Permiso especial "Enviar flyers por WhatsApp" (Usuarios y accesos). Superadministrador siempre puede.</summary>
        private bool PuedeEnviar => HttpContext.Items[AccesoModuloFilter.ClavePermisos] is PermisosUsuario p && p.Puede(Modulos.FlyersEnviar);

        /// <summary>Centro de Flyers: elige Flyer Agencia (/Agencia) o Flyer Editar (clientes).</summary>
        public IActionResult Hub() => View();
        /// <summary>Flyer Editar: flyers para clientes (destinos tipo «Cliente»), galería, contactos y envío por WhatsApp.</summary>
        public IActionResult Editar() => View();
        public IActionResult Index() => RedirectToAction(nameof(Hub));

        // ================= EDITOR (copia del editor de Flyer Cliente de BackTiTan) =================
        // Mismas rutas y respuestas que FlyerClienteController de BackTiTan (la pantalla es la misma), con los datos de Eurovip:
        // destinos y precios de tipo «Cliente», hoteles de Solo Hotel y Paquetes, fondos de uploads/flyers/fondos y la galería de Flyer Editar.
        private string WebEurovip => (HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Eurovip:Web"] is { Length: > 0 } w ? w : "http://eurovip.com.do").TrimEnd('/');

        private async Task<List<FlyerDestino>> DestinosClienteAsync()
        {
            var destinos = await _repo.DestinosAsync(IFlyerRepository.Cliente);
            foreach (var d in destinos) d.Fondo = _img.FondoUrl(d.Nombre_Destino);
            return destinos;
        }

        public async Task<IActionResult> Editor()
        {
            var configs = await _repo.ConfigsAsync(IFlyerRepository.Cliente);
            var destinos = await DestinosClienteAsync();
            var fondos = new Dictionary<string, string>();
            foreach (var n in configs.Select(c => c.Nombre_Destino ?? c.Id_Destino).Concat(destinos.Select(d => d.Nombre_Destino)).Distinct())
                if (_img.FondoUrl(n) is string u) fondos[n] = u;
            return View(new FlyerEditorModelo
            {
                Destinos = destinos, Configs = configs, Fondos = fondos, Web = WebEurovip,
                WebTexto = System.Text.RegularExpressions.Regex.Replace(WebEurovip, "^https?://(www\\.)?", ""),
                UltimaActualizacion = destinos.Select(d => d.Fecha_Actualizacion).Where(f => f.HasValue).Max()
            });
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerDatos() => Json(await DestinosClienteAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarDestino(int id)
        {
            var cfg = (await _repo.ConfigsAsync(IFlyerRepository.Cliente)).FirstOrDefault(c => c.Id == id);
            if (cfg is null) return Json(new { success = false, mensaje = "Configuración no encontrada." });
            var n = await _repo.ActualizarPreciosAsync(id, IFlyerRepository.Cliente);
            return Json(n > 0 ? new { success = true, mensaje = $"Precios actualizados para {cfg.Nombre_Destino ?? cfg.Id_Destino}." }
                              : new { success = false, mensaje = "Sin ofertas a la venta para este destino (revisa campaña, origen y régimen)." });
        }

        /// <summary>
        /// Guarda en la galería de Flyer Editar el PNG que dibujó el editor. Como BackTiTan, el nombre tiene que llevar un destino
        /// de la configuración (así el envío «con link» sabe a qué página de la web lleva el botón).
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(25_000_000)]
        public async Task<IActionResult> GuardarImagen([FromBody] FlyerGuardarPngDto d)
        {
            if (string.IsNullOrWhiteSpace(d.ImageData)) return Json(new { success = false, mensaje = "Sin datos de imagen." });
            var configs = await _repo.ConfigsAsync(IFlyerRepository.Cliente);
            if (configs.Count == 0) return Json(new { success = false, mensaje = "No hay destinos configurados en Flyer Editar. Agrega al menos uno antes de guardar." });
            var nombre = string.IsNullOrWhiteSpace(d.NombreArchivo) ? d.NombreDestino ?? "" : d.NombreArchivo.Trim();
            var clave = AgenciaController.Slug(nombre);
            var destino = configs.Select(c => c.Nombre_Destino ?? c.Id_Destino).FirstOrDefault(x => clave.Contains(AgenciaController.Slug(x)));
            if (destino is null)
                return Json(new { success = false, mensaje = "El nombre del archivo debe contener un destino de la configuración. Destinos válidos: " + string.Join(", ", configs.Select(c => c.Nombre_Destino ?? c.Id_Destino)) });
            try
            {
                var img = await _img.GuardarPngAsync(d.ImageData, nombre);
                await Auditar("Flyer_EditorGuardar", new { img.Archivo, destino, d.LinkOferta });
                return Json(new { success = true, url = img.Url, nombre = img.Archivo });
            }
            catch (InvalidOperationException ex) { return Json(new { success = false, mensaje = ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> SubirImagenFondo(IFormFile? archivo, [FromForm] string? nombreDestino)
        {
            if (archivo is null || archivo.Length == 0) return Json(new { success = false, mensaje = "Sin archivo." });
            if (string.IsNullOrWhiteSpace(nombreDestino) || nombreDestino.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return Json(new { success = false, mensaje = "Nombre de destino inválido." });
            var ext = await ImagenSegura.ExtensionAsync(archivo);
            if (ext is null or "webp") return Json(new { success = false, mensaje = "El fondo debe ser JPG o PNG." });
            var url = await _img.GuardarFondoAsync(nombreDestino.Trim(), archivo, ext);
            await Auditar("Flyer_Fondo", new { destino = nombreDestino });
            return Json(new { success = true, url });
        }

        /// <summary>Buscador de destinos del editor: [{ value, text, id_pais }].</summary>
        [HttpGet]
        public async Task<IActionResult> BuscarDestinos(string? term)
        {
            if (string.IsNullOrWhiteSpace(term)) return Json(Array.Empty<object>());
            using var cn = _db.Conexion();
            var l = await Dapper.SqlMapper.QueryAsync<(string Destino, string Pais)>(cn,
                "SELECT TOP 20 id_destino, id_pais FROM Eurovip.destino WHERE id_idioma = 'ES' AND id_destino LIKE '%' + @term + '%' ORDER BY CASE WHEN id_destino LIKE @term + '%' THEN 0 ELSE 1 END, id_destino",
                new { term = term.Trim() });
            return Json(l.Select(x => new { value = x.Destino, text = x.Destino, id_pais = x.Pais }));
        }

        /// <summary>Hoteles con precio del destino elegido (o por nombre): [{ value, text, precio_web, desde, regimen, caratula }].</summary>
        [HttpGet]
        public async Task<IActionResult> BuscarHoteles(string? term, string? idDestino, string? idPaisDestino)
        {
            var con = await _repo.HotelesConPrecioAsync(term, idDestino);
            if (con.Count == 0 && !string.IsNullOrWhiteSpace(term))
                return Json((await _repo.HotelesPorNombreAsync(term)).Select(h => new { value = h.Id_Alojamiento.ToString(), text = h.Nombre }));
            return Json(con.Select(h => new { value = h.Id_Alojamiento.ToString(), text = h.Nombre, precio_web = h.Precio, desde = h.Desde, regimen = h.Regimen, caratula = h.Caratula }));
        }

        /// <summary>Precio más barato a la venta de un hotel: { nombre_hotel, regimen, nombre_habitacion, precio_final } o { error }.</summary>
        [HttpGet]
        public async Task<IActionResult> PrecioBarato(int idAlojamiento)
        {
            var h = (await _repo.HotelesConPrecioAsync(null, null, idAlojamiento)).FirstOrDefault();
            return h is null ? Json(new { error = "Sin precio disponible para este hotel" })
                             : Json(new { nombre_hotel = h.Nombre, regimen = h.Regimen, nombre_habitacion = h.Habitacion, precio_final = h.Precio });
        }

        [HttpGet]
        public async Task<IActionResult> Datos()
        {
            var configs = await _repo.ConfigsAsync();
            foreach (var c in configs) c.Fondo = _img.FondoUrl(c.Nombre_Destino ?? c.Id_Destino);
            var destinos = await _repo.DestinosAsync();
            foreach (var d in destinos) d.Fondo = _img.FondoUrl(d.Nombre_Destino);
            var contactos = await _repo.ContactosAsync();
            return Json(new
            {
                configs, destinos, galeria = _img.Galeria(), envios = await _repo.EnviosAsync(),
                contactos = new { total = contactos.Count, activos = contactos.Count(c => c.Activo) },
                whatsapp = _wa.Configurado, puedeEnviar = PuedeEnviar, estado = _job.Estado
            });
        }

        // ---------------- destinos ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Config([FromBody] FlyerConfig c)
        {
            if (!ModelState.IsValid) return Error();
            if (await _repo.ErrorDestinoAsync(c.Id_Destino) is string mal) return BadRequest(new { mensaje = mal });
            var id = await _repo.GuardarConfigAsync(c);
            await Auditar(c.Id > 0 ? "Flyer_ConfigEditar" : "Flyer_ConfigCrear", c);
            return Json(new { ok = true, id, mensaje = c.Id > 0 ? "Destino guardado." : "Destino agregado a los flyers." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfig(int id)
        {
            if (!await _repo.EliminarConfigAsync(id)) return NotFound(new { mensaje = "No encontramos ese destino." });
            await Auditar("Flyer_ConfigEliminar", new { id });
            return Json(new { ok = true, mensaje = "Destino quitado de los flyers." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPrecios(int? id)
        {
            var n = await _repo.ActualizarPreciosAsync(id);
            await Auditar("Flyer_Precios", new { id, destinosConPrecio = n });
            return Json(new { ok = true, mensaje = n == 0 ? "No hay ofertas a la venta que coincidan con los destinos configurados." : $"Precios al día en {n} destino(s)." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Generar([FromQuery] bool actualizar = true, [FromQuery] string? plantilla = null)
        {
            try
            {
                if (actualizar) await _repo.ActualizarPreciosAsync();
                var p = FlyerPlantillas.Buscar(plantilla);
                var imgs = await _img.GenerarAsync(await _repo.DestinosAsync(), p.Clave);
                await Auditar("Flyer_Generar", new { plantilla = p.Clave, imagenes = imgs.Select(i => i.Archivo) });
                return Json(new { ok = true, imagenes = imgs, mensaje = imgs.Count == 0 ? "No se generó ningún flyer: ningún destino tiene precios." : $"Se generaron {imgs.Count} flyer(s)." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando flyers");
                return StatusCode(500, new { mensaje = "No se pudieron generar: " + ex.Message });
            }
        }

        // ---------------- plantillas ----------------
        [HttpGet]
        public IActionResult Plantillas() => Json(FlyerPlantillas.Todas);

        /// <summary>Vista previa (PNG) de una plantilla: con un destino que tenga precios o, si no hay, con datos de ejemplo.</summary>
        [HttpGet, ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> Muestra(string? plantilla, string? destino)
        {
            var p = FlyerPlantillas.Buscar(plantilla);
            var destinos = await _repo.DestinosAsync();
            var d = destinos.FirstOrDefault(x => x.Hoteles.Count > 0 && (destino is null || string.Equals(x.Id_Destino, destino, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Nombre_Destino, destino, StringComparison.OrdinalIgnoreCase)))
                    ?? destinos.FirstOrDefault(x => x.Hoteles.Count > 0);
            return File(_img.Muestra(p.Clave, d), "image/png");
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Fondo([FromForm] string destino, IFormFile? archivo)
        {
            if (string.IsNullOrWhiteSpace(destino) || destino.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return BadRequest(new { mensaje = "Destino inválido." });
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > 8_000_000) return BadRequest(new { mensaje = "La imagen pesa más de 8 MB." });
            var ext = await ImagenSegura.ExtensionAsync(archivo);
            if (ext is null or "webp") return BadRequest(new { mensaje = "El fondo debe ser JPG o PNG." });
            var url = await _img.GuardarFondoAsync(destino, archivo, ext);
            await Auditar("Flyer_Fondo", new { destino });
            return Json(new { ok = true, url, mensaje = "Fondo actualizado." });
        }

        // ---------------- galería ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult EliminarImagen(string carpeta, string archivo) =>
            _img.Eliminar(carpeta, archivo) ? Json(new { ok = true, mensaje = "Flyer eliminado." }) : NotFound(new { mensaje = "No encontramos ese archivo." });

        [HttpGet]
        public IActionResult Descargar([FromQuery] string[] f)
        {
            var rutas = f.Select(x => x.Split('/', 2)).Where(p => p.Length == 2).Select(p => _img.Fisico(p[0], p[1])).Where(r => r is not null).Cast<string>().ToList();
            if (rutas.Count == 0) return NotFound("No hay flyers para descargar.");
            if (rutas.Count == 1) return PhysicalFile(rutas[0], "image/png", Path.GetFileName(rutas[0]));
            return File(_img.Zip(rutas), "application/zip", $"flyers-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        }

        // ---------------- contactos ----------------
        [HttpGet] public async Task<IActionResult> Contactos() => Json(await _repo.ContactosAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Contacto([FromBody] FlyerContacto c)
        {
            if (!ModelState.IsValid) return Error();
            var num = FlyerRepository.LimpiarNumero(c.Numero);
            if (num.Length is < 8 or > 15) return BadRequest(new { mensaje = "El número debe tener entre 8 y 15 dígitos, con código de país (ej. 34600111222)." });
            var id = await _repo.GuardarContactoAsync(c);
            if (id == -1) return BadRequest(new { mensaje = "Ese número ya está en la lista." });
            await Auditar(c.Id > 0 ? "Flyer_ContactoEditar" : "Flyer_ContactoCrear", new { id });
            return Json(new { ok = true, id, mensaje = c.Id > 0 ? "Contacto guardado." : "Contacto agregado." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarContacto(int id)
        {
            if (!await _repo.EliminarContactoAsync(id)) return NotFound(new { mensaje = "No encontramos ese contacto." });
            await Auditar("Flyer_ContactoEliminar", new { id });
            return Json(new { ok = true, mensaje = "Contacto eliminado." });
        }

        public class EstadoDto { public List<int> Ids { get; set; } = new(); public bool Activo { get; set; } }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EstadoContactos([FromBody] EstadoDto d)
        {
            var n = await _repo.EstadoContactosAsync(d.Ids, d.Activo);
            await Auditar("Flyer_ContactoEstado", d);
            return Json(new { ok = true, mensaje = $"{n} contacto(s) {(d.Activo ? "activados" : "desactivados")}." });
        }

        public class ImportarDto { public string Texto { get; set; } = ""; }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar([FromBody] ImportarDto d)
        {
            var (nuevos, repetidos, invalidos) = await _repo.ImportarAsync(d.Texto);
            await Auditar("Flyer_ContactoImportar", new { nuevos, repetidos, invalidos });
            return Json(new { ok = true, mensaje = $"{nuevos} nuevo(s) · {repetidos} ya estaban · {invalidos} sin número válido." });
        }

        // ---------------- envío ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Probar()
        {
            var (ok, mensaje) = await _wa.ProbarAsync();
            return ok ? Json(new { ok, mensaje }) : BadRequest(new { mensaje });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Enviar([FromBody] FlyerEnviarDto d)
        {
            if (!_wa.Configurado) return BadRequest(new { mensaje = "WhatsApp no está configurado en este servidor." });
            if (!PuedeEnviar) return StatusCode(403, new { mensaje = "No tienes el permiso \"Enviar flyers por WhatsApp\". Pídeselo a un administrador." });
            var rutas = d.Imagenes.Select(x => x.Split('/', 2)).Where(p => p.Length == 2).Select(p => _img.Fisico(p[0], p[1])).Where(r => r is not null).Cast<string>().ToList();
            if (rutas.Count == 0) return BadRequest(new { mensaje = "Elige al menos un flyer de la galería." });
            if (rutas.Count > 10) return BadRequest(new { mensaje = "Máximo 10 flyers por envío." });

            List<(string, string?)> dest;
            if (!string.IsNullOrWhiteSpace(d.NumeroPrueba))
            {
                var n = FlyerRepository.LimpiarNumero(d.NumeroPrueba);
                if (n.Length is < 8 or > 15) return BadRequest(new { mensaje = "Número de prueba inválido (con código de país)." });
                dest = new() { (n, "Prueba") };
            }
            else
            {
                var todos = await _repo.ContactosAsync();
                dest = todos.Where(c => c.Activo && (d.Contactos.Count == 0 || d.Contactos.Contains(c.Id)))
                            .Select(c => (FlyerRepository.LimpiarNumero(c.Numero), c.Nombre_Completo)).Where(x => x.Item1.Length >= 8).Distinct().ToList();
                if (dest.Count == 0) return BadRequest(new { mensaje = "No hay contactos activos para enviar." });
            }
            // Con link: el botón de cada flyer lleva a la página de su destino en la web (el destino sale del nombre de la imagen)
            List<string>? botones = null;
            var cfg = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var plantillaLink = cfg["Eurovip:Flyer:PlantillaClienteLink"] is { Length: > 0 } pl ? pl : "cliente_info";
            if (d.ConLink)
            {
                var destinos = (await _repo.ConfigsAsync(IFlyerRepository.Cliente)).Select(c => c.Nombre_Destino ?? c.Id_Destino).Distinct().OrderByDescending(x => x.Length).ToList();
                var formato = cfg["Eurovip:Flyer:BotonClienteInfo"] is { Length: > 0 } f ? f : "hotel/{destino}";
                botones = new();
                foreach (var ruta in rutas)
                {
                    var clave = AgenciaController.Slug(Path.GetFileNameWithoutExtension(ruta));
                    var destino = destinos.FirstOrDefault(x => clave.Contains(AgenciaController.Slug(x)));
                    if (destino is null) return BadRequest(new { mensaje = $"Con link, cada flyer tiene que ser de un destino: «{Path.GetFileName(ruta)}» no es de ninguno ({string.Join(", ", destinos)})." });
                    botones.Add(formato.Replace("{destino}", AgenciaController.Slug(destino)));
                }
            }
            var carpeta = d.Imagenes.First().Split('/')[0];
            if (!_job.Iniciar(dest, rutas, d.Texto, carpeta, User.Identity?.Name ?? "", botones, plantillaLink)) return Conflict(new { mensaje = "Ya hay un envío en curso. Espera a que termine o cancélalo." });
            await Auditar("Flyer_Enviar", new { destinatarios = dest.Count, prueba = !string.IsNullOrWhiteSpace(d.NumeroPrueba), imagenes = d.Imagenes, d.Texto, d.ConLink, botones });
            return Json(new { ok = true, mensaje = $"Envío iniciado a {dest.Count} contacto(s)." });
        }

        [HttpGet] public IActionResult Estado() => Json(_job.Estado);

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Cancelar() { _job.Cancelar(); return Json(new { ok = true, mensaje = "Cancelando… termina el contacto actual y se detiene." }); }

        private IActionResult Error() => BadRequest(new { mensaje = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        private Task Auditar(string tipo, object cambios) =>
            _db.AuditarAsync(tipo, Array.Empty<int>(), cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
