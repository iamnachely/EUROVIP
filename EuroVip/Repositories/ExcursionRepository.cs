using System.Data;
using Dapper;
using EuroVip.Models;
using EuroVip.Services;

namespace EuroVip.Repositories
{
    public interface IExcursionRepository
    {
        Task<List<Excursion>> ListarAsync();
        Task<Excursion?> ObtenerAsync(int id);
        /// <summary>Crea (Id_Excursion null) o actualiza; devuelve el número de la excursión.</summary>
        Task<int> GuardarAsync(ExcursionGuardarDto d);
        Task<bool> EliminarAsync(int id);
        /// <summary>Número de otra excursión con el mismo nombre en el mismo destino (o null).</summary>
        Task<int?> RepetidaAsync(string nombre, string destino, int? excepto);
        /// <summary>El destino tiene que existir en Destinos; devuelve el error o null, y el nombre bien escrito.</summary>
        Task<(string? Error, string? Destino)> ValidarDestinoAsync(string destino);
        /// <summary>Destinos para el buscador del campo "Destino" (los que ya tienen excursiones primero).</summary>
        Task<IEnumerable<Opcion>> DestinosAsync(string? q);
        /// <summary>Destinos que tienen excursiones, con su país y su carátula (cabeceras de las secciones de la pantalla).</summary>
        Task<List<ExcursionDestino>> DestinosConExcursionesAsync();
    }

    /// <summary>Mantenimiento de excursiones sobre Eurovip.servicio_excursion (nunca dbo, nunca ViajeVDT).</summary>
    public class ExcursionRepository : IExcursionRepository
    {
        private const string Tipo = "Excursion";
        private readonly ICircuitoRepository _db;
        public ExcursionRepository(ICircuitoRepository db) => _db = db;

        private const string Columnas = "id_Excursion, id_destino, id_tipo_servicio, nombre, descripcion, cancelable, precio, peso, precio_proveedor";

        public async Task<List<Excursion>> ListarAsync()
        {
            using var cn = _db.Conexion();
            // Arregla las tarjetas EXCURSION / TRASLADO de la Home que falten o estén desactualizadas (no escribe si están bien)
            await cn.ExecuteAsync("Eurovip.spSincronizaInicio", commandType: System.Data.CommandType.StoredProcedure);
            return (await cn.QueryAsync<Excursion>($"SELECT {Columnas} FROM Eurovip.servicio_excursion ORDER BY nombre")).ToList();
        }

        public async Task<Excursion?> ObtenerAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<Excursion>($"SELECT {Columnas} FROM Eurovip.servicio_excursion WHERE id_Excursion = @id", new { id });
        }

