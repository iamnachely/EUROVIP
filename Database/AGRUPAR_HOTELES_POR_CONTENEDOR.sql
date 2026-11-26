/* ============================================================================
   EUROVIP - Ofertas/Hoteles: elegir en QUÉ CONTENEDOR sale cada oferta (agrupa por destino)
   BASE: TropitoursVDT  |  ESQUEMA: Eurovip
   Fecha: 26/11/2026   (probar primero en Desarrollo, luego correr en 10.10.10.29)

   REGLA:
   - Sin elegir nada (id_contenedor_inicio = NULL) todo funciona IGUAL que antes: la tarjeta del destino
     sale en el contenedor que ya tiene (HOTELES (BLOQUEOS), etc.).
   - Si una oferta/hotel tiene id_contenedor_inicio, su destino sale como tarjeta en ESE contenedor
     (una tarjeta por destino dentro del contenedor, con la oferta más barata de ese grupo).
   Es idempotente: se puede correr varias veces. No borra datos.
   ============================================================================ */
USE [TropitoursVDT];
SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON;
GO
PRINT 'Servidor: ' + @@SERVERNAME + ' | Base: ' + DB_NAME();
GO

-- 1) Columna nueva: contenedor elegido para la oferta (NULL = automático)
IF COL_LENGTH('Eurovip.servicio', 'id_contenedor_inicio') IS NULL
    ALTER TABLE Eurovip.servicio ADD id_contenedor_inicio int NULL;
GO

-- 2) La tarjeta automática NO incluye las ofertas que eligieron otro contenedor
CREATE OR ALTER FUNCTION Eurovip.fnServicioMinimo
(
    @tipoServicio VARCHAR(30), @idCampanya SMALLINT, @destino VARCHAR(MAX), @idIdioma CHAR(2),
    @idMoneda CHAR(3), @origen VARCHAR(30), @idPaisOrigen CHAR(2)
)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1
        s.id_servicio, s.id_idioma, s.id_tipo_servicio, s.id_campanya, s.id_moneda, so.id_pais_origen, so.origen,
        a.id_pais, a.id_destino, a.caratula,
        r.descripcion, d.latitud, d.longitud, Eurovip.fnPrecioWeb(s.id_servicio) AS precioWeb,
        hc.id_contenedor, cont.nombre AS nombre_contenedor, cont.posicion AS posicion_contenedor,
        hc.posicion AS posicion_destino
    FROM Eurovip.servicio s
        INNER JOIN Eurovip.servicio_detalle sd ON s.id_servicio = sd.id_servicio
        INNER JOIN Eurovip.alojamiento a ON sd.id_alojamiento = a.id_alojamiento AND sd.id_idioma = a.id_idioma
        INNER JOIN Eurovip.destino d ON a.id_destino = d.id_destino AND a.id_idioma = d.id_idioma AND a.id_pais = d.id_pais
        INNER JOIN Eurovip.servicio_origen so ON s.id_servicio = so.id_servicio
        INNER JOIN Eurovip.regimen r ON sd.id_regimen = r.id_regimen AND sd.id_idioma = r.id_idioma
        LEFT JOIN Eurovip.home_contenido hc
            ON hc.id_idioma = s.id_idioma AND hc.id_campanya = s.id_campanya AND hc.id_moneda = s.id_moneda
           AND hc.id_pais_origen = so.id_pais_origen AND hc.origen = so.origen AND hc.id_destino = a.id_destino
        LEFT JOIN Eurovip.contenedores cont ON cont.id_contenedor = hc.id_contenedor
    WHERE s.activo = 1
      AND s.id_contenedor_inicio IS NULL          -- las que eligieron contenedor salen en el suyo
      AND s.id_tipo_servicio = @tipoServicio
      AND s.id_campanya = @idCampanya
      AND a.id_destino = @destino
      AND s.id_idioma = @idIdioma
      AND s.id_moneda = @idMoneda
      AND so.origen = @origen
      AND so.id_pais_origen = @idPaisOrigen
      AND sd.desde > GETDATE()
    ORDER BY Eurovip.fnPrecioWeb(s.id_servicio) ASC
);
GO

