using Dapper;
using EuroVip.Models;
using EuroVip.Repositories;
using System.Text.Json;

namespace EuroVip.Services
{
    /// <summary>
    /// CAMBIO 138: envíos programados de Flyer Agencia (WhatsApp). Pedido de la usuaria en /Agencia/Index: «un apartado
    /// para establecer los envíos: un día de la semana y una hora, o una fecha del calendario».
    ///   • SEMANAL: los días elegidos (0 = domingo … 6 = sábado) a la hora elegida, todas las semanas.
    ///   • FECHA: una sola vez, en la fecha y hora del calendario; después queda inactiva.
    /// Las horas son de República Dominicana (Eurovip:ZonaHoraria, por defecto «SA Western Standard Time», UTC-4).
    /// Cada minuto <see cref="AgenciaProgramadorWorker"/> busca los que tocan y lanza el MISMO envío que el botón «Enviar»
    /// (<see cref="AgenciaEnvioJob"/>). Si ya hay uno en curso, espera al minuto siguiente.
    /// </summary>
    public class EnvioProgramado
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string Tipo { get; set; } = "SEMANAL";
        public string? Dias { get; set; }
        public TimeSpan? Hora { get; set; }
        public DateTime? Fecha { get; set; }
        public string? Imagenes { get; set; }
        public string? Agencias { get; set; }
        public string? Prueba { get; set; }
        public string? Base_Editor { get; set; }
        public bool Activo { get; set; }
        public DateTime? Proxima { get; set; }
        public DateTime? Ultima_Ejecucion { get; set; }
        public int? Ultimo_Envio { get; set; }
        public string? Ultimo_Error { get; set; }
        public string? Usuario { get; set; }
        public DateTime Fecha_Alta { get; set; }
    }

    /// <summary>Lo que llega de la pantalla al guardar una programación.</summary>
    public class EnvioProgramadoDto
    {
        public int? Id { get; set; }
        public string? Nombre { get; set; }
        public string Tipo { get; set; } = "SEMANAL";
        public List<int> Dias { get; set; } = new();
        /// <summary>«HH:mm».</summary>
        public string? Hora { get; set; }
        /// <summary>«yyyy-MM-ddTHH:mm» (input datetime-local).</summary>
        public string? Fecha { get; set; }
        public List<string> Imagenes { get; set; } = new();
        public List<int> Agencias { get; set; } = new();
        public string? Prueba { get; set; }
        public bool Activo { get; set; } = true;
    }

    public class AgenciaProgramador
    {
        private readonly ICircuitoRepository _db;
        private readonly IConfiguration _cfg;
        public AgenciaProgramador(ICircuitoRepository db, IConfiguration cfg) { _db = db; _cfg = cfg; }

        public static readonly string[] NombreDias = { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };

        /// <summary>Hora actual en República Dominicana (o la zona configurada).</summary>
        public static DateTime Ahora(IConfiguration cfg)
        {
            var id = cfg["Eurovip:ZonaHoraria"] is { Length: > 0 } z ? z : "SA Western Standard Time";
            try { return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(id)); }
            catch
            {
                try { return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo")); }
                catch { return DateTime.UtcNow.AddHours(-4); }
            }
        }
        public DateTime Ahora() => Ahora(_cfg);

        public static List<int> LeerDias(string? dias) =>
            (dias ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => int.TryParse(d, out var n) ? n : -1).Where(n => n is >= 0 and <= 6).Distinct().OrderBy(n => n).ToList();

        /// <summary>La próxima vez que toca, estrictamente después de «desde». NULL si ya no toca más (fecha pasada).</summary>
        public static DateTime? Proxima(EnvioProgramado p, DateTime desde)
        {
            if (p.Tipo == "FECHA") return p.Fecha is DateTime f && f > desde ? f : null;
            var dias = LeerDias(p.Dias);
            if (dias.Count == 0 || p.Hora is not TimeSpan h) return null;
            for (var i = 0; i <= 7; i++)
            {
                var dia = desde.Date.AddDays(i);
                var cuando = dia + new TimeSpan(h.Hours, h.Minutes, 0);
                if (dias.Contains((int)dia.DayOfWeek) && cuando > desde) return cuando;
            }
            return null;
        }

        /// <summary>«Cada lunes y jueves a las 9:00 a. m.» / «El 24 de noviembre de 2026 a las 3:30 p. m.»</summary>
        public static string Describir(EnvioProgramado p)
        {
            static string Hora(TimeSpan h) => DateTime.Today.Add(h).ToString("h:mm tt", new System.Globalization.CultureInfo("es-DO"));
            if (p.Tipo == "FECHA")
                return p.Fecha is DateTime f ? $"El {f.ToString("dddd d 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-DO"))} a las {Hora(f.TimeOfDay)}" : "Sin fecha";
            var d = LeerDias(p.Dias).Select(x => NombreDias[x]).ToList();
            var dias = d.Count == 7 ? "Todos los días" : d.Count == 0 ? "Sin días" : "Cada " + (d.Count == 1 ? d[0] : string.Join(", ", d.Take(d.Count - 1)) + " y " + d[^1]);
            return p.Hora is TimeSpan h ? $"{dias} a las {Hora(h)}" : dias;
        }

        public async Task<List<EnvioProgramado>> ListarAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<EnvioProgramado>("SELECT * FROM Eurovip.flyer_envio_programado ORDER BY activo DESC, CASE WHEN proxima IS NULL THEN 1 ELSE 0 END, proxima, id DESC")).ToList();
        }

        public async Task<EnvioProgramado?> UnoAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<EnvioProgramado>("SELECT * FROM Eurovip.flyer_envio_programado WHERE id = @id", new { id });
        }

        /// <summary>Valida y guarda. Devuelve (id, null) o (null, mensaje para el usuario).</summary>
        public async Task<(int? Id, string? Error)> GuardarAsync(EnvioProgramadoDto d, string baseEditor, string usuario)
        {
            var p = new EnvioProgramado { Tipo = d.Tipo == "FECHA" ? "FECHA" : "SEMANAL", Nombre = (d.Nombre ?? "").Trim() is { Length: > 0 } n ? (n.Length > 120 ? n[..120] : n) : null, Activo = d.Activo };
            if (p.Tipo == "SEMANAL")
            {
                var dias = d.Dias.Where(x => x is >= 0 and <= 6).Distinct().OrderBy(x => x).ToList();
                if (dias.Count == 0) return (null, "Elige al menos un día de la semana.");
                if (!TimeSpan.TryParse(d.Hora, out var h) || h < TimeSpan.Zero || h >= TimeSpan.FromDays(1)) return (null, "Elige la hora del envío.");
                p.Dias = string.Join(",", dias); p.Hora = new TimeSpan(h.Hours, h.Minutes, 0);
            }
            else
            {
                if (!DateTime.TryParse(d.Fecha, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var f))
                    return (null, "Elige la fecha y la hora del envío en el calendario.");
                f = new DateTime(f.Year, f.Month, f.Day, f.Hour, f.Minute, 0);
                if (f <= Ahora()) return (null, "Esa fecha y hora ya pasó. Elige una en el futuro.");
                p.Fecha = f;
            }
            if (!string.IsNullOrWhiteSpace(d.Prueba))
            {
                var tel = AgenciaRepository.Telefono(d.Prueba);
                if (!AgenciaRepository.TelefonoValido(tel)) return (null, "Número de prueba inválido (con el código del país, ej. 18095551234).");
                p.Prueba = tel;
            }
            if (d.Imagenes.Count > 10) return (null, "Máximo 10 imágenes por envío.");
            p.Imagenes = d.Imagenes.Count > 0 ? JsonSerializer.Serialize(d.Imagenes) : null;
            p.Agencias = d.Agencias.Count > 0 ? JsonSerializer.Serialize(d.Agencias) : null;
            p.Proxima = p.Activo ? Proxima(p, Ahora()) : null;
            if (usuario.Length > 50) usuario = usuario[..50];
            if (baseEditor.Length > 200) baseEditor = baseEditor[..200];
            using var cn = _db.Conexion();
            if (d.Id is int id)
            {
                var filas = await cn.ExecuteAsync(@"UPDATE Eurovip.flyer_envio_programado SET nombre = @Nombre, tipo = @Tipo, dias = @Dias, hora = @Hora, fecha = @Fecha,
                        imagenes = @Imagenes, agencias = @Agencias, prueba = @Prueba, base_editor = @baseEditor, activo = @Activo, proxima = @Proxima,
                        usuario_modificacion = @usuario, fecha_modificacion = SYSDATETIME()
                    WHERE id = @id", new { p.Nombre, p.Tipo, p.Dias, p.Hora, p.Fecha, p.Imagenes, p.Agencias, p.Prueba, baseEditor, p.Activo, p.Proxima, usuario, id });
                return filas > 0 ? (id, null) : (null, "Esa programación ya no existe. Recarga la página.");
            }
            var nuevo = await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.flyer_envio_programado (nombre, tipo, dias, hora, fecha, imagenes, agencias, prueba, base_editor, activo, proxima, usuario)
                VALUES (@Nombre, @Tipo, @Dias, @Hora, @Fecha, @Imagenes, @Agencias, @Prueba, @baseEditor, @Activo, @Proxima, @usuario);
                SELECT CAST(SCOPE_IDENTITY() AS int);", new { p.Nombre, p.Tipo, p.Dias, p.Hora, p.Fecha, p.Imagenes, p.Agencias, p.Prueba, baseEditor, p.Activo, p.Proxima, usuario });
            return (nuevo, null);
        }

        public async Task<bool> ActivarAsync(int id, bool activo, string usuario)
        {
            var p = await UnoAsync(id);
            if (p is null) return false;
            p.Activo = activo;
            var proxima = activo ? Proxima(p, Ahora()) : null;
            using var cn = _db.Conexion();
            await cn.ExecuteAsync(@"UPDATE Eurovip.flyer_envio_programado SET activo = @activo, proxima = @proxima, usuario_modificacion = @usuario, fecha_modificacion = SYSDATETIME() WHERE id = @id",
                new { id, activo, proxima, usuario = usuario.Length > 50 ? usuario[..50] : usuario });
            return true;
        }

        public async Task<bool> BorrarAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("DELETE FROM Eurovip.flyer_envio_programado WHERE id = @id", new { id }) > 0;
        }

        /// <summary>Los activos cuya hora ya llegó.</summary>
        public async Task<List<EnvioProgramado>> PendientesAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<EnvioProgramado>("SELECT * FROM Eurovip.flyer_envio_programado WHERE activo = 1 AND proxima IS NOT NULL AND proxima <= @ahora ORDER BY proxima, id",
                new { ahora = Ahora() })).ToList();
        }

        /// <summary>Después de intentarlo: guarda el resultado y calcula la próxima vez (las de fecha única quedan inactivas).</summary>
        public async Task MarcarAsync(EnvioProgramado p, int? idEnvio, string? error)
        {
            var ahora = Ahora();
            var proxima = p.Tipo == "FECHA" ? null : Proxima(p, ahora);
            using var cn = _db.Conexion();
            await cn.ExecuteAsync(@"UPDATE Eurovip.flyer_envio_programado SET ultima_ejecucion = @ahora, ultimo_envio = ISNULL(@idEnvio, ultimo_envio), ultimo_error = @error,
                    proxima = @proxima, activo = CASE WHEN @proxima IS NULL THEN 0 ELSE activo END WHERE id = @Id",
                new { ahora, idEnvio, error = error is { Length: > 400 } e ? e[..400] : error, proxima, p.Id });
        }
    }

    /// <summary>Revisa cada minuto si toca algún envío programado y lo lanza con <see cref="AgenciaEnvioJob"/>.</summary>
    public class AgenciaProgramadorWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly AgenciaEnvioJob _job;
        private readonly AgenciaImagenes _arch;
        private readonly IConfiguration _cfg;
        private readonly ILogger<AgenciaProgramadorWorker> _log;
        private readonly IHostEnvironment _env;

        public AgenciaProgramadorWorker(IServiceScopeFactory scopes, AgenciaEnvioJob job, AgenciaImagenes arch, IConfiguration cfg, ILogger<AgenciaProgramadorWorker> log, IHostEnvironment env)
        { _scopes = scopes; _job = job; _arch = arch; _cfg = cfg; _log = log; _env = env; }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            // Eurovip:Flyer:EnviosProgramados = true/false lo enciende o apaga. Sin configurar: encendido en el servidor y APAGADO en
            // local (Development), para que una copia local que usa la misma base de datos no mande los mismos WhatsApp otra vez.
            var conf = _cfg["Eurovip:Flyer:EnviosProgramados"];
            var encendido = conf is { Length: > 0 } ? string.Equals(conf, "true", StringComparison.OrdinalIgnoreCase) : !_env.IsDevelopment();
            if (!encendido)
            {
                _log.LogInformation("Envíos programados de Agencia apagados en este servidor (Eurovip:Flyer:EnviosProgramados).");
                return;
            }
            try { await Task.Delay(TimeSpan.FromSeconds(20), ct); } catch (OperationCanceledException) { return; }
            while (!ct.IsCancellationRequested)
            {
                try { await RevisarAsync(); }
                catch (Exception ex) { _log.LogError(ex, "Envíos programados de Agencia: error al revisar"); }
                try { await Task.Delay(TimeSpan.FromSeconds(60), ct); } catch (OperationCanceledException) { return; }
            }
        }

        private async Task RevisarAsync()
        {
            using var scope = _scopes.CreateScope();
            var prog = scope.ServiceProvider.GetRequiredService<AgenciaProgramador>();
            var pendientes = await prog.PendientesAsync();
            if (pendientes.Count == 0) return;
            var wa = scope.ServiceProvider.GetRequiredService<WhatsAppService>();
            var flyers = scope.ServiceProvider.GetRequiredService<IFlyerRepository>();
            var repo = scope.ServiceProvider.GetRequiredService<IAgenciaRepository>();
            foreach (var p in pendientes)
            {
                if (_job.EnCurso is not null) return;   // hay un envío en curso: se intenta en el próximo minuto
                if (!wa.Configurado) { await prog.MarcarAsync(p, null, "WhatsApp no está configurado en este servidor."); continue; }
                try
                {
                    var elegidas = string.IsNullOrWhiteSpace(p.Imagenes) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(p.Imagenes) ?? new();
                    var web = (_cfg["Eurovip:Web"] is { Length: > 0 } w ? w : "http://eurovip.com.do").TrimEnd('/');
                    var (imgs, _) = await AgenciaEnvioPreparar.ImagenesAsync(flyers, _arch, web, elegidas);
                    if (imgs.Count == 0) { await prog.MarcarAsync(p, null, "No había imágenes de la galería para enviar."); continue; }
                    if (imgs.Count > 10) imgs = imgs.Take(10).ToList();
                    List<FlyerAgencia> agencias;
                    if (!string.IsNullOrWhiteSpace(p.Prueba))
                        agencias = new() { new FlyerAgencia { Id = 0, Nombre = "Prueba Eurovip", Telefono = p.Prueba, Activo = true } };
                    else
                    {
                        var ids = string.IsNullOrWhiteSpace(p.Agencias) ? new List<int>() : JsonSerializer.Deserialize<List<int>>(p.Agencias) ?? new();
                        agencias = (await repo.AgenciasAsync(true)).Where(a => ids.Count == 0 || ids.Contains(a.Id)).ToList();
                    }
                    if (agencias.Count == 0) { await prog.MarcarAsync(p, null, "No había agencias activas para enviar."); continue; }
                    var costo = decimal.TryParse(_cfg["Eurovip:Flyer:CostoMensajeUsd"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : 0.07m;
                    var usuario = $"programado #{p.Id}";
                    var id = await repo.CrearEnvioAsync(new FlyerEnvioMasivo
                    {
                        Modulo = "Agencia", Subtipo = string.IsNullOrWhiteSpace(p.Prueba) ? "Programado" : "Prueba", Plantilla = _job.Plantilla,
                        Total_Mensajes = imgs.Count * agencias.Count, Total_Contactos = agencias.Count, Total_Imagenes = imgs.Count, Costo_Unitario = costo,
                        Usuario = p.Usuario is { Length: > 0 } u ? u : usuario
                    });
                    var baseEditor = _cfg["Eurovip:Flyer:UrlEditor"] is { Length: > 0 } e ? e.TrimEnd('/') : (p.Base_Editor ?? "").TrimEnd('/');
                    if (!_job.Iniciar(id, imgs, agencias, baseEditor, usuario)) { await repo.ActualizarEnvioAsync(id, estado: "Cancelado", error: "Había otro envío en curso"); return; }
                    await prog.MarcarAsync(p, id, null);
                    _log.LogInformation("Envío programado #{Prog} lanzado como envío #{Envio}: {Imgs} imagen(es) a {Ag} agencia(s)", p.Id, id, imgs.Count, agencias.Count);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Envío programado #{Prog} falló", p.Id);
                    await prog.MarcarAsync(p, null, "No se pudo lanzar: " + ex.Message);
                }
            }
        }
    }

    /// <summary>Qué imágenes de la galería de Agencia se envían y a qué destino corresponde cada una (lo usan el botón «Enviar» y los programados).</summary>
    public static class AgenciaEnvioPreparar
    {
        public static async Task<(List<AgenciaImagenEnvio> Imagenes, List<string> SinDestino)> ImagenesAsync(IFlyerRepository flyers, AgenciaImagenes arch, string web, List<string> elegidas)
        {
            var configs = await flyers.ConfigsAsync(IFlyerRepository.Agencia);
            var archivos = arch.Listar().Where(i => elegidas.Count == 0 || elegidas.Contains(i.Archivo)).ToList();
            var r = new List<AgenciaImagenEnvio>(); var sin = new List<string>();
            foreach (var a in archivos)
            {
                var clave = AgenciaImagenes.ClaveDe(a.Archivo);
                var c = configs.FirstOrDefault(x => clave is not null && (AgenciaImagenes.Clave(x.Id_Destino) == clave || AgenciaImagenes.Clave(x.Nombre_Destino ?? "") == clave));
                var ruta = arch.Fisico(a.Archivo);
                if (c is null || ruta is null) { sin.Add(a.Archivo); continue; }
                var nombre = c.Nombre_Destino ?? c.Id_Destino;
                r.Add(new AgenciaImagenEnvio(ruta, a.Archivo, c.Id_Destino, nombre, $"{web.TrimEnd('/')}/hotel/{EuroVip.Controllers.AgenciaController.Slug(nombre)}"));
            }
            return (r, sin);
        }
    }
}
