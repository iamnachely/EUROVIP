using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EuroVip.Controllers
{
    /// <summary>
    /// CAMBIO 140 · API de cargas de PROVEEDORES para la Fase 2 (las pantallas del proveedor en Gestión, Next.js, la llaman
    /// servidor-a-servidor). No usa la cookie de inicio de sesión del BackEurovip ni los módulos de «Usuarios y accesos»:
    /// cada petición lleva dos cabeceras
    ///   • X-Eurovip-Clave   : la clave compartida (config Eurovip:Proveedores:ClaveApi, en appsettings.Local.json / servidor; NUNCA en git)
    ///   • X-Proveedor-Email : el correo de la cuenta del proveedor (Eurovip.proveedor_cuenta, tiene que estar ACTIVA)
    /// El proveedor solo ve y cambia SUS cargas; nunca puede aprobar, programar ni ver las de otro. Respuestas en JSON
    /// ({ mensaje, errores: [{ indice, producto, campo, mensaje }] } cuando algo está mal; indice 0 = la carga entera).
    ///
    /// Rutas:
    ///   GET  /api/proveedor/yo                         → la cuenta, su proveedor y los módulos habilitados (con su nombre)
    ///   GET  /api/proveedor/catalogo/{nombre}          → listas para los formularios (los mismos datos de los GET de cada pantalla):
    ///        monedas · campanyas · tasas · regimenes · categorias (de hotel, circuitos) · paises · destinos?q=&amp;pais= ·
    ///        hoteles?q=&amp;destino=&amp;pais= · habitaciones?hotel= · aeropuertos?q=&amp;pais= · circuitos?q=&amp;pais= · estados (de oferta) ·
    ///        traslado-valores · navieras · barcos · categorias-crucero · precio-minimo?modulo=
    ///   POST /api/proveedor/validar                    → { modulo, codigo?, datos: [ ...DTO del módulo ] } revisa SIN guardar
    ///   GET  /api/proveedor/cargas?estado=             → mis cargas (BORRADOR, PENDIENTE, RECHAZADA, PROGRAMADA, ACTIVA) y conteo
    ///   GET  /api/proveedor/cargas/{id}                → una carga mía: datos, productos, motivo/instrucciones del rechazo, flyer
    ///   POST /api/proveedor/cargas                     → crear borrador { modulo, codigo, titulo?, datos: [...] }
    ///   PUT  /api/proveedor/cargas/{id}                → editar (solo BORRADOR o RECHAZADA)
    ///   POST /api/proveedor/cargas/{id}/enviar         → enviar a revisión (se revisa todo; genera el flyer; pasa a PENDIENTE).
    ///        CAMBIO 141: cuerpo opcional { activarSugerido: "2026-12-01T09:00", comentario } (fecha que sugiere el proveedor)
    ///   GET/POST /api/proveedor/cargas/{id}/comentarios → (CAMBIO 141) la conversación con Eurovip y agregar { texto }
    ///   GET/POST /api/proveedor/cargas/{id}/flyer       → (CAMBIO 141) editor del flyer y guardar { datos } (null = el automático)
    ///   GET  /api/proveedor/activas                    → lo publicado en la web desde mis cargas (con si sigue activo)
    ///   DELETE /api/proveedor/cargas/{id}              → (fase 2) borrar un borrador mío
    ///   GET  /api/proveedor/plantilla/{modulo}         → (fase 2) la plantilla Excel del módulo (.xlsx)
    ///   POST /api/proveedor/excel/{modulo}             → (fase 2) multipart «archivo» (+ «mapa» opcional): lee el Excel SIN guardar y
    ///        devuelve los DTO y los errores por fila (Excursiones/Traslados), la matriz hotel × fecha (Solo Hotel/Paquetes) o
    ///        las salidas con su precio (Circuitos). Ver Services/ProveedoresExcel.cs.
    /// Lo que el proveedor no sabe (la campaña) lo completa el BackEurovip al guardar (ProveedoresCarga.CompletarAsync).
    /// La administración de las cuentas (Gestión › Proveedores, fase 3) está en ProveedorAdminApiController (/api/proveedor-admin).
    /// Los DTO de «datos» son los mismos de los formularios: OFERTAS/PAQUETES = CargaOfertaDto, CIRCUITOS = CargaCircuitoDto,
    /// EXCURSIONES = ExcursionGuardarDto, TRASLADOS = TrasladoGuardarDto, CRUCEROS = CruceroDto (Id_Proveedor y el código de la
    /// carga los pone siempre el BackEurovip).
    /// </summary>
    [AllowAnonymous]
    [Route("api/proveedor")]
    [ClaveProveedor]
    public class ProveedorApiController : Controller
    {
        private readonly ProveedoresCarga _srv;
        private readonly ICircuitoRepository _repo;
        private readonly ProveedoresSolicitudes _sol;
        public ProveedorApiController(ProveedoresCarga srv, ICircuitoRepository repo, ProveedoresSolicitudes sol) { _srv = srv; _repo = repo; _sol = sol; }

        private ProveedorCuenta Cuenta => (ProveedorCuenta)HttpContext.Items[ClaveProveedorAttribute.ClaveCuenta]!;

        [HttpGet("yo")]
        public IActionResult Yo() => Json(new
        {
            cuenta = new { Cuenta.Id, Cuenta.Nombre, Cuenta.Email, Cuenta.Telefono, Cuenta.Id_Proveedor, Cuenta.Proveedor },
            modulos = Cuenta.ListaModulos.Select(m => new { clave = m, nombre = ProveedorModulos.Todos[m].Nombre, icono = ProveedorModulos.Todos[m].Icono })
        });

        /// <summary>Listas para los formularios (reutiliza las consultas de las pantallas del BackEurovip).</summary>
        [HttpGet("catalogo/{nombre}")]
        public async Task<IActionResult> Catalogo(string nombre, string? q, string? destino, string? pais, int? hotel, string? modulo,
            [FromServices] IOfertaRepository ofertas, [FromServices] ICruceroRepository cruceros)
        {
            using var cn = _repo.Conexion();
            object? r = (nombre ?? "").Trim().ToLowerInvariant() switch
            {
                "monedas" => await _repo.MonedasAsync(),
                "campanyas" => await _repo.CampanyasAsync(),
                "tasas" => await _repo.TasasAsync(),
                "regimenes" => await _repo.RegimenesAsync(),
                "categorias" => await _repo.CategoriasAsync(),
                "paises" => await _repo.PaisesAsync(),
                "destinos" => await _repo.DestinosAsync(q, pais),
                "hoteles" => (await _sol.HotelesPendientesAsync(Cuenta.Id, q)).Select(o => new CircuitoHotelDto { Id_Alojamiento = int.Parse(o.Valor), Nombre = o.Texto, Destino = o.Extra, Estrellas = o.Extra2 })
                    .Concat(await _repo.HotelesAsync(q, destino, pais)).ToList(),
                "tipos-habitacion" => await _sol.TiposHabitacionAsync(q),
                "habitaciones" => hotel is int h ? await ofertas.HabitacionesAsync(h) : Array.Empty<object>(),
                "aeropuertos" => await _repo.AeropuertosAsync(q, pais),
                "circuitos" => await _repo.ListarAsync(q, pais),
                "estados" => await EstadosOferta.ListarAsync(cn),
                "traslado-valores" => new { tipos = TrasladoValores.Tipos, categorias = TrasladoValores.Categorias, vehiculos = TrasladoValores.Vehiculos, cobros = TrasladoValores.Cobros },
                "navieras" => await cruceros.NavierasAsync(),
                "barcos" => await cruceros.BarcosAsync(),
                "categorias-crucero" => new { categorias = await cruceros.CategoriasAsync(), cabinas = CruceroValores.CategoriasCabina },
                "precio-minimo" => PreciosMinimos.Normalizar(modulo) is string m ? new { modulo = m, minimo = await PreciosMinimos.ObtenerAsync(cn, m) } : null,
                _ => null
            };
            return r is null ? NotFound(new { mensaje = $"No conozco la lista «{nombre}». Mira las rutas al principio de ProveedorApiController." }) : Json(r);
        }

        public sealed class ValidarDto
        {
            public string? Modulo { get; set; }
            public string? Codigo { get; set; }
            public JsonElement Datos { get; set; }
        }

        [HttpPost("validar")]
        public async Task<IActionResult> Validar([FromBody] ValidarDto dto)
        {
            var (productos, errores) = await _srv.ValidarDatosAsync(Cuenta, dto.Modulo, dto.Codigo, dto.Datos);
            return Json(new { ok = errores.Count == 0, errores, productos });
        }

        [HttpGet("cargas")]
        public async Task<IActionResult> Cargas(string? estado, string? modulo, string? q, int pagina = 1, int tam = 25)
        {
            var (items, total) = await _srv.CargasAsync(new CargaProveedorFiltro { Cuenta = Cuenta.Id, Estado = estado, Modulo = modulo, Q = q, Pagina = pagina, Tam = tam });
            return Json(new { items, total, conteo = await _srv.ConteoAsync(Cuenta.Id) });
        }

        [HttpGet("cargas/{id:int}")]
        public async Task<IActionResult> Carga(int id) =>
            await _srv.DetalleAsync(id, Cuenta.Id) is { } d ? Json(d) : NotFound(new { mensaje = "Esa carga no existe." });

        [HttpPost("cargas")]
        public Task<IActionResult> Crear([FromBody] CargaProveedorDto dto) { dto.Id = null; return Guardar(dto); }

        [HttpPut("cargas/{id:int}")]
        public Task<IActionResult> Editar(int id, [FromBody] CargaProveedorDto dto) { dto.Id = id; return Guardar(dto); }

        private async Task<IActionResult> Guardar(CargaProveedorDto dto)
        {
            var (id, errores) = await _srv.GuardarCargaAsync(Cuenta, dto);
            if (id is null) return BadRequest(new { mensaje = errores.FirstOrDefault()?.Mensaje ?? "No se pudo guardar.", errores });
            await Auditar(dto.Id is null ? "Proveedor_Carga_Crear" : "Proveedor_Carga_Editar", new { id, dto.Modulo, dto.Codigo, dto.Titulo });
            return Json(new { ok = true, id, mensaje = dto.Id is null ? "Borrador guardado." : "Cambios guardados." });
        }

        [HttpPost("cargas/{id:int}/enviar")]
        public async Task<IActionResult> Enviar(int id, [FromBody] EnviarDto? dto)
        {
            var (ok, mensaje, errores) = await _srv.EnviarAsync(Cuenta, id, dto?.ActivarSugerido);
            if (!ok) return BadRequest(new { mensaje, errores });
            if (!string.IsNullOrWhiteSpace(dto?.Comentario)) await _srv.ComentarAsync(id, "PROVEEDOR", Cuenta.Nombre, dto.Comentario);
            await Auditar("Proveedor_Carga_Enviar", new { id, sugerido = dto?.ActivarSugerido });
            return Json(new { ok, mensaje, carga = (await _srv.CargaAsync(id)) });
        }

        /// <summary>CAMBIO 141: enviar a revisión con la fecha y hora (RD) que sugiere el proveedor y un comentario opcional.</summary>
        public sealed class EnviarDto
        {
            public DateTime? ActivarSugerido { get; set; }
            public string? Comentario { get; set; }
        }

        public sealed class ComentarioDto { public string? Texto { get; set; } }

        /// <summary>CAMBIO 141: la conversación de una carga suya con Eurovip.</summary>
        [HttpGet("cargas/{id:int}/comentarios")]
        public async Task<IActionResult> Comentarios(int id) =>
            (await _srv.CargaAsync(id))?.Id_Cuenta == Cuenta.Id ? Json(await _srv.ComentariosAsync(id)) : NotFound(new { mensaje = "Esa carga no existe." });

        [HttpPost("cargas/{id:int}/comentarios")]
        public async Task<IActionResult> Comentar(int id, [FromBody] ComentarioDto dto)
        {
            if ((await _srv.CargaAsync(id))?.Id_Cuenta != Cuenta.Id) return NotFound(new { mensaje = "Esa carga no existe." });
            if (await _srv.ComentarAsync(id, "PROVEEDOR", Cuenta.Nombre, dto.Texto) is string mal) return BadRequest(new { mensaje = mal });
            await Auditar("Proveedor_Carga_Comentar", new { id });
            return Json(new { ok = true, comentarios = await _srv.ComentariosAsync(id) });
        }

        /// <summary>CAMBIO 141: el editor del flyer (lo que tiene, los productos para elegir, fotos de fondo y plantillas).</summary>
        [HttpGet("cargas/{id:int}/flyer")]
        public async Task<IActionResult> FlyerEditor(int id) =>
            await _srv.FlyerEditorAsync(Cuenta, id) is { } r ? Json(r) : NotFound(new { mensaje = "Esa carga no existe." });

        /// <summary>CAMBIO 141: guarda el flyer editado y dibuja el PNG ({ datos: null } = volver al automático).</summary>
        [HttpPost("cargas/{id:int}/flyer")]
        public async Task<IActionResult> FlyerGuardar(int id, [FromBody] FlyerGuardarDto dto)
        {
            var (flyer, error) = await _srv.GuardarFlyerAsync(Cuenta, id, dto.Datos);
            if (error is not null) return BadRequest(new { mensaje = error });
            await Auditar("Proveedor_Carga_Flyer", new { id });
            return Json(new { ok = true, flyer });
        }

        public sealed class FlyerGuardarDto { public FlyerProveedorDatos? Datos { get; set; } }

        [HttpGet("activas")]
        public async Task<IActionResult> Activas() => Json(await _srv.ItemsAsync(null, Cuenta.Id));

        [HttpDelete("cargas/{id:int}")]
        public async Task<IActionResult> Borrar(int id)
        {
            if (await _srv.BorrarCargaAsync(Cuenta, id) is string mal) return BadRequest(new { mensaje = mal });
            await Auditar("Proveedor_Carga_Borrar", new { id });
            return Json(new { ok = true, mensaje = "Borrador eliminado." });
        }

        /// <summary>Fase 2: plantilla Excel del módulo (solo de los módulos habilitados en su cuenta).</summary>
        [HttpGet("plantilla/{modulo}")]
        public async Task<IActionResult> Plantilla(string modulo, [FromServices] ProveedoresExcel excel)
        {
            var m = ProveedorModulos.Normalizar(modulo);
            if (m is null || !Cuenta.ListaModulos.Contains(m)) return NotFound(new { mensaje = "Tu cuenta no tiene habilitado ese tipo de producto." });
            return await excel.PlantillaAsync(m) is var (archivo, nombre) ? File(archivo, CargaExcel.TipoXlsx, nombre) : NotFound(new { mensaje = "Este tipo de producto no tiene plantilla Excel: usa el formulario." });
        }

        /// <summary>Fase 2: lee el Excel del proveedor SIN guardar (la pantalla pasa las filas al formulario para corregirlas).</summary>
        [HttpPost("excel/{modulo}")]
        [RequestSizeLimit(CargaExcel.MaxBytes + 100_000)]
        public async Task<IActionResult> Excel(string modulo, IFormFile? archivo, [FromForm] string? mapa, [FromServices] ProveedoresExcel excel)
        {
            var m = ProveedorModulos.Normalizar(modulo);
            if (m is null || !Cuenta.ListaModulos.Contains(m)) return NotFound(new { mensaje = "Tu cuenta no tiene habilitado ese tipo de producto." });
            var (r, error) = await excel.LeerAsync(Cuenta, m, archivo, mapa);
            return error is not null ? BadRequest(new { mensaje = error }) : Json(r);
        }

        // ---------- Solicitudes de creación (20/11/2026): «no existe mi hotel → pido que lo creen y lo uso ya» ----------
        /// <summary>El formulario de ese tipo (hotel, destino, aeropuerto, habitacion): los mismos campos de Herramientas.</summary>
        [HttpGet("solicitudes/formulario/{tipo}")]
        public async Task<IActionResult> SolicitudFormulario(string tipo) =>
            await _sol.FormularioAsync(tipo) is { } f ? Json(f) : NotFound(new { mensaje = "No sé crear eso." });

        public sealed class ParecidosSolDto { public string Col { get; set; } = ""; public string Texto { get; set; } = ""; public Dictionary<string, string?> Contexto { get; set; } = new(); }

        /// <summary>Lo que ya existe o se parece a lo que escribe (para no pedirlo dos veces).</summary>
        [HttpPost("solicitudes/parecidos/{tipo}")]
        public async Task<IActionResult> SolicitudParecidos(string tipo, [FromBody] ParecidosSolDto dto) =>
            await _sol.ParecidosAsync(tipo, dto.Col, dto.Texto, dto.Contexto) is { } r ? Json(r) : NotFound(new { mensaje = "No sé revisar eso." });

        public sealed class SolicitudDto { public Dictionary<string, JsonElement> Datos { get; set; } = new(); }

        /// <summary>Crea el dato (mismas reglas que Herramientas) y lo deja anotado para que Eurovip lo revise.</summary>
        [HttpPost("solicitudes/{tipo}")]
        public async Task<IActionResult> SolicitudCrear(string tipo, [FromBody] SolicitudDto dto)
        {
            var (r, error) = await _sol.CrearAsync(Cuenta, tipo, dto.Datos);
            if (r is null) return BadRequest(new { mensaje = error });
            await Auditar("Proveedor_Solicitud_" + tipo, new { dto.Datos });
            return Json(r);
        }

        /// <summary>Mis solicitudes (pendientes, aprobadas y rechazadas con su motivo).</summary>
        [HttpGet("solicitudes")]
        public async Task<IActionResult> Solicitudes(string? estado)
        {
            var (items, conteo) = await _sol.ListarAsync(Cuenta.Id, estado);
            return Json(new { items, conteo });
        }

        private Task Auditar(string tipo, object cambios) =>
            _repo.AuditarAsync(tipo, Array.Empty<int>(), cambios, $"proveedor:{Cuenta.Email}", HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }

    /// <summary>
    /// CAMBIO 140: el «inicio de sesión» de la API de proveedores. Clave compartida (X-Eurovip-Clave, comparada en tiempo
    /// constante) + cuenta de proveedor ACTIVA (X-Proveedor-Email). Sin clave configurada en el servidor la API queda cerrada.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ClaveProveedorAttribute : Attribute, IAsyncActionFilter
    {
        public const string ClaveCuenta = "ev:cuentaProveedor";

        /// <summary>Comprueba la clave compartida (X-Eurovip-Clave). Devuelve la respuesta de error o null si es correcta.</summary>
        public static IActionResult? RevisarClave(HttpContext http)
        {
            var cfg = http.RequestServices.GetRequiredService<IConfiguration>();
            var clave = cfg["Eurovip:Proveedores:ClaveApi"];
            if (string.IsNullOrWhiteSpace(clave) || clave.Length < 16)
                return new ObjectResult(new { mensaje = "La API de proveedores no está configurada en este servidor." }) { StatusCode = 503 };
            var enviada = http.Request.Headers["X-Eurovip-Clave"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(enviada), Encoding.UTF8.GetBytes(clave)))
                return new ObjectResult(new { mensaje = "Clave de la API incorrecta." }) { StatusCode = 401 };
            return null;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            var http = ctx.HttpContext;
            if (RevisarClave(http) is { } mal)
            {
                ctx.Result = mal;
                return;
            }
            var cuenta = await http.RequestServices.GetRequiredService<ProveedoresCarga>().CuentaPorEmailAsync(http.Request.Headers["X-Proveedor-Email"].ToString());
            if (cuenta is null || !cuenta.Activo)
            {
                ctx.Result = new ObjectResult(new { mensaje = cuenta is null ? "No hay ninguna cuenta de proveedor con ese correo." : "La cuenta de proveedor está desactivada." }) { StatusCode = 403 };
                return;
            }
            http.Items[ClaveCuenta] = cuenta;
            await next();
        }
    }
}