-- 3) La oferta más barata de un destino entre las que eligieron un contenedor concreto
CREATE OR ALTER FUNCTION Eurovip.fnServicioMinimoContenedor
(
    @tipoServicio VARCHAR(30), @idCampanya SMALLINT, @destino VARCHAR(MAX), @idIdioma CHAR(2),
    @idMoneda CHAR(3), @origen VARCHAR(30), @idPaisOrigen CHAR(2), @idContenedor int
)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1
        s.id_servicio, s.id_idioma, s.id_tipo_servicio, s.id_campanya, s.id_moneda, so.id_pais_origen, so.origen,
        a.id_pais, a.id_destino, a.caratula,
        r.descripcion, d.latitud, d.longitud, Eurovip.fnPrecioWeb(s.id_servicio) AS precioWeb,
        cont.id_contenedor, cont.nombre AS nombre_contenedor, cont.posicion AS posicion_contenedor,
        hc.posicion AS posicion_destino
    FROM Eurovip.servicio s
        INNER JOIN Eurovip.servicio_detalle sd ON s.id_servicio = sd.id_servicio
        INNER JOIN Eurovip.alojamiento a ON sd.id_alojamiento = a.id_alojamiento AND sd.id_idioma = a.id_idioma
        INNER JOIN Eurovip.destino d ON a.id_destino = d.id_destino AND a.id_idioma = d.id_idioma AND a.id_pais = d.id_pais
        INNER JOIN Eurovip.servicio_origen so ON s.id_servicio = so.id_servicio
        INNER JOIN Eurovip.regimen r ON sd.id_regimen = r.id_regimen AND sd.id_idioma = r.id_idioma
        INNER JOIN Eurovip.contenedores cont ON cont.id_contenedor = @idContenedor AND cont.id_idioma = s.id_idioma
        LEFT JOIN Eurovip.home_contenido hc
            ON hc.id_idioma = s.id_idioma AND hc.id_campanya = s.id_campanya AND hc.id_moneda = s.id_moneda
           AND hc.id_pais_origen = so.id_pais_origen AND hc.origen = so.origen AND hc.id_destino = a.id_destino
           AND hc.id_contenedor = @idContenedor
    WHERE s.activo = 1
      AND s.id_contenedor_inicio = @idContenedor
      AND s.id_tipo_servicio = @tipoServicio
      AND s.id_campanya = @idCampanya
      AND a.id_destino = @destino
      AND s.id_idioma = @idIdioma
      AND s.id_moneda = @idMoneda
      AND so.origen = @origen
      AND so.id_pais_origen = @idPaisOrigen
      AND sd.desde > GETDATE()
    ORDER BY Eurovip.fnPrecioWeb(s.id_servicio) ASC
);
GO

