using Dapper;
using EuroVip.Models;
using EuroVip.Services;

namespace EuroVip.Repositories
{
    public interface IFlyerRepository
    {
        /// <summary>El destino del flyer tiene que existir en Herramientas › Destinos. Devuelve el error o null.</summary>
        Task<string?> ErrorDestinoAsync(string? destino);
        /// <summary>tipo: «Cliente» (Flyer Editar, por defecto) o «Agencia» (Flyer Agencia): cada uno tiene sus destinos y sus precios.</summary>
        Task<List<FlyerConfig>> ConfigsAsync(string tipo = Cliente);
        Task<int> GuardarConfigAsync(FlyerConfig c);
        Task<bool> EliminarConfigAsync(int id);
        Task<List<FlyerDestino>> DestinosAsync(string tipo = Cliente);
        Task<int> ActualizarPreciosAsync(int? soloConfig = null, string tipo = Cliente);
        const string Cliente = "Cliente";
        const string Agencia = "Agencia";
        Task<List<FlyerContacto>> ContactosAsync();
        Task<int> GuardarContactoAsync(FlyerContacto c);
        Task<bool> EliminarContactoAsync(int id);
        Task<int> EstadoContactosAsync(IEnumerable<int> ids, bool activo);
        Task<(int Nuevos, int Repetidos, int Invalidos)> ImportarAsync(string texto);
        Task<List<FlyerEnvio>> EnviosAsync();
        /// <summary>Editor de Flyer Editar: hoteles con su precio más barato a la venta (Solo Hotel o Paquetes), por destino y/o nombre.</summary>
        Task<List<FlyerHotelPrecio>> HotelesConPrecioAsync(string? texto, string? destino, int? idAlojamiento = null);
        /// <summary>Hoteles por nombre aunque no tengan ofertas (el editor los muestra; el precio se escribe a mano).</summary>
        Task<List<FlyerHotelPrecio>> HotelesPorNombreAsync(string texto);
        Task RegistrarEnvioAsync(FlyerEnvio e);
    }

    /// <summary>
    /// Flyers sobre [Eurovip] (tablas copiadas de ViajeVDT, CAMBIO 41). Los precios se calculan con las ofertas
    /// de Eurovip a la venta: top 5 hoteles más baratos por destino (como FlyerService de BackTiTan), con o sin vuelo.
    /// </summary>
    public class FlyerRepository : IFlyerRepository
    {
        private readonly ICircuitoRepository _db;
        public FlyerRepository(ICircuitoRepository db) => _db = db;

