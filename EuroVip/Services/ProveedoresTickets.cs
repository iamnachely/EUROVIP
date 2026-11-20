using System.Text.Json;
using Dapper;
using EuroVip.Helpers;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Http;

namespace EuroVip.Services
{
    /// <summary>
    /// Sistema de TICKETS de los proveedores (20/11/2026): el proveedor reporta un error o un fallo con documentos y capturas desde Gestión
    /// y el equipo de Eurovip lo atiende en BackOffice › Cargas de proveedores › Tickets. Tablas: Database/proveedor-tickets.sql.
    /// Los archivos se guardan en App_Data/tickets/{id}/ (fuera de wwwroot: solo se bajan con sesión y permiso).
    /// </summary>
    public class ProveedoresTickets
    {
        private readonly ICircuitoRepository _db;
        private readonly IWebHostEnvironment _env;
        public ProveedoresTickets(ICircuitoRepository db, IWebHostEnvironment env) { _db = db; _env = env; }

        private static readonly JsonSerializerOptions Opt = new(JsonSerializerDefaults.Web);
        public static readonly string[] Categorias = { "error", "fallo", "carga", "acceso", "duda", "otro" };
        public static readonly string[] Estados = { "ABIERTO", "EN_PROCESO", "ESPERANDO", "RESUELTO", "CERRADO" };
        private const int MaxArchivos = 5; private const long MaxBytes = 10_000_000;
        private static readonly string[] Extensiones = { ".png", ".jpg", ".jpeg", ".webp", ".pdf", ".xlsx", ".xls", ".docx", ".doc", ".csv", ".txt" };

        private static bool? _hay; private static DateTime _mirado = DateTime.MinValue;
        public async Task<bool> HayAsync()
        {
            if (_hay == true) return true;
            if (DateTime.UtcNow - _mirado < TimeSpan.FromMinutes(2)) return false;
            _mirado = DateTime.UtcNow;
            using var cn = _db.Conexion();
            _hay = await SqlMapper.ExecuteScalarAsync<int?>(cn, "SELECT CASE WHEN OBJECT_ID('Eurovip.proveedor_ticket','U') IS NOT NULL AND OBJECT_ID('Eurovip.proveedor_ticket_mensaje','U') IS NOT NULL THEN 1 END") is not null;
            return _hay == true;
        }
        private const string NoHay = "Los tickets todavía no están activados en este servidor (falta aplicar Database/proveedor-tickets.sql).";

        // ---------------------------------------------------------------- modelos
        public sealed class TicketFila
        {
            public int Id_Ticket { get; set; } public int Id_Cuenta { get; set; } public string? Cuenta { get; set; } public string Categoria { get; set; } = ""; public string Asunto { get; set; } = "";
            public string Descripcion { get; set; } = ""; public int Prioridad { get; set; } public string Estado { get; set; } = ""; public int? Id_Carga { get; set; } public string? Codigo_Carga { get; set; }
            public string? Asignado_A { get; set; } public int? Calificacion { get; set; } public string? Calificacion_Texto { get; set; }
            public DateTime Creado_En { get; set; } public DateTime Actualizado_En { get; set; } public DateTime? Primera_Respuesta_En { get; set; } public DateTime? Resuelto_En { get; set; }
            public int Mensajes { get; set; } public string? Ultimo_Tipo { get; set; } public string Codigo => $"TCK-{Id_Ticket:D5}";
        }
        public sealed class MensajeFila
        {
            public int Id { get; set; } public int Id_Ticket { get; set; } public string Tipo { get; set; } = ""; public string Autor { get; set; } = ""; public string? Texto { get; set; }
            public string? Adjuntos { get; set; } public DateTime Creado_En { get; set; }
        }
        public sealed record Adjunto(string Nombre, string Archivo, long Bytes, string Tipo);

