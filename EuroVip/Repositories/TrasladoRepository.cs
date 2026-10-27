using System.Data;
using Dapper;
using EuroVip.Models;
using EuroVip.Services;

namespace EuroVip.Repositories
{
    public interface ITrasladoRepository
    {
        Task<List<Traslado>> ListarAsync();
        Task<Traslado?> ObtenerAsync(int id);
        /// <summary>Crea (Id_Traslado null) o actualiza, con sus hoteles; devuelve el número del traslado.</summary>
        Task<int> GuardarAsync(TrasladoGuardarDto d, string usuario);
        Task<bool> EliminarAsync(int id);
        Task<bool> ActivarAsync(int id, bool activo);
        Task PonerImagenAsync(int id, string? archivo);
        Task<int?> RepetidoAsync(TrasladoGuardarDto d);
        Task<(string? Error, string? Destino, string? Pais)> ValidarDestinoAsync(string destino);
        Task<string?> ValidarEstacionAsync(string codigo);
        Task<IEnumerable<Opcion>> DestinosAsync(string? q);
        Task<IEnumerable<Opcion>> AeropuertosAsync(string? q);
        Task<IEnumerable<Opcion>> HotelesAsync(string? destino, string? q);
        Task<List<int>> HotelesQueNoSonDelDestinoAsync(IEnumerable<int> ids, string destino);
    }

    /// <summary>
    /// Traslados aeropuerto → hotel/destino (Eurovip.traslado). La web los busca con Eurovip.spPortalBuscarTraslados.
    /// </summary>
    public class TrasladoRepository : ITrasladoRepository
    {
        private readonly ICircuitoRepository _db;
        public TrasladoRepository(ICircuitoRepository db) => _db = db;

        private const string Columnas = @"t.id_traslado AS Id_Traslado, t.nombre AS Nombre, t.tipo AS Tipo, t.categoria AS Categoria, t.vehiculo AS Vehiculo,
            t.id_estacion_origen AS Id_Estacion_Origen,
            (SELECT TOP 1 e.nombre FROM Eurovip.estacion e WHERE e.id_estacion = t.id_estacion_origen) AS Origen_Nombre,
            t.id_destino AS Id_Destino, t.id_pais AS Id_Pais, t.maletas AS Maletas, t.duracion_min AS Duracion_Min,
            t.pasajeros_min AS Pasajeros_Min, t.pasajeros_max AS Pasajeros_Max, t.cobro AS Cobro,
            t.precio_ida AS Precio_Ida, t.precio_ida_vuelta AS Precio_Ida_Vuelta, t.id_moneda AS Id_Moneda,
            t.cancelable AS Cancelable, t.horas_cancelacion AS Horas_Cancelacion, t.descripcion AS Descripcion, t.imagen AS Archivo,
            t.todos_hoteles AS Todos_Hoteles, t.activo AS Activo, t.peso AS Peso, t.precio_proveedor AS Precio_Proveedor, t.usuario AS Usuario, t.fecha_alta AS Fecha_Alta, t.fecha_modificacion AS Fecha_Modificacion";

        private const string SelHoteles = @"
            SELECT ta.id_traslado AS Id, CAST(ta.id_alojamiento AS varchar(20)) AS Valor, ISNULL(LTRIM(RTRIM(a.nombre)), 'Hotel ' + CAST(ta.id_alojamiento AS varchar(20))) AS Texto,
                   LTRIM(RTRIM(a.id_destino)) AS Extra
            FROM Eurovip.traslado_alojamiento ta
            OUTER APPLY (SELECT TOP 1 nombre, id_destino FROM Eurovip.alojamiento x WHERE x.id_alojamiento = ta.id_alojamiento) a";

        private sealed class HotelFila { public int Id { get; set; } public string Valor { get; set; } = ""; public string Texto { get; set; } = ""; public string? Extra { get; set; } }

