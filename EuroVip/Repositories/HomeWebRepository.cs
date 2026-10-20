using Dapper;
using EuroVip.Models;
using System.Data;
using System.Data.SqlClient;

namespace EuroVip.Repositories
{
    public interface IHomeWebRepository
    {
        Task<HwDatos> DatosAsync();
        Task OrdenContenedoresAsync(List<int> ids);
        Task<int> GuardarContenedorAsync(HwContenedorDto dto);
        Task<int> EliminarContenedorAsync(int id);
        Task OrdenTarjetasAsync(HwOrdenDto dto);
        Task<bool?> EstadoTarjetaAsync(int id, bool estado);
        Task<HwTarjeta?> TarjetaAsync(int id);
        Task<int> GuardarTarjetaAsync(HwTarjetaDto dto);
        Task<bool> EliminarTarjetaAsync(int id);
        Task<int> ActualizarDesdeOfertasAsync(HwActualizarDto? solo);
        Task<IEnumerable<HwActualizarDto>> ClavesOfertasAsync();
        Task<List<HwFuera>> FueraDeHomeAsync();

        Task OrdenSlidesAsync(List<int> ids);
        Task<bool> EditarSlideAsync(HwSlideDto dto);
        Task<int> CrearSlideAsync(string ruta, string orientacion, string? url, bool activo);
        Task<string?> EliminarSlideAsync(int id);
        Task<bool> ExisteRutaAsync(string ruta);
        Task<HwSlide?> SlideAsync(int id);
        Task<int> GuardarSlideDestinoAsync(HwSlideDestinoDto dto);
        Task<bool> EliminarSlideDestinoAsync(int id);
        Task<decimal?> PrecioMinimoAsync(string origen, string destino, int idCampanya, string? regimen);
        Task<int> RecalcularPreciosSlidesAsync();
    }

    /// <summary>
    /// Home de la web de Eurovip (equivale a /Inicio2/Index + /Carrusel/Index de BackTiTan), solo ES:
    ///   • contenedores = secciones de la Home, tarjetas = filas de [Eurovip].[inicio]
    ///   • el orden que pone el usuario se guarda también en [Eurovip].[home_contenido] para que
    ///     Eurovip.spActualizaInicio lo respete cuando refresca precios desde las ofertas
    ///   • carrusel = slider; un slide es la fila formato 1 (web) y tiene copias formato 2 (tablet) y 3 (móvil)
    /// </summary>
    public class HomeWebRepository : IHomeWebRepository
    {
        private const string Idioma = "ES";
        private readonly ICircuitoRepository _db;
        public HomeWebRepository(ICircuitoRepository db) => _db = db;

        private const string SelTarjeta = @"
            SELECT i.id AS Id, i.id_servicio AS Id_Servicio, i.tipo_servicio AS Tipo_Servicio, i.id_campanya AS Id_Campanya, c.nombre AS Campanya,
                   i.id_moneda AS Id_Moneda, i.id_pais_origen AS Id_Pais_Origen, i.origen AS Origen, i.id_paisDestino AS Id_PaisDestino,
                   i.id_destino AS Id_Destino, i.caratulaDestino AS CaratulaDestino, i.descripcion AS Descripcion, i.precioWeb AS PrecioWeb,
                   i.id_contenedor AS Id_Contenedor, i.contenedor AS Contenedor, i.posicion_destino AS Posicion_Destino,
                   CAST(ISNULL(i.estado, 1) AS bit) AS Estado, i.noches AS Noches, i.notas AS Notas,
                   CASE WHEN i.tipo_servicio IN ('OFERTA', 'CIRCUITO') THEN
                        CAST(CASE WHEN EXISTS (SELECT 1 FROM Eurovip.servicio s
                                                 LEFT JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio
                                                 LEFT JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
                                               WHERE s.id_servicio = i.id_servicio AND s.activo = 1
                                                 AND ISNULL(sd.desde, sc.desde) > GETDATE()) THEN 1 ELSE 0 END AS bit)
                   END AS Vigente,
                   (SELECT TOP 1 ci.caratula FROM Eurovip.servicio_circuito sc3
                        JOIN Eurovip.circuito ci ON ci.nombre_circuito = sc3.nombre_circuito
                    WHERE sc3.id_servicio = i.id_servicio AND i.tipo_servicio = 'CIRCUITO') AS Caratula_Circuito
            FROM Eurovip.inicio i
                LEFT JOIN Eurovip.campanya c ON c.id_campanya = i.id_campanya AND c.id_idioma = i.id_idioma";