        private const string Sel = @"SELECT t.id_ticket, t.id_cuenta, pc.nombre AS Cuenta, t.categoria, t.asunto, t.descripcion, t.prioridad, t.estado, t.id_carga, c.codigo AS Codigo_Carga, t.asignado_a,
                t.calificacion, t.calificacion_texto, t.creado_en, t.actualizado_en, t.primera_respuesta_en, t.resuelto_en,
                (SELECT COUNT(*) FROM Eurovip.proveedor_ticket_mensaje m WHERE m.id_ticket = t.id_ticket AND m.tipo <> 'NOTA') AS Mensajes,
                (SELECT TOP 1 m.tipo FROM Eurovip.proveedor_ticket_mensaje m WHERE m.id_ticket = t.id_ticket AND m.tipo IN ('PROVEEDOR','EUROVIP') ORDER BY m.id DESC) AS Ultimo_Tipo
            FROM Eurovip.proveedor_ticket t JOIN Eurovip.proveedor_cuenta pc ON pc.id = t.id_cuenta LEFT JOIN Eurovip.carga_proveedor c ON c.id = t.id_carga";

        // ---------------------------------------------------------------- archivos
        private string Dir(int id) => Path.Combine(_env.ContentRootPath, "App_Data", "tickets", id.ToString());

        private static async Task<bool> FirmaValidaAsync(IFormFile f, string ext)
        {
            var cab = new byte[8]; await using var s = f.OpenReadStream(); var n = await s.ReadAsync(cab);
            switch (ext)
            {
                case ".png": return n >= 4 && cab[0] == 0x89 && cab[1] == 0x50;
                case ".jpg": case ".jpeg": return n >= 3 && cab[0] == 0xFF && cab[1] == 0xD8;
                case ".webp": return n >= 4 && cab[0] == 0x52 && cab[1] == 0x49;
                case ".pdf": return n >= 4 && cab[0] == 0x25 && cab[1] == 0x50 && cab[2] == 0x44 && cab[3] == 0x46;
                case ".xlsx": case ".docx": return n >= 4 && cab[0] == 0x50 && cab[1] == 0x4B;
                case ".xls": case ".doc": return n >= 4 && cab[0] == 0xD0 && cab[1] == 0xCF && cab[2] == 0x11 && cab[3] == 0xE0;
                default: { var buf = new byte[1024]; s.Position = 0; var m = await s.ReadAsync(buf); return !buf.Take(m).Contains((byte)0); }   // csv / txt: texto, sin bytes nulos
            }
        }

        /// <summary>Valida y guarda los archivos; devuelve la lista para el JSON del mensaje (o el error).</summary>
        private async Task<(List<Adjunto>? Lista, string? Error)> GuardarArchivosAsync(int idTicket, IList<IFormFile>? archivos)
        {
            var lista = new List<Adjunto>();
            var validos = (archivos ?? new List<IFormFile>()).Where(f => f.Length > 0).ToList();
            if (validos.Count == 0) return (lista, null);
            if (validos.Count > MaxArchivos) return (null, $"Puedes adjuntar hasta {MaxArchivos} archivos por mensaje.");
            foreach (var f in validos)
            {
                var ext = Path.GetExtension(f.FileName).ToLowerInvariant();
                if (!Extensiones.Contains(ext)) return (null, $"«{f.FileName}» no se puede adjuntar. Se aceptan imágenes, PDF, Excel, Word, CSV y TXT.");
                if (f.Length > MaxBytes) return (null, $"«{f.FileName}» pesa más de 10 MB.");
                if (!await FirmaValidaAsync(f, ext)) return (null, $"«{f.FileName}» no parece un archivo {ext.TrimStart('.').ToUpperInvariant()} válido.");
            }
            Directory.CreateDirectory(Dir(idTicket));
            foreach (var f in validos)
            {
                var ext = Path.GetExtension(f.FileName).ToLowerInvariant(); var guardado = Guid.NewGuid().ToString("N") + ext;
                await using (var fs = File.Create(Path.Combine(Dir(idTicket), guardado))) await f.CopyToAsync(fs);
                var nombre = Path.GetFileName(f.FileName); if (nombre.Length > 120) nombre = nombre[^120..];
                lista.Add(new Adjunto(nombre, guardado, f.Length, ext.TrimStart('.')));
            }
            return (lista, null);
        }