        // Igual que BackTiTan, pero el origen sale de servicio_origen (vuelo opcional) y solo esquema Eurovip.
        private const string TopHoteles = @"
            ;WITH precios AS (
                SELECT a.id_alojamiento, a.id_pais AS Id_Pais, a.nombre AS Nombre_Hotel, ah.nombre AS Nombre_Habitacion, r.descripcion AS Regimen,
                       CAST(ROUND(s.precio_base * (1.0 - s.descuento / 100.0) + ISNULL(s.precio_mas_menos, 0) + ISNULL(s.ajuste, 0)
                                  + ISNULL((SELECT SUM(st.valor) FROM Eurovip.servicio_tasa st WHERE st.id_Servicio = s.id_servicio), 0)
                                  + ISNULL(sh.precio, 0), 0) AS INT) AS Precio_Final
                FROM Eurovip.servicio s
                    JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.id_idioma = s.id_idioma
                    JOIN Eurovip.alojamiento a ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
                    JOIN Eurovip.regimen r ON r.id_regimen = sd.id_regimen AND r.id_idioma = sd.id_idioma
                    JOIN Eurovip.servicio_habitacion sh ON sh.id_servicio = s.id_servicio AND sh.id_alojamiento = sd.id_alojamiento
                    JOIN Eurovip.alojamiento_habitacion ah ON ah.id_alojamiento = sh.id_alojamiento AND ah.id_habitacion = sh.id_habitacion AND ah.id_idioma = s.id_idioma
                    OUTER APPLY (SELECT TOP 1 origen FROM Eurovip.servicio_itinerario i WHERE i.id_servicio = s.id_servicio AND i.segmento = 1) si
                    LEFT JOIN Eurovip.servicio_origen so ON so.id_servicio = s.id_servicio
                WHERE s.id_tipo_servicio = @Id_Tipo_Servicio AND s.id_campanya = @Id_Campanya AND s.id_idioma = @Id_Idioma AND s.id_moneda = @Id_Moneda
                  AND s.activo = @Activo_Servicio AND COALESCE(so.origen, si.origen) = @Origen AND sd.id_regimen = @Id_Regimen
                  AND sd.desde >= DATEADD(DAY, 1, CAST(GETDATE() AS DATE)) AND a.id_destino = @Id_Destino
            ), ranked AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY id_alojamiento ORDER BY Precio_Final) AS rn FROM precios)
            SELECT TOP 5 Id_Pais, Nombre_Hotel, Nombre_Habitacion, Regimen, Precio_Final FROM ranked WHERE rn = 1 ORDER BY Precio_Final";

        /// <summary>«Agencia» o «Cliente» (cualquier otro valor = Cliente, que es lo que había antes de Flyer Agencia).</summary>
        public static string Tipo(string? tipo) => string.Equals(tipo?.Trim(), IFlyerRepository.Agencia, StringComparison.OrdinalIgnoreCase) ? IFlyerRepository.Agencia : IFlyerRepository.Cliente;

        private const string HotelesPrecio = @"
            ;WITH p AS (
                SELECT a.id_alojamiento, a.nombre, a.caratula, a.id_destino, r.descripcion AS regimen, ah.nombre AS habitacion, sd.desde,
                       CAST(ROUND(s.precio_base * (1.0 - s.descuento / 100.0) + ISNULL(s.precio_mas_menos, 0) + ISNULL(s.ajuste, 0)
                                  + ISNULL((SELECT SUM(st.valor) FROM Eurovip.servicio_tasa st WHERE st.id_Servicio = s.id_servicio), 0)
                                  + ISNULL(sh.precio, 0), 0) AS INT) AS precio
                FROM Eurovip.servicio s
                    JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.id_idioma = s.id_idioma
                    JOIN Eurovip.alojamiento a ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
                    JOIN Eurovip.regimen r ON r.id_regimen = sd.id_regimen AND r.id_idioma = sd.id_idioma
                    JOIN Eurovip.servicio_habitacion sh ON sh.id_servicio = s.id_servicio AND sh.id_alojamiento = sd.id_alojamiento
                    JOIN Eurovip.alojamiento_habitacion ah ON ah.id_alojamiento = sh.id_alojamiento AND ah.id_habitacion = sh.id_habitacion AND ah.id_idioma = s.id_idioma
                WHERE s.id_tipo_servicio IN ('OFERTA', 'PAQUETES') AND s.activo = 1 AND s.id_idioma = 'ES' AND sd.desde > CAST(GETDATE() AS date)
                  AND (@destino IS NULL OR a.id_destino = @destino) AND (@id IS NULL OR a.id_alojamiento = @id)
                  AND (@texto IS NULL OR a.nombre LIKE '%' + @texto + '%')
            ), r AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY id_alojamiento ORDER BY precio, desde) AS rn FROM p)
            SELECT TOP 30 id_alojamiento AS Id_Alojamiento, nombre AS Nombre, caratula AS Caratula, id_destino AS Destino, regimen AS Regimen,
                   habitacion AS Habitacion, desde AS Desde, precio AS Precio
            FROM r WHERE rn = 1 ORDER BY precio";

        public async Task<List<FlyerHotelPrecio>> HotelesConPrecioAsync(string? texto, string? destino, int? idAlojamiento = null)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<FlyerHotelPrecio>(HotelesPrecio, new { texto = Vacio(texto), destino = Vacio(destino), id = idAlojamiento })).ToList();
        }

        public async Task<List<FlyerHotelPrecio>> HotelesPorNombreAsync(string texto)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<FlyerHotelPrecio>(@"SELECT TOP 30 id_alojamiento AS Id_Alojamiento, nombre AS Nombre, caratula AS Caratula, id_destino AS Destino
                FROM Eurovip.alojamiento WHERE id_idioma = 'ES' AND activo = 1 AND nombre LIKE '%' + @texto + '%' ORDER BY nombre", new { texto = texto.Trim() })).ToList();
        }

        public async Task<List<FlyerConfig>> ConfigsAsync(string tipo = IFlyerRepository.Cliente)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<FlyerConfig>(@"
                SELECT f.*, c.nombre AS Campanya,
                       (SELECT COUNT(*) FROM Eurovip.flyer_datos d WHERE d.id_destino = f.id_destino AND ISNULL(d.tipo, 'Cliente') = @tipo) AS Hoteles,
                       (SELECT MIN(precio_final) FROM Eurovip.flyer_datos d WHERE d.id_destino = f.id_destino AND ISNULL(d.tipo, 'Cliente') = @tipo) AS PrecioDesde
                FROM Eurovip.flyer_config f LEFT JOIN Eurovip.campanya c ON c.id_campanya = f.id_campanya AND c.id_idioma = 'ES'
                WHERE ISNULL(f.tipo, 'Cliente') = @tipo
                ORDER BY ISNULL(f.nombre_destino, f.id_destino)", new { tipo = Tipo(tipo) })).ToList();
        }

        public async Task<string?> ErrorDestinoAsync(string? destino)
        {
            using var cn = _db.Conexion();
            return await ReglasDatos.ErrorDestinoAsync(cn, destino);
        }

        public async Task<int> GuardarConfigAsync(FlyerConfig c)
        {
            using var cn = _db.Conexion();
            var p = new
            {
                c.Id, Nombre_Config = Vacio(c.Nombre_Config) ?? $"{c.Nombre_Destino ?? c.Id_Destino} {c.Id_Regimen}", Id_Tipo_Servicio = c.Id_Tipo_Servicio.ToUpperInvariant(), c.Id_Campanya,
                Id_Idioma = "ES", Id_Moneda = c.Id_Moneda.ToUpperInvariant(), c.Activo_Servicio, Origen = c.Origen.Trim(), Id_Regimen = c.Id_Regimen.Trim(), Id_Destino = c.Id_Destino.Trim(),
                Nombre_Destino = Vacio(c.Nombre_Destino) ?? c.Id_Destino.Trim(), Url_Destino = Vacio(c.Url_Destino), Tipo = Tipo(c.Tipo)
            };
            if (c.Id > 0)
            {
                await cn.ExecuteAsync(@"UPDATE Eurovip.flyer_config SET nombre_config = @Nombre_Config, id_tipo_servicio = @Id_Tipo_Servicio, id_campanya = @Id_Campanya,
                    id_idioma = @Id_Idioma, id_moneda = @Id_Moneda, activo_servicio = @Activo_Servicio, origen = @Origen, id_regimen = @Id_Regimen, id_destino = @Id_Destino,
                    nombre_destino = @Nombre_Destino, url_destino = @Url_Destino, tipo = @Tipo WHERE id = @Id", p);
                return c.Id;
            }
            return await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.flyer_config (nombre_config, id_tipo_servicio, id_campanya, id_idioma, id_moneda, activo_servicio,
                    origen, id_regimen, id_destino, nombre_destino, url_destino, tipo, fecha_creacion)
                VALUES (@Nombre_Config, @Id_Tipo_Servicio, @Id_Campanya, @Id_Idioma, @Id_Moneda, @Activo_Servicio, @Origen, @Id_Regimen, @Id_Destino, @Nombre_Destino, @Url_Destino, @Tipo, GETDATE());
                SELECT CAST(SCOPE_IDENTITY() AS int);", p);
        }

        public async Task<bool> EliminarConfigAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync(@"
                DELETE d FROM Eurovip.flyer_datos d JOIN Eurovip.flyer_config f ON f.id_destino = d.id_destino AND ISNULL(d.tipo, 'Cliente') = ISNULL(f.tipo, 'Cliente') WHERE f.id = @id
                    AND NOT EXISTS (SELECT 1 FROM Eurovip.flyer_config o WHERE o.id_destino = f.id_destino AND ISNULL(o.tipo, 'Cliente') = ISNULL(f.tipo, 'Cliente') AND o.id <> f.id);
                DELETE FROM Eurovip.flyer_config WHERE id = @id;", new { id }) > 0;
        }

        /// <summary>Lo que se va a pintar: cada destino con sus 5 hoteles (de flyer_datos).</summary>
        public async Task<List<FlyerDestino>> DestinosAsync(string tipo = IFlyerRepository.Cliente)
        {
            using var cn = _db.Conexion();
            var p = new { tipo = Tipo(tipo) };
            var conf = (await cn.QueryAsync<FlyerConfig>("SELECT * FROM Eurovip.flyer_config WHERE ISNULL(tipo, 'Cliente') = @tipo", p)).ToList();
            var datos = (await cn.QueryAsync<FlyerHotel>("SELECT * FROM Eurovip.flyer_datos WHERE ISNULL(activo, 1) = 1 AND ISNULL(tipo, 'Cliente') = @tipo ORDER BY id_destino, posicion", p)).ToList();
            return datos.GroupBy(d => d.Id_Destino ?? "").Select(g =>
            {
                var c = conf.FirstOrDefault(x => string.Equals(x.Id_Destino, g.Key, StringComparison.OrdinalIgnoreCase));
                return new FlyerDestino
                {
                    IdConfig = c?.Id ?? 0, Id_Destino = g.Key, Nombre_Destino = g.First().Nombre_Destino ?? g.Key, Origen = c?.Origen ?? "",
                    Fondo = c?.Url_Destino, Fecha_Actualizacion = g.Max(x => x.Fecha_Actualizacion), Hoteles = g.OrderBy(x => x.Posicion).ToList()
                };
            }).OrderBy(d => d.Nombre_Destino).ToList();
        }

        /// <summary>Recalcula flyer_datos con las ofertas a la venta. Devuelve cuántos destinos quedaron con precios.</summary>
        public async Task<int> ActualizarPreciosAsync(int? soloConfig = null, string tipo = IFlyerRepository.Cliente)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            tipo = Tipo(tipo);
            var configs = (await cn.QueryAsync<FlyerConfig>("SELECT * FROM Eurovip.flyer_config WHERE ISNULL(tipo, 'Cliente') = @tipo AND (@id IS NULL OR id = @id)", new { id = soloConfig, tipo })).ToList();
            var ahora = DateTime.Now;
            var conPrecios = 0;
            // Un destino puede tener varias configuraciones (p. ej. dos campañas): se juntan y quedan los 5 hoteles más baratos.
            // Si solo se recalcula una, se incluyen las demás del mismo destino para no pisar sus precios.
            if (soloConfig is not null && configs.Count > 0)
                configs = (await cn.QueryAsync<FlyerConfig>("SELECT * FROM Eurovip.flyer_config WHERE id_destino = @Id_Destino AND ISNULL(tipo, 'Cliente') = @tipo", new { configs[0].Id_Destino, tipo })).ToList();
            using var tx = cn.BeginTransaction();
            foreach (var grupo in configs.GroupBy(c => c.Id_Destino.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var todos = new List<FlyerHotel>();
                foreach (var c in grupo)
                    todos.AddRange(await cn.QueryAsync<FlyerHotel>(TopHoteles, new { c.Id_Tipo_Servicio, c.Id_Campanya, c.Id_Idioma, c.Id_Moneda, c.Activo_Servicio, c.Origen, c.Id_Regimen, c.Id_Destino }, tx));
                var top = todos.GroupBy(h => h.Nombre_Hotel).Select(g => g.MinBy(h => h.Precio_Final)!).OrderBy(h => h.Precio_Final).Take(5).ToList();
                var c0 = grupo.First();
                var nombre = grupo.Select(c => c.Nombre_Destino).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? c0.Id_Destino;
                await cn.ExecuteAsync("DELETE FROM Eurovip.flyer_datos WHERE id_destino = @Id_Destino AND ISNULL(tipo, 'Cliente') = @tipo", new { c0.Id_Destino, tipo }, tx);
                for (var i = 0; i < top.Count; i++)
                    await cn.ExecuteAsync(@"INSERT INTO Eurovip.flyer_datos (id_destino, nombre_destino, nombre_hotel, nombre_habitacion, regimen, precio_final, posicion, fecha_actualizacion, activo, tipo)
                        VALUES (@id, @nom, @hotel, @hab, @reg, @precio, @pos, @ahora, 1, @tipo)",
                        new { tipo, id = c0.Id_Destino, nom = nombre, hotel = top[i].Nombre_Hotel, hab = top[i].Nombre_Habitacion, reg = top[i].Regimen, precio = top[i].Precio_Final, pos = i + 1, ahora }, tx);
                if (top.Count > 0) conPrecios++;
            }
            tx.Commit();
            return conPrecios;
        }

        // ---------------- contactos ----------------
        public async Task<List<FlyerContacto>> ContactosAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<FlyerContacto>("SELECT * FROM Eurovip.flyer_contactos ORDER BY activo DESC, nombre_completo, numero")).ToList();
        }

        public static string LimpiarNumero(string n) => new string((n ?? "").Where(char.IsDigit).ToArray());

        public async Task<int> GuardarContactoAsync(FlyerContacto c)
        {
            var num = LimpiarNumero(c.Numero);
            using var cn = _db.Conexion();
            if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.flyer_contactos WHERE numero = @num AND id <> @Id", new { num, c.Id }) > 0) return -1;
            var p = new { c.Id, num, nom = Vacio(c.Nombre_Completo), email = Vacio(c.Email), c.Activo };
            if (c.Id > 0)
            {
                await cn.ExecuteAsync("UPDATE Eurovip.flyer_contactos SET numero = @num, nombre_completo = @nom, email = @email, activo = @Activo WHERE id = @Id", p);
                return c.Id;
            }
            return await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.flyer_contactos (activo, created_at, nombre_completo, numero, email) VALUES (@Activo, GETDATE(), @nom, @num, @email);
                SELECT CAST(SCOPE_IDENTITY() AS int);", p);
        }

        public async Task<bool> EliminarContactoAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("DELETE FROM Eurovip.flyer_contactos WHERE id = @id", new { id }) > 0;
        }

        public async Task<int> EstadoContactosAsync(IEnumerable<int> ids, bool activo)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("UPDATE Eurovip.flyer_contactos SET activo = @activo WHERE id IN @ids", new { ids = ids.ToArray(), activo });
        }

        /// <summary>Importa números pegados (uno por línea; opcional "Nombre; número; correo" o separados por coma/tab).</summary>
        public async Task<(int Nuevos, int Repetidos, int Invalidos)> ImportarAsync(string texto)
        {
            int nuevos = 0, repetidos = 0, invalidos = 0;
            using var cn = _db.Conexion();
            var existentes = (await cn.QueryAsync<string>("SELECT numero FROM Eurovip.flyer_contactos")).ToHashSet();
            foreach (var linea in (texto ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
            {
                var partes = linea.Split(new[] { ';', '\t', ',' }).Select(x => x.Trim()).ToArray();
                var num = partes.Select(LimpiarNumero).FirstOrDefault(x => x.Length >= 8 && x.Length <= 15);
                if (num is null) { invalidos++; continue; }
                if (!existentes.Add(num)) { repetidos++; continue; }
                var nombre = partes.FirstOrDefault(x => x.Any(char.IsLetter) && !x.Contains('@'));
                var email = partes.FirstOrDefault(x => x.Contains('@'));
                await cn.ExecuteAsync("INSERT INTO Eurovip.flyer_contactos (activo, created_at, nombre_completo, numero, email) VALUES (1, GETDATE(), @nombre, @num, @email)",
                    new { nombre = nombre?.Length > 200 ? nombre[..200] : nombre, num, email = email?.Length > 200 ? null : email });
                nuevos++;
            }
            return (nuevos, repetidos, invalidos);
        }

        // ---------------- historial ----------------
        public async Task<List<FlyerEnvio>> EnviosAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<FlyerEnvio>("SELECT TOP 50 * FROM Eurovip.flyer_envios ORDER BY fecha_envio DESC")).ToList();
        }

        public async Task RegistrarEnvioAsync(FlyerEnvio e)
        {
            using var cn = _db.Conexion();
            await cn.ExecuteAsync(@"INSERT INTO Eurovip.flyer_envios (fecha_envio, carpeta_fecha, total_contactos, enviados_ok, errores, imagenes_count, texto_mensaje, estado)
                VALUES (@Fecha_Envio, @Carpeta_Fecha, @Total_Contactos, @Enviados_Ok, @Errores, @Imagenes_Count, @Texto_Mensaje, @Estado)", e);
        }

        private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
