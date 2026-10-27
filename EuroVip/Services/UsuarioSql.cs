using System.Data;
using System.Data.Common;

namespace EuroVip.Services
{
    /// <summary>
    /// Quién hace el cambio (FASE 33): al abrir cada conexión se pone el usuario de la sesión en SESSION_CONTEXT('usuario').
    /// Los triggers trEurovip_*_quien lo usan para usuario_alta / usuario_modificacion y para Eurovip.registro_eliminado.
    /// Sin sesión (tareas en segundo plano) no se pone nada y el trigger anota «sql:&lt;login&gt;».
    /// El pool de conexiones limpia el SESSION_CONTEXT al reutilizar una conexión, así que no pasa de un usuario a otro.
    /// </summary>
    public static class UsuarioSql
    {
        public static IHttpContextAccessor? Accesor { get; set; }

        public static T Marcar<T>(T cn) where T : DbConnection
        {
            cn.StateChange += AlAbrir;
            return cn;
        }

        private static void AlAbrir(object? sender, StateChangeEventArgs e)
        {
            if (e.CurrentState != ConnectionState.Open || sender is not DbConnection cn) return;
            var u = Accesor?.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(u)) return;
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "EXEC sp_set_session_context @key = N'usuario', @value = @u";
            var p = cmd.CreateParameter();
            p.ParameterName = "@u";
            p.Value = u.Length > 50 ? u[..50] : u;
            cmd.Parameters.Add(p);
            cmd.ExecuteNonQuery();
        }
    }
}
