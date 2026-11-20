using System.Text.Json;
using EuroVip.Services;
using System.Text.RegularExpressions;
using Dapper;
using EuroVip.Repositories;

namespace EuroVip.Services
{
    /// <summary>
    /// Seguimiento interno de las cargas de proveedores (20/11/2026). Pedido de la usuaria: «tablero, seguimiento del día, ver toda la info
    /// de los proveedores, organizar y revisar las cargas, chat tipo WhatsApp de cada solicitud con usuario, fecha y hora, notas internas».
    /// Todo se calcula de las tablas que ya existen (carga_proveedor, carga_proveedor_comentario, proveedor_cuenta, proveedor_solicitud) y,
    /// para lo nuevo (notas internas, responsable y prioridad), de Database/proveedor-seguimiento.sql. Sin esas tablas la pantalla sigue
    /// funcionando y avisa.
    /// </summary>
    public class ProveedoresSeguimiento
    {
        private readonly ICircuitoRepository _db;
        private readonly IWebHostEnvironment _env;
        private readonly IMultimedia _mm;
        public ProveedoresSeguimiento(ICircuitoRepository db, IWebHostEnvironment env, IMultimedia mm) { _db = db; _env = env; _mm = mm; }

        /// <summary>Plazo máximo para atender una solicitud de un proveedor (pedido de la usuaria: 5 días). Pasado el plazo queda «vencida · por ver».</summary>
        public const int PlazoDias = 5;

        // ---------------------------------------------------------------- logos de los proveedores
        private const string CarpetaLogos = "uploads/proveedores/logos";
        private static readonly string[] ExtLogo = { "png", "jpg", "webp" };

        /// <summary>Logo de cada cuenta (id → URL) leyendo la carpeta. Sin logo = no está en el diccionario.</summary>
        public Dictionary<int, string> Logos()
        {
            var d = new Dictionary<int, string>();
            var dir = Path.Combine(_env.WebRootPath ?? "wwwroot", CarpetaLogos.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir)) return d;
            foreach (var f in Directory.EnumerateFiles(dir))
                if (int.TryParse(Path.GetFileNameWithoutExtension(f), out var id) && ExtLogo.Contains(Path.GetExtension(f).TrimStart('.').ToLowerInvariant()))
                    d[id] = $"/{CarpetaLogos}/{Path.GetFileName(f)}?v={File.GetLastWriteTimeUtc(f).Ticks}";
            return d;
        }

