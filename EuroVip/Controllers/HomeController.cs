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
        private readonly IHerramientasRepository _herr;
        private readonly IMultimedia _mm;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger, IHomeWebRepository home, IImagenesLocales img, IHerramientasRepository herr, IMultimedia mm)
        {
            _mm = mm;
            _configuration = configuration;
            _logger = logger;
            _home = home;
            _img = img;
            _herr = herr;
        }

        // Pantallas y accesos directos que encuentra el buscador de arriba (texto, palabras clave, dirección, módulo, icono)
        private static readonly (string Titulo, string Claves, string Url, string? Modulo, string Icono)[] Pantallas =
        {
            ("Nuevo paquete (Express)", "cargar paquete oferta hotel express nueva carga manual", "/CargaManual/Express", "carga_manual", "bi-lightning-charge"),
            ("Nuevo paquete con Excel (Clásica)", "cargar paquete oferta excel clasica", "/CargaManual/Clasica", "carga_manual", "bi-file-earmark-excel"),
            ("Excursiones", "excursiones excursion tour actividad buscar cargar editar foto", "/Excursiones/Index", "excursiones", "bi-compass"),
            ("Traslados", "traslados traslado transfer aeropuerto hotel shuttle compartido privado van", "/Traslados/Index", "traslados", "bi-bus-front"),
            ("Paquetes cargados", "lista paquetes ofertas editar desactivar", "/Ofertas/Index", "carga_manual", "bi-pencil-square"),
            ("Nuevo hotel", "crear hotel vitrina", "/Hoteles/Index?nuevo=1", "hoteles", "bi-building-add"),
            ("Hoteles", "hotel fotos vitrina", "/Hoteles/Index", "hoteles", "bi-building"),
            ("Nuevo circuito", "crear circuito itinerario", "/Circuitos/Index?nuevo=1", "circuitos", "bi-signpost-split"),
            ("Cargar salidas de circuito", "circuito salidas express fechas", "/CargaCircuitos/Express", "circuitos", "bi-calendar-plus"),
            ("Circuitos", "circuito lista", "/Circuitos/Index", "circuitos", "bi-signpost-split"),
            ("Home de la web", "home portada inicio web tarjetas secciones", "/HomeWeb/Index", "home_web", "bi-window-sidebar"),
            ("Carrusel (slider) de la Home", "carrusel slider slide banner", "/HomeWeb/Index#carrusel", "home_web", "bi-images"),
            ("Consultar ofertas", "buscar consultar duplicar editar en masa", "/ConsultarOferta/Index", "consultar_ofertas", "bi-search-heart"),
            ("Flyers", "flyer whatsapp contactos imagenes", "/Flyer/Hub", "flyers", "bi-file-earmark-richtext"),
            ("Herramientas (catálogos)", "herramientas catalogos destinos monedas campañas paises tasas regimen proveedores", "/Herramientas/Index", "herramientas", "bi-tools"),
            ("Usuarios y accesos", "usuarios permisos accesos contraseña", "/Usuarios/Index", "usuarios", "bi-people"),
            ("Historial de cargas", "cargas historial log errores revisar", "/Cargas/Index", "historial_cargas", "bi-clock-history"),
            ("Reportes", "reportes salidas estadisticas", "/Reportes/Index", "reportes", "bi-bar-chart-line"),
            ("Mi perfil", "perfil foto nombre telefono", "/Perfil/Index", null, "bi-person-circle"),
        };

        private static string SinAcentos(string s) =>
            new string((s ?? "").Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();

        /// <summary>
        /// Buscador de la barra de arriba (Ctrl+K): pantallas y accesos directos, circuitos, hoteles, ofertas por hotel y destino,
        /// y catálogos de Herramientas. Solo devuelve lo de los módulos a los que el usuario tiene acceso.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Buscar(string? q)
        {
            q = (q ?? "").Trim();
            if (q.Length < 2) return Json(Array.Empty<object>());
            var p = HttpContext.Items[AccesoModuloFilter.ClavePermisos] as PermisosUsuario;
            bool Puede(string? m) => m is null || (p?.Puede(m) ?? true);
            var res = new List<object>();
            void Agregar(string grupo, string icono, string titulo, string? sub, string url) => res.Add(new { grupo, icono, titulo, sub, url });

            var qn = SinAcentos(q);
            foreach (var x in Pantallas.Where(x => Puede(x.Modulo) && (SinAcentos(x.Titulo).Contains(qn) || SinAcentos(x.Claves).Contains(qn))).Take(5))
                Agregar("Ir a", x.Icono, x.Titulo, null, x.Url);

            try
            {
                using var cn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                var like = $"%{q}%";
                if (Puede("circuitos"))
                    foreach (var c in await cn.QueryAsync<(string Id, string Nombre)>(
                        "SELECT TOP 5 id, nombre_circuito FROM Eurovip.circuito WHERE id_idioma = 'ES' AND (nombre_circuito LIKE @like OR id LIKE @like) ORDER BY nombre_circuito", new { like }))
                        Agregar("Circuitos", "bi-signpost-split", c.Nombre, $"Código {c.Id}", "/Circuitos/Index?editar=" + Uri.EscapeDataString(c.Id));
                if (Puede("hoteles"))
                    foreach (var h in await cn.QueryAsync<(int Id, string Nombre, string? Destino)>(
                        "SELECT TOP 5 id_servicio, LTRIM(RTRIM(id_destino)), caratulaDestino FROM Eurovip.inicio WHERE tipo_servicio = 'HOTEL' AND (id_destino LIKE @like OR caratulaDestino LIKE @like) ORDER BY id_destino", new { like }))
                        Agregar("Hoteles de la web", "bi-building", h.Nombre, h.Destino, "/Hoteles/Index?editar=" + h.Id);
                if (Puede("carga_manual"))
                {
                    foreach (var o in await cn.QueryAsync<(string Hotel, int N)>(@"
                        SELECT TOP 5 a.nombre, COUNT(*) FROM Eurovip.servicio s
                            JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.secuencia = 1
                            JOIN Eurovip.alojamiento a ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
                        WHERE s.id_tipo_servicio = 'OFERTA' AND s.activo = 1 AND sd.desde > CAST(GETDATE() AS date) AND a.nombre LIKE @like
                        GROUP BY a.nombre ORDER BY a.nombre", new { like }))
                        Agregar("Ofertas", "bi-pencil-square", o.Hotel, $"{o.N} oferta{(o.N == 1 ? "" : "s")} a la venta", "/Ofertas/Index?q=" + Uri.EscapeDataString(o.Hotel));
                    foreach (var d in await cn.QueryAsync<(string Destino, int N)>(@"
                        SELECT TOP 3 d.destino, COUNT(*) FROM Eurovip.servicio_destino d JOIN Eurovip.servicio s ON s.id_servicio = d.id_servicio
                        WHERE s.activo = 1 AND d.destino LIKE @like GROUP BY d.destino ORDER BY d.destino", new { like }))
                        Agregar("Ofertas", "bi-geo-alt", $"Ofertas de {d.Destino}", $"{d.N} oferta{(d.N == 1 ? "" : "s")} activas", "/Ofertas/Index?destino=" + Uri.EscapeDataString(d.Destino));
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en el buscador global"); }

            if (Puede("herramientas"))
            {
                try
                {
                    foreach (var g in (await _herr.BuscarGlobalAsync(q)).Take(4))
                        Agregar("Catálogos", g.Icono, $"Ver en {g.Titulo}", "Herramientas", $"/Herramientas/Catalogo/{g.Clave}?q={Uri.EscapeDataString(q)}");
                }
                catch (Exception ex) { _logger.LogError(ex, "Error buscando en catálogos"); }
            }
            return Json(res);
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
                    var conFoto = await _mm.PortadasAsync("HOTEL");   // foto nueva (Eurovip.multimedia) o, si no, la de antes por nombre
                    var hoteles = (await cn.QueryAsync<(int Id, string Nombre)>("SELECT id_servicio, LTRIM(RTRIM(id_destino)) FROM Eurovip.inicio WHERE tipo_servicio = 'HOTEL'"))
                        .Where(h => !conFoto.ContainsKey(h.Id.ToString()) && _img.Buscar("hoteles", h.Nombre) is null).Select(h => h.Nombre).Distinct().ToList();
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
            new() { Titulo = "Paquetes",         Descripcion = "Ofertas de hotel: Express o Clásica (con Excel). Vuelo opcional.", Icono = "bi-box-seam", Etiqueta = "Disponible", Url = "/Ofertas/Index", Disponible = true, Modulo = "carga_manual" },
            new() { Titulo = "Excursiones",      Descripcion = "Excursiones por destino: precio, cancelación, descripción y foto.", Icono = "bi-compass", Etiqueta = "Disponible", Url = "/Excursiones/Index", Disponible = true, Modulo = "excursiones" },
            new() { Titulo = "Traslados",        Descripcion = "Traslados aeropuerto → hotel: compartido o privado, vehículo, precio y foto.", Icono = "bi-bus-front", Etiqueta = "Disponible", Url = "/Traslados/Index", Disponible = true, Modulo = "traslados" },
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
