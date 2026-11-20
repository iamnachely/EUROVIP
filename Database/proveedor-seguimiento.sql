-- Cargas de proveedores: seguimiento interno (20/11/2026, pedido de la usuaria).
-- Para que el equipo de carga se organice: NOTAS INTERNAS en cada carga (nunca las ve el proveedor) y, por carga, quién la lleva
-- (responsable) y su prioridad. La conversación con el proveedor sigue en Eurovip.carga_proveedor_comentario (no cambia).
-- Aplicar en TropitoursVDT (esquema Eurovip). Es idempotente. Mientras no existan las tablas, la pantalla funciona igual y
-- avisa que las notas y el responsable todavía no están activados.
IF OBJECT_ID('Eurovip.carga_proveedor_nota', 'U') IS NULL
BEGIN
    CREATE TABLE Eurovip.carga_proveedor_nota (
        id        int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_carga_proveedor_nota PRIMARY KEY,
        id_carga  int            NOT NULL,                       -- Eurovip.carga_proveedor
        autor     nvarchar(150)  NOT NULL,                       -- quién la escribió (usuario del BackEurovip)
        texto     nvarchar(2000) NOT NULL,
        fijada    bit            NOT NULL CONSTRAINT DF_carga_proveedor_nota_fijada DEFAULT 0,
        creado_en datetime2(0)   NOT NULL CONSTRAINT DF_carga_proveedor_nota_creado DEFAULT SYSDATETIME()
    );
    CREATE INDEX IX_carga_proveedor_nota_carga ON Eurovip.carga_proveedor_nota (id_carga, id);
END
GO
IF OBJECT_ID('Eurovip.carga_proveedor_gestion', 'U') IS NULL
BEGIN
    CREATE TABLE Eurovip.carga_proveedor_gestion (
        id_carga       int           NOT NULL CONSTRAINT PK_carga_proveedor_gestion PRIMARY KEY,
        asignado_a     nvarchar(150) NULL,                       -- quién la lleva
        prioridad      tinyint       NOT NULL CONSTRAINT DF_carga_proveedor_gestion_prio DEFAULT 0,   -- 0 normal · 1 alta · 2 urgente
        actualizado_en datetime2(0)  NOT NULL CONSTRAINT DF_carga_proveedor_gestion_fecha DEFAULT SYSDATETIME(),
        actualizado_por nvarchar(150) NULL
    );
END
GO