        /// <summary>Guarda el logo tal cual lo subió (sin recomprimir: nunca se baja la calidad). Devuelve (url, error).</summary>
        public async Task<(string? Url, string? Error)> GuardarLogoAsync(int cuenta, Microsoft.AspNetCore.Http.IFormFile? archivo)
        {
            if (archivo is null || archivo.Length == 0) return (null, "Elige una imagen.");
            if (archivo.Length > 5_000_000) return (null, "El logo pesa más de 5 MB.");
            var ext = await EuroVip.Helpers.ImagenSegura.ExtensionAsync(archivo);
            if (ext is null || !ExtLogo.Contains(ext)) return (null, "El logo tiene que ser una imagen PNG, JPG o WebP.");
            var dir = Path.Combine(_env.WebRootPath ?? "wwwroot", CarpetaLogos.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(dir);
            foreach (var e in ExtLogo) { var v = Path.Combine(dir, $"{cuenta}.{e}"); if (File.Exists(v)) File.Delete(v); }
            var destino = Path.Combine(dir, $"{cuenta}.{ext}");
            await using (var fs = File.Create(destino)) await archivo.CopyToAsync(fs);
            return ($"/{CarpetaLogos}/{cuenta}.{ext}?v={DateTime.UtcNow.Ticks}", null);
        }

        // ---------------------------------------------------------------- equipo (a quién se le puede encargar un chat)
        public sealed class Miembro { public string UserName { get; set; } = ""; public string Nombre { get; set; } = ""; public string? Imagen { get; set; } public string? Foto { get; set; } }
        public async Task<List<Miembro>> EquipoAsync()
        {
            using var cn = _db.Conexion();
            var l = (await cn.QueryAsync<Miembro>("SELECT userName AS UserName, LTRIM(RTRIM(nombre)) + ' ' + LTRIM(RTRIM(apellido)) AS Nombre, imagen AS Imagen FROM Eurovip.usuario WHERE activo = 1 ORDER BY nombre, apellido")).ToList();
            l.ForEach(m => m.Foto = EuroVip.Models.PerfilFoto.Url(m.Imagen));
            return l;
        }

        private static bool _hayNotas, _hayGestion; private static DateTime _mirado = DateTime.MinValue;
        /// <summary>¿Ya están las tablas nuevas? Se mira una vez y, mientras falten, cada 2 minutos.</summary>
        private async Task MirarAsync(System.Data.Common.DbConnection cn)
        {
            if (_hayNotas && _hayGestion) return;
            if (DateTime.UtcNow - _mirado < TimeSpan.FromMinutes(2)) return;
            _mirado = DateTime.UtcNow;
            _hayNotas = await SqlMapper.ExecuteScalarAsync<int?>(cn, "SELECT OBJECT_ID('Eurovip.carga_proveedor_nota', 'U')") is not null;
            _hayGestion = await SqlMapper.ExecuteScalarAsync<int?>(cn, "SELECT OBJECT_ID('Eurovip.carga_proveedor_gestion', 'U')") is not null;
        }
        public async Task<bool> DisponibleAsync() { using var cn = _db.Conexion(); await MirarAsync(cn); return _hayNotas && _hayGestion; }

        // ---------------------------------------------------------------- notas internas
        public sealed class Nota { public int Id { get; set; } public int Id_Carga { get; set; } public string Autor { get; set; } = ""; public string Texto { get; set; } = ""; public bool Fijada { get; set; } public DateTime Creado_En { get; set; } }

        public async Task<List<Nota>> NotasAsync(int idCarga)
        {
            using var cn = _db.Conexion(); await MirarAsync(cn);
            if (!_hayNotas) return new();
            return (await cn.QueryAsync<Nota>("SELECT id, id_carga, autor, texto, fijada, creado_en FROM Eurovip.carga_proveedor_nota WHERE id_carga = @idCarga ORDER BY id", new { idCarga })).ToList();
        }

        public async Task<string?> NotaAsync(int idCarga, string autor, string? texto)
        {
            texto = (texto ?? "").Trim();
            if (texto.Length == 0) return "Escribe la nota.";
            if (texto.Length > 2000) return "La nota admite como máximo 2000 caracteres.";
            using var cn = _db.Conexion(); await MirarAsync(cn);
            if (!_hayNotas) return "Las notas internas todavía no están activadas en este servidor (falta aplicar Database/proveedor-seguimiento.sql).";
            await cn.ExecuteAsync("INSERT INTO Eurovip.carga_proveedor_nota (id_carga, autor, texto) VALUES (@idCarga, @autor, @texto)", new { idCarga, autor = autor.Length > 150 ? autor[..150] : autor, texto });
            return null;
        }

        public async Task<string?> FijarNotaAsync(int id, bool fijada)
        {
            using var cn = _db.Conexion(); await MirarAsync(cn);
            if (!_hayNotas) return "Las notas internas todavía no están activadas.";
            return await cn.ExecuteAsync("UPDATE Eurovip.carga_proveedor_nota SET fijada = @fijada WHERE id = @id", new { id, fijada }) == 0 ? "Esa nota ya no existe." : null;
        }

        /// <summary>Solo borra su autor (una nota interna es del equipo, pero nadie borra la de otro).</summary>
        public async Task<string?> BorrarNotaAsync(int id, string usuario)
        {
            using var cn = _db.Conexion(); await MirarAsync(cn);
            if (!_hayNotas) return "Las notas internas todavía no están activadas.";
            return await cn.ExecuteAsync("DELETE FROM Eurovip.carga_proveedor_nota WHERE id = @id AND autor = @usuario", new { id, usuario }) == 0 ? "Solo puedes borrar tus propias notas." : null;
        }

        // ---------------------------------------------------------------- responsable y prioridad
        public sealed class Gestion { public int Id_Carga { get; set; } public string? Asignado_A { get; set; } public int Prioridad { get; set; } public DateTime? Actualizado_En { get; set; } public string? Actualizado_Por { get; set; } }

        public async Task<List<Gestion>> GestionesAsync()
        {
            using var cn = _db.Conexion(); await MirarAsync(cn);
            if (!_hayGestion) return new();
            return (await cn.QueryAsync<Gestion>("SELECT id_carga, asignado_a, prioridad, actualizado_en, actualizado_por FROM Eurovip.carga_proveedor_gestion WHERE asignado_a IS NOT NULL OR prioridad > 0")).ToList();
        }

        public async Task<string?> GuardarGestionAsync(int idCarga, string? asignado, int prioridad, string usuario)
        {
            if (prioridad is < 0 or > 2) return "La prioridad no es válida.";
            asignado = string.IsNullOrWhiteSpace(asignado) ? null : asignado.Trim();
            if (asignado is { Length: > 150 }) asignado = asignado[..150];
            using var cn = _db.Conexion(); await MirarAsync(cn);
            if (!_hayGestion) return "El responsable y la prioridad todavía no están activados en este servidor (falta aplicar Database/proveedor-seguimiento.sql).";
            var antes = await cn.QueryFirstOrDefaultAsync<(string? Asignado, int Prioridad)?>("SELECT asignado_a, prioridad FROM Eurovip.carga_proveedor_gestion WHERE id_carga = @idCarga", new { idCarga });
            if (_hayNotas && (antes?.Asignado ?? null) != asignado)
                await cn.ExecuteAsync("INSERT INTO Eurovip.carga_proveedor_nota (id_carga, autor, texto) VALUES (@idCarga, @usuario, @texto)",
                    new { idCarga, usuario, texto = asignado is null ? "[[SIS]]Quitó la asignación de la conversación" : asignado == usuario ? "[[SIS]]Se encargó de la conversación" : $"[[SIS]]Encargó la conversación a {asignado}" });
            await cn.ExecuteAsync(@"MERGE Eurovip.carga_proveedor_gestion AS t USING (SELECT @idCarga AS id) AS s ON t.id_carga = s.id
                WHEN MATCHED THEN UPDATE SET asignado_a = @asignado, prioridad = @prioridad, actualizado_en = SYSDATETIME(), actualizado_por = @usuario
                WHEN NOT MATCHED THEN INSERT (id_carga, asignado_a, prioridad, actualizado_por) VALUES (@idCarga, @asignado, @prioridad, @usuario);",
                new { idCarga, asignado, prioridad, usuario });
            return null;
        }

        // ---------------------------------------------------------------- seguimiento del día
        public sealed class CargaCorta
        {
            public int Id { get; set; } public string Codigo { get; set; } = ""; public string? Titulo { get; set; } public string Modulo { get; set; } = ""; public string Estado { get; set; } = "";
            public string? Cuenta { get; set; } public DateTime? Fecha { get; set; }
        }
        public sealed class Evento
        {
            public DateTime Fecha { get; set; } public string Tipo { get; set; } = ""; public int Id_Carga { get; set; } public string Codigo { get; set; } = "";
            public string? Cuenta { get; set; } public string? Quien { get; set; } public string? Texto { get; set; }
        }

        /// <summary>Los números de HOY, lo que está atrasado, lo que sale pronto y el movimiento reciente (todos los proveedores).</summary>
        public async Task<object> DiaAsync()
        {
            using var cn = _db.Conexion(); await MirarAsync(cn);
            var n = await cn.QueryFirstAsync<dynamic>(@"
                DECLARE @hoy date = CAST(SYSDATETIME() AS date), @manana date = DATEADD(DAY, 1, CAST(SYSDATETIME() AS date));
                SELECT
                  (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE CAST(fecha_solicitud AS date) = @hoy) AS EnviadasHoy,
                  (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE CAST(fecha_revision AS date) = @hoy) AS RevisadasHoy,
                  (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE CAST(fecha_activacion AS date) = @hoy) AS ActivadasHoy,
                  (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE estado = 'PROGRAMADA' AND CAST(activar_en AS date) = @hoy) AS SalenHoy,
                  (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE estado = 'PROGRAMADA' AND CAST(activar_en AS date) = @manana) AS SalenManana,
                  (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE estado = 'PENDIENTE' AND fecha_solicitud < DATEADD(HOUR, -24, SYSDATETIME())) AS Atrasadas,
                  (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE estado = 'PENDIENTE') AS PorRevisar,
                  (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE error IS NOT NULL AND estado <> 'ACTIVA') AS ConError");
            var atrasadas = (await cn.QueryAsync<CargaCorta>(@"
                SELECT TOP 8 c.id, c.codigo, c.titulo, c.modulo, c.estado, pc.nombre AS Cuenta, c.fecha_solicitud AS Fecha
                FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta
                WHERE c.estado = 'PENDIENTE' ORDER BY c.fecha_solicitud")).ToList();
            var salen = (await cn.QueryAsync<CargaCorta>(@"
                SELECT TOP 8 c.id, c.codigo, c.titulo, c.modulo, c.estado, pc.nombre AS Cuenta, c.activar_en AS Fecha
                FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta
                WHERE c.estado = 'PROGRAMADA' ORDER BY c.activar_en")).ToList();
            var sinResponder = (await cn.QueryAsync<CargaCorta>(@"
                SELECT TOP 8 c.id, c.codigo, c.titulo, c.modulo, c.estado, pc.nombre AS Cuenta, m.fecha AS Fecha
                FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta
                CROSS APPLY (SELECT TOP 1 autor, fecha FROM Eurovip.carga_proveedor_comentario WHERE id_carga = c.id ORDER BY id DESC) m
                WHERE m.autor = 'PROVEEDOR' ORDER BY m.fecha DESC")).ToList();
            var actividad = (await cn.QueryAsync<Evento>(@"
                SELECT TOP 40 * FROM (
                    SELECT c.creado_en AS Fecha, 'CREADA' AS Tipo, c.id AS Id_Carga, c.codigo AS Codigo, pc.nombre AS Cuenta, pc.nombre AS Quien, NULL AS Texto
                      FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta
                    UNION ALL SELECT c.fecha_solicitud, 'ENVIADA', c.id, c.codigo, pc.nombre, pc.nombre, NULL
                      FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta WHERE c.fecha_solicitud IS NOT NULL
                    UNION ALL SELECT c.fecha_revision, c.estado, c.id, c.codigo, pc.nombre, c.revisado_por, c.motivo
                      FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta WHERE c.fecha_revision IS NOT NULL
                    UNION ALL SELECT c.fecha_activacion, 'EN_WEB', c.id, c.codigo, pc.nombre, c.revisado_por, NULL
                      FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta WHERE c.fecha_activacion IS NOT NULL
                    UNION ALL SELECT m.fecha, 'MENSAJE_' + m.autor, c.id, c.codigo, pc.nombre, m.nombre, m.texto
                      FROM Eurovip.carga_proveedor_comentario m JOIN Eurovip.carga_proveedor c ON c.id = m.id_carga JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta
                ) x ORDER BY Fecha DESC")).ToList();
            var datos = 0;
            try { datos = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.proveedor_solicitud WHERE estado = 'PENDIENTE'"); } catch (System.Data.SqlClient.SqlException) { }
            object sla;
            try
            {
                var plazoH = PlazoDias * 24;
                var s = await cn.QueryFirstAsync<dynamic>(@"
                    SELECT (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE estado = 'PENDIENTE' AND fecha_solicitud < DATEADD(HOUR, -@plazo, SYSDATETIME())) AS Vencidas,
                           (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE estado = 'PENDIENTE' AND fecha_solicitud >= DATEADD(HOUR, -@plazo, SYSDATETIME()) AND fecha_solicitud < DATEADD(HOUR, -(@plazo - 24), SYSDATETIME())) AS VencenPronto,
                           (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE estado = 'PENDIENTE' AND fecha_solicitud >= DATEADD(HOUR, -(@plazo - 24), SYSDATETIME())) AS EnTiempo,
                           (SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE fecha_revision > DATEADD(DAY, -30, SYSDATETIME()) AND fecha_solicitud IS NOT NULL) AS Revisadas30,
                           (SELECT AVG(CAST(DATEDIFF(MINUTE, fecha_solicitud, fecha_revision) AS float)) / 60.0 FROM Eurovip.carga_proveedor WHERE fecha_revision > DATEADD(DAY, -30, SYSDATETIME()) AND fecha_solicitud IS NOT NULL) AS PromedioHoras,
                           (SELECT SUM(CASE WHEN DATEDIFF(HOUR, fecha_solicitud, fecha_revision) <= @plazo THEN 1 ELSE 0 END) FROM Eurovip.carga_proveedor WHERE fecha_revision > DATEADD(DAY, -30, SYSDATETIME()) AND fecha_solicitud IS NOT NULL) AS EnPlazo", new { plazo = plazoH });
                var porRevisor = (await cn.QueryAsync<(string? Quien, int N, double? Horas)>(@"
                    SELECT ISNULL(revisado_por, '—'), COUNT(*), AVG(CAST(DATEDIFF(MINUTE, fecha_solicitud, fecha_revision) AS float)) / 60.0
                    FROM Eurovip.carga_proveedor WHERE fecha_revision > DATEADD(DAY, -30, SYSDATETIME()) AND fecha_solicitud IS NOT NULL
                    GROUP BY revisado_por ORDER BY COUNT(*) DESC")).Select(x => new { quien = x.Quien, revisadas = x.N, horas = x.Horas }).ToList();
                sla = new { plazoDias = PlazoDias, vencidas = (int)s.Vencidas, vencenPronto = (int)s.VencenPronto, enTiempo = (int)s.EnTiempo, revisadas30 = (int)s.Revisadas30, promedioHoras = (double?)s.PromedioHoras, enPlazo = (int?)s.EnPlazo ?? 0, porRevisor };
            }
            catch (Exception) { sla = new { plazoDias = PlazoDias, vencidas = 0, vencenPronto = 0, enTiempo = 0, revisadas30 = 0, promedioHoras = (double?)null, enPlazo = 0, porRevisor = Array.Empty<object>() }; }
            var cuidados = new List<Punto>();
            try
            {
                foreach (var x in await cn.QueryAsync<(string Nombre, int N)>("SELECT pc.nombre, COUNT(*) FROM Eurovip.proveedor_cuenta pc JOIN Eurovip.carga_proveedor c ON c.id_cuenta = pc.id WHERE pc.activo = 0 AND c.estado IN ('PENDIENTE','PROGRAMADA') GROUP BY pc.nombre"))
                    cuidados.Add(new("rojo", $"{x.Nombre}: cuenta desactivada con {x.N} carga(s) por salir", "Revisa si deben publicarse."));
                foreach (var x in await cn.QueryAsync<(string Nombre, int N)>("SELECT pc.nombre, COUNT(*) FROM Eurovip.proveedor_cuenta pc JOIN Eurovip.carga_proveedor c ON c.id_cuenta = pc.id WHERE c.estado IN ('RECHAZADA','DEVUELTA') AND c.fecha_revision > DATEADD(DAY, -30, SYSDATETIME()) GROUP BY pc.nombre HAVING COUNT(*) >= 3"))
                    cuidados.Add(new("ama", $"{x.Nombre}: {x.N} cargas rechazadas o devueltas en 30 días", "Conviene hablar con ellos."));
                var atras = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE estado = 'PROGRAMADA' AND activar_en < DATEADD(MINUTE, -30, SYSDATETIME())");
                if (atras > 0) cuidados.Add(new("rojo", $"{atras} carga(s) programada(s) no salieron a su hora", "Mira si el servidor de altas programadas está encendido."));
                var viejas = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.proveedor_cuenta pc WHERE pc.activo = 1 AND pc.modulos <> '' AND NOT EXISTS (SELECT 1 FROM Eurovip.carga_proveedor c WHERE c.id_cuenta = pc.id AND c.creado_en > DATEADD(DAY, -30, SYSDATETIME()))");
                if (viejas > 0) cuidados.Add(new("info", $"{viejas} proveedor(es) activos no cargan nada hace 30 días", "Mira la pestaña Proveedores."));
            }
            catch (System.Data.SqlClient.SqlException) { }
            if (((dynamic)sla).vencidas > 0) cuidados.Insert(0, new("rojo", $"{((dynamic)sla).vencidas} carga(s) pasaron el plazo de {PlazoDias} días sin revisar", "Quedan como «vencidas · por ver». Atiéndelas primero."));
            if (datos > 0) cuidados.Add(new("ama", $"{datos} dato(s) pedido(s) por proveedores sin revisar", "Hasta aprobar un hotel pedido, no se pueden aprobar las cargas que lo usan."));
            return new { numeros = n, atrasadas, salen, sinResponder, actividad, datosPendientes = datos, cuidados, sla, seguimientoActivo = _hayNotas && _hayGestion };
        }

        // ---------------------------------------------------------------- control de calidad de una carga
        public sealed record Punto(string Nivel, string Titulo, string? Detalle);

        private static readonly Regex Enlace = new(@"https?://|www\.|\b[\w.+-]+@[\w-]+\.[\w.]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Telefono = new(@"(?<!\d)(\+?\d[\d\s().-]{8,}\d)(?!\d)", RegexOptions.Compiled);
        private static readonly Regex Html = new(@"<\s*/?\s*(script|iframe|img|a|style|svg|object|embed)\b|javascript:|on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Todos los textos del JSON (nombre, descripción, incluye…) con el nombre de su campo.</summary>
        private static IEnumerable<(string Campo, string Texto)> Textos(JsonElement e, string campo = "")
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.String: yield return (campo, e.GetString() ?? ""); break;
                case JsonValueKind.Array: foreach (var x in e.EnumerateArray()) foreach (var r in Textos(x, campo)) yield return r; break;
                case JsonValueKind.Object: foreach (var p in e.EnumerateObject()) foreach (var r in Textos(p.Value, p.Name)) yield return r; break;
            }
        }

        /// <summary>Ids de hotel (id_Alojamiento) que usa la carga, en cualquier parte del JSON.</summary>
        private static IEnumerable<int> HotelesDe(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Array: foreach (var x in e.EnumerateArray()) foreach (var r in HotelesDe(x)) yield return r; break;
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject())
                    {
                        if (p.Name.Equals("id_Alojamiento", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var id) && id > 0) yield return id;
                        else foreach (var r in HotelesDe(p.Value)) yield return r;
                    }
                    break;
            }
        }

        /// <summary>Fechas «desde / fecha» del JSON (para avisar si ya pasaron).</summary>
        private static IEnumerable<DateTime> FechasDe(JsonElement e, string campo = "")
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.String:
                    var c = campo.ToLowerInvariant();
                    if ((c.Contains("desde") || c.StartsWith("fecha") || c.Contains("salida")) && DateTime.TryParse(e.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)) yield return d;
                    break;
                case JsonValueKind.Array: foreach (var x in e.EnumerateArray()) foreach (var r in FechasDe(x, campo)) yield return r; break;
                case JsonValueKind.Object: foreach (var p in e.EnumerateObject()) foreach (var r in FechasDe(p.Value, p.Name)) yield return r; break;
            }
        }

        /// <summary>Hoteles que la carga usa y que un proveedor pidió crear y Eurovip todavía NO aprobó (siguen inactivos).</summary>
        public async Task<List<string>> HotelesPendientesDeCargaAsync(CargaProveedor c)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(c.Datos) ? "[]" : c.Datos);
                var ids = HotelesDe(doc.RootElement).Distinct().ToList();
                if (ids.Count == 0) return new();
                using var cn = _db.Conexion();
                return (await cn.QueryAsync<string>("SELECT a.nombre FROM Eurovip.alojamiento a WHERE a.id_idioma = 'ES' AND a.id_alojamiento IN @ids AND a.activo = 0", new { ids })).ToList();
            }
            catch (Exception) { return new(); }
        }

        /// <summary>
        /// Control de calidad de una carga ANTES de aprobarla: las reglas de siempre (precio mínimo, repetidos, campos…) más lo que suele colarse
        /// cuando carga una empresa externa: hoteles sin aprobar, enlaces/teléfonos/correos en los textos, código HTML, precios muy distintos,
        /// fechas pasadas, proveedor con muchos rechazos y cargas que llevan días esperando. Devuelve un semáforo y la lista de puntos.
        /// </summary>
        public async Task<object?> RevisionAsync(ProveedoresCarga prov, int id)
        {
            var c = await prov.CargaAsync(id);
            if (c is null) return null;
            var puntos = new List<Punto>();
            var cuenta = await prov.CuentaAsync(c.Id_Cuenta);
            if (cuenta is not null && !cuenta.Activo) puntos.Add(new("rojo", "La cuenta del proveedor está desactivada", "No debería publicarse nada de una cuenta desactivada."));
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(c.Datos) ? "[]" : c.Datos); } catch (JsonException) { puntos.Add(new("rojo", "Los datos de la carga no se pueden leer", null)); }
            if (doc is not null)
            {
                var raiz = doc.RootElement;
                if (cuenta is not null && c.Estado is "PENDIENTE" or "PROGRAMADA" or "RECHAZADA" or "DEVUELTA" or "BORRADOR")
                {
                    try
                    {
                        var (prods, errores) = await prov.ValidarDatosAsync(cuenta, c.Modulo, c.Codigo, raiz);
                        foreach (var e in errores.Take(8)) puntos.Add(new("rojo", e.Indice > 0 ? $"Producto {e.Indice}{(e.Producto is null ? "" : $" ({e.Producto})")}" : "Carga", e.Mensaje));
                        if (errores.Count > 8) puntos.Add(new("rojo", $"…y {errores.Count - 8} problemas más", null));
                        if (prods.Count == 0) puntos.Add(new("rojo", "La carga no tiene productos", null));
                        if (prods.Count > 200) puntos.Add(new("ama", $"Carga muy grande: {prods.Count} productos", "Revísala con calma o pídele que la divida."));
                        var precios = prods.Where(p => p.Precio is > 0).Select(p => p.Precio!.Value).ToList();
                        if (precios.Count >= 2 && precios.Max() / precios.Min() >= 5)
                            puntos.Add(new("ama", "Precios muy distintos entre sí", $"El más barato es {precios.Min():N0} y el más caro {precios.Max():N0}: puede haber un precio mal escrito."));
                    }
                    catch (Exception) { puntos.Add(new("ama", "No se pudieron revisar las reglas automáticas", "Revisa los productos a mano.")); }
                }
                var pend = await HotelesPendientesDeCargaAsync(c);
                if (pend.Count > 0) puntos.Add(new("rojo", "Usa hoteles que pidió el proveedor y todavía no aprobaste", string.Join(", ", pend) + ". Apruébalos primero en la pestaña «Datos pedidos»."));
                var enlaces = 0; var tel = 0; var html = 0; string? ejemplo = null;
                foreach (var (campo, texto) in Textos(raiz))
                {
                    if (campo.Contains("codigo", StringComparison.OrdinalIgnoreCase) || campo.Contains("flyer", StringComparison.OrdinalIgnoreCase)) continue;
                    if (Html.IsMatch(texto)) { html++; ejemplo ??= texto; }
                    if (Enlace.IsMatch(texto)) { enlaces++; ejemplo ??= texto; }
                    if (Telefono.IsMatch(texto) && !Regex.IsMatch(texto, @"^\d{4}-\d{2}-\d{2}")) { tel++; ejemplo ??= texto; }
                }
                if (html > 0) puntos.Add(new("rojo", $"Hay código HTML o scripts en {html} texto(s)", "No se debe publicar. Pídele que lo quite."));
                if (enlaces > 0) puntos.Add(new("ama", $"Hay enlaces o correos en {enlaces} texto(s)", "Evita que el cliente se vaya con el proveedor. Ej.: «" + Corta(ejemplo, 80) + "»"));
                if (tel > 0) puntos.Add(new("ama", $"Hay teléfonos en {tel} texto(s)", "Los datos de contacto del proveedor no van en la web."));
                var hoy = DateTime.Today;
                var pasadas = FechasDe(raiz).Count(d => d.Date < hoy && d.Year >= 2000);
                if (pasadas > 0) puntos.Add(new("ama", $"Tiene {pasadas} fecha(s) que ya pasaron", "No se pueden vender fechas pasadas."));
            }
            if (c.Estado == "PENDIENTE" && c.Fecha_Solicitud is DateTime fs)
            {
                var h = (DateTime.Now - fs).TotalHours;
                if (h >= 48) puntos.Add(new("rojo", $"Lleva {Math.Floor(h / 24):0} días esperando revisión", null));
                else if (h >= 24) puntos.Add(new("ama", "Lleva más de 24 horas esperando revisión", null));
                if (string.IsNullOrWhiteSpace(c.Flyer)) puntos.Add(new("ama", "No tiene flyer", "Se genera al enviarla; pulsa «Generar flyer» si falta."));
            }
            if (!string.IsNullOrWhiteSpace(c.Error)) puntos.Add(new("rojo", "Falló al publicarse", c.Error));
            try
            {
                using var cn = _db.Conexion();
                var malas = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM (SELECT TOP 10 estado FROM Eurovip.carga_proveedor WHERE id_cuenta = @c AND id <> @id AND fecha_revision IS NOT NULL ORDER BY fecha_revision DESC) x WHERE estado IN ('RECHAZADA','DEVUELTA')", new { c = c.Id_Cuenta, id });
                if (malas >= 3) puntos.Add(new("ama", $"Este proveedor tiene {malas} de sus últimas cargas rechazadas o devueltas", "Revisa esta con más cuidado."));
            }
            catch (System.Data.SqlClient.SqlException) { }
            doc?.Dispose();
            var riesgo = puntos.Any(p => p.Nivel == "rojo") ? "rojo" : puntos.Any(p => p.Nivel == "ama") ? "ama" : "verde";
            return new { riesgo, puntos = puntos.OrderBy(p => p.Nivel == "rojo" ? 0 : 1) };
        }

        // ---------------------------------------------------------------- simulación: cómo se verá en la web
        private static JsonElement? P(JsonElement e, string nombre) { if (e.ValueKind != JsonValueKind.Object) return null; foreach (var p in e.EnumerateObject()) if (p.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase)) return p.Value; return null; }
        private static string? S(JsonElement e, string nombre) => P(e, nombre) is { ValueKind: JsonValueKind.String } v ? v.GetString() : P(e, nombre) is { ValueKind: JsonValueKind.Number } n ? n.ToString() : null;
        private static decimal? N(JsonElement e, string nombre) => P(e, nombre) is { ValueKind: JsonValueKind.Number } v && v.TryGetDecimal(out var d) ? d : null;
        private static IEnumerable<JsonElement> L(JsonElement e, string nombre) => P(e, nombre) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray() : Enumerable.Empty<JsonElement>();

        public sealed class Img { public string Uso { get; set; } = ""; public string Url { get; set; } = ""; public int? Ancho { get; set; } public int? Alto { get; set; } }

        /// <summary>
        /// Cada producto de la carga con lo que mostraría la web (tarjeta de la Home, pantalla intermedia y ficha): nombre, destino, estrellas,
        /// régimen, noches, precio desde, fechas, habitaciones, textos y las fotos que ya tiene el hotel o el destino en Eurovip.
        /// </summary>
        public async Task<object?> SimulacionAsync(ProveedoresCarga prov, int id)
        {
            var c = await prov.CargaAsync(id);
            if (c is null) return null;
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(c.Datos) ? "[]" : c.Datos);
            var salida = new List<object>();
            using var cn = _db.Conexion();
            foreach (var p in doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray() : Enumerable.Empty<JsonElement>())
            {
                string? nombre = null, destino = null, pais = null, regimen = null, estrellas = null, desc = null, incluye = null, noIncluye = null, detalle = null; int? noches = null; decimal? desde = null;
                var moneda = S(p, "divisa") ?? S(p, "id_Moneda") ?? c.Moneda ?? "USD";
                var fechas = new List<object>(); var habs = new List<object>(); var imgs = new List<Img>();
                if (P(p, "info") is { } info) { incluye = S(info, "incluye"); noIncluye = S(info, "no_Incluye"); }
                desc = S(p, "descripcion");
                switch (c.Modulo)
                {
                    case "OFERTAS": case "PAQUETES":
                        {
                            noches = (int?)N(p, "noches");
                            var h = L(p, "hoteles").FirstOrDefault();
                            if (h.ValueKind == JsonValueKind.Object)
                            {
                                var ida = (int?)N(h, "id_Alojamiento") ?? 0; regimen = S(h, "id_Regimen"); nombre = S(h, "nombre");
                                var a = ida > 0 ? await cn.QueryFirstOrDefaultAsync<(string Nombre, string? Destino, string? Estrellas, string? Pais, string? Desc)?>("SELECT nombre, id_destino, categoria, id_pais, descripcion FROM Eurovip.alojamiento WHERE id_alojamiento = @ida AND id_idioma = 'ES'", new { ida }) : null;
                                if (a is not null) { nombre = a.Value.Nombre; destino = a.Value.Destino; estrellas = a.Value.Estrellas; pais = a.Value.Pais; desc ??= a.Value.Desc; }
                                if (ida > 0) imgs.AddRange((await _mm.ListarAsync("ALOJAMIENTO", ida.ToString())).Select(m => new Img { Uso = m.Uso, Url = m.Url, Ancho = m.Ancho, Alto = m.Alto }));
                                foreach (var x in L(h, "habitaciones")) habs.Add(new { nombre = S(x, "nombre"), suplemento = N(x, "precio") });
                                foreach (var s in L(h, "salidas")) { var pr = N(s, "precioTotal"); fechas.Add(new { desde = S(s, "desde"), hasta = S(s, "hasta"), precio = pr }); if (pr is > 0 && (desde is null || pr < desde)) desde = pr; }
                            }
                            break;
                        }
                    case "EXCURSIONES": nombre = S(p, "nombre"); destino = S(p, "id_Destino"); desde = N(p, "precio"); detalle = "Excursión de un día"; break;
                    case "TRASLADOS":
                        nombre = S(p, "nombre"); destino = S(p, "id_Destino"); detalle = $"{S(p, "id_Estacion_Origen")} → {S(p, "id_Destino")} · {(S(p, "tipo") == "PRIVADO" ? "Privado" : "Compartido")} · {S(p, "vehiculo")}";
                        desde = new[] { N(p, "precio_Ida_Vuelta"), N(p, "precio_Ida") }.Where(x => x is > 0).Min(); break;
                    case "CIRCUITOS":
                        nombre = S(p, "_nombreCircuito") ?? (P(p, "nuevoCircuito") is { } nc ? S(nc, "nombre") : null) ?? S(p, "codigoCircuito"); noches = (int?)N(p, "noches"); regimen = S(p, "categoria");
                        foreach (var s in L(p, "salidas")) { var pr = N(s, "precioTotal"); fechas.Add(new { desde = S(s, "desde"), hasta = S(s, "hasta"), precio = pr }); if (pr is > 0 && (desde is null || pr < desde)) desde = pr; }
                        break;
                    case "CRUCEROS":
                        nombre = S(p, "titulo"); detalle = $"{S(p, "barco_Nombre")} · {S(p, "naviera_Nombre")} · salida {S(p, "puerto_Salida")}"; noches = (int?)N(p, "noches"); desc = S(p, "descripcion"); incluye = S(p, "incluye"); noIncluye = S(p, "no_Incluye");
                        foreach (var x in L(p, "precios")) { var pr = N(x, "precio_Pp"); if (pr is > 0 && (desde is null || pr < desde)) desde = pr; }
                        break;
                }
                if (!string.IsNullOrWhiteSpace(destino) && imgs.Count == 0)
                {
                    var portada = await _mm.PortadaAsync("DESTINO", destino!);
                    if (portada is not null) imgs.Add(new Img { Uso = "PORTADA", Url = portada });
                }
                salida.Add(new { nombre = nombre ?? "Sin nombre", destino, pais, regimen, estrellas, noches, detalle, descripcion = desc, incluye, noIncluye, desde, moneda, fechas = fechas.Take(12), habitaciones = habs, imagenes = imgs });
            }
            var logos = Logos();
            return new { carga = new { c.Id, c.Codigo, c.Titulo, c.Modulo, c.Estado, c.Cuenta, logo = logos.GetValueOrDefault(c.Id_Cuenta) }, productos = salida };
        }

        private static string Corta(string? s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n] + "…";

        // ---------------------------------------------------------------- bandeja de chats
        public sealed class Chat
        {
            public int Id { get; set; } public string Codigo { get; set; } = ""; public string? Titulo { get; set; } public string Modulo { get; set; } = ""; public string Estado { get; set; } = "";
            public string? Cuenta { get; set; } public int Id_Cuenta { get; set; } public string? Asignado_A { get; set; } public int Prioridad { get; set; } public string? Logo { get; set; } public string? Ultimo_Autor { get; set; } public string? Ultimo_Texto { get; set; } public DateTime? Ultima_Fecha { get; set; } public int Mensajes { get; set; }
        }

        /// <summary>Todas las conversaciones (cargas con mensajes), la última primero.</summary>
        public async Task<List<Chat>> ChatsAsync()
        {
            using var cn = _db.Conexion();
            try
            {
                await MirarAsync(cn);
                var gj = _hayGestion ? "LEFT JOIN Eurovip.carga_proveedor_gestion g ON g.id_carga = c.id" : "";
                var gc = _hayGestion ? "g.asignado_a AS Asignado_A, ISNULL(g.prioridad, 0) AS Prioridad," : "CAST(NULL AS nvarchar(150)) AS Asignado_A, 0 AS Prioridad,";
                var l = (await cn.QueryAsync<Chat>($@"
                    SELECT TOP 150 c.id, c.codigo, c.titulo, c.modulo, c.estado, pc.nombre AS Cuenta, pc.id AS Id_Cuenta, {gc} m.autor AS Ultimo_Autor, m.texto AS Ultimo_Texto, m.fecha AS Ultima_Fecha,
                           (SELECT COUNT(*) FROM Eurovip.carga_proveedor_comentario x WHERE x.id_carga = c.id) AS Mensajes
                    FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta {gj}
                    CROSS APPLY (SELECT TOP 1 autor, texto, fecha FROM Eurovip.carga_proveedor_comentario WHERE id_carga = c.id ORDER BY id DESC) m
                    ORDER BY m.fecha DESC")).ToList();
                var logos = Logos(); l.ForEach(x => x.Logo = logos.GetValueOrDefault(x.Id_Cuenta));
                return l;
            }
            catch (System.Data.SqlClient.SqlException) { return new(); }
        }

        // ---------------------------------------------------------------- ficha de los proveedores
        public sealed class FichaProveedor
        {
            public int Id { get; set; } public string Nombre { get; set; } = ""; public string? Email { get; set; } public string? Telefono { get; set; }
            public string? Id_Proveedor { get; set; } public string? Proveedor { get; set; } public string? Modulos { get; set; } public bool Activo { get; set; }
            public DateTime Fecha_Alta { get; set; } public int Cargas { get; set; } public int Borrador { get; set; } public int Pendiente { get; set; } public int Devuelta { get; set; }
            public int Rechazada { get; set; } public int Programada { get; set; } public int Activa { get; set; } public int Productos { get; set; } public int Mensajes { get; set; }
            public DateTime? Ultima_Actividad { get; set; } public DateTime? Ultima_Carga { get; set; } public string? Logo { get; set; }
        }

        public async Task<List<FichaProveedor>> FichasAsync()
        {
            using var cn = _db.Conexion();
            var lista = (await cn.QueryAsync<FichaProveedor>(@"
                SELECT pc.id, pc.nombre, pc.email, pc.telefono, pc.id_proveedor, LTRIM(RTRIM(p.nombre)) AS Proveedor, pc.modulos, pc.activo, pc.fecha_alta,
                    COUNT(c.id) AS Cargas,
                    SUM(CASE WHEN c.estado = 'BORRADOR' THEN 1 ELSE 0 END) AS Borrador, SUM(CASE WHEN c.estado = 'PENDIENTE' THEN 1 ELSE 0 END) AS Pendiente,
                    SUM(CASE WHEN c.estado = 'DEVUELTA' THEN 1 ELSE 0 END) AS Devuelta, SUM(CASE WHEN c.estado = 'RECHAZADA' THEN 1 ELSE 0 END) AS Rechazada,
                    SUM(CASE WHEN c.estado = 'PROGRAMADA' THEN 1 ELSE 0 END) AS Programada, SUM(CASE WHEN c.estado = 'ACTIVA' THEN 1 ELSE 0 END) AS Activa,
                    ISNULL(SUM(c.productos), 0) AS Productos, MAX(c.actualizado_en) AS Ultima_Actividad, MAX(c.creado_en) AS Ultima_Carga,
                    (SELECT COUNT(*) FROM Eurovip.carga_proveedor_comentario m JOIN Eurovip.carga_proveedor c2 ON c2.id = m.id_carga WHERE c2.id_cuenta = pc.id) AS Mensajes
                FROM Eurovip.proveedor_cuenta pc LEFT JOIN Eurovip.proveedor p ON p.id_proveedor = pc.id_proveedor LEFT JOIN Eurovip.carga_proveedor c ON c.id_cuenta = pc.id
                GROUP BY pc.id, pc.nombre, pc.email, pc.telefono, pc.id_proveedor, p.nombre, pc.modulos, pc.activo, pc.fecha_alta
                ORDER BY pc.activo DESC, MAX(c.actualizado_en) DESC, pc.nombre")).ToList();
            var logos = Logos(); lista.ForEach(x => x.Logo = logos.GetValueOrDefault(x.Id));
            return lista;
        }
    }
}
