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

        public class ParecidosDto
        {
            public string Col { get; set; } = "";
            public string Texto { get; set; } = "";
            public Dictionary<string, string?> Contexto { get; set; } = new();
            public Dictionary<string, string?> Propio { get; set; } = new();
        }

        /// <summary>Aviso en vivo al escribir: lo que ya existe (rojo) o se parece (amarillo) en este catálogo.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Parecidos(string id, [FromBody] ParecidosDto dto)
        {
            var c = HrCatalogos.Buscar(id);
            if (c is null) return NotFound(new { mensaje = "Catálogo desconocido." });
            return Json(await _repo.ParecidosAsync(c, dto.Col, dto.Texto, dto.Contexto, dto.Propio));
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
                // Punto 2: un hotel o destino sin foto recibe la imagen por defecto (la de su destino o la general)
                if (HrCatalogos.Fotos(c) is { } mmc && mmc.Tipo is "ALOJAMIENTO" or "DESTINO" && clave is not null && clave.TryGetValue(mmc.Col, out var kv) && kv?.ToString() is { Length: > 0 } valor)
                {
                    string? Dato(string k) => dto.Datos.TryGetValue(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
                    var tieneFoto = !string.IsNullOrWhiteSpace(Dato("caratula")) || (mmc.Tipo == "DESTINO" && _img.Buscar("destinos", valor) is not null);
                    await HttpContext.RequestServices.GetRequiredService<IImagenesPorDefecto>().AsegurarAsync(mmc.Tipo, valor, mmc.Tipo == "ALOJAMIENTO" ? Dato("id_destino") : null, tieneFoto);
                }
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
            var sinNombre = await Dapper.SqlMapper.ExecuteScalarAsync<int>(cn,
                "SELECT COUNT(*) FROM Eurovip.imagen WHERE LEN(nombre_archivo) = 32 AND nombre_archivo NOT LIKE '%[^0-9a-f]%'");
            return Json(new { fotos = filas.Select(f => new { f.Tipo, f.Uso, f.N }), carpeta = _mm.Estado(), conCodigo = sinNombre });
        }

        /// <summary>Pone nombre legible a las fotos que aún tienen código (destinos/punta-cana/punta-cana.webp). Solo Superadministrador.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RenombrarFotos()
        {
            if ((HttpContext.Items[AccesoModuloFilter.ClavePermisos] as PermisosUsuario)?.EsSuper != true)
                return StatusCode(403, new { mensaje = "Solo un Superadministrador puede renombrar las fotos." });
            var r = await _mm.RenombrarArchivosAsync();
            await Auditar("Herramienta_RenombrarFotos", new { r.Renombradas, r.YaBien, sinArchivo = r.SinArchivo.Count, r.Errores });
            return Json(new { ok = true, renombradas = r.Renombradas, yaBien = r.YaBien, sinArchivo = r.SinArchivo, errores = r.Errores,
                              mensaje = r.Renombradas == 0 ? "No había fotos que renombrar en este servidor." : $"Se renombraron {r.Renombradas} fotos." });
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
            return Json(new { ok = true, pasadas = r.Pasadas, yaEstaban = r.YaEstaban, sinArchivo = r.SinArchivo, errores = r.Errores, itinerario = r.Itinerario, slider = r.Slider,
                              mensaje = total + r.Itinerario + r.Slider == 0 ? "No había fotos nuevas que pasar."
                                      : $"Se pasaron {total} fotos a multimedia, {r.Itinerario} del itinerario y {r.Slider} archivos del slider." });
        }

        // =====================================================================================================================
        // CAMBIO 140 · Cargas de PROVEEDORES: bandeja de revisión (aprobar y publicar ahora, programar el alta o rechazar con
        // motivo e instrucciones) y las cuentas de proveedor (alta/edición, módulos habilitados). Las consultas son
        // [AccesoEstricto]: sin el módulo de Herramientas no se ve nada (datos de empresas externas).
        // =====================================================================================================================
        private ProveedoresCarga Prov => HttpContext.RequestServices.GetRequiredService<ProveedoresCarga>();

        [HttpGet, AccesoEstricto, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedoresResumen() => Json(new
        {
            conteo = await Prov.ConteoAsync(),
            cuentas = (await Prov.CuentasAsync()).Count,
            modulos = ProveedorModulos.Todos.Select(m => new { clave = m.Key, nombre = m.Value.Nombre, icono = m.Value.Icono }),
            publicaProgramadas = ProveedoresCargaWorker.Encendido(HttpContext.RequestServices.GetRequiredService<IConfiguration>(), HttpContext.RequestServices.GetRequiredService<IHostEnvironment>()),
            ahora = AgenciaProgramador.Ahora(HttpContext.RequestServices.GetRequiredService<IConfiguration>())
        });

        [HttpGet, AccesoEstricto, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedoresCuentas()
        {
            using var cn = _aud.Conexion();
            return Json(new { cuentas = await Prov.CuentasAsync(), catalogo = await CatalogoCargas.ProveedoresAsync(cn) });
        }

        [HttpPost, ValidateAntiForgeryToken, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorCuentaGuardar([FromBody] ProveedorCuentaDto dto)
        {
            var antes = dto.Id is int idA ? await Prov.CuentaAsync(idA) : null;
            var (id, error) = await Prov.GuardarCuentaAsync(dto, User.Identity?.Name ?? "desconocido");
            if (error is not null) return BadRequest(new { mensaje = error });
            await _aud.AuditarAsync(antes is null ? "Proveedor_Cuenta_Crear" : "Proveedor_Cuenta_Editar", new[] { id!.Value },
                new { antes = antes is null ? null : new { antes.Nombre, antes.Email, antes.Id_Proveedor, antes.Modulos, antes.Activo }, despues = dto },
                User.Identity?.Name ?? "desconocido", HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            return Json(new { ok = true, id, mensaje = antes is null ? $"Se creó la cuenta de «{dto.Nombre}»." : "Cambios guardados." });
        }

        [HttpGet, AccesoEstricto, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedoresCargas([FromQuery] CargaProveedorFiltro f)
        {
            var (items, total) = await Prov.CargasAsync(f);
            return Json(new { items, total, pagina = Math.Max(1, f.Pagina), tam = Math.Clamp(f.Tam, 5, 100), conteo = await Prov.ConteoAsync() });
        }

        [HttpGet, AccesoEstricto, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorCarga(int id) =>
            await Prov.DetalleAsync(id) is { } d ? Json(d) : NotFound(new { mensaje = "Esa carga ya no existe." });

        public sealed class RevisionCargaDto
        {
            public int Id { get; set; }
            /// <summary>Programar: «yyyy-MM-ddTHH:mm» (hora de República Dominicana).</summary>
            public DateTime? Fecha { get; set; }
            public string? Motivo { get; set; }
            public string? Instrucciones { get; set; }
        }

        /// <summary>Aprobar y publicar ahora en la web (el mismo guardado de cada pantalla).</summary>
        [HttpPost, ValidateAntiForgeryToken, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorCargaAprobar([FromBody] RevisionCargaDto dto)
        {
            var (ok, mensaje, errores) = await Prov.PublicarAsync(dto.Id, User.Identity?.Name ?? "desconocido");
            await AuditarCarga("Proveedor_Carga_Aprobar", dto.Id, new { ok, mensaje });
            return ok ? Json(new { ok, mensaje }) : BadRequest(new { mensaje, errores = errores.Select(e => $"Producto {e.Indice}{(e.Producto is null ? "" : $" ({e.Producto})")}: {e.Mensaje}") });
        }

        [HttpPost, ValidateAntiForgeryToken, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorCargaProgramar([FromBody] RevisionCargaDto dto)
        {
            if (dto.Fecha is not DateTime cuando) return BadRequest(new { mensaje = "Elige la fecha y la hora del alta." });
            var (error, errores) = await Prov.ProgramarAsync(dto.Id, cuando, User.Identity?.Name ?? "desconocido");
            if (error is not null) return BadRequest(new { mensaje = error, errores = errores.Select(e => $"Producto {e.Indice}{(e.Producto is null ? "" : $" ({e.Producto})")}: {e.Mensaje}") });
            await AuditarCarga("Proveedor_Carga_Programar", dto.Id, new { activar_en = cuando });
            return Json(new { ok = true, mensaje = $"Programada: se publicará el {cuando:dd/MM/yyyy} a las {cuando:HH:mm} (hora de República Dominicana)." });
        }

        [HttpPost, ValidateAntiForgeryToken, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorCargaRechazar([FromBody] RevisionCargaDto dto)
        {
            if (await Prov.RechazarAsync(dto.Id, dto.Motivo, dto.Instrucciones, User.Identity?.Name ?? "desconocido") is string error) return BadRequest(new { mensaje = error });
            await AuditarCarga("Proveedor_Carga_Rechazar", dto.Id, new { dto.Motivo, dto.Instrucciones });
            return Json(new { ok = true, mensaje = "Carga rechazada. El proveedor verá el motivo y las instrucciones para corregirla." });
        }

        [HttpPost, ValidateAntiForgeryToken, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorCargaFlyer([FromBody] RevisionCargaDto dto)
        {
            var flyer = await Prov.RegenerarFlyerAsync(dto.Id);
            return flyer is null ? BadRequest(new { mensaje = "No se pudo generar el flyer (mira el log del servidor)." }) : Json(new { ok = true, flyer, mensaje = "Flyer generado de nuevo." });
        }

        // ================= CAMBIO 141: pantalla propia de revisión, conversación y «devolver al proveedor» =================

        /// <summary>
        /// Pantalla propia para revisar las cargas de proveedores y sus flyers (menú Cargas › Cargas de proveedores y la campana).
        /// Pedido de la usuaria: «una vista nueva para revisar las cargas de proveedores y sus flyers». Herramientas conserva su
        /// pestaña con un acceso directo. Mismo módulo (herramientas) que el resto de este controlador.
        /// </summary>
        [HttpGet, ModuloAccion(Modulos.CargasProveedores)]
        public IActionResult CargasProveedores() => View();

        /// <summary>Devolver al proveedor para que cambie algo (con instrucciones): queda DEVUELTA y la puede corregir y reenviar.</summary>
        [HttpPost, ValidateAntiForgeryToken, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorCargaDevolver([FromBody] RevisionCargaDto dto)
        {
            if (await Prov.DevolverAsync(dto.Id, dto.Instrucciones, User.Identity?.Name ?? "desconocido") is string error) return BadRequest(new { mensaje = error });
            await AuditarCarga("Proveedor_Carga_Devolver", dto.Id, new { dto.Instrucciones });
            return Json(new { ok = true, mensaje = "Se la devolviste al proveedor. Verá tus instrucciones y la podrá corregir y volver a enviar." });
        }

        // ---------- Solicitudes de datos de los proveedores (20/11/2026) ----------
        private ProveedoresSolicitudes Sol => HttpContext.RequestServices.GetRequiredService<ProveedoresSolicitudes>();

        [HttpGet, AccesoEstricto, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorSolicitudes(string? estado)
        {
            var (items, conteo) = await Sol.ListarAsync(null, estado);
            return Json(new { items, conteo });
        }

        public sealed class SolicitudRevisionDto { public int Id { get; set; } public string? Motivo { get; set; } }

        [HttpPost, ValidateAntiForgeryToken, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorSolicitudAprobar([FromBody] SolicitudRevisionDto dto)
        {
            if (await Sol.AprobarAsync(dto.Id, User.Identity?.Name ?? "desconocido") is string error) return BadRequest(new { mensaje = error });
            await Auditar("Proveedor_Solicitud_Aprobar", new { dto.Id });
            return Json(new { ok = true, mensaje = "Aprobada: el dato queda activo." });
        }

        [HttpPost, ValidateAntiForgeryToken, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorSolicitudRechazar([FromBody] SolicitudRevisionDto dto)
        {
            if (await Sol.RechazarAsync(dto.Id, dto.Motivo, User.Identity?.Name ?? "desconocido") is string error) return BadRequest(new { mensaje = error });
            await Auditar("Proveedor_Solicitud_Rechazar", new { dto.Id, dto.Motivo });
            return Json(new { ok = true, mensaje = "Rechazada: el proveedor verá tu motivo." });
        }

        public sealed class ComentarioRevisionDto
        {
            public int Id { get; set; }
            public string? Texto { get; set; }
        }

        /// <summary>Comentario de Eurovip en la conversación de la carga (lo ve el proveedor en Gestión).</summary>
        [HttpPost, ValidateAntiForgeryToken, ModuloAccion(Modulos.CargasProveedores)]
        public async Task<IActionResult> ProveedorCargaComentar([FromBody] ComentarioRevisionDto dto)
        {
            if (await Prov.CargaAsync(dto.Id) is null) return NotFound(new { mensaje = "Esa carga ya no existe." });
            if (await Prov.ComentarAsync(dto.Id, "EUROVIP", User.Identity?.Name ?? "Eurovip", dto.Texto) is string error) return BadRequest(new { mensaje = error });
            await AuditarCarga("Proveedor_Carga_Comentar", dto.Id, new { });
            return Json(new { ok = true, mensaje = "Comentario enviado: el proveedor lo verá en su carga.", comentarios = await Prov.ComentariosAsync(dto.Id) });
        }

        private Task AuditarCarga(string tipo, int id, object cambios) =>
            _aud.AuditarAsync(tipo, Array.Empty<int>(), new { carga = id, cambios }, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());

        private Task Auditar(string tipo, object cambios) =>
            _aud.AuditarAsync(tipo, Array.Empty<int>(), cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