        public async Task<HwDatos> DatosAsync()
        {
            using var cn = _db.Conexion();
            // Arregla las tarjetas EXCURSION / TRASLADO de la Home que falten o estén desactualizadas (no escribe si están bien)
            await cn.ExecuteAsync("Eurovip.spSincronizaInicio", commandType: System.Data.CommandType.StoredProcedure);
            var conts = (await cn.QueryAsync<HwContenedor>(@"
                SELECT id_contenedor AS Id_Contenedor, nombre AS Nombre, posicion AS Posicion, CAST(ISNULL(estado, 1) AS bit) AS Estado
                FROM Eurovip.contenedores WHERE UPPER(id_idioma) = @Idioma ORDER BY posicion, id_contenedor", new { Idioma })).ToList();
            var tarjetas = (await cn.QueryAsync<HwTarjeta>(SelTarjeta + @"
                WHERE i.id_idioma = @Idioma ORDER BY ISNULL(i.posicion_destino, 9999), i.id_destino", new { Idioma })).ToList();
            var porCont = conts.ToDictionary(c => c.Id_Contenedor);
            var sin = new List<HwTarjeta>();
            foreach (var t in tarjetas)
                if (t.Id_Contenedor is int ic && porCont.TryGetValue(ic, out var c)) c.Tarjetas.Add(t); else sin.Add(t);

            var slides = (await cn.QueryAsync<HwSlide>(@"
                SELECT c.id_carrusel AS Id_Carrusel, c.orden AS Orden, RTRIM(c.orientacion) AS Orientacion, c.ruta AS Ruta, c.activo AS Activo, c.url AS Url,
                       CAST(CASE WHEN EXISTS (SELECT 1 FROM Eurovip.carrusel x WHERE x.ruta = c.ruta AND x.id_idioma = c.id_idioma AND x.formato = '2') THEN 1 ELSE 0 END AS bit) AS TieneTablet,
                       CAST(CASE WHEN EXISTS (SELECT 1 FROM Eurovip.carrusel x WHERE x.ruta = c.ruta AND x.id_idioma = c.id_idioma AND x.formato = '3') THEN 1 ELSE 0 END AS bit) AS TieneMovil
                FROM Eurovip.carrusel c WHERE c.id_idioma = @Idioma AND ISNULL(c.formato, '1') = '1'
                ORDER BY c.activo DESC, c.orden, c.id_carrusel", new { Idioma })).ToList();
            var dest = (await cn.QueryAsync<HwSlideDestino>(@"
                SELECT id AS Id, id_carrusel AS Id_Carrusel, origen AS Origen, destino AS Destino, precio AS Precio, id_campanya AS Id_Campanya, regimen AS Regimen
                FROM Eurovip.carrucelDestino ORDER BY id")).ToLookup(d => d.Id_Carrusel);
            foreach (var s in slides) s.Destinos = dest[s.Id_Carrusel].ToList();

            return new HwDatos { Contenedores = conts, SinContenedor = sin, Slides = slides };
        }

        // ---------------------------------------------------------------- CONTENEDORES
        public async Task OrdenContenedoresAsync(List<int> ids)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            for (var i = 0; i < ids.Count; i++)
            {
                await cn.ExecuteAsync("UPDATE Eurovip.contenedores SET posicion = @p WHERE id_contenedor = @id AND UPPER(id_idioma) = @Idioma", new { p = i + 1, id = ids[i], Idioma }, tx);
                await cn.ExecuteAsync("UPDATE Eurovip.inicio SET posicion_contenedor = @p WHERE id_contenedor = @id AND id_idioma = @Idioma", new { p = i + 1, id = ids[i], Idioma }, tx);
            }
            tx.Commit();
        }

        public async Task<int> GuardarContenedorAsync(HwContenedorDto dto)
        {
            using var cn = _db.Conexion();
            var nombre = dto.Nombre.Trim().ToUpperInvariant();
            if (dto.Id_Contenedor is int id)
            {
                await cn.ExecuteAsync(@"UPDATE Eurovip.contenedores SET nombre = @nombre, estado = @Estado WHERE id_contenedor = @id;
                                        UPDATE Eurovip.inicio SET contenedor = @nombre WHERE id_contenedor = @id AND id_idioma = @Idioma;",
                    new { nombre, dto.Estado, id, Idioma });
                return id;
            }
            return await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO Eurovip.contenedores (nombre, posicion, estado, id_idioma)
                VALUES (@nombre, (SELECT ISNULL(MAX(posicion), 0) + 1 FROM Eurovip.contenedores WHERE UPPER(id_idioma) = @Idioma), @Estado, 'es');
                SELECT CAST(SCOPE_IDENTITY() AS int);", new { nombre, dto.Estado, Idioma });
        }

        /// <summary>Elimina la sección; sus tarjetas NO se borran: quedan "sin sección".</summary>
        public async Task<int> EliminarContenedorAsync(int id)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var n = await cn.ExecuteAsync(@"UPDATE Eurovip.inicio SET id_contenedor = NULL, contenedor = NULL, posicion_contenedor = NULL
                                            WHERE id_contenedor = @id AND id_idioma = @Idioma", new { id, Idioma }, tx);
            await cn.ExecuteAsync("DELETE FROM Eurovip.home_contenido WHERE id_contenedor = @id AND id_idioma = @Idioma", new { id, Idioma }, tx);
            await cn.ExecuteAsync("DELETE FROM Eurovip.contenedores WHERE id_contenedor = @id AND UPPER(id_idioma) = @Idioma", new { id, Idioma }, tx);
            tx.Commit();
            return n;
        }

        // ---------------------------------------------------------------- TARJETAS
        /// <summary>Pone las tarjetas en la sección y en ese orden; recuerda la ubicación en home_contenido.</summary>
        public async Task OrdenTarjetasAsync(HwOrdenDto dto)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var cont = dto.Id_Contenedor is int ic
                ? await cn.QueryFirstOrDefaultAsync<(int Id, string Nombre, int Posicion)>(
                    "SELECT id_contenedor, nombre, posicion FROM Eurovip.contenedores WHERE id_contenedor = @ic", new { ic }, tx)
                : default;
            for (var i = 0; i < dto.Ids.Count; i++)
            {
                var p = new { id = dto.Ids[i], pos = i + 1, ic = dto.Id_Contenedor, nom = dto.Id_Contenedor is null ? null : cont.Nombre, pc = dto.Id_Contenedor is null ? (int?)null : cont.Posicion, Idioma };
                await cn.ExecuteAsync(@"UPDATE Eurovip.inicio SET id_contenedor = @ic, contenedor = @nom, posicion_contenedor = @pc, posicion_destino = @pos
                                        WHERE id = @id AND id_idioma = @Idioma", p, tx);
                // memoria para spActualizaInicio (clave natural de la tarjeta)
                await cn.ExecuteAsync(@"
                    DELETE hc FROM Eurovip.home_contenido hc JOIN Eurovip.inicio i ON i.id = @id
                    WHERE hc.id_idioma = i.id_idioma AND ISNULL(hc.id_campanya, -1) = i.id_campanya AND hc.id_moneda = i.id_moneda
                      AND hc.id_pais_origen = i.id_pais_origen AND hc.origen = i.origen AND hc.id_destino = i.id_destino;
                    IF @ic IS NOT NULL
                        INSERT INTO Eurovip.home_contenido (id_idioma, id_contenedor, id_campanya, id_moneda, id_pais_origen, origen, id_destino, posicion, oferta_especial)
                        SELECT i.id_idioma, @ic, i.id_campanya, i.id_moneda, i.id_pais_origen, i.origen, i.id_destino, @pos, 0 FROM Eurovip.inicio i WHERE i.id = @id;", p, tx);
            }
            tx.Commit();
        }

        public async Task<bool?> EstadoTarjetaAsync(int id, bool estado)
        {
            using var cn = _db.Conexion();
            // Sin sección no se puede mostrar: el trigger trInicioSinSeccion la deja oculta (CAMBIO 61)
            if (estado && await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM Eurovip.inicio i
                    WHERE i.id = @id AND i.id_idioma = @Idioma AND (i.id_contenedor IS NULL
                          OR NOT EXISTS (SELECT 1 FROM Eurovip.contenedores c WHERE c.id_contenedor = i.id_contenedor))", new { id, Idioma }) > 0)
                throw new InvalidOperationException("Esta tarjeta no tiene sección: ponla en una sección y saldrá en la web sola.");
            var n = await cn.ExecuteAsync("UPDATE Eurovip.inicio SET estado = @estado WHERE id = @id AND id_idioma = @Idioma", new { id, estado, Idioma });
            return n == 0 ? null : estado;
        }

        public async Task<HwTarjeta?> TarjetaAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<HwTarjeta>(SelTarjeta + " WHERE i.id = @id AND i.id_idioma = @Idioma", new { id, Idioma });
        }

        public async Task<int> GuardarTarjetaAsync(HwTarjetaDto d)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var cont = d.Id_Contenedor is int ic
                ? await cn.QueryFirstOrDefaultAsync<(int Id, string Nombre, int Posicion)?>("SELECT id_contenedor, nombre, posicion FROM Eurovip.contenedores WHERE id_contenedor = @ic", new { ic }, tx)
                : null;
            var p = new DynamicParameters(new
            {
                tipo = d.Tipo_Servicio.Trim().ToUpperInvariant(), d.Id_Campanya, ps = d.Id_Pais_Origen.Trim().ToUpperInvariant(), origen = d.Origen.Trim(),
                pd = d.Id_PaisDestino.Trim().ToUpperInvariant(), destino = d.Id_Destino.Trim(), car = Vacio(d.CaratulaDestino), desc = d.Descripcion?.Trim() ?? "",
                d.PrecioWeb, ic = cont?.Id, nom = cont?.Nombre, pc = cont?.Posicion, d.Noches, notas = Vacio(d.Notas), d.Estado, Idioma
            });
            int id;
            if (d.Id is int existente)
            {
                p.Add("id", existente);
                await cn.ExecuteAsync(@"
                    UPDATE Eurovip.inicio SET tipo_servicio = @tipo, id_campanya = @Id_Campanya, id_pais_origen = @ps, origen = @origen, id_paisDestino = @pd,
                           id_destino = @destino, caratulaDestino = @car, descripcion = @desc, precioWeb = @PrecioWeb, noches = @Noches, notas = @notas, estado = @Estado,
                           posicion_destino = CASE WHEN ISNULL(id_contenedor, -1) = ISNULL(@ic, -1) THEN posicion_destino
                                                   ELSE (SELECT ISNULL(MAX(posicion_destino), 0) + 1 FROM Eurovip.inicio WHERE id_contenedor = @ic) END,
                           id_contenedor = @ic, contenedor = @nom, posicion_contenedor = @pc
                    WHERE id = @id AND id_idioma = @Idioma", p, tx);
                id = existente;
            }
            else
            {
                // tarjeta manual: id_servicio propio (negativo) para no confundirse con ofertas reales
                id = await cn.ExecuteScalarAsync<int>(@"
                    DECLARE @sid int = (SELECT ISNULL(MIN(CASE WHEN id_servicio < 0 THEN id_servicio END), 0) - 1 FROM Eurovip.inicio WITH (UPDLOCK, HOLDLOCK));
                    INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino, id_destino,
                           caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor, posicion_contenedor, posicion_destino, estado, notas, noches)
                    SELECT @sid, @Idioma, @tipo, @Id_Campanya, 'DOP', @ps, @origen, @pd, @destino, @car, @desc, d.latitud, d.longitud, @PrecioWeb, @ic, @nom, @pc,
                           (SELECT ISNULL(MAX(posicion_destino), 0) + 1 FROM Eurovip.inicio WHERE id_contenedor = @ic), @Estado, @notas, @Noches
                    FROM (SELECT 1 x) uno LEFT JOIN Eurovip.destino d ON d.id_destino = @destino AND d.id_pais = @pd AND d.id_idioma = @Idioma;
                    SELECT CAST(SCOPE_IDENTITY() AS int);", p, tx);
            }
            tx.Commit();
            return id;
        }

        public async Task<bool> EliminarTarjetaAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("DELETE FROM Eurovip.inicio WHERE id = @id AND id_idioma = @Idioma", new { id, Idioma }) > 0;
        }

        /// <summary>
        /// Circuitos y destinos de ofertas cargados que NO tienen tarjeta visible en la Home, con el motivo en palabras sencillas.
        /// "A la venta" = salidas activas desde mañana (igual que fnServicioMinimo / fnServicioMinimoCircuito).
        /// </summary>
        public async Task<List<HwFuera>> FueraDeHomeAsync()
        {
            using var cn = _db.Conexion();
            var filas = (await cn.QueryAsync<HwFuera>(@"
                SELECT 'CIRCUITO' AS Tipo, c.nombre_circuito AS Nombre, c.id AS Codigo,
                       ISNULL(SUM(CASE WHEN s.activo = 1 THEN 1 ELSE 0 END), 0) AS Total,
                       MIN(CASE WHEN s.activo = 1 AND sc.desde > CAST(GETDATE() AS date) THEN sc.desde END) AS Proxima,
                       MAX(CASE WHEN s.activo = 1 THEN sc.desde END) AS Ultima, CAST(NULL AS nvarchar(300)) AS Motivo
                FROM Eurovip.circuito c
                    LEFT JOIN Eurovip.servicio_circuito sc ON sc.nombre_circuito = c.nombre_circuito AND sc.id_idoma = c.id_idioma
                    LEFT JOIN Eurovip.servicio s ON s.id_servicio = sc.id_servicio
                WHERE c.id_idioma = @Idioma
                  AND NOT EXISTS (SELECT 1 FROM Eurovip.inicio i JOIN Eurovip.servicio_circuito x ON x.id_servicio = i.id_servicio
                                  WHERE i.tipo_servicio = 'CIRCUITO' AND ISNULL(i.estado, 1) = 1 AND x.nombre_circuito = c.nombre_circuito)
                GROUP BY c.nombre_circuito, c.id
                UNION ALL
                SELECT 'OFERTA', d.destino, NULL,
                       SUM(CASE WHEN s.activo = 1 THEN 1 ELSE 0 END),
                       MIN(CASE WHEN s.activo = 1 AND sd.desde > CAST(GETDATE() AS date) THEN sd.desde END),
                       MAX(CASE WHEN s.activo = 1 THEN sd.desde END), NULL
                FROM Eurovip.servicio_destino d
                    JOIN Eurovip.servicio s ON s.id_servicio = d.id_servicio
                    JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio
                WHERE s.id_tipo_servicio = 'OFERTA' AND s.id_idioma = @Idioma
                  AND NOT EXISTS (SELECT 1 FROM Eurovip.inicio i WHERE i.tipo_servicio = 'OFERTA' AND ISNULL(i.estado, 1) = 1 AND i.id_destino = d.destino)
                GROUP BY d.destino
                UNION ALL
                -- Hoteles (tarjetas manuales de /Hoteles): no salen si están ocultos o no tienen sección de la web
                SELECT 'HOTEL', LTRIM(RTRIM(i.id_destino)), CAST(i.id_servicio AS varchar(20)), 0, NULL, NULL,
                       CASE WHEN ISNULL(i.estado, 1) = 0 THEN 'Está oculto: tiene apagado «Visible en la web».'
                            ELSE 'No tiene sección de la web: elige en qué sección va (Hotel, Islas…).' END
                FROM Eurovip.inicio i
                WHERE i.tipo_servicio = 'HOTEL' AND i.id_idioma = @Idioma AND (ISNULL(i.estado, 1) = 0 OR i.id_contenedor IS NULL)", new { Idioma })).ToList();

            var manana = DateTime.Today.AddDays(1).ToString("dd/MM/yyyy");
            foreach (var f in filas)
            {
                if (f.Tipo == "HOTEL")
                {
                    (f.Accion, f.Url) = ("Editar hotel", "/Hoteles/Index?editar=" + f.Codigo);
                    continue;
                }
                var que = f.Tipo == "CIRCUITO" ? "salidas" : "fechas";
                if (f.Total == 0)
                    f.Motivo = f.Tipo == "CIRCUITO" ? "No tiene salidas activas cargadas." : "No tiene ofertas activas.";
                else if (f.Proxima is null)
                    f.Motivo = $"Sus {que} son de hoy o ya pasaron (la última: {f.Ultima:dd/MM/yyyy}). Solo sale en la Home lo que tiene {que} desde mañana ({manana}).";
                else
                    f.Motivo = $"Tiene {que} a la venta (la próxima: {f.Proxima:dd/MM/yyyy}), pero en la Home va solo lo más barato de cada " +
                               (f.Tipo == "CIRCUITO" ? "país" : "destino") + ". Si no aparece, pulsa \"Traer ofertas a la Home\".";
                if (f.Proxima is null)
                    (f.Accion, f.Url) = f.Tipo == "CIRCUITO"
                        ? ("Cargar salidas", "/CargaCircuitos/Express?circuito=" + Uri.EscapeDataString(f.Codigo ?? f.Nombre))
                        : ("Cargar ofertas", "/CargaManual/Express");
            }
            return filas.OrderBy(f => f.Proxima is null && f.Tipo != "HOTEL" ? 0 : f.Tipo == "HOTEL" ? 1 : 2).ThenBy(f => f.Tipo).ThenBy(f => f.Nombre).ToList();
        }

        /// <summary>Claves (tipo, campaña, destino, origen…) de todo lo que está a la venta: lo que puede ir a la Home.</summary>
        public async Task<IEnumerable<HwActualizarDto>> ClavesOfertasAsync()
        {
            using var cn = _db.Conexion();
            return await cn.QueryAsync<HwActualizarDto>(@"
                SELECT DISTINCT 'OFERTA' AS Tipo_Servicio, s.id_campanya AS Id_Campanya, a.id_destino AS Id_Destino, so.origen AS Origen,
                       so.id_pais_origen AS Id_Pais_Origen, s.id_moneda AS Id_Moneda
                FROM Eurovip.servicio s
                    JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio
                    JOIN Eurovip.alojamiento a ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
                    JOIN Eurovip.servicio_origen so ON so.id_servicio = s.id_servicio
                WHERE s.activo = 1 AND s.id_tipo_servicio = 'OFERTA' AND s.id_idioma = @Idioma AND sd.desde > GETDATE()
                UNION
                SELECT DISTINCT 'CIRCUITO', s.id_campanya, cd.id_pais, sc.origen, sc.id_pais_origen, s.id_moneda
                FROM Eurovip.servicio s
                    JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
                    JOIN Eurovip.circuito_destino cd ON cd.nombre_circuito = sc.nombre_circuito AND cd.id_idioma = sc.id_idoma
                WHERE s.activo = 1 AND s.id_tipo_servicio = 'CIRCUITO' AND s.id_idioma = @Idioma AND sc.desde > GETDATE() AND sc.origen IS NOT NULL",
                new { Idioma });
        }

        /// <summary>Ejecuta Eurovip.spActualizaInicio para una clave o para todo lo que está a la venta. Devuelve cuántas claves se refrescaron.</summary>
        public async Task<int> ActualizarDesdeOfertasAsync(HwActualizarDto? solo)
        {
            var claves = solo is null ? (await ClavesOfertasAsync()).ToList() : new List<HwActualizarDto> { solo };
            using var cn = _db.Conexion();
            foreach (var k in claves)
                await cn.ExecuteAsync("Eurovip.spActualizaInicio", new
                {
                    tipoServicio = k.Tipo_Servicio, idCampanya = k.Id_Campanya, destino = k.Id_Destino, idIdioma = Idioma,
                    idMoneda = k.Id_Moneda, origen = k.Origen, idPaisOrigen = k.Id_Pais_Origen
                }, commandType: CommandType.StoredProcedure, commandTimeout: 120);
            return claves.Count;
        }

        // ---------------------------------------------------------------- CARRUSEL
        public async Task OrdenSlidesAsync(List<int> ids)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            for (var i = 0; i < ids.Count; i++)
                await cn.ExecuteAsync("UPDATE Eurovip.carrusel SET orden = @o WHERE id_carrusel = @id AND id_idioma = @Idioma AND ISNULL(formato, '1') = '1'",
                    new { o = i + 1, id = ids[i], Idioma }, tx);
            tx.Commit();
        }

        public async Task<bool> EditarSlideAsync(HwSlideDto d)
        {
            using var cn = _db.Conexion();
            // la orientación se replica a tablet y móvil (mismo archivo)
            return await cn.ExecuteAsync(@"
                UPDATE c2 SET c2.orientacion = @or FROM Eurovip.carrusel c1 JOIN Eurovip.carrusel c2 ON c2.ruta = c1.ruta AND c2.id_idioma = c1.id_idioma
                WHERE c1.id_carrusel = @Id_Carrusel;
                UPDATE Eurovip.carrusel SET url = @url, activo = @Activo WHERE id_carrusel = @Id_Carrusel AND ISNULL(formato, '1') = '1';",
                new { d.Id_Carrusel, or = d.Orientacion == "V" ? "V" : "H", url = Vacio(d.Url), d.Activo }) > 0;
        }

        public async Task<bool> ExisteRutaAsync(string ruta)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.carrusel WHERE ruta = @ruta AND id_idioma = @Idioma", new { ruta, Idioma }) > 0;
        }

        /// <summary>Crea el slide: fila web (formato 1, al final del orden) + filas tablet (2) y móvil (3) inactivas, como BackTiTan.</summary>
        public async Task<int> CrearSlideAsync(string ruta, string orientacion, string? url, bool activo)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var or = orientacion == "V" ? "V" : "H";
            var id = await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO Eurovip.carrusel (id_idioma, orden, orientacion, ruta, activo, url, formato)
                VALUES (@Idioma, (SELECT ISNULL(MAX(orden), 0) + 1 FROM Eurovip.carrusel WHERE id_idioma = @Idioma AND ISNULL(formato, '1') = '1'), @or, @ruta, @activo, @url, '1');
                SELECT CAST(SCOPE_IDENTITY() AS int);", new { Idioma, or, ruta, activo, url = Vacio(url) }, tx);
            await cn.ExecuteAsync(@"INSERT INTO Eurovip.carrusel (id_idioma, orden, orientacion, ruta, activo, url, formato)
                                    VALUES (@Idioma, -1, @or, @ruta, 0, NULL, '2'), (@Idioma, -1, @or, @ruta, 0, NULL, '3');", new { Idioma, or, ruta }, tx);
            tx.Commit();
            return id;
        }

        public async Task<HwSlide?> SlideAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<HwSlide>(@"SELECT id_carrusel AS Id_Carrusel, orden AS Orden, RTRIM(orientacion) AS Orientacion, ruta AS Ruta, activo AS Activo, url AS Url
                FROM Eurovip.carrusel WHERE id_carrusel = @id AND id_idioma = @Idioma", new { id, Idioma });
        }

        /// <summary>Borra el slide con sus copias tablet/móvil y sus precios; reordena. Devuelve el nombre del archivo.</summary>
        public async Task<string?> EliminarSlideAsync(int id)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var ruta = await cn.ExecuteScalarAsync<string?>("SELECT ruta FROM Eurovip.carrusel WHERE id_carrusel = @id AND id_idioma = @Idioma", new { id, Idioma }, tx);
            if (ruta is null) { tx.Rollback(); return null; }
            await cn.ExecuteAsync(@"
                DELETE FROM Eurovip.carrucelDestino WHERE id_carrusel IN (SELECT id_carrusel FROM Eurovip.carrusel WHERE ruta = @ruta AND id_idioma = @Idioma);
                DELETE FROM Eurovip.carrusel WHERE ruta = @ruta AND id_idioma = @Idioma;
                WITH o AS (SELECT id_carrusel, ROW_NUMBER() OVER (ORDER BY orden, id_carrusel) AS n FROM Eurovip.carrusel WHERE id_idioma = @Idioma AND ISNULL(formato, '1') = '1')
                UPDATE c SET c.orden = o.n FROM Eurovip.carrusel c JOIN o ON o.id_carrusel = c.id_carrusel;", new { ruta, Idioma }, tx);
            tx.Commit();
            return ruta;
        }

        public async Task<int> GuardarSlideDestinoAsync(HwSlideDestinoDto d)
        {
            using var cn = _db.Conexion();
            var p = new { d.Id, d.Id_Carrusel, origen = d.Origen.Trim(), destino = d.Destino.Trim(), d.Precio, d.Id_Campanya, regimen = Vacio(d.Regimen) };
            if (d.Id is int id)
            {
                await cn.ExecuteAsync(@"UPDATE Eurovip.carrucelDestino SET origen = @origen, destino = @destino, precio = @Precio, id_campanya = @Id_Campanya, regimen = @regimen
                                        WHERE id = @Id", p);
                return id;
            }
            return await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.carrucelDestino (id_carrusel, origen, destino, precio, id_campanya, regimen)
                VALUES (@Id_Carrusel, @origen, @destino, @Precio, @Id_Campanya, @regimen); SELECT CAST(SCOPE_IDENTITY() AS int);", p);
        }

        public async Task<bool> EliminarSlideDestinoAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("DELETE FROM Eurovip.carrucelDestino WHERE id = @id", new { id }) > 0;
        }

        /// <summary>Precio web más bajo de las ofertas de Eurovip a la venta para ese destino/origen/campaña (y régimen, si se indica).</summary>
        public async Task<decimal?> PrecioMinimoAsync(string origen, string destino, int idCampanya, string? regimen)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteScalarAsync<decimal?>(@"
                SELECT MIN(Eurovip.fnPrecioWeb(s.id_servicio))
                FROM Eurovip.servicio s
                    JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio
                    JOIN Eurovip.alojamiento a ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
                    JOIN Eurovip.servicio_origen so ON so.id_servicio = s.id_servicio
                    LEFT JOIN Eurovip.regimen r ON r.id_regimen = sd.id_regimen AND r.id_idioma = sd.id_idioma
                WHERE s.activo = 1 AND s.id_idioma = @Idioma AND sd.desde > GETDATE() AND s.id_campanya = @idCampanya
                  AND a.id_destino = @destino AND so.origen = @origen
                  AND (@regimen IS NULL OR r.descripcion = @regimen OR sd.id_regimen = @regimen)",
                new { Idioma, origen, destino, idCampanya, regimen = Vacio(regimen) });
        }

        /// <summary>Recalcula el precio "desde" de todos los slides con lo que hay a la venta (lo que BackTiTan hace cada 30 min).</summary>
        public async Task<int> RecalcularPreciosSlidesAsync()
        {
            using var cn = _db.Conexion();
            var filas = (await cn.QueryAsync<HwSlideDestino>("SELECT id AS Id, origen AS Origen, destino AS Destino, id_campanya AS Id_Campanya, regimen AS Regimen FROM Eurovip.carrucelDestino")).ToList();
            var n = 0;
            foreach (var f in filas)
            {
                var precio = await PrecioMinimoAsync(f.Origen, f.Destino, f.Id_Campanya, f.Regimen);
                if (precio is > 0)
                    n += await cn.ExecuteAsync("UPDATE Eurovip.carrucelDestino SET precio = ROUND(@precio, 0) WHERE id = @Id", new { precio, f.Id });
            }
            return n;
        }

        private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
