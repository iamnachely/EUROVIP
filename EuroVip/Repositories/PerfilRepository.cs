using Dapper;
using EuroVip.Models;

namespace EuroVip.Repositories
{
    public interface IPerfilRepository
    {
        Task<PerfilDto?> PerfilAsync(string userName);
        Task<bool> GuardarAsync(string userName, PerfilEditarDto dto);
        Task<string?> CambiarFotoAsync(string userName, string? ruta);
        Task<NotificacionesDto> NotificacionesAsync(string userName);
        Task MarcarLeidasAsync(string userName);
    }

    /// <summary>
    /// "Mi perfil" y la campana de la barra superior, sobre [Eurovip].[usuario].
    /// Desde aquí NO se cambian correo, usuario ni contraseña: el UPDATE solo toca nombre, apellido y teléfono.
    /// Las notificaciones se arman con lo que ya existe (Log_Cargas, auditoría, servicio, inicio).
    /// </summary>
    public class PerfilRepository : IPerfilRepository
    {
        private readonly ICircuitoRepository _db;
        public PerfilRepository(ICircuitoRepository db) => _db = db;

        public async Task<PerfilDto?> PerfilAsync(string userName)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<PerfilDto>(@"
                SELECT u.codigoUsuario AS CodigoUsuario, u.nombre AS Nombre, u.apellido AS Apellido, u.telefono AS Telefono, u.imagen AS Imagen,
                       u.rol AS Rol, u.email AS Email, u.userName AS UserName, u.correoAD AS CorreoAD, u.activo AS Activo,
                       (SELECT COUNT(*) FROM Eurovip.Log_Cargas l WHERE l.usuario = u.userName) AS Cargas,
                       (SELECT ISNULL(SUM(ISNULL(l.registros_insertados, 0)), 0) FROM Eurovip.Log_Cargas l WHERE l.usuario = u.userName) AS Registros,
                       (SELECT COUNT(*) FROM Eurovip.auditoria_consulta_oferta a WHERE a.usuario = u.userName) AS Cambios,
                       (SELECT MAX(l.fecha_hora) FROM Eurovip.Log_Cargas l WHERE l.usuario = u.userName) AS UltimaCarga
                FROM Eurovip.usuario u WHERE u.userName = @userName", new { userName });
        }

        public async Task<bool> GuardarAsync(string userName, PerfilEditarDto d)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync(
                "UPDATE Eurovip.usuario SET nombre = @Nombre, apellido = @Apellido, telefono = @Telefono WHERE userName = @userName",
                new { Nombre = d.Nombre.Trim(), Apellido = d.Apellido.Trim(), Telefono = string.IsNullOrWhiteSpace(d.Telefono) ? null : d.Telefono.Trim(), userName }) > 0;
        }

        /// <summary>Guarda la nueva ruta de la foto (o null para quitarla) y devuelve la anterior.</summary>
        public async Task<string?> CambiarFotoAsync(string userName, string? ruta)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteScalarAsync<string?>(@"
                DECLARE @antes varchar(100) = (SELECT imagen FROM Eurovip.usuario WHERE userName = @userName);
                UPDATE Eurovip.usuario SET imagen = @ruta WHERE userName = @userName;
                SELECT @antes;", new { userName, ruta });
        }

        public async Task MarcarLeidasAsync(string userName)
        {
            using var cn = _db.Conexion();
            await cn.ExecuteAsync("UPDATE Eurovip.usuario SET notif_leidas_hasta = GETDATE() WHERE userName = @userName", new { userName });
        }