        public async Task<List<Traslado>> ListarAsync()
        {
            using var cn = _db.Conexion();
            // Arregla las tarjetas EXCURSION / TRASLADO de la Home que falten o estén desactualizadas (no escribe si están bien)
            await cn.ExecuteAsync("Eurovip.spSincronizaInicio", commandType: System.Data.CommandType.StoredProcedure);
            var l = (await cn.QueryAsync<Traslado>($"SELECT {Columnas} FROM Eurovip.traslado t ORDER BY t.id_estacion_origen, t.id_destino, t.tipo, t.categoria")).ToList();
            var hoteles = (await cn.QueryAsync<HotelFila>(SelHoteles)).ToLookup(h => h.Id);
            foreach (var t in l)
                t.Hoteles = hoteles[t.Id_Traslado].Select(h => new Opcion { Valor = h.Valor, Texto = h.Texto, Extra = h.Extra }).OrderBy(h => h.Texto).ToList();
            return l;
        }

        public async Task<Traslado?> ObtenerAsync(int id)
        {
            using var cn = _db.Conexion();
            var t = await cn.QueryFirstOrDefaultAsync<Traslado>($"SELECT {Columnas} FROM Eurovip.traslado t WHERE t.id_traslado = @id", new { id });
            if (t is not null)
                t.Hoteles = (await cn.QueryAsync<HotelFila>(SelHoteles + " WHERE ta.id_traslado = @id", new { id }))
                    .Select(h => new Opcion { Valor = h.Valor, Texto = h.Texto, Extra = h.Extra }).OrderBy(h => h.Texto).ToList();
            return t;
        }

