using Dapper;
using EuroVip.Models;
using Microsoft.Extensions.Caching.Memory;

namespace EuroVip.Repositories
{
    public interface IUsuariosRepository
    {
        Task<List<UsuarioFila>> ListarAsync();
        Task<UsuarioFila?> ObtenerAsync(int id);
        Task<List<string>> RolesAsync();
        Task<(bool Email, bool Usuario)> DuplicadoAsync(string email, string userName, int excluirId);
        Task<int> CrearAsync(UsuarioEditarDto u, string hash);
        Task ActualizarAsync(UsuarioEditarDto u, string userNameAnterior);
        Task CambiarClaveAsync(int id, string hash);
        Task EstadoAsync(int id, bool activo);
        Task EliminarAsync(int id, string userName);
        Task<int> SuperActivosAsync(int excluirId);

        Task<List<ModuloDto>> ModulosAsync();
        Task<List<int>> AccesosAsync(string usuario);
        Task<Dictionary<string, List<int>>> MatrizAsync();
        Task GuardarAccesosAsync(string usuario, IEnumerable<int> modulos);
        Task CambiarAccesoAsync(string usuario, int idModulo, bool acceso);
        Task<int> GuardarModuloAsync(ModuloEditarDto m);
        Task<bool> EliminarModuloAsync(int id);
    }

    /// <summary>
    /// Usuarios y accesos sobre [Eurovip].[usuario], [UsuarioAcceso] (módulos) y [UsuarioModulos] (quién ve qué).
    /// Como /Usuario2 de BackTiTan, pero en Eurovip: solo se guardan las filas con acceso = 1.
    /// </summary>
    public class UsuariosRepository : IUsuariosRepository
    {
        private readonly ICircuitoRepository _db;
        private readonly IAccesoService _accesos;

        public UsuariosRepository(ICircuitoRepository db, IAccesoService accesos)
        {
            _db = db;
            _accesos = accesos;
        }

        private const string Columnas = @"u.codigoUsuario AS CodigoUsuario, u.nombre AS Nombre, u.apellido AS Apellido, u.userName AS UserName,
                       u.email AS Email, u.telefono AS Telefono, u.rol AS Rol, u.activo AS Activo, u.imagen AS Imagen,
                       (SELECT COUNT(*) FROM Eurovip.UsuarioModulos m WHERE m.usuario = u.userName AND m.acceso = 1) AS Modulos,
                       (SELECT MAX(f) FROM (SELECT MAX(l.fecha_hora) f FROM Eurovip.Log_Cargas l WHERE l.usuario = u.userName
                                            UNION ALL SELECT MAX(a.fecha_hora) FROM Eurovip.auditoria_consulta_oferta a WHERE a.usuario = u.userName) x) AS UltimaActividad";

        public async Task<List<UsuarioFila>> ListarAsync()
        {
            using var cn = _db.Conexion();
            var l = (await cn.QueryAsync<UsuarioFila>($"SELECT {Columnas} FROM Eurovip.usuario u ORDER BY u.activo DESC, u.nombre, u.apellido")).ToList();
            l.ForEach(u => u.Foto = PerfilFoto.Url(u.Imagen));
            return l;
        }

        public async Task<UsuarioFila?> ObtenerAsync(int id)
        {
            using var cn = _db.Conexion();
            var u = await cn.QueryFirstOrDefaultAsync<UsuarioFila>($"SELECT {Columnas} FROM Eurovip.usuario u WHERE u.codigoUsuario = @id", new { id });
            if (u is not null) u.Foto = PerfilFoto.Url(u.Imagen);
            return u;
        }

