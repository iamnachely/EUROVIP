-- Proveedores: solicitudes de creación de datos (20/11/2026, pedido de la usuaria).
-- Si al cargar falta un hotel, destino, aeropuerto o habitación, el proveedor pide crearlo desde el mismo formulario que usa
-- Eurovip en Herramientas. El dato se crea al momento (el hotel queda INACTIVO hasta que Eurovip lo apruebe) y el proveedor ya lo
-- puede usar en su carga. Eurovip lo revisa en Herramientas › Cargas de proveedores › Solicitudes de datos.
-- Aplicar en TropitoursVDT (esquema Eurovip) ANTES de desplegar. Es idempotente. Mientras no exista la tabla, «Pedir que lo creen» responde
-- con un aviso y todo lo demás funciona igual.
IF OBJECT_ID('Eurovip.proveedor_solicitud', 'U') IS NULL
BEGIN
    CREATE TABLE Eurovip.proveedor_solicitud (
        id_solicitud   int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_proveedor_solicitud PRIMARY KEY,
        id_cuenta      int            NOT NULL,                 -- Eurovip.proveedor_cuenta
        tipo           varchar(20)    NOT NULL,                 -- hotel | destino | aeropuerto | habitacion
        nombre         nvarchar(200)  NOT NULL,                 -- lo que se ve en las listas («Riu Palace Bávaro»)
        datos          nvarchar(max)  NULL,                     -- JSON con lo que escribió el proveedor
        clave          nvarchar(300)  NULL,                     -- JSON con la clave del dato creado ({"id_alojamiento":123})
        estado         varchar(12)    NOT NULL CONSTRAINT DF_proveedor_solicitud_estado DEFAULT 'PENDIENTE',   -- PENDIENTE | APROBADA | RECHAZADA
        motivo         nvarchar(500)  NULL,                     -- por qué se rechazó
        creado_en      datetime2(0)   NOT NULL CONSTRAINT DF_proveedor_solicitud_creado DEFAULT SYSDATETIME(),
        revisado_en    datetime2(0)   NULL,
        revisado_por   nvarchar(100)  NULL
    );
    CREATE INDEX IX_proveedor_solicitud_cuenta ON Eurovip.proveedor_solicitud (id_cuenta, estado);
END
GO
