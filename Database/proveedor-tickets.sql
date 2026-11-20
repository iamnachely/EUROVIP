-- Proveedores: sistema de TICKETS (20/11/2026, pedido de la usuaria).
-- El proveedor reporta un error o un fallo (con documentos y capturas) desde Gestión; Eurovip lo atiende en
-- BackOffice › Cargas de proveedores › Tickets. Cada ticket tiene su conversación, su estado y su calificación.
-- Los archivos NO van a la base: se guardan en App_Data/tickets (fuera de wwwroot) y aquí solo queda su nombre.
-- Aplicar en TropitoursVDT (esquema Eurovip). Es idempotente. Mientras no existan las tablas, la pantalla avisa y todo lo demás funciona.
IF OBJECT_ID('Eurovip.proveedor_ticket', 'U') IS NULL
BEGIN
    CREATE TABLE Eurovip.proveedor_ticket (
        id_ticket       int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_proveedor_ticket PRIMARY KEY,
        id_cuenta       int            NOT NULL,                        -- Eurovip.proveedor_cuenta
        categoria       varchar(15)    NOT NULL,                        -- error | fallo | carga | acceso | duda | otro
        asunto          nvarchar(150)  NOT NULL,
        descripcion     nvarchar(max)  NOT NULL,
        prioridad       tinyint        NOT NULL CONSTRAINT DF_proveedor_ticket_prio DEFAULT 1,       -- 0 baja · 1 normal · 2 alta · 3 urgente
        estado          varchar(12)    NOT NULL CONSTRAINT DF_proveedor_ticket_estado DEFAULT 'ABIERTO',   -- ABIERTO | EN_PROCESO | ESPERANDO | RESUELTO | CERRADO
        id_carga        int            NULL,                            -- carga relacionada (opcional)
        asignado_a      nvarchar(150)  NULL,                            -- quién del equipo lo lleva
        calificacion    tinyint        NULL,                            -- 1 a 5 estrellas que da el proveedor al cerrarse
        calificacion_texto nvarchar(500) NULL,
        creado_en       datetime2(0)   NOT NULL CONSTRAINT DF_proveedor_ticket_creado DEFAULT SYSDATETIME(),
        actualizado_en  datetime2(0)   NOT NULL CONSTRAINT DF_proveedor_ticket_act DEFAULT SYSDATETIME(),
        primera_respuesta_en datetime2(0) NULL,                         -- para medir qué tan rápido se atiende
        resuelto_en     datetime2(0)   NULL
    );
    CREATE INDEX IX_proveedor_ticket_cuenta ON Eurovip.proveedor_ticket (id_cuenta, estado);
    CREATE INDEX IX_proveedor_ticket_estado ON Eurovip.proveedor_ticket (estado, actualizado_en);
END
GO
IF OBJECT_ID('Eurovip.proveedor_ticket_mensaje', 'U') IS NULL
BEGIN
    CREATE TABLE Eurovip.proveedor_ticket_mensaje (
        id          int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_proveedor_ticket_mensaje PRIMARY KEY,
        id_ticket   int            NOT NULL,
        tipo        varchar(10)    NOT NULL,                            -- PROVEEDOR | EUROVIP | NOTA (nota interna: el proveedor nunca la ve) | SISTEMA
        autor       nvarchar(150)  NOT NULL,
        texto       nvarchar(max)  NULL,
        adjuntos    nvarchar(max)  NULL,                                -- JSON: [{ n, nombre, archivo, bytes, tipo }]
        creado_en   datetime2(0)   NOT NULL CONSTRAINT DF_proveedor_ticket_msg_creado DEFAULT SYSDATETIME()
    );
    CREATE INDEX IX_proveedor_ticket_msg_ticket ON Eurovip.proveedor_ticket_mensaje (id_ticket, id);
END
GO