        /// <summary>Archivo de un ticket: solo si pertenece a un mensaje de ese ticket (y, para el proveedor, a su cuenta y no a una nota).</summary>
        public async Task<(string? Ruta, string? Nombre, string? Tipo)> ArchivoAsync(int idTicket, string archivo, int? cuenta)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(archivo ?? "", @"^[0-9a-f]{32}\.[a-z]{3,4}$")) return default;
            using var cn = _db.Conexion(); if (!await HayAsync()) return default;
            var dueno = await cn.ExecuteScalarAsync<int?>("SELECT id_cuenta FROM Eurovip.proveedor_ticket WHERE id_ticket = @idTicket", new { idTicket });
            if (dueno is null || (cuenta is int c && c != dueno)) return default;
            var nombre = await cn.ExecuteScalarAsync<string?>(@"SELECT TOP 1 JSON_VALUE(a.value, '$.nombre') FROM Eurovip.proveedor_ticket_mensaje m CROSS APPLY OPENJSON(m.adjuntos) a
                WHERE m.id_ticket = @idTicket AND (@cuenta IS NULL OR m.tipo <> 'NOTA') AND JSON_VALUE(a.value, '$.archivo') = @archivo", new { idTicket, archivo, cuenta });
            if (nombre is null) return default;
            var ruta = Path.Combine(Dir(idTicket), archivo); if (!File.Exists(ruta)) return default;
            var ext = Path.GetExtension(archivo);
            var tipo = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".pdf" => "application/pdf", ".csv" => "text/csv", ".txt" => "text/plain",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".xls" => "application/vnd.ms-excel", ".doc" => "application/msword", _ => "application/octet-stream" };
            return (ruta, nombre, tipo);
        }

        // ---------------------------------------------------------------- crear y responder
        public async Task<(object? Resultado, string? Error)> CrearAsync(ProveedorCuenta cuenta, string? categoria, string? asunto, string? descripcion, int prioridad, int? idCarga, IList<IFormFile>? archivos)
        {
            if (!await HayAsync()) return (null, NoHay);
            categoria = (categoria ?? "otro").Trim().ToLowerInvariant(); if (!Categorias.Contains(categoria)) categoria = "otro";
            asunto = (asunto ?? "").Trim(); descripcion = (descripcion ?? "").Trim();
            if (asunto.Length < 4) return (null, "Escribe un título corto del problema (al menos 4 letras).");
            if (descripcion.Length < 15) return (null, "Cuéntanos qué pasó con un poco más de detalle (al menos 15 letras).");
            if (asunto.Length > 150) asunto = asunto[..150]; if (descripcion.Length > 6000) descripcion = descripcion[..6000];
            prioridad = Math.Clamp(prioridad, 0, 3);
            using var cn = _db.Conexion();
            if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.proveedor_ticket WHERE id_cuenta = @c AND creado_en > DATEADD(MINUTE, -2, SYSDATETIME())", new { c = cuenta.Id }) >= 5)
                return (null, "Enviaste varios tickets seguidos. Espera un par de minutos y vuelve a intentarlo.");
            if (idCarga is int ic && await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.carga_proveedor WHERE id = @ic AND id_cuenta = @c", new { ic, c = cuenta.Id }) == 0) idCarga = null;   // solo una carga suya
            var id = await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.proveedor_ticket (id_cuenta, categoria, asunto, descripcion, prioridad, id_carga) OUTPUT INSERTED.id_ticket VALUES (@c, @cat, @a, @d, @p, @ic)",
                new { c = cuenta.Id, cat = categoria, a = asunto, d = descripcion, p = prioridad, ic = idCarga });
            var (lista, error) = await GuardarArchivosAsync(id, archivos);
            if (lista is null) { await cn.ExecuteAsync("DELETE FROM Eurovip.proveedor_ticket WHERE id_ticket = @id", new { id }); return (null, error); }
            await cn.ExecuteAsync("INSERT INTO Eurovip.proveedor_ticket_mensaje (id_ticket, tipo, autor, texto, adjuntos) VALUES (@id, 'PROVEEDOR', @a, @t, @j)",
                new { id, a = cuenta.Nombre, t = descripcion, j = lista.Count > 0 ? JsonSerializer.Serialize(lista, Opt) : null });
            return (new { ok = true, id, codigo = $"TCK-{id:D5}", mensaje = $"Ticket TCK-{id:D5} creado. Eurovip lo va a revisar y te responde aquí." }, null);
        }

        /// <summary>Mensaje en un ticket. tipo: PROVEEDOR (cuenta ≠ null: solo en lo suyo), EUROVIP o NOTA (nota interna).</summary>
        public async Task<string?> MensajeAsync(int id, string tipo, string autor, string? texto, IList<IFormFile>? archivos, int? cuenta)
        {
            if (!await HayAsync()) return NoHay;
            texto = (texto ?? "").Trim(); if (texto.Length > 6000) texto = texto[..6000];
            using var cn = _db.Conexion();
            var t = await cn.QueryFirstOrDefaultAsync<(int Cuenta, string Estado)?>("SELECT id_cuenta, estado FROM Eurovip.proveedor_ticket WHERE id_ticket = @id", new { id });
            if (t is null || (cuenta is int c && c != t.Value.Cuenta)) return "Ese ticket no existe.";
            if (texto.Length == 0 && (archivos is null || archivos.Count == 0)) return "Escribe un mensaje o adjunta un archivo.";
            var (lista, error) = await GuardarArchivosAsync(id, archivos); if (lista is null) return error;
            await cn.ExecuteAsync("INSERT INTO Eurovip.proveedor_ticket_mensaje (id_ticket, tipo, autor, texto, adjuntos) VALUES (@id, @tipo, @autor, @texto, @j)",
                new { id, tipo, autor = autor.Length > 150 ? autor[..150] : autor, texto = texto.Length > 0 ? texto : null, j = lista.Count > 0 ? JsonSerializer.Serialize(lista, Opt) : null });
            if (tipo == "PROVEEDOR") await cn.ExecuteAsync("UPDATE Eurovip.proveedor_ticket SET actualizado_en = SYSDATETIME(), estado = CASE WHEN estado IN ('ESPERANDO','RESUELTO','CERRADO') THEN 'ABIERTO' ELSE estado END, resuelto_en = CASE WHEN estado IN ('RESUELTO','CERRADO') THEN NULL ELSE resuelto_en END WHERE id_ticket = @id", new { id });
            else if (tipo == "EUROVIP") await cn.ExecuteAsync("UPDATE Eurovip.proveedor_ticket SET actualizado_en = SYSDATETIME(), primera_respuesta_en = ISNULL(primera_respuesta_en, SYSDATETIME()), estado = CASE WHEN estado = 'ABIERTO' THEN 'EN_PROCESO' ELSE estado END WHERE id_ticket = @id", new { id });
            else await cn.ExecuteAsync("UPDATE Eurovip.proveedor_ticket SET actualizado_en = SYSDATETIME() WHERE id_ticket = @id", new { id });
            return null;
        }

        /// <summary>Eurovip cambia el estado, la prioridad o quién lo lleva; queda anotado en la conversación.</summary>
        public async Task<string?> GestionarAsync(int id, string? estado, int? prioridad, string? asignado, bool cambiaAsignado, string usuario)
        {
            if (!await HayAsync()) return NoHay;
            using var cn = _db.Conexion();
            var t = await cn.QueryFirstOrDefaultAsync<(string Estado, int Prioridad, string? Asignado)?>("SELECT estado, prioridad, asignado_a FROM Eurovip.proveedor_ticket WHERE id_ticket = @id", new { id });
            if (t is null) return "Ese ticket no existe.";
            var notas = new List<string>();
            if (estado is not null && estado != t.Value.Estado) { if (!Estados.Contains(estado)) return "Estado no válido."; notas.Add($"Cambió el estado a «{NombreEstado(estado)}»"); }
            if (prioridad is int p && p != t.Value.Prioridad) { if (p is < 0 or > 3) return "Prioridad no válida."; notas.Add($"Cambió la prioridad a «{new[] { "Baja", "Normal", "Alta", "Urgente" }[p]}»"); }
            if (cambiaAsignado && (asignado ?? "") != (t.Value.Asignado ?? "")) notas.Add(string.IsNullOrWhiteSpace(asignado) ? "Quitó la asignación" : asignado == usuario ? "Se encargó del ticket" : $"Encargó el ticket a {asignado}");
            if (notas.Count == 0) return null;
            await cn.ExecuteAsync(@"UPDATE Eurovip.proveedor_ticket SET estado = ISNULL(@estado, estado), prioridad = ISNULL(@prioridad, prioridad), asignado_a = CASE WHEN @cambia = 1 THEN NULLIF(@asignado, '') ELSE asignado_a END,
                actualizado_en = SYSDATETIME(), resuelto_en = CASE WHEN @estado IN ('RESUELTO','CERRADO') THEN ISNULL(resuelto_en, SYSDATETIME()) WHEN @estado IS NOT NULL THEN NULL ELSE resuelto_en END WHERE id_ticket = @id",
                new { id, estado, prioridad, asignado, cambia = cambiaAsignado ? 1 : 0 });
            await cn.ExecuteAsync("INSERT INTO Eurovip.proveedor_ticket_mensaje (id_ticket, tipo, autor, texto) VALUES (@id, 'SISTEMA', @u, @t)", new { id, u = usuario, t = string.Join(" · ", notas) });
            return null;
        }
        public static string NombreEstado(string e) => e switch { "ABIERTO" => "Abierto", "EN_PROCESO" => "En proceso", "ESPERANDO" => "Esperando al proveedor", "RESUELTO" => "Resuelto", "CERRADO" => "Cerrado", _ => e };

        /// <summary>El proveedor marca que ya se resolvió o lo reabre.</summary>
        public async Task<string?> ProveedorEstadoAsync(int id, int cuenta, bool resuelto, string autor)
        {
            if (!await HayAsync()) return NoHay;
            using var cn = _db.Conexion();
            var n = await cn.ExecuteAsync(@"UPDATE Eurovip.proveedor_ticket SET estado = @e, actualizado_en = SYSDATETIME(), resuelto_en = CASE WHEN @r = 1 THEN SYSDATETIME() ELSE NULL END WHERE id_ticket = @id AND id_cuenta = @cuenta AND estado <> @e",
                new { id, cuenta, e = resuelto ? "RESUELTO" : "ABIERTO", r = resuelto ? 1 : 0 });
            if (n == 0) return "No se pudo cambiar el ticket.";
            await cn.ExecuteAsync("INSERT INTO Eurovip.proveedor_ticket_mensaje (id_ticket, tipo, autor, texto) VALUES (@id, 'SISTEMA', @a, @t)", new { id, a = autor, t = resuelto ? "El proveedor marcó el ticket como resuelto" : "El proveedor reabrió el ticket" });
            return null;
        }

        public async Task<string?> CalificarAsync(int id, int cuenta, int estrellas, string? texto)
        {
            if (!await HayAsync()) return NoHay;
            if (estrellas is < 1 or > 5) return "Elige de 1 a 5 estrellas.";
            using var cn = _db.Conexion();
            var n = await cn.ExecuteAsync("UPDATE Eurovip.proveedor_ticket SET calificacion = @e, calificacion_texto = @t WHERE id_ticket = @id AND id_cuenta = @cuenta AND estado IN ('RESUELTO','CERRADO')",
                new { id, cuenta, e = estrellas, t = string.IsNullOrWhiteSpace(texto) ? null : (texto.Trim().Length > 500 ? texto.Trim()[..500] : texto.Trim()) });
            return n == 0 ? "Solo se califica un ticket resuelto." : null;
        }

        // ---------------------------------------------------------------- consultas
        public async Task<(List<TicketFila> Items, Dictionary<string, int> Conteo, bool Activo)> ListarAsync(int? cuenta, string? estado, string? categoria, string? asignado, string? q)
        {
            if (!await HayAsync()) return (new(), new(), false);
            using var cn = _db.Conexion();
            var items = (await cn.QueryAsync<TicketFila>(Sel + @" WHERE (@cuenta IS NULL OR t.id_cuenta = @cuenta) AND (@estado IS NULL OR t.estado = @estado) AND (@categoria IS NULL OR t.categoria = @categoria)
                AND (@asignado IS NULL OR t.asignado_a = @asignado) AND (@q IS NULL OR t.asunto LIKE '%' + @q + '%' OR pc.nombre LIKE '%' + @q + '%' OR CAST(t.id_ticket AS varchar(10)) = @q)
                ORDER BY CASE t.estado WHEN 'CERRADO' THEN 2 WHEN 'RESUELTO' THEN 1 ELSE 0 END, t.prioridad DESC, t.actualizado_en DESC",
                new { cuenta, estado = Nulo(estado), categoria = Nulo(categoria), asignado = Nulo(asignado), q = Nulo(q)?.Replace("TCK-", "").TrimStart('0') })).Take(300).ToList();
            var conteo = (await cn.QueryAsync<(string E, int N)>("SELECT estado, COUNT(*) FROM Eurovip.proveedor_ticket WHERE (@cuenta IS NULL OR id_cuenta = @cuenta) GROUP BY estado", new { cuenta })).ToDictionary(x => x.E, x => x.N);
            return (items, conteo, true);
        }
        private static string? Nulo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        public async Task<(TicketFila? Ticket, List<MensajeFila> Mensajes)> DetalleAsync(int id, int? cuenta)
        {
            if (!await HayAsync()) return (null, new());
            using var cn = _db.Conexion();
            var t = await cn.QueryFirstOrDefaultAsync<TicketFila>(Sel + " WHERE t.id_ticket = @id AND (@cuenta IS NULL OR t.id_cuenta = @cuenta)", new { id, cuenta });
            if (t is null) return (null, new());
            var m = (await cn.QueryAsync<MensajeFila>("SELECT id, id_ticket, tipo, autor, texto, adjuntos, creado_en FROM Eurovip.proveedor_ticket_mensaje WHERE id_ticket = @id AND (@cuenta IS NULL OR tipo <> 'NOTA') ORDER BY id", new { id, cuenta })).ToList();
            return (t, m);
        }

        /// <summary>Números para el tablero de Eurovip y la satisfacción (estrellas) de los últimos 30 días.</summary>
        public async Task<object> ResumenAsync()
        {
            if (!await HayAsync()) return new { activo = false };
            using var cn = _db.Conexion();
            var r = await cn.QueryFirstAsync<dynamic>(@"SELECT
                (SELECT COUNT(*) FROM Eurovip.proveedor_ticket WHERE estado IN ('ABIERTO','EN_PROCESO','ESPERANDO')) AS Abiertos,
                (SELECT COUNT(*) FROM Eurovip.proveedor_ticket WHERE estado = 'ABIERTO' AND primera_respuesta_en IS NULL) AS SinRespuesta,
                (SELECT COUNT(*) FROM Eurovip.proveedor_ticket WHERE estado IN ('ABIERTO','EN_PROCESO','ESPERANDO') AND creado_en < DATEADD(DAY, -5, SYSDATETIME())) AS Vencidos,
                (SELECT AVG(CAST(DATEDIFF(MINUTE, creado_en, primera_respuesta_en) AS float)) / 60.0 FROM Eurovip.proveedor_ticket WHERE primera_respuesta_en IS NOT NULL AND creado_en > DATEADD(DAY, -30, SYSDATETIME())) AS HorasPrimeraRespuesta,
                (SELECT AVG(CAST(calificacion AS float)) FROM Eurovip.proveedor_ticket WHERE calificacion IS NOT NULL AND creado_en > DATEADD(DAY, -90, SYSDATETIME())) AS Estrellas,
                (SELECT COUNT(*) FROM Eurovip.proveedor_ticket WHERE calificacion IS NOT NULL AND creado_en > DATEADD(DAY, -90, SYSDATETIME())) AS Calificados");
            return new { activo = true, abiertos = (int)r.Abiertos, sinRespuesta = (int)r.SinRespuesta, vencidos = (int)r.Vencidos, horasPrimeraRespuesta = (double?)r.HorasPrimeraRespuesta, estrellas = (double?)r.Estrellas, calificados = (int)r.Calificados };
        }
    }
}