        public async Task<int> GuardarAsync(TrasladoGuardarDto d, string usuario)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var p = new
            {
                id = d.Id_Traslado, d.Nombre, d.Tipo, d.Categoria, d.Vehiculo, d.Id_Estacion_Origen, d.Id_Destino, d.Maletas, d.Duracion_Min,
                d.Pasajeros_Min, d.Pasajeros_Max, d.Cobro, d.Precio_Ida, d.Precio_Ida_Vuelta, d.Cancelable,
                Horas = d.Cancelable ? d.Horas_Cancelacion : null,
                Descripcion = string.IsNullOrWhiteSpace(d.Descripcion) ? null : d.Descripcion.Trim(), d.Todos_Hoteles, d.Activo, usuario, d.Peso, d.Precio_Proveedor
            };
            int id;
            string? destinoAntes = null;
            if (d.Id_Traslado is int existente)
            {
                destinoAntes = await cn.ExecuteScalarAsync<string?>("SELECT id_destino FROM Eurovip.traslado WHERE id_traslado = @id", new { id = existente }, tx);
                var n = await cn.ExecuteAsync(@"
                    UPDATE Eurovip.traslado SET nombre = @Nombre, tipo = @Tipo, categoria = @Categoria, vehiculo = @Vehiculo,
                           id_estacion_origen = @Id_Estacion_Origen, id_destino = @Id_Destino,
                           id_pais = (SELECT TOP 1 id_pais FROM Eurovip.destino WHERE id_destino = @Id_Destino AND id_idioma = 'ES'),
                           maletas = @Maletas, duracion_min = @Duracion_Min, pasajeros_min = @Pasajeros_Min, pasajeros_max = @Pasajeros_Max,
                           cobro = @Cobro, precio_ida = @Precio_Ida, precio_ida_vuelta = @Precio_Ida_Vuelta, id_moneda = 'DOP',
                           cancelable = @Cancelable, horas_cancelacion = @Horas, descripcion = @Descripcion, todos_hoteles = @Todos_Hoteles,
                           activo = @Activo, peso = @Peso, precio_proveedor = @Precio_Proveedor, usuario = @usuario, fecha_modificacion = SYSDATETIME()
                    WHERE id_traslado = @id", p, tx);
                if (n == 0) throw new InvalidOperationException("Ese traslado ya no existe. Recarga la página.");
                id = existente;
            }
            else
            {
                id = await cn.ExecuteScalarAsync<int>(@"
                    INSERT INTO Eurovip.traslado (nombre, tipo, categoria, vehiculo, id_estacion_origen, id_destino, id_pais, maletas, duracion_min,
                                                  pasajeros_min, pasajeros_max, cobro, precio_ida, precio_ida_vuelta, id_moneda, cancelable,
                                                  horas_cancelacion, descripcion, todos_hoteles, activo, usuario, peso, precio_proveedor)
                    VALUES (@Nombre, @Tipo, @Categoria, @Vehiculo, @Id_Estacion_Origen, @Id_Destino,
                            (SELECT TOP 1 id_pais FROM Eurovip.destino WHERE id_destino = @Id_Destino AND id_idioma = 'ES'),
                            @Maletas, @Duracion_Min, @Pasajeros_Min, @Pasajeros_Max, @Cobro, @Precio_Ida, @Precio_Ida_Vuelta, 'DOP', @Cancelable,
                            @Horas, @Descripcion, @Todos_Hoteles, @Activo, @usuario, @Peso, @Precio_Proveedor);
                    SELECT CAST(SCOPE_IDENTITY() AS int);", p, tx);
            }

            // Hoteles: se reemplazan (solo cuenta si no es "todos los hoteles del destino")
            await cn.ExecuteAsync("DELETE FROM Eurovip.traslado_alojamiento WHERE id_traslado = @id", new { id }, tx);
            if (!d.Todos_Hoteles)
                foreach (var h in d.Hoteles.Distinct())
                    await cn.ExecuteAsync("INSERT INTO Eurovip.traslado_alojamiento (id_traslado, id_alojamiento) VALUES (@id, @h)", new { id, h }, tx);
            tx.Commit();
            await ActualizarInicioAsync(cn, destinoAntes, d.Id_Destino);
            return id;
        }

        public async Task<bool> EliminarAsync(int id)
        {
            using var cn = _db.Conexion();
            var destino = await cn.ExecuteScalarAsync<string?>("DELETE FROM Eurovip.traslado OUTPUT deleted.id_destino WHERE id_traslado = @id", new { id });
            if (destino is null) return false;
            await ActualizarInicioAsync(cn, destino);
            return true;
        }

        public async Task<bool> ActivarAsync(int id, bool activo)
        {
            using var cn = _db.Conexion();
            var destino = await cn.ExecuteScalarAsync<string?>(@"UPDATE Eurovip.traslado SET activo = @activo, fecha_modificacion = SYSDATETIME()
                                                                OUTPUT inserted.id_destino WHERE id_traslado = @id", new { id, activo });
            if (destino is null) return false;
            await ActualizarInicioAsync(cn, destino);
            return true;
        }

        /// <summary>
        /// Tarjeta de la Home (Eurovip.inicio, tipo 'TRASLADO'): una por destino con el precio "desde" del más barato activo.
        /// spActualizaInicioTraslado la crea, la actualiza o la borra si el destino se quedó sin traslados activos.
        /// </summary>
        private static async Task ActualizarInicioAsync(IDbConnection cn, params string?[] destinos)
        {
            foreach (var d in destinos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                await cn.ExecuteAsync("Eurovip.spActualizaInicioTraslado", new { destino = d }, commandType: CommandType.StoredProcedure);
        }

        public async Task PonerImagenAsync(int id, string? archivo)
        {
            using var cn = _db.Conexion();
            await cn.ExecuteAsync("UPDATE Eurovip.traslado SET imagen = @archivo, fecha_modificacion = SYSDATETIME() WHERE id_traslado = @id", new { id, archivo });
        }

        /// <summary>Mismo traslado ya cargado: misma ruta, tipo, categoría y vehículo.</summary>
        public async Task<int?> RepetidoAsync(TrasladoGuardarDto d)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteScalarAsync<int?>(@"SELECT TOP 1 id_traslado FROM Eurovip.traslado
                WHERE id_estacion_origen = @Id_Estacion_Origen AND id_destino = @Id_Destino AND tipo = @Tipo AND categoria = @Categoria
                  AND vehiculo = @Vehiculo AND nombre = @Nombre AND (@Id_Traslado IS NULL OR id_traslado <> @Id_Traslado)", d);
        }

        public async Task<(string? Error, string? Destino, string? Pais)> ValidarDestinoAsync(string destino)
        {
            using var cn = _db.Conexion();
            var error = await ReglasDatos.ErrorDestinoAsync(cn, destino);
            if (error is not null) return (error, null, null);
            var fila = await cn.QueryFirstOrDefaultAsync<(string Destino, string? Pais)>(
                "SELECT TOP 1 id_destino, id_pais FROM Eurovip.destino WHERE id_destino = @destino AND id_idioma = 'ES'", new { destino = ReglasDatos.Limpiar(destino) });
            return (null, fila.Destino, fila.Pais);
        }

        public async Task<string?> ValidarEstacionAsync(string codigo)
        {
            using var cn = _db.Conexion();
            var c = ReglasDatos.Limpiar(codigo).ToUpperInvariant();
            return await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.estacion WHERE id_estacion = @c", new { c }) > 0
                ? null : $"El aeropuerto «{c}» no existe en Estaciones. Búscalo por su código (ej. PUJ) o nombre.";
        }

        public async Task<IEnumerable<Opcion>> DestinosAsync(string? q)
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<Opcion>(@"
                SELECT TOP 15 d.id_destino AS Valor, d.id_destino AS Texto, ISNULL(p.nombre, d.id_pais) AS Extra, d.id_pais AS Extra2
                FROM Eurovip.destino d LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES'
                WHERE d.id_idioma = 'ES' AND (@q IS NULL OR d.id_destino LIKE '%' + @q + '%')
                ORDER BY CASE WHEN EXISTS (SELECT 1 FROM Eurovip.traslado t WHERE t.id_destino = d.id_destino) THEN 0 ELSE 1 END,
                         CASE WHEN d.id_destino LIKE @q + '%' THEN 0 ELSE 1 END, d.id_destino",
                new { q = string.IsNullOrWhiteSpace(q) ? null : q.Trim() });
        }

        /// <summary>Aeropuertos por código o nombre (primero los de República Dominicana y los ya usados).</summary>
        public async Task<IEnumerable<Opcion>> AeropuertosAsync(string? q)
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<Opcion>(@"
                SELECT TOP 15 e.id_estacion AS Valor, MIN(e.nombre) AS Texto, MIN(LTRIM(RTRIM(e.id_destino))) AS Extra, MIN(e.id_pais) AS Extra2
                FROM Eurovip.estacion e
                WHERE (@q IS NULL OR e.id_estacion LIKE @q + '%' OR e.nombre LIKE '%' + @q + '%' OR e.id_destino LIKE '%' + @q + '%')
                GROUP BY e.id_estacion
                ORDER BY CASE WHEN EXISTS (SELECT 1 FROM Eurovip.traslado t WHERE t.id_estacion_origen = e.id_estacion) THEN 0 ELSE 1 END,
                         CASE WHEN e.id_estacion = @q THEN 0 ELSE 1 END,
                         CASE WHEN MIN(e.id_pais) = 'DO' THEN 0 ELSE 1 END, e.id_estacion",
                new { q = string.IsNullOrWhiteSpace(q) ? null : q.Trim() });
        }

        /// <summary>Hoteles de un destino (para elegir a cuáles llega el traslado).</summary>
        public async Task<IEnumerable<Opcion>> HotelesAsync(string? destino, string? q)
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<Opcion>(@"
                SELECT TOP 40 CAST(a.id_alojamiento AS varchar(20)) AS Valor, LTRIM(RTRIM(MIN(a.nombre))) AS Texto, LTRIM(RTRIM(MIN(a.id_destino))) AS Extra
                FROM Eurovip.alojamiento a
                WHERE ISNULL(a.activo, 1) = 1
                  AND (@destino IS NULL OR LTRIM(RTRIM(a.id_destino)) = @destino)
                  AND (@q IS NULL OR a.nombre LIKE '%' + @q + '%' OR CAST(a.id_alojamiento AS varchar(20)) = @q)
                GROUP BY a.id_alojamiento
                ORDER BY Texto",
                new { destino = string.IsNullOrWhiteSpace(destino) ? null : destino.Trim(), q = string.IsNullOrWhiteSpace(q) ? null : q.Trim() });
        }

        public async Task<List<int>> HotelesQueNoSonDelDestinoAsync(IEnumerable<int> ids, string destino)
        {
            var l = ids.Distinct().ToList();
            if (l.Count == 0) return l;
            using var cn = _db.Conexion();
            var buenos = (await cn.QueryAsync<int>(@"SELECT DISTINCT id_alojamiento FROM Eurovip.alojamiento
                WHERE id_alojamiento IN @l AND LTRIM(RTRIM(id_destino)) = @destino", new { l, destino })).ToHashSet();
            return l.Where(x => !buenos.Contains(x)).ToList();
        }
    }
}