        public async Task<NotificacionesDto> NotificacionesAsync(string userName)
        {
            using var cn = _db.Conexion();
            var leidas = await cn.ExecuteScalarAsync<DateTime?>("SELECT notif_leidas_hasta FROM Eurovip.usuario WHERE userName = @userName", new { userName });
            var lista = new List<NotificacionDto>();

            // 1) cargas (de todos): terminadas, en proceso o con error
            var cargas = await cn.QueryAsync<(int Id, string Tipo, string Usuario, DateTime Fecha, string? Estatus, int? Registros, string? Mensaje)>(@"
                SELECT TOP 12 id, tipo_carga, usuario, fecha_hora, estatus, registros_insertados, mensaje FROM Eurovip.Log_Cargas ORDER BY id DESC");
            foreach (var c in cargas)
            {
                var ok = string.Equals(c.Estatus, "Correcto", StringComparison.OrdinalIgnoreCase);
                var enCurso = string.Equals(c.Estatus, "En proceso", StringComparison.OrdinalIgnoreCase);
                lista.Add(new NotificacionDto
                {
                    Id = $"carga-{c.Id}", Tipo = ok ? "carga" : enCurso ? "proceso" : "error",
                    Icono = ok ? "bi-cloud-check" : enCurso ? "bi-hourglass-split" : "bi-exclamation-octagon",
                    Titulo = ok ? $"{c.Tipo}: {c.Registros ?? 0} registro{(c.Registros == 1 ? "" : "s")}" : enCurso ? $"{c.Tipo} en proceso" : $"{c.Tipo} con error",
                    Texto = $"{Corta(c.Mensaje, 110)}{(string.IsNullOrWhiteSpace(c.Mensaje) ? "" : " · ")}{(c.Usuario == userName ? "tú" : c.Usuario)}",
                    Fecha = c.Fecha, Url = "/Cargas/Index"
                });
            }

            // 2) cambios hechos por OTROS usuarios (auditoría)
            var cambios = await cn.QueryAsync<(int Id, string Tipo, string Usuario, DateTime Fecha, int? N)>(@"
                SELECT TOP 15 id, tipo_mantenimiento, usuario, fecha_hora, cantidad_servicios FROM Eurovip.auditoria_consulta_oferta
                WHERE usuario <> @userName AND tipo_mantenimiento NOT LIKE 'Perfil[_]%' ORDER BY id DESC", new { userName });
            foreach (var a in cambios)
            {
                var (texto, icono, url) = Describir(a.Tipo, a.N ?? 0);
                lista.Add(new NotificacionDto { Id = $"aud-{a.Id}", Tipo = "cambio", Icono = icono, Titulo = $"{a.Usuario} {texto}", Texto = "", Fecha = a.Fecha, Url = url });
            }

            // 3) alertas del día (no se acumulan: una por día)
            var hoy = DateTime.Today;
            var semana = await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM Eurovip.servicio s
                    LEFT JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio
                    LEFT JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
                WHERE s.activo = 1 AND ISNULL(sd.desde, sc.desde) >= @hoy AND ISNULL(sd.desde, sc.desde) < DATEADD(DAY, 8, @hoy)", new { hoy });
            if (semana > 0)
                lista.Add(new NotificacionDto
                {
                    Id = $"semana-{hoy:yyyyMMdd}", Tipo = "alerta", Icono = "bi-calendar-event",
                    Titulo = $"{semana} salida{(semana == 1 ? "" : "s")} en los próximos 7 días", Texto = "Revisa que precios y cupos estén al día.",
                    Fecha = hoy, Url = "/Ofertas/Index"
                });
            var vencidas = await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM Eurovip.inicio i
                WHERE i.tipo_servicio IN ('OFERTA', 'PAQUETES', 'CIRCUITO') AND ISNULL(i.estado, 1) = 1
                  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio s
                                    LEFT JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio
                                    LEFT JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
                                  WHERE s.id_servicio = i.id_servicio AND s.activo = 1 AND ISNULL(sd.desde, sc.desde) > GETDATE())");
            if (vencidas > 0)
                lista.Add(new NotificacionDto
                {
                    Id = $"home-{hoy:yyyyMMdd}", Tipo = "alerta", Icono = "bi-house-exclamation",
                    Titulo = $"{vencidas} tarjeta{(vencidas == 1 ? "" : "s")} de la Home sin oferta a la venta", Texto = "La web las muestra pero ya no se pueden vender.",
                    Fecha = hoy, Url = "/"
                });

            var orden = lista.OrderByDescending(n => n.Fecha).Take(25).ToList();
            foreach (var n in orden) n.Leida = leidas is not null && n.Fecha <= leidas;
            return new NotificacionesDto { Items = orden, NoLeidas = orden.Count(n => !n.Leida), LeidasHasta = leidas };
        }

        private static (string Texto, string Icono, string Url) Describir(string tipo, int n)
        {
            var p = tipo.Split('_');
            var cuantas = n > 1 ? $" ({n})" : "";
            return p[0] switch
            {
                "Oferta" => p.ElementAtOrDefault(1) switch
                {
                    "Carga" => ($"cargó ofertas{cuantas}", "bi-cloud-upload", "/Ofertas/Index"),
                    "Editar" => ("editó una oferta", "bi-pencil-square", "/Ofertas/Index"),
                    "Eliminar" => ($"eliminó ofertas{cuantas}", "bi-trash3", "/Ofertas/Index"),
                    _ => ($"cambió ofertas en lote{cuantas}", "bi-ui-checks-grid", "/Ofertas/Index")
                },
                "Circuito" => p.ElementAtOrDefault(1) switch
                {
                    "Carga" => ($"cargó salidas de circuito{cuantas}", "bi-signpost-split", "/Circuitos/Index"),
                    "Eliminar" => ("eliminó un circuito o salidas", "bi-trash3", "/Circuitos/Index"),
                    _ => ("modificó un circuito", "bi-signpost-split", "/Circuitos/Index")
                },
                "Hotel" => ("modificó un hotel de la Home", "bi-building", "/Hoteles/Index"),
                "Herramienta" => ($"{(p.ElementAtOrDefault(2) switch { "Crear" => "creó", "Eliminar" => "eliminó", _ => "editó" })} en {HrCatalogos.Buscar(p.ElementAtOrDefault(1))?.Titulo ?? "Herramientas"}",
                                  "bi-tools", $"/Herramientas/Catalogo/{p.ElementAtOrDefault(1)}"),
                _ => ($"hizo un cambio ({tipo})", "bi-activity", "/")
            };
        }

        private static string Corta(string? s, int n) => string.IsNullOrWhiteSpace(s) ? "" : s.Length <= n ? s : s[..n] + "…";
    }
}
