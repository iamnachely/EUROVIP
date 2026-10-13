using EuroVip.Models;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EuroVip.Helpers
{
    /// <summary>
    /// Filtro global de accesos (equivale a [ModuloAuthorize] de BackTiTan, pero sin poner el número en cada acción):
    /// cada controlador pertenece a un módulo de [Eurovip].[UsuarioAcceso] (columna controladores).
    /// • Superadministrador entra a todo.
    /// • Usuario desactivado o borrado: se cierra su sesión.
    /// • Guardar/borrar (POST) sin el módulo: 403.
    /// • Abrir pantallas o descargar (GET que devuelve vista o archivo) sin el módulo: "Acceso denegado".
    ///   Las consultas JSON de solo lectura se permiten porque otras pantallas las usan como listas
    ///   (destinos, campañas, regímenes…).
    /// </summary>
    public class AccesoModuloFilter : IAsyncActionFilter
    {
        public const string ClavePermisos = "ev:permisos";
        private readonly IAccesoService _accesos;

        public AccesoModuloFilter(IAccesoService accesos) => _accesos = accesos;

        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            var http = ctx.HttpContext;
            var userName = http.User.Identity?.IsAuthenticated == true ? http.User.Identity.Name : null;
            if (string.IsNullOrEmpty(userName)) { await next(); return; }

            var p = await _accesos.PermisosAsync(userName);
            if (!p.Existe || !p.Activo)
            {
                await http.SignOutAsync(IdentityConstants.ApplicationScheme);
                http.Session.Clear();
                ctx.Result = EsAjax(http.Request) ? new UnauthorizedObjectResult(new { mensaje = "Tu usuario está desactivado. Vuelve a iniciar sesión." })
                                                  : new RedirectToActionResult("Login", "Auth", null);
                return;
            }
            http.Items[ClavePermisos] = p;

            var controlador = ctx.RouteData.Values["controller"]?.ToString() ?? "";
            var modulo = await _accesos.ModuloDeAsync(controlador);
            if (modulo is null || p.Puede(modulo)) { await next(); return; }

            if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
            {
                ctx.Result = new ObjectResult(new { mensaje = "No tienes acceso a este módulo. Pídeselo a un administrador." }) { StatusCode = 403 };
                return;
            }

            var estricto = ctx.ActionDescriptor.EndpointMetadata.OfType<AccesoEstrictoAttribute>().Any();
            if (estricto && EsAjax(http.Request))
            {
                ctx.Result = new ObjectResult(new { mensaje = "No tienes acceso a este módulo. Pídeselo a un administrador." }) { StatusCode = 403 };
                return;
            }
            if (estricto)
            {
                ctx.Result = new ViewResult { ViewName = "~/Views/Auth/Denied.cshtml", StatusCode = 403 };
                return;
            }

            var hecho = await next();
            if (hecho.Result is JsonResult) return;
            hecho.Result = new ViewResult { ViewName = "~/Views/Auth/Denied.cshtml", StatusCode = 403 };
        }

        private static bool EsAjax(HttpRequest r) =>
            r.Headers.XRequestedWith == "XMLHttpRequest" || r.Headers.Accept.ToString().Contains("application/json") || !HttpMethods.IsGet(r.Method);
    }

    /// <summary>
    /// Para módulos con datos delicados (Usuarios y accesos): sin el módulo no se permite NADA, ni siquiera las consultas JSON.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AccesoEstrictoAttribute : Attribute { }
}
