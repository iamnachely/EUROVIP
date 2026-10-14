using Dapper;
using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Diagnostics;

namespace EuroVip.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;
        private readonly IHomeWebRepository _home;
        private readonly IImagenesLocales _img;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger, IHomeWebRepository home, IImagenesLocales img)
        {
            _configuration = configuration;
            _logger = logger;
            _home = home;
            _img = img;
        }

        /// <summary>
        /// "Tus pendientes" del Inicio: lo que conviene revisar hoy, cada cosa con un botón para ir a resolverla.
        /// Solo se muestran los pendientes de los módulos a los que el usuario tiene acceso.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Pendientes()
        {
            var p = HttpContext.Items[AccesoModuloFilter.ClavePermisos] as PermisosUsuario;
            bool Puede(string m) => p?.Puede(m) ?? true;
            var lista = new List<object>();
            void Agregar(string nivel, string icono, string titulo, string detalle, string? accion, string? url) =>
                lista.Add(new { nivel, icono, titulo, detalle, accion, url });

            try
            {
                using var cn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                // 1) Lo que se acaba pronto: la última fecha a la venta es dentro de los próximos 7 días
                var vencen = await cn.QueryAsync<(string Tipo, string Nombre, DateTime Ultima)>(@"
                    SELECT 'OFERTA', d.destino, MAX(sd.desde)
                    FROM Eurovip.servicio_destino d JOIN Eurovip.servicio s ON s.id_servicio = d.id_servicio
                        JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio
                    WHERE s.activo = 1 AND s.id_tipo_servicio = 'OFERTA' AND sd.desde > CAST(GETDATE() AS date)
                    GROUP BY d.destino HAVING MAX(sd.desde) <= DATEADD(DAY, 7, CAST(GETDATE() AS date))
                    UNION ALL
                    SELECT 'CIRCUITO', sc.nombre_circuito, MAX(sc.desde)
                    FROM Eurovip.servicio_circuito sc JOIN Eurovip.servicio s ON s.id_servicio = sc.id_servicio
                    WHERE s.activo = 1 AND sc.desde > CAST(GETDATE() AS date)
                    GROUP BY sc.nombre_circuito HAVING MAX(sc.desde) <= DATEADD(DAY, 7, CAST(GETDATE() AS date))");
                foreach (var v in vencen)
                {
                    if (v.Tipo == "OFERTA" && !Puede("carga_manual")) continue;
                    if (v.Tipo == "CIRCUITO" && !Puede("circuitos")) continue;
                    Agregar("aviso", "bi-hourglass-split", v.Tipo == "OFERTA" ? $"Las ofertas de {v.Nombre} se acaban pronto" : $"El circuito {v.Nombre} se acaba pronto",
                        $"Su última fecha a la venta es el {v.Ultima:dd/MM/yyyy}. Después deja de salir en la web.",
                        v.Tipo == "OFERTA" ? "Cargar más fechas" : "Cargar salidas", v.Tipo == "OFERTA" ? "/CargaManual/Express" : "/Circuitos/Index");
                }

                // 2) Cargas que quedaron para revisar (últimos 30 días)
                if (Puede("historial_cargas"))
                {
                    var revisar = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.Log_Cargas WHERE estatus = N'Revisar' AND fecha_hora >= DATEADD(DAY, -30, GETDATE())");
                    if (revisar > 0)
                        Agregar("error", "bi-exclamation-octagon", $"{revisar} carga{(revisar == 1 ? "" : "s")} para revisar",
                            "Alguna carga de los últimos 30 días terminó con problemas.", "Ver cargas", "/Cargas/Index");
                }

                // 3) Hoteles de la Home sin foto
                if (Puede("hoteles"))
                {
                    var hoteles = (await cn.QueryAsync<string>("SELECT DISTINCT LTRIM(RTRIM(id_destino)) FROM Eurovip.inicio WHERE tipo_servicio = 'HOTEL'"))
                        .Where(n => _img.Buscar("hoteles", n) is null).ToList();
                    if (hoteles.Count > 0)
                        Agregar("info", "bi-image", $"{hoteles.Count} hotel{(hoteles.Count == 1 ? "" : "es")} sin foto",
                            string.Join(", ", hoteles.Take(4)) + (hoteles.Count > 4 ? "…" : "") + ". En la web se ven sin imagen.", "Subir fotos", "/Hoteles/Index");
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error calculando pendientes del Inicio"); }

            // 4) Lo que no sale en la Home de la web (ya explica el motivo)
            if (Puede("home_web"))
            {
                try
                {
                    foreach (var f in (await _home.FueraDeHomeAsync()).Where(f => f.Proxima is null).Take(6))
                        Agregar("aviso", "bi-house-exclamation", $"{f.Nombre} no sale en la Home", f.Motivo, f.Accion ?? "Ver Home", f.Url ?? "/HomeWeb/Index");
                }
                catch (Exception ex) { _logger.LogError(ex, "Error leyendo lo que no sale en la Home"); }
            }
            return Json(lista);
        }

        public async Task<IActionResult> Index()
        {
            var userName = User.Identity?.Name ?? string.Empty;
            var modelo = new HomeViewModel
            {
                NombreUsuario = HttpContext.Session.GetString("NombreUsuario") ?? userName,
                Rol = HttpContext.Session.GetString("Rol"),
                ModulosCarga = Visibles(ModulosCarga()),
                ModulosAdministracion = Visibles(ModulosAdministracion())
            };

            try
            {
                using var cn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                var stats = await cn.QueryFirstAsync(@"
                    SELECT
                        (SELECT COUNT(*) FROM Eurovip.usuario WHERE activo = 1)                                AS UsuariosActivos,
                        (SELECT COUNT(*) FROM Eurovip.UsuarioModulos WHERE usuario = @userName AND acceso = 1) AS ModulosConAcceso,
                        (SELECT COUNT(*) FROM Eurovip.servicio s JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio
                          WHERE s.activo = 1 AND s.id_tipo_servicio = 'OFERTA' AND sd.desde > CAST(GETDATE() AS date)) AS OfertasVenta,
                        (SELECT COUNT(DISTINCT sc.nombre_circuito) FROM Eurovip.servicio_circuito sc JOIN Eurovip.servicio s ON s.id_servicio = sc.id_servicio
                          WHERE s.activo = 1 AND sc.desde > CAST(GETDATE() AS date))                         AS CircuitosVenta",
                    new { userName });

                modelo.UsuariosActivos = stats.UsuariosActivos;
                modelo.ModulosConAcceso = stats.ModulosConAcceso;
                modelo.OfertasVenta = stats.OfertasVenta;
                modelo.CircuitosVenta = stats.CircuitosVenta;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando indicadores del Home");
            }

            return View(modelo);
        }

        // Módulos de carga Eurovip — todo manual; los vuelos son OPCIONALES en carga manual y circuitos.
        private static List<ModuloHome> ModulosCarga() => new()
        {
            new() { Titulo = "Carga de Hoteles", Descripcion = "Publica hoteles en la vitrina: precio, régimen e imagen.", Icono = "bi-building", Etiqueta = "Disponible", Url = "/Hoteles/Index", Disponible = true, Modulo = "hoteles" },
            new() { Titulo = "Carga Manual",     Descripcion = "Ofertas de hotel: Express o Clásica (con Excel). Vuelo opcional.", Icono = "bi-pencil-square", Etiqueta = "Disponible", Url = "/Ofertas/Index", Disponible = true, Modulo = "carga_manual" },
            new() { Titulo = "Carga Excursión",  Descripcion = "Excursiones por destino, precio y cancelación.",    Icono = "bi-compass",        Etiqueta = "Fase 2" },
            new() { Titulo = "Carga Traslado",   Descripcion = "Traslados aeropuerto ↔ hotel y entre ciudades.",   Icono = "bi-car-front",      Etiqueta = "Nuevo" },
            new() { Titulo = "Carga Circuito",   Descripcion = "Catálogo, itinerario, hoteles y salidas. Vuelo opcional.", Icono = "bi-signpost-split", Etiqueta = "Disponible", Url = "/Circuitos/Index", Disponible = true, Modulo = "circuitos" },
        };

        /// <summary>Solo las tarjetas de los módulos que el usuario tiene (Usuarios y accesos).</summary>
        private List<ModuloHome> Visibles(List<ModuloHome> l) =>
            HttpContext.Items[EuroVip.Helpers.AccesoModuloFilter.ClavePermisos] is EuroVip.Models.PermisosUsuario p
                ? l.Where(m => m.Modulo is null || p.Puede(m.Modulo)).ToList() : l;

        private static List<ModuloHome> ModulosAdministracion() => new()
        {
            new() { Titulo = "Usuarios y accesos", Descripcion = "Altas, contraseñas y qué ve cada uno.", Icono = "bi-people",  Url = "/Usuarios/Index",     Disponible = true, Modulo = "usuarios" },
            new() { Titulo = "Herramientas",       Descripcion = "Destinos, monedas, proveedores…",       Icono = "bi-tools",   Url = "/Herramientas/Index", Disponible = true, Modulo = "herramientas" },
        };
    }
}
