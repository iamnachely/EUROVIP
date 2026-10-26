using Dapper;
using EuroVip.Models;

namespace EuroVip.Repositories
{
    public interface IAgenciaRepository
    {
        Task<List<FlyerAgencia>> AgenciasAsync(bool soloActivas = false);
        Task<int> GuardarAgenciaAsync(FlyerAgencia a);
        Task<bool> EliminarAgenciaAsync(int id);
        Task<int> EstadoAgenciasAsync(IEnumerable<int>? ids, bool activo);
        Task<(int Nuevas, int Repetidas, int Invalidas)> ImportarAsync(string texto);

        Task<List<FlyerAgenciaLink>> LinksAsync();
        Task<string> CrearLinkAsync(string idDestino, string? nombreDestino, int? idAgencia, string? nombreAgencia, string? email, int? dias,
                                    string? usuario, string? imagen = null, int? idEnvio = null);
        Task<bool> RevocarLinkAsync(int id);
        Task<bool> ExtenderLinkAsync(int id, int dias);
        Task<int> LinksSinVencimientoAsync(int? id);
        Task<FlyerAgenciaLink?> LinkVigenteAsync(string token, bool contarVisita);

        Task<int> CrearEnvioAsync(FlyerEnvioMasivo e);
        Task ActualizarEnvioAsync(int id, string? estado = null, int? sumarOk = null, int? sumarError = null, string? contacto = null, string? imagen = null,
                                  bool inicio = false, bool fin = false, string? error = null, int? totalMensajes = null);
        Task RegistrarDetalleAsync(int idEnvio, string? nombre, string? telefono, string? imagen, bool ok, string? error, string? token);
        Task<List<FlyerEnvioMasivo>> EnviosAsync(string modulo, int top = 50);
        Task<FlyerEnvioMasivo?> EnvioAsync(int id);
        Task<List<FlyerEnvioMasivoDetalle>> DetalleAsync(int id);
        /// <summary>Envíos que quedaron «EnProceso» sin estar corriendo (se reinició la app): pasan a «Interrumpido».</summary>
        Task MarcarInterrumpidosAsync(string modulo, int? enCurso);
    }

    /// <summary>
    /// Flyer Agencia (como /Agencia de BackTiTan): agencias, links únicos del editor y registro de envíos masivos.
    /// Los precios y destinos de los flyers de agencia están en flyer_config / flyer_datos con tipo = 'Agencia' (FlyerRepository).
    /// </summary>
    public class AgenciaRepository : IAgenciaRepository
    {
        private readonly ICircuitoRepository _db;
        public AgenciaRepository(ICircuitoRepository db) => _db = db;

        /// <summary>Solo números; los de 10 cifras que empiezan por 809/829/849 (RD) llevan el 1 delante.</summary>
        public static string Telefono(string? t)
        {
            var n = new string((t ?? "").Where(char.IsDigit).ToArray()).TrimStart('0');
            if (n.Length == 10 && (n.StartsWith("809") || n.StartsWith("829") || n.StartsWith("849"))) n = "1" + n;
            return n;
        }
        public static bool TelefonoValido(string t) => t.Length is >= 8 and <= 15;