        public async Task<List<string>> RolesAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<string>("SELECT codigoRol FROM Eurovip.rol WHERE codigoRol IS NOT NULL ORDER BY CASE codigoRol WHEN 'Superadministrador' THEN 0 WHEN 'Administrador' THEN 1 ELSE 2 END, codigoRol")).ToList();
        }

        public async Task<(bool Email, bool Usuario)> DuplicadoAsync(string email, string userName, int excluirId)
        {
            using var cn = _db.Conexion();
            var r = await cn.QuerySingleAsync<(int E, int U)>(@"
                SELECT ISNULL(SUM(CASE WHEN email = @email THEN 1 ELSE 0 END), 0), ISNULL(SUM(CASE WHEN userName = @userName THEN 1 ELSE 0 END), 0)
                FROM Eurovip.usuario WHERE codigoUsuario <> @excluirId", new { email, userName, excluirId });
            return (r.E > 0, r.U > 0);
        }

        public async Task<int> CrearAsync(UsuarioEditarDto u, string hash)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var id = await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO Eurovip.usuario (nombre, apellido, telefono, clave, contrasena, activo, rol, email, userName, Auth2FACode, SecurityStamp)
                VALUES (@Nombre, @Apellido, @Telefono, @hash, @hash, @Activo, @Rol, @Email, @UserName, 0, @sello);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { u.Nombre, u.Apellido, u.Telefono, hash, u.Activo, u.Rol, u.Email, u.UserName, sello = Guid.NewGuid().ToString("N") }, tx);
            await Reemplazar(cn, tx, u.UserName, u.Modulos ?? new());
            tx.Commit();
            _accesos.Olvidar(u.UserName);
            return id;
        }

        public async Task ActualizarAsync(UsuarioEditarDto u, string userNameAnterior)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            await cn.ExecuteAsync(@"
                UPDATE Eurovip.usuario SET nombre = @Nombre, apellido = @Apellido, telefono = @Telefono, rol = @Rol, activo = @Activo,
                       email = @Email, userName = @UserName, SecurityStamp = @sello
                WHERE codigoUsuario = @CodigoUsuario",
                new { u.Nombre, u.Apellido, u.Telefono, u.Rol, u.Activo, u.Email, u.UserName, u.CodigoUsuario, sello = Guid.NewGuid().ToString("N") }, tx);
            // los accesos van por nombre de usuario: si cambió, se mueven con él
            if (!string.Equals(u.UserName, userNameAnterior, StringComparison.Ordinal))
                await cn.ExecuteAsync("UPDATE Eurovip.UsuarioModulos SET usuario = @nuevo WHERE usuario = @viejo", new { nuevo = u.UserName, viejo = userNameAnterior }, tx);
            tx.Commit();
            _accesos.Olvidar(userNameAnterior);
            _accesos.Olvidar(u.UserName);
        }

        public async Task CambiarClaveAsync(int id, string hash)
        {
            using var cn = _db.Conexion();
            await cn.ExecuteAsync("UPDATE Eurovip.usuario SET clave = @hash, contrasena = @hash, SecurityStamp = @sello WHERE codigoUsuario = @id",
                new { id, hash, sello = Guid.NewGuid().ToString("N") });
        }

        public async Task EstadoAsync(int id, bool activo)
        {
            using var cn = _db.Conexion();
            var user = await cn.ExecuteScalarAsync<string>("UPDATE Eurovip.usuario SET activo = @activo OUTPUT inserted.userName WHERE codigoUsuario = @id", new { id, activo });
            if (user is not null) _accesos.Olvidar(user);
        }

        public async Task EliminarAsync(int id, string userName)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            await cn.ExecuteAsync("DELETE FROM Eurovip.UsuarioModulos WHERE usuario = @userName", new { userName }, tx);
            await cn.ExecuteAsync("DELETE FROM Eurovip.usuario WHERE codigoUsuario = @id", new { id }, tx);
            tx.Commit();
            _accesos.Olvidar(userName);
        }

        public async Task<int> SuperActivosAsync(int excluirId)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.usuario WHERE rol = 'Superadministrador' AND activo = 1 AND codigoUsuario <> @excluirId", new { excluirId });
        }

        // ---------------- módulos y accesos ----------------
        public async Task<List<ModuloDto>> ModulosAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<ModuloDto>(@"
                SELECT a.id_modulo AS Id_Modulo, a.nombre_modulo AS Nombre_Modulo, a.Categoria, a.clave AS Clave, a.controladores AS Controladores,
                       a.icono AS Icono, a.descripcion AS Descripcion, a.orden AS Orden,
                       (SELECT COUNT(*) FROM Eurovip.UsuarioModulos m JOIN Eurovip.usuario u ON u.userName = m.usuario
                        WHERE m.id_modulo = a.id_modulo AND m.acceso = 1) AS Usuarios
                FROM Eurovip.UsuarioAcceso a ORDER BY ISNULL(a.orden, 9999), a.Categoria, a.id_modulo")).ToList();
        }

        public async Task<List<int>> AccesosAsync(string usuario)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<int>("SELECT id_modulo FROM Eurovip.UsuarioModulos WHERE usuario = @usuario AND acceso = 1", new { usuario })).ToList();
        }

        public async Task<Dictionary<string, List<int>>> MatrizAsync()
        {
            using var cn = _db.Conexion();
            var filas = await cn.QueryAsync<(string Usuario, int Id)>("SELECT usuario, id_modulo FROM Eurovip.UsuarioModulos WHERE acceso = 1");
            return filas.GroupBy(f => f.Usuario, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToList(), StringComparer.OrdinalIgnoreCase);
        }

        public async Task GuardarAccesosAsync(string usuario, IEnumerable<int> modulos)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            await Reemplazar(cn, tx, usuario, modulos);
            tx.Commit();
            _accesos.Olvidar(usuario);
        }

        public async Task CambiarAccesoAsync(string usuario, int idModulo, bool acceso)
        {
            using var cn = _db.Conexion();
            await cn.ExecuteAsync(acceso
                ? @"IF NOT EXISTS (SELECT 1 FROM Eurovip.UsuarioModulos WHERE usuario = @usuario AND id_modulo = @idModulo)
                        INSERT INTO Eurovip.UsuarioModulos (usuario, acceso, id_modulo) VALUES (@usuario, 1, @idModulo)
                    ELSE UPDATE Eurovip.UsuarioModulos SET acceso = 1 WHERE usuario = @usuario AND id_modulo = @idModulo"
                : "DELETE FROM Eurovip.UsuarioModulos WHERE usuario = @usuario AND id_modulo = @idModulo", new { usuario, idModulo });
            _accesos.Olvidar(usuario);
        }

        private static async Task Reemplazar(System.Data.IDbConnection cn, System.Data.IDbTransaction tx, string usuario, IEnumerable<int> modulos)
        {
            await cn.ExecuteAsync("DELETE FROM Eurovip.UsuarioModulos WHERE usuario = @usuario", new { usuario }, tx);
            var ids = modulos.Distinct().ToList();
            if (ids.Count > 0)
                await cn.ExecuteAsync(@"INSERT INTO Eurovip.UsuarioModulos (usuario, acceso, id_modulo)
                                        SELECT @usuario, 1, id_modulo FROM Eurovip.UsuarioAcceso WHERE id_modulo IN @ids", new { usuario, ids }, tx);
        }

        public async Task<int> GuardarModuloAsync(ModuloEditarDto m)
        {
            using var cn = _db.Conexion();
            int id;
            if (m.Id_Modulo > 0)
            {
                await cn.ExecuteAsync(@"UPDATE Eurovip.UsuarioAcceso SET nombre_modulo = @Nombre_Modulo, Categoria = @Categoria, descripcion = @Descripcion, icono = @Icono
                                        WHERE id_modulo = @Id_Modulo", m);
                id = m.Id_Modulo;
            }
            else
                id = await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.UsuarioAcceso (nombre_modulo, Categoria, descripcion, icono, orden)
                                                       VALUES (@Nombre_Modulo, @Categoria, @Descripcion, @Icono, 1000);
                                                       SELECT CAST(SCOPE_IDENTITY() AS INT);", m);
            _accesos.OlvidarModulos();
            return id;
        }

        /// <summary>Solo módulos creados a mano (sin clave): los de Eurovip los usa el programa y no se borran.</summary>
        public async Task<bool> EliminarModuloAsync(int id)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var n = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.UsuarioAcceso WHERE id_modulo = @id AND clave IS NULL", new { id }, tx);
            if (n == 0) return false;
            await cn.ExecuteAsync("DELETE FROM Eurovip.UsuarioModulos WHERE id_modulo = @id; DELETE FROM Eurovip.UsuarioAcceso WHERE id_modulo = @id;", new { id }, tx);
            tx.Commit();
            _accesos.OlvidarTodo();
            return true;
        }
    }

    // =====================================================================================
    public interface IAccesoService
    {
        Task<PermisosUsuario> PermisosAsync(string userName);
        /// <summary>Clave del módulo que cubre ese controlador, o null si el controlador es libre (Inicio, Perfil…).</summary>
        Task<string?> ModuloDeAsync(string controlador);
        void Olvidar(string userName);
        void OlvidarModulos();
        void OlvidarTodo();
    }

    /// <summary>
    /// Permisos del usuario conectado (rol, activo y módulos), guardados un minuto en memoria para no ir a la base
    /// en cada clic. Cualquier cambio desde "Usuarios y accesos" borra la memoria de ese usuario al instante.
    /// </summary>
    public class AccesoService : IAccesoService
    {
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _cfg;
        private CancellationTokenSource _todo = new();

        public AccesoService(IMemoryCache cache, IConfiguration cfg)
        {
            _cache = cache;
            _cfg = cfg;
        }

        private System.Data.SqlClient.SqlConnection Conexion() => new(_cfg.GetConnectionString("DefaultConnection"));

        public async Task<PermisosUsuario> PermisosAsync(string userName)
        {
            return (await _cache.GetOrCreateAsync("acc:u:" + userName.ToLowerInvariant(), async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                e.AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(_todo.Token));
                using var cn = Conexion();
                var u = await cn.QueryFirstOrDefaultAsync<UsuarioEstado>("SELECT rol AS Rol, activo AS Activo FROM Eurovip.usuario WHERE userName = @userName", new { userName });
                var p = new PermisosUsuario { Existe = u is not null, Activo = u?.Activo ?? false, Rol = u?.Rol };
                if (u is not null)
                    foreach (var c in await cn.QueryAsync<string>(@"SELECT a.clave FROM Eurovip.UsuarioModulos m JOIN Eurovip.UsuarioAcceso a ON a.id_modulo = m.id_modulo
                                                                   WHERE m.usuario = @userName AND m.acceso = 1 AND a.clave IS NOT NULL", new { userName }))
                        p.Claves.Add(c);
                return p;
            }))!;
        }

        public async Task<string?> ModuloDeAsync(string controlador)
        {
            var mapa = (await _cache.GetOrCreateAsync("acc:modulos", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                e.AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(_todo.Token));
                using var cn = Conexion();
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (clave, ctrls) in await cn.QueryAsync<(string, string)>("SELECT clave, controladores FROM Eurovip.UsuarioAcceso WHERE clave IS NOT NULL AND controladores IS NOT NULL"))
                    foreach (var c in ctrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        d[c] = clave;
                return d;
            }))!;
            return mapa.TryGetValue(controlador, out var m) ? m : null;
        }

        private class UsuarioEstado { public string? Rol { get; set; } public bool Activo { get; set; } }

        public void Olvidar(string userName) => _cache.Remove("acc:u:" + userName.ToLowerInvariant());
        public void OlvidarModulos() => _cache.Remove("acc:modulos");
        public void OlvidarTodo()
        {
            var viejo = _todo;
            _todo = new CancellationTokenSource();
            viejo.Cancel();
            viejo.Dispose();
        }
    }
}
