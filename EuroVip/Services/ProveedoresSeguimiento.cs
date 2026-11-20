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
        public ProveedoresSeguimiento(ICircuitoRepository db) { _db = db; }

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
            return new { numeros = n, atrasadas, salen, sinResponder, actividad, datosPendientes = datos, seguimientoActivo = _hayNotas && _hayGestion };
        }

        // ---------------------------------------------------------------- bandeja de chats
        public sealed class Chat
        {
            public int Id { get; set; } public string Codigo { get; set; } = ""; public string? Titulo { get; set; } public string Modulo { get; set; } = ""; public string Estado { get; set; } = "";
            public string? Cuenta { get; set; } public string? Ultimo_Autor { get; set; } public string? Ultimo_Texto { get; set; } public DateTime? Ultima_Fecha { get; set; } public int Mensajes { get; set; }
        }

        /// <summary>Todas las conversaciones (cargas con mensajes), la última primero.</summary>
        public async Task<List<Chat>> ChatsAsync()
        {
            using var cn = _db.Conexion();
            try
            {
                return (await cn.QueryAsync<Chat>(@"
                    SELECT TOP 150 c.id, c.codigo, c.titulo, c.modulo, c.estado, pc.nombre AS Cuenta, m.autor AS Ultimo_Autor, m.texto AS Ultimo_Texto, m.fecha AS Ultima_Fecha,
                           (SELECT COUNT(*) FROM Eurovip.carga_proveedor_comentario x WHERE x.id_carga = c.id) AS Mensajes
                    FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta
                    CROSS APPLY (SELECT TOP 1 autor, texto, fecha FROM Eurovip.carga_proveedor_comentario WHERE id_carga = c.id ORDER BY id DESC) m
                    ORDER BY m.fecha DESC")).ToList();
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
            public DateTime? Ultima_Actividad { get; set; } public DateTime? Ultima_Carga { get; set; }
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
            return lista;
        }
    }
}
