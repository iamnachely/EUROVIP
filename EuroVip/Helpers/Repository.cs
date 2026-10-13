using Dapper;
using System.Data;
using System.Data.SqlClient;

namespace EuroVip.Helpers
{
    public interface IRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> ObtenerPorIdAsync(object id, string columnaId);
        Task<IEnumerable<T>> GetIdAllAsync(object id, string columnaId);
        Task<T?> GetUserByEmailAsync(string email);
    }

    /// <summary>
    /// Repositorio genérico Dapper (mismo patrón que BackTiTan) pero SIEMPRE sobre el esquema Eurovip:
    /// la tabla se resuelve como [Eurovip].[NombreDeLaClase]. Nunca toca dbo.
    /// </summary>
    public class Repository<T> : IRepository<T> where T : class
    {
        public const string Esquema = "Eurovip";
        private readonly string _connectionString;

        public Repository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection");
        }

        private static string Tabla => $"[{Esquema}].[{typeof(T).Name}]";

        private IDbConnection Conexion() => new SqlConnection(_connectionString);

        // columnaId se interpola en el SQL: solo se aceptan nombres de columna simples.
        private static string Columna(string columna)
        {
            if (string.IsNullOrWhiteSpace(columna) || !columna.All(c => char.IsLetterOrDigit(c) || c == '_'))
                throw new ArgumentException($"Nombre de columna inválido: {columna}");
            return $"[{columna}]";
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            using var cn = Conexion();
            return await cn.QueryAsync<T>($"SELECT * FROM {Tabla}");
        }

        public async Task<T?> ObtenerPorIdAsync(object id, string columnaId)
        {
            using var cn = Conexion();
            return await cn.QueryFirstOrDefaultAsync<T>($"SELECT TOP(1) * FROM {Tabla} WHERE {Columna(columnaId)} = @Id", new { Id = id });
        }

        public async Task<IEnumerable<T>> GetIdAllAsync(object id, string columnaId)
        {
            using var cn = Conexion();
            return await cn.QueryAsync<T>($"SELECT * FROM {Tabla} WHERE {Columna(columnaId)} = @Id", new { Id = id });
        }

        public async Task<T?> GetUserByEmailAsync(string email)
        {
            using var cn = Conexion();
            return await cn.QueryFirstOrDefaultAsync<T>($"SELECT TOP(1) * FROM {Tabla} WHERE email = @email", new { email });
        }
    }
}
