using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EuroVip.Controllers
{
    /// <summary>
    /// CAMBIO 140 · Fase 3 · Administración de las cuentas de PROVEEDOR desde Gestión (staging /es/gestion › Proveedores).
    /// Pedido de la usuaria: «El Super-Administrador y el Super-Comercial crean los proveedores, les dan de alta y establecen
    /// a qué tendrán acceso». Gestión (EuroVip.Gestion, ProveedoresController) crea el usuario de entrada en VDTDESA
    /// (Eurovip.usuario_admin, rol PROVEEDOR) y la cuenta aquí (Eurovip.proveedor_cuenta); el correo une las dos.
    /// Servidor-a-servidor con la MISMA clave compartida que /api/proveedor (cabecera X-Eurovip-Clave), SIN X-Proveedor-Email:
    /// quien llama es el personal de Eurovip, que Gestión ya autorizó. X-Gestion-Usuario (opcional) = quién lo hizo, para la auditoría.
    ///
    /// Rutas:
    ///   GET    /api/proveedor-admin/cuentas                 → todas las cuentas (con cuántas cargas y pendientes)
    ///   GET    /api/proveedor-admin/cuentas/{id}            → una cuenta
    ///   POST   /api/proveedor-admin/cuentas                 → crear  { nombre, email, telefono?, id_Proveedor, modulos: [...], activo, id_Gestion? }
    ///   PUT    /api/proveedor-admin/cuentas/{id}            → editar (mismo cuerpo)
    ///   POST   /api/proveedor-admin/cuentas/{id}/activo     → { activo: true|false }
    ///   DELETE /api/proveedor-admin/cuentas/{id}            → borrar (si ya cargó algo, solo se desactiva: { resultado: "desactivada" })
    ///   GET    /api/proveedor-admin/cargas?cuenta=&estado=&modulo=&q=&pagina=  → (CAMBIO 141) cargas para verlas desde Gestión (solo lectura)
    ///   GET    /api/proveedor-admin/cargas/{id}             → una carga con sus productos, flyer y comentarios
    ///   GET    /api/proveedor-admin/catalogo                → proveedores del catálogo (Eurovip.proveedor) y módulos posibles
    /// </summary>
    [AllowAnonymous]
    [Route("api/proveedor-admin")]
    [ClaveAdminProveedor]
    public class ProveedorAdminApiController : Controller
    {
        private readonly ProveedoresCarga _srv;
        private readonly ICircuitoRepository _repo;
        public ProveedorAdminApiController(ProveedoresCarga srv, ICircuitoRepository repo) { _srv = srv; _repo = repo; }

        /// <summary>Quién lo hace (lo manda Gestión): «gestion:correo@…».</summary>
        private string Usuario
        {
            get
            {
                var u = Request.Headers["X-Gestion-Usuario"].ToString().Trim();
                return "gestion:" + (u.Length is > 0 and <= 80 ? u : "desconocido");
            }
        }

        [HttpGet("cuentas")]
        public async Task<IActionResult> Cuentas() => Json(await _srv.CuentasAsync());

        [HttpGet("cuentas/{id:int}")]
        public async Task<IActionResult> Cuenta(int id) =>
            await _srv.CuentaAsync(id) is { } c ? Json(c) : NotFound(new { mensaje = "Esa cuenta de proveedor no existe." });

        [HttpPost("cuentas")]
        public Task<IActionResult> Crear([FromBody] ProveedorCuentaDto dto) { dto.Id = null; return Guardar(dto); }

        [HttpPut("cuentas/{id:int}")]
        public Task<IActionResult> Editar(int id, [FromBody] ProveedorCuentaDto dto) { dto.Id = id; return Guardar(dto); }

        private async Task<IActionResult> Guardar(ProveedorCuentaDto dto)
        {
            var antes = dto.Id is int idA ? await _srv.CuentaAsync(idA) : null;
            var (id, error) = await _srv.GuardarCuentaAsync(dto, Usuario);
            if (id is null) return BadRequest(new { mensaje = error });
            await _repo.AuditarAsync(antes is null ? "Proveedor_Cuenta_Crear" : "Proveedor_Cuenta_Editar", new[] { id.Value },
                new { antes = antes is null ? null : new { antes.Nombre, antes.Email, antes.Id_Proveedor, antes.Modulos, antes.Activo }, despues = dto, desde = "Gestión" },
                Usuario, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            return Json(new { ok = true, id, cuenta = await _srv.CuentaAsync(id.Value) });
        }

        public sealed class ActivoDto { public bool Activo { get; set; } }

        [HttpPost("cuentas/{id:int}/activo")]
        public async Task<IActionResult> Activo(int id, [FromBody] ActivoDto dto)
        {
            if (!await _srv.ActivarCuentaAsync(id, dto.Activo, Usuario)) return NotFound(new { mensaje = "Esa cuenta de proveedor no existe." });
            await _repo.AuditarAsync(dto.Activo ? "Proveedor_Cuenta_Activar" : "Proveedor_Cuenta_Desactivar", new[] { id }, new { dto.Activo, desde = "Gestión" },
                Usuario, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            return Json(new { ok = true });
        }

        [HttpDelete("cuentas/{id:int}")]
        public async Task<IActionResult> Borrar(int id)
        {
            var r = await _srv.BorrarCuentaAsync(id, Usuario);
            if (r is null) return NotFound(new { mensaje = "Esa cuenta de proveedor no existe." });
            await _repo.AuditarAsync(r == "borrada" ? "Proveedor_Cuenta_Borrar" : "Proveedor_Cuenta_Desactivar", new[] { id }, new { resultado = r, desde = "Gestión" },
                Usuario, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            return Json(new { ok = true, resultado = r });
        }

        /// <summary>CAMBIO 141: las cargas (de un proveedor o de todos) para verlas desde Gestión. Solo lectura: se activan en el BackEurovip.</summary>
        [HttpGet("cargas")]
        public async Task<IActionResult> Cargas([FromQuery] CargaProveedorFiltro f)
        {
            var (items, total) = await _srv.CargasAsync(f);
            return Json(new { items, total, conteo = await _srv.ConteoAsync(f.Cuenta) });
        }

        [HttpGet("cargas/{id:int}")]
        public async Task<IActionResult> Carga(int id) =>
            await _srv.DetalleAsync(id) is { } d ? Json(d) : NotFound(new { mensaje = "Esa carga no existe." });

        [HttpGet("catalogo")]
        public async Task<IActionResult> Catalogo()
        {
            using var cn = _repo.Conexion();
            return Json(new
            {
                proveedores = await CatalogoCargas.ProveedoresAsync(cn),
                modulos = ProveedorModulos.Todos.Select(m => new { clave = m.Key, nombre = m.Value.Nombre, icono = m.Value.Icono })
            });
        }
    }

    /// <summary>Fase 3: solo la clave compartida (X-Eurovip-Clave), sin cuenta de proveedor: lo usa el personal de Eurovip desde Gestión.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ClaveAdminProveedorAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            if (ClaveProveedorAttribute.RevisarClave(ctx.HttpContext) is { } mal)
            {
                ctx.Result = mal;
                return;
            }
            await next();
        }
    }
}
