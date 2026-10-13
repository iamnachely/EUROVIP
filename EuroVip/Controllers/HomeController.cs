using Dapper;
using EuroVip.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Diagnostics;

namespace EuroVip.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger)
        {
            _configuration = configuration;
            _logger = logger;
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
                        (SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
                          WHERE s.name = 'Eurovip')                                                            AS TablasEurovip",
                    new { userName });

                modelo.UsuariosActivos = stats.UsuariosActivos;
                modelo.ModulosConAcceso = stats.ModulosConAcceso;
                modelo.TablasEurovip = stats.TablasEurovip;
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