-- 4) El procedimiento que arma las tarjetas: la automática + una por cada contenedor elegido
CREATE OR ALTER PROCEDURE Eurovip.spActualizaInicio
    @tipoServicio VARCHAR(30), @idCampanya SMALLINT, @destino VARCHAR(MAX), @idIdioma CHAR(2),
    @idMoneda CHAR(3), @origen VARCHAR(30), @idPaisOrigen CHAR(2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;

    -- todo lo que el usuario había organizado en las tarjetas de esta clave (puede haber varias: una por contenedor)
    DECLARE @prev TABLE (id_viejo INT, caratula_vieja VARCHAR(300), estado BIT, id_contenedor INT, contenedor NVARCHAR(100), posicion_contenedor INT, posicion_destino INT, peso SMALLINT, precio_proveedor DECIMAL(12, 2), es_grupo bit);
    INSERT INTO @prev
    SELECT id, caratulaDestino, estado, id_contenedor, contenedor, posicion_contenedor, posicion_destino, peso, precio_proveedor,
           CASE WHEN notas = 'GRUPO' THEN 1 ELSE 0 END   -- las tarjetas de un contenedor elegido se marcan con notas = 'GRUPO'
    FROM Eurovip.inicio
    WHERE tipo_servicio = @tipoServicio AND id_campanya = @idCampanya AND id_destino = @destino AND id_idioma = @idIdioma
      AND id_moneda = @idMoneda AND origen = @origen AND id_pais_origen = @idPaisOrigen;

    -- contenedores elegidos por las ofertas de este destino
    DECLARE @elegidos TABLE (id_contenedor int PRIMARY KEY);
    IF @tipoServicio <> 'CIRCUITO'
        INSERT INTO @elegidos
        SELECT DISTINCT s.id_contenedor_inicio
        FROM Eurovip.servicio s
            INNER JOIN Eurovip.servicio_detalle sd ON s.id_servicio = sd.id_servicio
            INNER JOIN Eurovip.alojamiento a ON sd.id_alojamiento = a.id_alojamiento AND sd.id_idioma = a.id_idioma
            INNER JOIN Eurovip.servicio_origen so ON s.id_servicio = so.id_servicio
        WHERE s.activo = 1 AND s.id_contenedor_inicio IS NOT NULL AND s.id_tipo_servicio = @tipoServicio AND s.id_campanya = @idCampanya
          AND a.id_destino = @destino AND s.id_idioma = @idIdioma AND s.id_moneda = @idMoneda AND so.origen = @origen
          AND so.id_pais_origen = @idPaisOrigen AND sd.desde > GETDATE();

    -- la tarjeta automática conserva lo de la fila más antigua que no sea de un contenedor elegido (o la más antigua)
    DECLARE @auto TABLE (id_viejo INT, caratula_vieja VARCHAR(300), estado BIT, id_contenedor INT, contenedor NVARCHAR(100), posicion_contenedor INT, posicion_destino INT, peso SMALLINT, precio_proveedor DECIMAL(12, 2), es_grupo bit);
    INSERT INTO @auto
    SELECT TOP 1 * FROM @prev WHERE es_grupo = 0 ORDER BY id_viejo;   -- solo la tarjeta automática (si se quitó el contenedor elegido, vuelve al suyo)

    DELETE FROM Eurovip.inicio
    WHERE tipo_servicio = @tipoServicio AND id_campanya = @idCampanya AND id_destino = @destino AND id_idioma = @idIdioma
      AND id_moneda = @idMoneda AND origen = @origen AND id_pais_origen = @idPaisOrigen;

    IF @tipoServicio = 'CIRCUITO'
        INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino,
               id_destino, caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor,
               posicion_contenedor, posicion_destino, estado, noches, notas)
        SELECT id_servicio, id_idioma, id_tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_pais,
               paisDestino, caratula, nombre_circuito, latitud, longitud, precioWeb, id_contenedor, contenedor,
               posicion_contenedor, posicion_destino, 1, NULL, NULL
        FROM Eurovip.fnServicioMinimoCircuito(@idCampanya, @idIdioma, @idMoneda, @origen, @idPaisOrigen, @destino);
    ELSE
        INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino,
               id_destino, caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor,
               posicion_contenedor, posicion_destino, estado, noches, notas)
        SELECT id_servicio, id_idioma, id_tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_pais,
               id_destino, caratula, descripcion, latitud, longitud, precioWeb, id_contenedor, nombre_contenedor,
               posicion_contenedor, posicion_destino, 1, NULL, NULL
        FROM Eurovip.fnServicioMinimo(@tipoServicio, @idCampanya, @destino, @idIdioma, @idMoneda, @origen, @idPaisOrigen);

    -- restaurar lo que el usuario había organizado en la tarjeta automática
    UPDATE i SET i.estado = p.estado,
                 i.id_contenedor = ISNULL(p.id_contenedor, i.id_contenedor), i.contenedor = ISNULL(p.contenedor, i.contenedor),
                 i.posicion_contenedor = ISNULL(p.posicion_contenedor, i.posicion_contenedor),
                 i.posicion_destino = ISNULL(p.posicion_destino, i.posicion_destino),
                 i.peso = p.peso, i.precio_proveedor = p.precio_proveedor
    FROM Eurovip.inicio i CROSS JOIN @auto p
    WHERE i.tipo_servicio = @tipoServicio AND i.id_campanya = @idCampanya AND i.id_destino = @destino AND i.id_idioma = @idIdioma
      AND i.id_moneda = @idMoneda AND i.origen = @origen AND i.id_pais_origen = @idPaisOrigen;

    -- una tarjeta por cada contenedor elegido (si la automática ya cayó en ese contenedor, no se duplica)
    DECLARE @c int;
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT id_contenedor FROM @elegidos;
    OPEN cur; FETCH NEXT FROM cur INTO @c;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM Eurovip.inicio WHERE tipo_servicio = @tipoServicio AND id_campanya = @idCampanya AND id_destino = @destino
                          AND id_idioma = @idIdioma AND id_moneda = @idMoneda AND origen = @origen AND id_pais_origen = @idPaisOrigen AND id_contenedor = @c)
        BEGIN
            INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino,
                   id_destino, caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor,
                   posicion_contenedor, posicion_destino, estado, noches, notas)
            SELECT id_servicio, id_idioma, id_tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_pais,
                   id_destino, caratula, descripcion, latitud, longitud, precioWeb, id_contenedor, nombre_contenedor,
                   posicion_contenedor, posicion_destino, 1, NULL, NULL
            FROM Eurovip.fnServicioMinimoContenedor(@tipoServicio, @idCampanya, @destino, @idIdioma, @idMoneda, @origen, @idPaisOrigen, @c);

            UPDATE Eurovip.inicio SET notas = 'GRUPO'
             WHERE tipo_servicio = @tipoServicio AND id_campanya = @idCampanya AND id_destino = @destino AND id_idioma = @idIdioma
               AND id_moneda = @idMoneda AND origen = @origen AND id_pais_origen = @idPaisOrigen AND id_contenedor = @c;

            UPDATE i SET i.estado = p.estado, i.posicion_contenedor = ISNULL(p.posicion_contenedor, i.posicion_contenedor),
                         i.posicion_destino = ISNULL(p.posicion_destino, i.posicion_destino), i.peso = p.peso, i.precio_proveedor = p.precio_proveedor
            FROM Eurovip.inicio i JOIN @prev p ON p.id_contenedor = i.id_contenedor AND p.es_grupo = 1
            WHERE i.tipo_servicio = @tipoServicio AND i.id_campanya = @idCampanya AND i.id_destino = @destino AND i.id_idioma = @idIdioma
              AND i.id_moneda = @idMoneda AND i.origen = @origen AND i.id_pais_origen = @idPaisOrigen AND i.id_contenedor = @c;
        END
        FETCH NEXT FROM cur INTO @c;
    END
    CLOSE cur; DEALLOCATE cur;

    -- paquetes: la foto del DESTINO si tiene portada en multimedia (todas las tarjetas del destino)
    IF @tipoServicio IN ('OFERTA', 'PAQUETES', 'COMBINADO')
        UPDATE i SET i.caratulaDestino = d.caratula
          FROM Eurovip.inicio i JOIN Eurovip.destino d ON d.id_destino = i.id_destino
         WHERE i.tipo_servicio = @tipoServicio AND i.id_campanya = @idCampanya AND i.id_destino = @destino AND i.id_idioma = @idIdioma
           AND i.id_moneda = @idMoneda AND i.origen = @origen AND i.id_pais_origen = @idPaisOrigen
           AND ISNULL(d.caratula, '') <> ''
           AND EXISTS (SELECT 1 FROM Eurovip.vMultimedia v WHERE v.tipo = 'DESTINO' AND v.id_destino = d.id_destino AND v.uso = 'PORTADA');

    -- la foto propia de la tarjeta (Eurovip.multimedia tipo INICIO) pasa a la tarjeta nueva (la automática)
    DECLARE @idNuevo int = (SELECT TOP 1 i.id FROM Eurovip.inicio i
                             WHERE i.tipo_servicio = @tipoServicio AND i.id_campanya = @idCampanya AND i.id_destino = @destino AND i.id_idioma = @idIdioma
                               AND i.id_moneda = @idMoneda AND i.origen = @origen AND i.id_pais_origen = @idPaisOrigen
                             ORDER BY CASE WHEN i.id_contenedor = (SELECT TOP 1 id_contenedor FROM @auto) THEN 0 ELSE 1 END, i.id);
    IF @idNuevo IS NOT NULL AND EXISTS (SELECT 1 FROM Eurovip.multimedia m JOIN @auto p ON m.id_inicio = p.id_viejo)
    BEGIN
        UPDATE m SET m.id_inicio = @idNuevo FROM Eurovip.multimedia m JOIN @auto p ON m.id_inicio = p.id_viejo;
        UPDATE i SET i.caratulaDestino = p.caratula_vieja FROM Eurovip.inicio i CROSS JOIN @auto p WHERE i.id = @idNuevo;
    END

    COMMIT;
END
GO

-- 5) Verificación
SELECT 'columna' AS cosa, CASE WHEN COL_LENGTH('Eurovip.servicio','id_contenedor_inicio') IS NOT NULL THEN 'OK' ELSE 'FALTA' END AS estado
UNION ALL SELECT 'fnServicioMinimoContenedor', CASE WHEN OBJECT_ID('Eurovip.fnServicioMinimoContenedor') IS NOT NULL THEN 'OK' ELSE 'FALTA' END
UNION ALL SELECT 'spActualizaInicio', CASE WHEN OBJECT_DEFINITION(OBJECT_ID('Eurovip.spActualizaInicio')) LIKE '%@elegidos%' THEN 'OK' ELSE 'FALTA' END;
GO