        // ---------------- agencias ----------------
        public async Task<List<FlyerAgencia>> AgenciasAsync(bool soloActivas = false)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<FlyerAgencia>("SELECT * FROM Eurovip.flyer_agencia WHERE @solo = 0 OR activo = 1 ORDER BY activo DESC, nombre",
                new { solo = soloActivas })).ToList();
        }

        public async Task<int> GuardarAgenciaAsync(FlyerAgencia a)
        {
            var tel = Telefono(a.Telefono);
            if (!TelefonoValido(tel)) throw new InvalidOperationException("El WhatsApp tiene que tener entre 8 y 15 números, con el código del país (ej. 18095551234).");
            using var cn = _db.Conexion();
            var otra = await cn.ExecuteScalarAsync<string?>("SELECT nombre FROM Eurovip.flyer_agencia WHERE telefono = @tel AND id <> @Id", new { tel, a.Id });
            if (otra is not null) throw new InvalidOperationException($"Ese WhatsApp ya es de la agencia «{otra}».");
            var p = new { a.Id, Nombre = a.Nombre.Trim(), tel, Email = string.IsNullOrWhiteSpace(a.Email) ? null : a.Email.Trim(), a.Activo };
            if (a.Id > 0)
            {
                await cn.ExecuteAsync("UPDATE Eurovip.flyer_agencia SET nombre = @Nombre, telefono = @tel, email = @Email, activo = @Activo WHERE id = @Id", p);
                return a.Id;
            }
            return await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.flyer_agencia (nombre, telefono, email, activo, origen) VALUES (@Nombre, @tel, @Email, @Activo, 'manual');
                                                     SELECT CAST(SCOPE_IDENTITY() AS int);", p);
        }

        public async Task<bool> EliminarAgenciaAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("DELETE FROM Eurovip.flyer_agencia WHERE id = @id", new { id }) > 0;
        }

        /// <summary>Activa o pausa las agencias elegidas (ids vacío/null = TODAS).</summary>
        public async Task<int> EstadoAgenciasAsync(IEnumerable<int>? ids, bool activo)
        {
            using var cn = _db.Conexion();
            var lista = ids?.ToList() ?? new();
            return lista.Count == 0
                ? await cn.ExecuteAsync("UPDATE Eurovip.flyer_agencia SET activo = @activo WHERE activo <> @activo", new { activo })
                : await cn.ExecuteAsync("UPDATE Eurovip.flyer_agencia SET activo = @activo WHERE id IN @lista", new { activo, lista });
        }

        /// <summary>
        /// Pegar una lista: una agencia por línea, «Nombre de la agencia 18095551234» (el número al final; también vale con
        /// tabulador, coma o punto y coma). Los números que ya están se saltan.
        /// </summary>
        public async Task<(int Nuevas, int Repetidas, int Invalidas)> ImportarAsync(string texto)
        {
            int nuevas = 0, repetidas = 0, invalidas = 0;
            using var cn = _db.Conexion();
            var existentes = (await cn.QueryAsync<string>("SELECT telefono FROM Eurovip.flyer_agencia")).ToHashSet();
            foreach (var linea in (texto ?? "").Split('\n').Select(l => l.Trim()).Where(l => l != ""))
            {
                var m = System.Text.RegularExpressions.Regex.Match(linea, @"^(?<n>.*?)[\s,;\t]*(?<t>\+?[\d\s\-\(\)]{8,})\s*$");
                var tel = m.Success ? Telefono(m.Groups["t"].Value) : "";
                var nombre = m.Success ? m.Groups["n"].Value.Trim(' ', ',', ';', '\t', '-') : "";
                if (!TelefonoValido(tel)) { invalidas++; continue; }
                if (!existentes.Add(tel)) { repetidas++; continue; }
                await cn.ExecuteAsync("INSERT INTO Eurovip.flyer_agencia (nombre, telefono, activo, origen) VALUES (@n, @tel, 1, 'pegado')",
                    new { n = nombre == "" ? "Agencia " + tel : (nombre.Length > 200 ? nombre[..200] : nombre), tel });
                nuevas++;
            }
            return (nuevas, repetidas, invalidas);
        }

        // ---------------- links únicos ----------------
        public async Task<List<FlyerAgenciaLink>> LinksAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<FlyerAgenciaLink>("SELECT TOP 1000 * FROM Eurovip.flyer_agencia_link ORDER BY id DESC")).ToList();
        }

        public async Task<string> CrearLinkAsync(string idDestino, string? nombreDestino, int? idAgencia, string? nombreAgencia, string? email, int? dias,
                                                 string? usuario, string? imagen = null, int? idEnvio = null)
        {
            using var cn = _db.Conexion();
            var token = Guid.NewGuid().ToString("N");   // 32 caracteres, imposible de adivinar
            await cn.ExecuteAsync(@"INSERT INTO Eurovip.flyer_agencia_link (token, id_destino, nombre_destino, id_agencia, nombre_agencia, email_agencia, nombre_imagen,
                                        id_envio, fecha_expiracion, creado_por)
                                    VALUES (@token, @idDestino, @nombreDestino, @idAgencia, @nombreAgencia, @email, @imagen, @idEnvio,
                                            CASE WHEN @dias IS NULL THEN NULL ELSE DATEADD(DAY, @dias, GETDATE()) END, @usuario)",
                new { token, idDestino, nombreDestino, idAgencia, nombreAgencia, email, imagen, idEnvio, dias, usuario });
            return token;
        }

        public async Task<bool> RevocarLinkAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("UPDATE Eurovip.flyer_agencia_link SET activo = 0 WHERE id = @id", new { id }) > 0;
        }

        /// <summary>Suma días (desde hoy si ya venció) y lo reactiva.</summary>
        public async Task<bool> ExtenderLinkAsync(int id, int dias)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync(@"UPDATE Eurovip.flyer_agencia_link SET activo = 1,
                fecha_expiracion = DATEADD(DAY, @dias, CASE WHEN fecha_expiracion IS NULL OR fecha_expiracion < GETDATE() THEN GETDATE() ELSE fecha_expiracion END)
                WHERE id = @id", new { id, dias }) > 0;
        }

        /// <summary>Quita la fecha de vencimiento (un link o, con id null, todos los activos).</summary>
        public async Task<int> LinksSinVencimientoAsync(int? id)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("UPDATE Eurovip.flyer_agencia_link SET fecha_expiracion = NULL, activo = 1 WHERE (@id IS NULL AND activo = 1) OR id = @id", new { id });
        }

        public async Task<FlyerAgenciaLink?> LinkVigenteAsync(string token, bool contarVisita)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 32 || !token.All(char.IsLetterOrDigit)) return null;
            using var cn = _db.Conexion();
            var l = await cn.QueryFirstOrDefaultAsync<FlyerAgenciaLink>(@"SELECT * FROM Eurovip.flyer_agencia_link
                WHERE token = @token AND activo = 1 AND (fecha_expiracion IS NULL OR fecha_expiracion > GETDATE())", new { token });
            if (l is not null && contarVisita)
                await cn.ExecuteAsync("UPDATE Eurovip.flyer_agencia_link SET visitas = visitas + 1, ultima_visita = GETDATE() WHERE id = @Id", new { l.Id });
            return l;
        }

        // ---------------- envíos masivos ----------------
        public async Task<int> CrearEnvioAsync(FlyerEnvioMasivo e)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.flyer_envio_masivo (modulo, subtipo, estado, plantilla, total_mensajes, total_contactos, total_imagenes, costo_unitario, usuario)
                VALUES (@Modulo, @Subtipo, 'Pendiente', @Plantilla, @Total_Mensajes, @Total_Contactos, @Total_Imagenes, @Costo_Unitario, @Usuario);
                SELECT CAST(SCOPE_IDENTITY() AS int);", e);
        }

        public async Task ActualizarEnvioAsync(int id, string? estado = null, int? sumarOk = null, int? sumarError = null, string? contacto = null, string? imagen = null,
                                               bool inicio = false, bool fin = false, string? error = null, int? totalMensajes = null)
        {
            using var cn = _db.Conexion();
            await cn.ExecuteAsync(@"UPDATE Eurovip.flyer_envio_masivo SET
                    estado = ISNULL(@estado, estado),
                    enviados_ok = enviados_ok + ISNULL(@sumarOk, 0),
                    enviados_error = CASE WHEN enviados_error + ISNULL(@sumarError, 0) < 0 THEN 0 ELSE enviados_error + ISNULL(@sumarError, 0) END,
                    contacto_actual = CASE WHEN @fin = 1 THEN NULL ELSE ISNULL(@contacto, contacto_actual) END,
                    imagen_actual = CASE WHEN @fin = 1 THEN NULL ELSE ISNULL(@imagen, imagen_actual) END,
                    fecha_inicio = CASE WHEN @inicio = 1 THEN GETDATE() ELSE fecha_inicio END,
                    fecha_fin = CASE WHEN @fin = 1 THEN GETDATE() ELSE fecha_fin END,
                    total_mensajes = ISNULL(@totalMensajes, total_mensajes),
                    mensaje_error = CASE WHEN @error IS NULL THEN mensaje_error ELSE LEFT(ISNULL(mensaje_error + CHAR(10), '') + @error, 2000) END
                WHERE id = @id", new { id, estado, sumarOk, sumarError, contacto, imagen, inicio, fin, error, totalMensajes });
        }

        public async Task RegistrarDetalleAsync(int idEnvio, string? nombre, string? telefono, string? imagen, bool ok, string? error, string? token)
        {
            using var cn = _db.Conexion();
            await cn.ExecuteAsync(@"INSERT INTO Eurovip.flyer_envio_masivo_detalle (id_envio, nombre_contacto, telefono, nombre_imagen, estado, mensaje_error, token)
                VALUES (@idEnvio, @nombre, @telefono, @imagen, @estado, @error, @token)",
                new { idEnvio, nombre, telefono, imagen, estado = ok ? "OK" : "Error", error = error is { Length: > 2000 } ? error[..2000] : error, token });
        }

        public async Task<List<FlyerEnvioMasivo>> EnviosAsync(string modulo, int top = 50)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<FlyerEnvioMasivo>("SELECT TOP (@top) * FROM Eurovip.flyer_envio_masivo WHERE modulo = @modulo ORDER BY id DESC", new { modulo, top })).ToList();
        }

        public async Task<FlyerEnvioMasivo?> EnvioAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<FlyerEnvioMasivo>("SELECT * FROM Eurovip.flyer_envio_masivo WHERE id = @id", new { id });
        }

        public async Task<List<FlyerEnvioMasivoDetalle>> DetalleAsync(int id)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<FlyerEnvioMasivoDetalle>("SELECT * FROM Eurovip.flyer_envio_masivo_detalle WHERE id_envio = @id ORDER BY id", new { id })).ToList();
        }

        public async Task MarcarInterrumpidosAsync(string modulo, int? enCurso)
        {
            using var cn = _db.Conexion();
            await cn.ExecuteAsync(@"UPDATE Eurovip.flyer_envio_masivo SET estado = 'Interrumpido', fecha_fin = ISNULL(fecha_fin, GETDATE()), contacto_actual = NULL, imagen_actual = NULL,
                    mensaje_error = LEFT(ISNULL(mensaje_error + CHAR(10), '') + N'Se detuvo porque la aplicación se reinició. Los contactos de abajo sí se enviaron.', 2000)
                WHERE modulo = @modulo AND estado IN ('Pendiente', 'EnProceso') AND (@enCurso IS NULL OR id <> @enCurso)
                  AND fecha_creacion < DATEADD(MINUTE, -1, GETDATE())", new { modulo, enCurso });
        }
    }
}
