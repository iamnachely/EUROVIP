using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EuroVip.Helpers
{
    /// <summary>
    /// Pedido de la usuaria: «el sistema nunca puede dar error a la hora de cargar (Excel, cargas manuales, formularios); si algo
    /// no cuadra, decirle al usuario la razón y dejarle corregirlo en el momento, sin cargar todo de nuevo».
    /// Si una petición de la pantalla (guardar, revisar, cargar Excel…: todo lo que no es abrir una página) falla por algo que no
    /// estaba previsto, en vez de la página de error se responde un mensaje claro en JSON que la pantalla muestra donde siempre.
    /// Nada se pierde: lo que la usuaria escribió sigue en la pantalla para corregirlo y volver a intentar. El error real se anota
    /// en el log para revisarlo.
    /// </summary>
    public sealed class SinErrorFilter : IExceptionFilter
    {
        private readonly ILogger<SinErrorFilter> _log;
        public SinErrorFilter(ILogger<SinErrorFilter> log) => _log = log;

        public void OnException(ExceptionContext ctx)
        {
            var rq = ctx.HttpContext.Request;
            var esPantalla = !HttpMethods.IsGet(rq.Method)
                             || rq.Headers.Accept.ToString().Contains("json", StringComparison.OrdinalIgnoreCase)
                             || rq.Headers.XRequestedWith == "XMLHttpRequest";
            if (!esPantalla || ctx.ExceptionHandled) return;
            var ex = ctx.Exception;
            _log.LogError(ex, "Error no previsto en {Ruta}", rq.Path);
            var causa = ex switch
            {
                System.Data.SqlClient.SqlException s when s.Number is -2 or 53 or 10054 or 10060 =>
                    "La base de datos tardó demasiado en responder o se cortó la conexión. Espera unos segundos y vuelve a intentar.",
                System.Data.SqlClient.SqlException s when s.Number is 2627 or 2601 =>
                    "Ya existe un registro con esos mismos datos (algún código o nombre está repetido). Cámbialo y vuelve a guardar.",
                System.Data.SqlClient.SqlException s when s.Number is 8152 or 2628 =>
                    "Algún texto es más largo de lo que se puede guardar. Acórtalo y vuelve a guardar.",
                System.Data.SqlClient.SqlException s when s.Number == 547 =>
                    "Algún dato elegido ya no existe (o está en uso por otro registro). Revisa las listas y vuelve a intentar.",
                InvalidOperationException => ex.Message,
                _ => "No se pudo terminar por un problema inesperado: " + ex.Message
            };
            ctx.Result = new ObjectResult(new { ok = false, mensaje = causa + " Lo que escribiste sigue aquí: corrígelo y vuelve a intentar.", sinError = true }) { StatusCode = 400 };
            ctx.ExceptionHandled = true;
        }
    }
}
