using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Flyer Agencia (equivale a /Agencia de BackTiTan, sus pantallas en una): flyers con los hoteles más baratos de cada destino
    /// pensados para AGENCIAS, la lista de agencias, links únicos de su editor (/agencia/editor/{token}), envío masivo por WhatsApp
    /// (plantilla «oferta_agencia») con costo previo, progreso, monitor y reporte. Se abre desde /Flyer/Hub.
    /// Destinos y precios: flyer_config / flyer_datos con tipo = 'Agencia' (los de Flyer Editar son tipo 'Cliente').
    /// </summary>
    [AccesoEstricto]   // tiene teléfonos de agencias: sin el módulo Flyers no se lee nada
    public class AgenciaController : Controller
    {
        private const string Tipo = IFlyerRepository.Agencia;
        private const string Modulo = "Agencia";
        private readonly IFlyerRepository _flyers;
        private readonly IAgenciaRepository _repo;
        private readonly IFlyerImagenService _img;
        private readonly AgenciaImagenes _arch;
        private readonly WhatsAppService _wa;
        private readonly AgenciaEnvioJob _job;
        private readonly ICircuitoRepository _db;
        private readonly IConfiguration _cfg;
        private readonly ILogger<AgenciaController> _logger;

        public AgenciaController(IFlyerRepository flyers, IAgenciaRepository repo, IFlyerImagenService img, AgenciaImagenes arch, WhatsAppService wa,
                                 AgenciaEnvioJob job, ICircuitoRepository db, IConfiguration cfg, ILogger<AgenciaController> logger)
        {
            _flyers = flyers;
            _repo = repo;
            _img = img;
            _arch = arch;
            _wa = wa;
            _job = job;
            _db = db;
            _cfg = cfg;
            _logger = logger;
        }

        private bool PuedeEnviar => HttpContext.Items[AccesoModuloFilter.ClavePermisos] is PermisosUsuario p && p.Puede(Modulos.FlyersEnviar);
        /// <summary>Web pública de Eurovip (pie del flyer y página del destino que va en el mensaje).</summary>
        private string Web => (_cfg["Eurovip:Web"] is { Length: > 0 } w ? w : "http://eurovip.com.do").TrimEnd('/');
        /// <summary>Dominio del back para armar el link del editor (el mismo de esta pantalla, salvo que se configure otro).</summary>
        private string BaseEditor => _cfg["Eurovip:Flyer:UrlEditor"] is { Length: > 0 } u ? u.TrimEnd('/') : $"{Request.Scheme}://{Request.Host}";
        private decimal CostoMensaje => decimal.TryParse(_cfg["Eurovip:Flyer:CostoMensajeUsd"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : 0.07m;
        private string UrlLink(string token) => $"{BaseEditor}/agencia/editor/{token}";

        /// <summary>«Punta Cana» → «punta-cana» (igual que la web: /hotel/punta-cana).</summary>
        public static string Slug(string? texto) =>
            System.Text.RegularExpressions.Regex.Replace(FlyerImagenService.Seguro(texto ?? "").ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        private string UrlDestino(string nombre) => $"{Web}/hotel/{Slug(nombre)}";

        public IActionResult Index() => View();

        /// <summary>Todo lo que pinta la pantalla en una llamada.</summary>
        [HttpGet]
        public async Task<IActionResult> Datos()
        {
            await _repo.MarcarInterrumpidosAsync(Modulo, _job.EnCurso);
            var configs = await _flyers.ConfigsAsync(Tipo);
            foreach (var c in configs) c.Fondo = _img.FondoUrl(c.Nombre_Destino ?? c.Id_Destino);
            var destinos = await _flyers.DestinosAsync(Tipo);
            foreach (var d in destinos) d.Fondo = _img.FondoUrl(d.Nombre_Destino);
            var links = await _repo.LinksAsync();
            foreach (var l in links) l.Url = UrlLink(l.Token);
            return Json(new
            {
                configs, destinos, galeria = _arch.Listar(), agencias = await _repo.AgenciasAsync(), links, envios = await _repo.EnviosAsync(Modulo),
                whatsapp = _wa.Configurado, puedeEnviar = PuedeEnviar, enCurso = _job.EnCurso, plantilla = _job.Plantilla, costo = CostoMensaje,
                web = Web, logo = "/img/flyer/logo-eurovip-travel.png"
            });
        }

        // ================= destinos y precios (tipo Agencia) =================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Config([FromBody] FlyerConfig c)
        {
            c.Tipo = Tipo;
            if (!ModelState.IsValid) return Error();
            if (await _flyers.ErrorDestinoAsync(c.Id_Destino) is string mal) return BadRequest(new { mensaje = mal });
            var id = await _flyers.GuardarConfigAsync(c);
            await Auditar(c.Id > 0 ? "Agencia_ConfigEditar" : "Agencia_ConfigCrear", c);
            return Json(new { ok = true, id, mensaje = "Destino guardado. Pulsa «Actualizar precios» para traer sus hoteles." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConfig(int id)
        {
            if (!(await _flyers.ConfigsAsync(Tipo)).Any(c => c.Id == id)) return NotFound(new { mensaje = "No encontramos ese destino." });
            await _flyers.EliminarConfigAsync(id);
            await Auditar("Agencia_ConfigEliminar", new { id });
            return Json(new { ok = true, mensaje = "Destino quitado de Flyer Agencia." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPrecios(int? id)
        {
            var n = await _flyers.ActualizarPreciosAsync(id, Tipo);
            return Json(new { ok = true, mensaje = n == 0 ? "No hay ofertas a la venta para esos destinos (revisa campaña, origen y régimen)." : $"Precios actualizados: {n} destino(s) con hoteles." });
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
            await Auditar("Agencia_Fondo", new { destino });
            return Json(new { ok = true, url, mensaje = "Fondo actualizado (es el mismo para Flyer Agencia y Flyer Editar)." });
        }

        // ================= galería =================
        /// <summary>Guarda el flyer que la pantalla dibujó (PNG en base64) para un destino.</summary>
        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(25_000_000)]
        public async Task<IActionResult> GuardarImagen([FromBody] GuardarImagenDto d)
        {
            var c = (await _flyers.ConfigsAsync(Tipo)).FirstOrDefault(x => string.Equals(x.Id_Destino, d.Id_Destino, StringComparison.OrdinalIgnoreCase));
            if (c is null) return BadRequest(new { mensaje = "Ese destino no está en Flyer Agencia." });
            try { return Json(new { ok = true, imagen = await _arch.GuardarPngAsync(d.ImageData, c.Id_Destino) }); }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult LimpiarImagenes() => Json(new { ok = true, borradas = _arch.LimpiarFlyers() });

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> SubirImagenes(List<IFormFile> archivos, [FromForm] string? destino)
        {
            if (archivos.Count == 0) return BadRequest(new { mensaje = "Elige al menos una imagen." });
            var subidas = new List<string>(); var errores = new List<string>();
            foreach (var a in archivos)
                try { subidas.Add(await _arch.SubirAsync(a, destino)); }
                catch (InvalidOperationException ex) { errores.Add(ex.Message); }
            await Auditar("Agencia_SubirImagenes", new { subidas, destino });
            return Json(new { ok = subidas.Count > 0, mensaje = $"{subidas.Count} imagen(es) subida(s)." + (errores.Count > 0 ? " " + string.Join(" ", errores) : "") });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult EliminarImagen(string archivo) =>
            _arch.Eliminar(archivo) ? Json(new { ok = true, mensaje = "Imagen eliminada." }) : NotFound(new { mensaje = "No encontramos esa imagen." });

        // ================= agencias =================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Agencia([FromBody] FlyerAgencia a)
        {
            if (!ModelState.IsValid) return Error();
            try
            {
                var id = await _repo.GuardarAgenciaAsync(a);
                await Auditar(a.Id > 0 ? "Agencia_Editar" : "Agencia_Crear", a);
                return Json(new { ok = true, id, mensaje = a.Id > 0 ? "Agencia guardada." : $"Agencia «{a.Nombre}» agregada." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarAgencia(int id)
        {
            if (!await _repo.EliminarAgenciaAsync(id)) return NotFound(new { mensaje = "No encontramos esa agencia." });
            await Auditar("Agencia_Eliminar", new { id });
            return Json(new { ok = true, mensaje = "Agencia eliminada." });
        }

        public class EstadoDto { public List<int>? Ids { get; set; } public bool Activo { get; set; } }

        /// <summary>Activar / pausar las elegidas (sin ids = todas, como «Inactivar todas» de BackTiTan).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EstadoAgencias([FromBody] EstadoDto d)
        {
            var n = await _repo.EstadoAgenciasAsync(d.Ids, d.Activo);
            await Auditar("Agencia_Estado", d);
            return Json(new { ok = true, mensaje = $"{n} agencia(s) {(d.Activo ? "activadas" : "pausadas")}." });
        }

        public class ImportarDto { public string Texto { get; set; } = ""; }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportarAgencias([FromBody] ImportarDto d)
        {
            var (nuevas, repetidas, invalidas) = await _repo.ImportarAsync(d.Texto);
            await Auditar("Agencia_Importar", new { nuevas, repetidas, invalidas });
            return Json(new { ok = true, mensaje = $"{nuevas} agencia(s) nueva(s) · {repetidas} ya estaban · {invalidas} línea(s) sin un WhatsApp válido." });
        }

        // ================= links únicos =================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Link([FromBody] AgenciaLinkDto d)
        {
            if (!ModelState.IsValid) return Error();
            var c = (await _flyers.ConfigsAsync(Tipo)).FirstOrDefault(x => string.Equals(x.Id_Destino, d.Id_Destino, StringComparison.OrdinalIgnoreCase));
            if (c is null) return BadRequest(new { mensaje = "Elige un destino de Flyer Agencia." });
            var nombre = d.Nombre_Agencia?.Trim();
            var email = d.Email_Agencia?.Trim();
            if (d.Id_Agencia is int ia)
            {
                var ag = (await _repo.AgenciasAsync()).FirstOrDefault(x => x.Id == ia);
                if (ag is null) return BadRequest(new { mensaje = "No encontramos esa agencia." });
                nombre ??= ag.Nombre; email ??= ag.Email;
            }
            if (string.IsNullOrWhiteSpace(nombre)) return BadRequest(new { mensaje = "Elige la agencia o escribe su nombre." });
            var token = await _repo.CrearLinkAsync(c.Id_Destino, c.Nombre_Destino ?? c.Id_Destino, d.Id_Agencia, nombre, string.IsNullOrWhiteSpace(email) ? null : email, d.Dias, User.Identity?.Name);
            await Auditar("Agencia_LinkCrear", new { d.Id_Destino, nombre, d.Dias });
            return Json(new { ok = true, url = UrlLink(token), mensaje = "Link creado." + (d.Dias is null ? " No vence." : $" Vence en {d.Dias} días.") });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RevocarLink(int id)
        {
            if (!await _repo.RevocarLinkAsync(id)) return NotFound(new { mensaje = "No encontramos ese link." });
            await Auditar("Agencia_LinkRevocar", new { id });
            return Json(new { ok = true, mensaje = "Link desactivado: ya no abre el editor." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ExtenderLink(int id, int dias = 10)
        {
            if (!await _repo.ExtenderLinkAsync(id, Math.Clamp(dias, 1, 3650))) return NotFound(new { mensaje = "No encontramos ese link." });
            await Auditar("Agencia_LinkExtender", new { id, dias });
            return Json(new { ok = true, mensaje = $"Link activo {dias} días más." });
        }

        /// <summary>Quitar el vencimiento a un link (id) o a todos los activos (sin id).</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkSinVencer(int? id)
        {
            var n = await _repo.LinksSinVencimientoAsync(id);
            await Auditar("Agencia_LinkSinVencer", new { id, n });
            return Json(new { ok = true, mensaje = id is null ? $"{n} link(s) ya no vencen." : "El link ya no vence." });
        }

        // ================= envío masivo =================
        /// <summary>Qué imágenes se enviarían y a qué destino corresponde cada una (las que no son de un destino de Agencia no se pueden enviar).</summary>
        private async Task<(List<AgenciaImagenEnvio> Imagenes, List<string> SinDestino)> ImagenesEnvioAsync(List<string> elegidas)
        {
            var configs = await _flyers.ConfigsAsync(Tipo);
            var archivos = _arch.Listar().Where(i => elegidas.Count == 0 || elegidas.Contains(i.Archivo)).ToList();
            var r = new List<AgenciaImagenEnvio>(); var sin = new List<string>();
            foreach (var a in archivos)
            {
                var clave = AgenciaImagenes.ClaveDe(a.Archivo);
                var c = configs.FirstOrDefault(x => clave is not null && (AgenciaImagenes.Clave(x.Id_Destino) == clave || AgenciaImagenes.Clave(x.Nombre_Destino ?? "") == clave));
                var ruta = _arch.Fisico(a.Archivo);
                if (c is null || ruta is null) { sin.Add(a.Archivo); continue; }
                var nombre = c.Nombre_Destino ?? c.Id_Destino;
                r.Add(new AgenciaImagenEnvio(ruta, a.Archivo, c.Id_Destino, nombre, UrlDestino(nombre)));
            }
            return (r, sin);
        }

        /// <summary>Antes de enviar: cuántos mensajes salen, cuánto cuesta (aprox., USD) y cuánto tarda.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Costo([FromBody] AgenciaEnviarDto d)
        {
            var (imgs, sin) = await ImagenesEnvioAsync(d.Imagenes);
            var agencias = string.IsNullOrWhiteSpace(d.Prueba) ? (await _repo.AgenciasAsync(true)).Count(a => d.Agencias.Count == 0 || d.Agencias.Contains(a.Id)) : 1;
            var mensajes = imgs.Count * agencias;
            return Json(new
            {
                imagenes = imgs.Count, agencias, mensajes, costoUnitario = CostoMensaje, costoTotal = Math.Round(mensajes * CostoMensaje, 2),
                minutos = Math.Ceiling(_job.Duracion(imgs.Count, agencias) / 60.0), sinDestino = sin, plantilla = _job.Plantilla
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Enviar([FromBody] AgenciaEnviarDto d)
        {
            if (!_wa.Configurado) return BadRequest(new { mensaje = "WhatsApp no está configurado en este servidor." });
            if (!PuedeEnviar) return StatusCode(403, new { mensaje = "No tienes el permiso \"Enviar flyers por WhatsApp\". Pídeselo a un administrador." });
            if (_job.EnCurso is not null) return Conflict(new { mensaje = "Ya hay un envío a agencias en curso. Espera a que termine o cancélalo." });
            var (imgs, sin) = await ImagenesEnvioAsync(d.Imagenes);
            if (imgs.Count == 0) return BadRequest(new { mensaje = sin.Count > 0 ? "Esas imágenes no son de ningún destino de Flyer Agencia." : "No hay imágenes para enviar: guárdalas primero en la pestaña Flyer." });
            if (imgs.Count > 10) return BadRequest(new { mensaje = "Máximo 10 imágenes por envío." });

            List<FlyerAgencia> agencias;
            if (!string.IsNullOrWhiteSpace(d.Prueba))
            {
                var n = AgenciaRepository.Telefono(d.Prueba);
                if (!AgenciaRepository.TelefonoValido(n)) return BadRequest(new { mensaje = "Número de prueba inválido (con el código del país)." });
                agencias = new() { new FlyerAgencia { Id = 0, Nombre = "Prueba Eurovip", Telefono = n, Activo = true } };
            }
            else
            {
                agencias = (await _repo.AgenciasAsync(true)).Where(a => d.Agencias.Count == 0 || d.Agencias.Contains(a.Id)).ToList();
                if (agencias.Count == 0) return BadRequest(new { mensaje = "No hay agencias activas para enviar." });
            }
            var id = await _repo.CrearEnvioAsync(new FlyerEnvioMasivo
            {
                Modulo = Modulo, Subtipo = string.IsNullOrWhiteSpace(d.Prueba) ? "ConLink" : "Prueba", Plantilla = _job.Plantilla, Total_Mensajes = imgs.Count * agencias.Count,
                Total_Contactos = agencias.Count, Total_Imagenes = imgs.Count, Costo_Unitario = CostoMensaje, Usuario = User.Identity?.Name
            });
            // La agencia de prueba (id 0) no está en la tabla: su link se guarda sin agencia
            if (!_job.Iniciar(id, imgs, agencias, BaseEditor, User.Identity?.Name ?? ""))
                return Conflict(new { mensaje = "Ya hay un envío a agencias en curso." });
            await Auditar("Agencia_Enviar", new { id, agencias = agencias.Count, imagenes = imgs.Select(i => i.Archivo), prueba = d.Prueba });
            return Json(new { ok = true, id, mensaje = $"Envío #{id} iniciado: {imgs.Count} imagen(es) a {agencias.Count} agencia(s)." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Cancelar()
        {
            if (_job.EnCurso is null) return BadRequest(new { mensaje = "No hay ningún envío en curso." });
            _job.Cancelar();
            return Json(new { ok = true, mensaje = "Cancelando… termina el mensaje actual y se detiene." });
        }

        [HttpGet]
        public async Task<IActionResult> Envio(int id)
        {
            var e = await _repo.EnvioAsync(id);
            return e is null ? NotFound(new { mensaje = "No encontramos ese envío." }) : Json(new { envio = e, enCurso = _job.EnCurso == id });
        }

        [HttpGet]
        public async Task<IActionResult> EnvioDetalle(int id) => Json(await _repo.DetalleAsync(id));

        /// <summary>Reporte del envío para imprimir o guardar como PDF (como el PDF de BackTiTan).</summary>
        [HttpGet]
        public async Task<IActionResult> Reporte(int id)
        {
            var e = await _repo.EnvioAsync(id);
            if (e is null) return NotFound();
            ViewData["Detalle"] = await _repo.DetalleAsync(id);
            return View(e);
        }

        private IActionResult Error() => BadRequest(new { mensaje = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        private Task Auditar(string tipo, object cambios) =>
            _db.AuditarAsync(tipo, Array.Empty<int>(), cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