        public async Task<int> GuardarAsync(ExcursionGuardarDto d)
        {
            using var cn = _db.Conexion();
            var p = new { id = d.Id_Excursion, destino = d.Id_Destino, tipo = Tipo, nombre = d.Nombre, descripcion = string.IsNullOrWhiteSpace(d.Descripcion) ? null : d.Descripcion.Trim(), d.Cancelable, d.Precio, d.Peso, d.Precio_Proveedor };
            if (d.Id_Excursion is int id)
            {
                var antes = await cn.ExecuteScalarAsync<string?>("SELECT id_destino FROM Eurovip.servicio_excursion WHERE id_Excursion = @id", new { id });
                var n = await cn.ExecuteAsync(@"UPDATE Eurovip.servicio_excursion SET id_destino = @destino, id_tipo_servicio = @tipo, nombre = @nombre,
                                                descripcion = @descripcion, cancelable = @Cancelable, precio = @Precio, peso = @Peso, precio_proveedor = @Precio_Proveedor WHERE id_Excursion = @id", p);
                if (n == 0) throw new InvalidOperationException("Esa excursión ya no existe. Recarga la página.");
                await ActualizarInicioAsync(cn, antes, d.Id_Destino);
                return id;
            }
            var nuevo = await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.servicio_excursion (id_destino, id_tipo_servicio, nombre, descripcion, cancelable, precio, peso, precio_proveedor)
                                                      VALUES (@destino, @tipo, @nombre, @descripcion, @Cancelable, @Precio, @Peso, @Precio_Proveedor);
                                                      SELECT CAST(SCOPE_IDENTITY() AS int);", p);
            await ActualizarInicioAsync(cn, d.Id_Destino);
            return nuevo;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            using var cn = _db.Conexion();
            var destino = await cn.ExecuteScalarAsync<string?>("DELETE FROM Eurovip.servicio_excursion OUTPUT deleted.id_destino WHERE id_Excursion = @id", new { id });
            if (destino is null) return false;
            await ActualizarInicioAsync(cn, destino);
            return true;
        }

        /// <summary>
        /// Tarjeta de la Home (Eurovip.inicio, tipo 'EXCURSION'): una por destino con el precio "desde" de la más barata.
        /// spActualizaInicioExcursion la crea, la actualiza o la borra si el destino se quedó sin excursiones.
        /// </summary>
        private static async Task ActualizarInicioAsync(IDbConnection cn, params string?[] destinos)
        {
            foreach (var d in destinos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                await cn.ExecuteAsync("Eurovip.spActualizaInicioExcursion", new { destino = d }, commandType: CommandType.StoredProcedure);
        }

        public async Task<int?> RepetidaAsync(string nombre, string destino, int? excepto)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteScalarAsync<int?>(@"SELECT TOP 1 id_Excursion FROM Eurovip.servicio_excursion
                WHERE nombre = @nombre AND id_destino = @destino AND (@excepto IS NULL OR id_Excursion <> @excepto)", new { nombre, destino, excepto });
        }

        public async Task<(string? Error, string? Destino)> ValidarDestinoAsync(string destino)
        {
            using var cn = _db.Conexion();
            var error = await ReglasDatos.ErrorDestinoAsync(cn, destino);
            if (error is not null) return (error, null);
            var bien = await cn.ExecuteScalarAsync<string>("SELECT TOP 1 id_destino FROM Eurovip.destino WHERE id_destino = @destino AND id_idioma = 'ES'",
                new { destino = ReglasDatos.Limpiar(destino) });
            return (null, bien);
        }

        public async Task<List<ExcursionDestino>> DestinosConExcursionesAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<ExcursionDestino>(@"
                SELECT e.id_destino AS Destino,
                       (SELECT TOP 1 ISNULL(p.nombre, d.id_pais) FROM Eurovip.destino d
                        LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES'
                        WHERE d.id_destino = e.id_destino AND d.id_idioma = 'ES') AS Pais,
                       (SELECT TOP 1 d.caratula FROM Eurovip.destino d WHERE d.id_destino = e.id_destino AND d.id_idioma = 'ES') AS Caratula
                FROM Eurovip.servicio_excursion e
                WHERE ISNULL(e.id_destino, '') <> ''
                GROUP BY e.id_destino
                ORDER BY e.id_destino")).ToList();
        }

        public async Task<IEnumerable<Opcion>> DestinosAsync(string? q)
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<Opcion>(@"
                SELECT TOP 15 d.id_destino AS Valor, d.id_destino AS Texto, ISNULL(p.nombre, d.id_pais) AS Extra
                FROM Eurovip.destino d LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES'
                WHERE d.id_idioma = 'ES' AND (@q IS NULL OR d.id_destino LIKE '%' + @q + '%')
                ORDER BY CASE WHEN EXISTS (SELECT 1 FROM Eurovip.servicio_excursion e WHERE e.id_destino = d.id_destino) THEN 0 ELSE 1 END,
                         CASE WHEN d.id_destino LIKE @q + '%' THEN 0 ELSE 1 END, d.id_destino",
                new { q = string.IsNullOrWhiteSpace(q) ? null : q.Trim() });
        }
    }
}
