/* ##########################################################################################
   EUROVIP — REGISTRO ÚNICO DE BASE DE DATOS
   ##########################################################################################

   ¿Qué es este archivo?
   ---------------------
   Aquí se anota TODO lo que se crea o se cambia en la base de datos para Eurovip
   (la nueva página de Dominicana Tours para vender viajes a República Dominicana).
   Cada cambio tiene: número, fecha, una explicación sencilla y el script SQL.

   ¿Dónde vive la base de datos?
   -----------------------------
   - Servidor ........ 93
   - Base de datos ... TropitoursVDT
   - Esquema ......... Eurovip   ← todas las tablas de Eurovip van aquí, nunca en "dbo".
                                   Así no se mezclan con las tablas de Tropitours.

   ¿De dónde se copian las tablas?
   -------------------------------
   Cuando una tabla se "replica", se toma como modelo la base ViajeVDT del servidor 10.10.10.29.
   En ese servidor NO se cambia nada: solo se mira cómo está hecha la tabla y se crea igual aquí.

   ¿Cómo se usa?
   -------------
   El archivo completo se puede ejecutar de arriba a abajo en SSMS sobre el servidor 93.
   Si algo ya existe, se salta (no rompe nada ni duplica).

   ÍNDICE
   ------
   CAMBIO 1  (12/10/2026)  Se creó el espacio "Eurovip" dentro de la base de datos.
   CAMBIO 2  (12/10/2026)  Se creó la tabla de ROLES (tipos de usuario).
   CAMBIO 3  (12/10/2026)  Se creó la tabla de USUARIOS (para iniciar sesión).
   CAMBIO 4  (12/10/2026)  Se creó la tabla de MÓDULOS del sistema (pantallas/secciones).
   CAMBIO 5  (12/10/2026)  Se creó la tabla de PERMISOS (qué módulo ve cada usuario).
   CAMBIO 6  (12/10/2026)  Se cargó el primer usuario: Nachely Rodríguez (Superadministrador).
   --- FASE 2: CARGA DE HOTELES ---
   CAMBIO 7  (12/10/2026)  Tabla de IDIOMAS / mercados (+ 7 filas).
   CAMBIO 8  (12/10/2026)  Tabla de TIPOS DE SERVICIO (+ 5 filas).
   CAMBIO 9  (12/10/2026)  Tabla de MONEDAS (+ EUR).
   CAMBIO 10 (12/10/2026)  Tabla de CAMPAÑAS (+ 126 filas).
   CAMBIO 11 (12/10/2026)  Tabla de CONTENEDORES / secciones de la web (+ 48 filas).
   CAMBIO 12 (12/10/2026)  Tabla INICIO: vitrina de la web, aquí se cargan los HOTELES.
   CAMBIO 13 (12/10/2026)  Tabla de AUDITORÍA: historial de quién cambió qué.
   --- FASE 3: CIRCUITOS ---
   CAMBIO 14 (12/10/2026)  PAÍSES (142) y DESTINOS (7.320, copia masiva) — solo ES.
   CAMBIO 15 (12/10/2026)  HOTELES: tipo_alojamiento + alojamiento (9.563, copia masiva).
   CAMBIO 16 (12/10/2026)  PROVEEDORES, AEROPUERTOS, RÉGIMEN, TASAS y CATEGORÍAS.
   CAMBIO 17 (12/10/2026)  CONFIGURACIÓN: precio base mínimo = 300.
   CAMBIO 18 (12/10/2026)  CATÁLOGO DE CIRCUITOS: circuito, destinos, itinerario, hoteles.
   CAMBIO 19 (12/10/2026)  CARGA DE CIRCUITOS: servicio, salidas, vuelos (opcional), tasas, notas, logs.
   CAMBIO 20 (12/10/2026)  FUNCIONES de cálculo (precio web, vitrina con o sin vuelo).
   CAMBIO 21 (12/10/2026)  PROCEDIMIENTOS de carga en esquema Eurovip.
   --- FASE 4: CARGA MANUAL DE OFERTAS ---
   CAMBIO 22 (13/10/2026)  TIPOS DE HABITACIÓN (15.877) y HABITACIONES DE CADA HOTEL (129.943).
   CAMBIO 23 (13/10/2026)  DETALLE DE HOTEL y PRECIO POR HABITACIÓN de cada salida (+ origen de venta).
   CAMBIO 24 (13/10/2026)  VITRINA WEB DE HOTELES (alojamiento_destino).
   CAMBIO 25 (13/10/2026)  FUNCIONES y PROCEDIMIENTOS de la carga manual (vitrina con o sin vuelo).
   --- FASE 5: HOME DE LA WEB (INICIO + CARRUSEL) ---
   CAMBIO 26 (13/10/2026)  INICIO con número propio (id) para que hoteles y ofertas no choquen.
   CAMBIO 27 (13/10/2026)  HOME_CONTENIDO: sección y posición de cada destino (176 filas).
   CAMBIO 28 (13/10/2026)  CARRUSEL: slides de la Home web / tablet / móvil (53 filas).
   CAMBIO 29 (13/10/2026)  CARRUCELDESTINO: precio "desde" de cada slide (36 filas).
   CAMBIO 30-31 (13/10/2026)  Funciones "más barato por destino" sin vuelo obligatorio.
   CAMBIO 32 (13/10/2026)  spActualizaInicio corregido (circuitos + conserva el orden).
   CAMBIO 33 (13/10/2026)  SP de lectura para la web (carrusel activo y destinos).
   --- FASE 6: HERRAMIENTAS (MANTENIMIENTOS BÁSICOS) ---
   CAMBIO 34 (13/10/2026)  SP para crear/editar ALOJAMIENTOS (copiado de ViajeVDT, mejorado).
   CAMBIO 35 (13/10/2026)  SP para crear/editar HABITACIONES de un hotel (copiado de ViajeVDT).
   CAMBIO 36 (13/10/2026)  ÍNDICES para buscar rápido hoteles y habitaciones en Herramientas.
   --- FASE 7: PERFIL DE USUARIO Y NOTIFICACIONES ---
   CAMBIO 37 (13/10/2026)  USUARIO: fecha hasta la que ya vio sus notificaciones.
   --- FASE 8: CONSULTAR OFERTAS ---
   CAMBIO 38 (13/10/2026)  Buscador general de servicios (fnFiltro), con o sin vuelo.
   CAMBIO 39 (13/10/2026)  Editar servicios uno a uno o en masa (spEditaServicio).
   CAMBIO 40 (13/10/2026)  Duplicar servicios, también a otras fechas (spDuplicaServicio).
   --- FASE 9: FLYERS ---
   CAMBIO 41 (13/10/2026)  Tablas de FLYERS copiadas de ViajeVDT (config, precios, 178 contactos, envíos).
   BITÁCORA de cambios de datos puntuales (al final).
   ########################################################################################## */

USE TropitoursVDT;
GO


/* ==========================================================================================
   CAMBIO 1 — 12/10/2026 — Espacio "Eurovip" dentro de la base de datos
   ------------------------------------------------------------------------------------------
   Qué se hizo: se creó el esquema "Eurovip". Es como una carpeta dentro de TropitoursVDT
   donde se guardan solo las tablas de Eurovip, separadas de las de Tropitours (dbo).
   ========================================================================================== */
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Eurovip')
    EXEC('CREATE SCHEMA Eurovip AUTHORIZATION dbo');
GO


/* ==========================================================================================
   CAMBIO 2 — 12/10/2026 — Tabla de ROLES
   ------------------------------------------------------------------------------------------
   Qué se hizo: se creó la tabla donde se guardan los tipos de usuario
   (por ejemplo: Superadministrador, Usuario).
   Copiada de: ViajeVDT.dbo.rol (servidor 10.10.10.29).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.rol', 'U') IS NULL
CREATE TABLE Eurovip.rol (
    codigoRol varchar(50) NULL          -- nombre del rol
);
GO


/* ==========================================================================================
   CAMBIO 3 — 12/10/2026 — Tabla de USUARIOS (inicio de sesión)
   ------------------------------------------------------------------------------------------
   Qué se hizo: se creó la tabla con las personas que pueden entrar al BackOffice de Eurovip.
   Se usa en la pantalla de "Iniciar sesión" y en "Crear usuario".
   La contraseña NUNCA se guarda en texto: se guarda cifrada (columna "clave").
   Copiada de: ViajeVDT.dbo.usuario (servidor 10.10.10.29).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.usuario', 'U') IS NULL
CREATE TABLE Eurovip.usuario (
    codigoUsuario    int IDENTITY(1,1) NOT NULL,   -- número interno del usuario
    nombre           varchar(50)  NOT NULL,
    apellido         varchar(50)  NOT NULL,
    telefono         varchar(15)  NULL,
    clave            varchar(max) NOT NULL,        -- contraseña cifrada
    imagen           varchar(100) NULL,            -- foto de perfil
    activo           bit          NOT NULL,        -- 1 = puede entrar, 0 = bloqueado
    rol              varchar(30)  NULL,            -- tipo de usuario
    email            varchar(50)  NOT NULL,        -- correo con el que inicia sesión
    userName         varchar(15)  NOT NULL,        -- nombre de usuario corto
    Auth2FAValidator bit          NULL,
    Auth2FACode      int          NULL,
    SecurityStamp    varchar(50)  NULL,            -- sello de seguridad de la sesión
    correoAD         varchar(70)  NULL,
    contrasena       varchar(max) NULL,
    CONSTRAINT PK_Eurovip_usuario PRIMARY KEY CLUSTERED (codigoUsuario)
);
GO


/* ==========================================================================================
   CAMBIO 4 — 12/10/2026 — Tabla de MÓDULOS del sistema
   ------------------------------------------------------------------------------------------
   Qué se hizo: se creó la lista de secciones del BackOffice (Hoteles, Circuitos, etc.)
   a las que se les puede dar o quitar acceso.
   Copiada de: ViajeVDT.dbo.UsuarioAcceso (servidor 10.10.10.29).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.UsuarioAcceso', 'U') IS NULL
CREATE TABLE Eurovip.UsuarioAcceso (
    id_modulo     int IDENTITY(1,1) NOT NULL,     -- número del módulo
    nombre_modulo nvarchar(200) NOT NULL,         -- nombre que se ve en pantalla
    Categoria     nvarchar(200) NULL,             -- grupo del menú
    CONSTRAINT PK_Eurovip_UsuarioAcceso PRIMARY KEY CLUSTERED (id_modulo)
);
GO


/* ==========================================================================================
   CAMBIO 5 — 12/10/2026 — Tabla de PERMISOS por usuario
   ------------------------------------------------------------------------------------------
   Qué se hizo: se creó la tabla que dice qué módulos puede usar cada usuario
   (acceso = 1 sí puede, 0 no puede). Está enlazada con la tabla de módulos (cambio 4).
   Copiada de: ViajeVDT.dbo.UsuarioModulos (servidor 10.10.10.29).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.UsuarioModulos', 'U') IS NULL
CREATE TABLE Eurovip.UsuarioModulos (
    id        int IDENTITY(1,1) NOT NULL,
    usuario   nvarchar(100) NOT NULL,                                                      -- userName del usuario
    acceso    bit NOT NULL CONSTRAINT DF_Eurovip_UsuarioModulos_acceso DEFAULT ((0)),      -- 1 = tiene acceso
    id_modulo int NOT NULL,                                                                -- módulo (cambio 4)
    CONSTRAINT PK_Eurovip_UsuarioModulos PRIMARY KEY CLUSTERED (id),
    CONSTRAINT FK_Eurovip_UsuarioModulos_UsuarioAcceso FOREIGN KEY (id_modulo)
        REFERENCES Eurovip.UsuarioAcceso (id_modulo)
);
GO


/* ==========================================================================================
   CAMBIO 6 — 12/10/2026 — Primer usuario: Nachely Rodríguez
   ------------------------------------------------------------------------------------------
   Qué se hizo: se copió el usuario "ARodriguez" (Nachely Rodríguez, Superadministrador)
   desde la tabla de usuarios de Tropitours (TropitoursVDT.dbo.usuario) para poder
   entrar a Eurovip. Antes se había creado una cuenta de prueba "Nachely" con el mismo
   correo desde la pantalla de registro; esa se borró para que no quedaran dos iguales.
   ========================================================================================== */
SET XACT_ABORT ON;
BEGIN TRAN;

    -- Quitar la cuenta de prueba creada desde "Crear usuario" (mismo correo)
    DELETE FROM Eurovip.usuario
    WHERE userName = 'Nachely' AND email = 'nachely.rodriguez@dominicanatours.net';

    -- Copiar ARodriguez desde Tropitours (solo si todavía no está)
    IF NOT EXISTS (SELECT 1 FROM Eurovip.usuario WHERE userName = 'ARodriguez')
    INSERT INTO Eurovip.usuario (nombre, apellido, telefono, clave, imagen, activo, rol, email, userName,
                                 Auth2FAValidator, Auth2FACode, SecurityStamp, correoAD, contrasena)
    SELECT nombre, apellido, telefono, clave, imagen, activo, rol, email, userName,
           Auth2FAValidator, Auth2FACode, SecurityStamp, correoAD, contrasena
    FROM dbo.usuario
    WHERE userName = 'ARodriguez';

COMMIT;
GO


/* ##########################################################################################
   FASE 2 — CARGA DE HOTELES  (12/10/2026)
   ------------------------------------------------------------------------------------------
   Para cargar hoteles se usa la tabla "inicio": es la VITRINA de la página web.
   Cada fila es un producto publicado (un hotel, una oferta, un circuito…) con su precio
   "desde", su imagen, su destino y el lugar donde aparece en la web (contenedor + posición).
   Los hoteles se guardan con tipo_servicio = 'HOTEL'.
   Para que funcione se necesitan también sus catálogos: idiomas, tipos de servicio, monedas,
   campañas y contenedores. Todo se copió de ViajeVDT (10.10.10.29) sin tocar ese servidor.
   ########################################################################################## */


/* ==========================================================================================
   CAMBIO 7 — 12/10/2026 — Tabla de IDIOMAS (mercados)
   ------------------------------------------------------------------------------------------
   Qué se hizo: lista de idiomas/mercados de la web (ES español, US inglés, FR francés…).
   Cada hotel publicado pertenece a un idioma.
   Copiada de: ViajeVDT.dbo.idioma (estructura + sus 7 filas).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.idioma', 'U') IS NULL
CREATE TABLE Eurovip.idioma (
    id_idioma char(2)     NOT NULL,   -- código (ES, US, FR…)
    nombre    varchar(20) NOT NULL,   -- nombre visible
    CONSTRAINT PK_Eurovip_idioma PRIMARY KEY CLUSTERED (id_idioma)
);
GO
-- Datos copiados de ViajeVDT (no duplica si ya existen)
INSERT INTO Eurovip.idioma (id_idioma, nombre)
SELECT v.id_idioma, v.nombre FROM (VALUES
    (N'ES', N'ESPAÑOL'),
    (N'EX', N'EXCURSIVO'),
    (N'FR', N'FRANCES'),
    (N'IT', N'ITALIANO'),
    (N'PT', N'PORTUGUES'),
    (N'RO', N'RUMANO'),
    (N'US', N'INGLES')
) v (id_idioma, nombre)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.idioma t WHERE t.id_idioma = v.id_idioma);
GO


/* ==========================================================================================
   CAMBIO 8 — 12/10/2026 — Tabla de TIPOS DE SERVICIO
   ------------------------------------------------------------------------------------------
   Qué se hizo: tipos de producto que se venden (Oferta, Circuito, Combinado, Excursión,
   Traslado) con su precio mínimo de referencia.
   Copiada de: ViajeVDT.dbo.tipo_servicio (estructura + sus 5 filas).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.tipo_servicio', 'U') IS NULL
CREATE TABLE Eurovip.tipo_servicio (
    id_tipo_servicio varchar(20)  NOT NULL,   -- código del tipo
    descripcion      varchar(100) NOT NULL,
    precio_minimo    money        NOT NULL,   -- precio mínimo de referencia
    CONSTRAINT PK_Eurovip_tipo_servicio PRIMARY KEY CLUSTERED (id_tipo_servicio)
);
GO
-- Datos copiados de ViajeVDT (no duplica si ya existen)
INSERT INTO Eurovip.tipo_servicio (id_tipo_servicio, descripcion, precio_minimo)
SELECT v.id_tipo_servicio, v.descripcion, v.precio_minimo FROM (VALUES
    (N'Circuito', N'Tres o más hoteles', 800.0000),
    (N'Combinado', N'Dos Hoteles', 600.0000),
    (N'Excursion', N'Excursiones - servicio extras', 200.0000),
    (N'Oferta', N'Servicios hoteleros', 700.0000),
    (N'Traslado', N'Traslado - servicio', 2000.0000)
) v (id_tipo_servicio, descripcion, precio_minimo)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.tipo_servicio t WHERE t.id_tipo_servicio = v.id_tipo_servicio);
GO


/* ==========================================================================================
   CAMBIO 9 — 12/10/2026 — Tabla de MONEDAS
   ------------------------------------------------------------------------------------------
   Qué se hizo: monedas en las que se publica el precio (hoy solo EUR, igual que ViajeVDT).
   Copiada de: ViajeVDT.dbo.moneda (estructura + su fila).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.moneda', 'U') IS NULL
CREATE TABLE Eurovip.moneda (
    id_moneda char(3)     NOT NULL,   -- código (EUR)
    nombre    varchar(20) NOT NULL,
    CONSTRAINT PK_Eurovip_moneda PRIMARY KEY CLUSTERED (id_moneda)
);
GO
-- Datos copiados de ViajeVDT (no duplica si ya existen)
INSERT INTO Eurovip.moneda (id_moneda, nombre)
SELECT v.id_moneda, v.nombre FROM (VALUES
    (N'EUR', N'EURO')
) v (id_moneda, nombre)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.moneda t WHERE t.id_moneda = v.id_moneda);
GO


/* ==========================================================================================
   CAMBIO 10 — 12/10/2026 — Tabla de CAMPAÑAS
   ------------------------------------------------------------------------------------------
   Qué se hizo: campañas comerciales (Verano, Navidad, General…). Cada hotel publicado
   pertenece a una campaña. Está enlazada con idiomas (cambio 7) y tipos de servicio (cambio 8).
   Copiada de: ViajeVDT.dbo.campanya (estructura + sus 126 filas).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.campanya', 'U') IS NULL
CREATE TABLE Eurovip.campanya (
    id_campanya      smallint     NOT NULL,   -- número de campaña
    id_idioma        char(2)      NOT NULL,   -- idioma (cambio 7)
    id_tipo_servicio varchar(20)  NOT NULL,   -- tipo de servicio (cambio 8)
    nombre           varchar(30)  NOT NULL,
    descripcion      varchar(max) NULL,
    CONSTRAINT PK_Eurovip_campanya PRIMARY KEY CLUSTERED (id_campanya, id_idioma),
    CONSTRAINT FK_Eurovip_campanya_idioma        FOREIGN KEY (id_idioma)        REFERENCES Eurovip.idioma (id_idioma),
    CONSTRAINT FK_Eurovip_campanya_tipo_servicio FOREIGN KEY (id_tipo_servicio) REFERENCES Eurovip.tipo_servicio (id_tipo_servicio)
);
GO
-- Datos copiados de ViajeVDT (no duplica si ya existen)
INSERT INTO Eurovip.campanya (id_campanya, id_idioma, id_tipo_servicio, nombre, descripcion)
SELECT v.id_campanya, v.id_idioma, v.id_tipo_servicio, v.nombre, v.descripcion FROM (VALUES
    (14, N'ES', N'OFERTA', N'DTBaleares', N'Su clima suave con más de 300 días de sol al año, sus bellísimos paisajes y playas, su amplísima oferta cultural, sus posibilidades de ocio o relax y la hospitalidad de sus habitantes las han convertido en un lugar cosmopolita visitado desde todos los lugares del planeta, a veces para pasar vacaciones, y a veces para quedarse para siempre.'),
    (15, N'ES', N'OFERTA', N'DTCanarias', N'Sobre las azules y refrescantes aguas del océano Atlántico, las Islas Canarias , también conocidas con "Las Islas Afortunadas", exhiben toda la magia de un archipiélago único en el mundo por su diversidad y riqueza subtropical; todo un universo del que disfrutar en un clima primaveral que se prolonga 12 meses al año.El Hierro, La Palma, La Gomera, Tenerife, Gran Canaria, Fuerteventura y Lanzarote, todas ellas espectaculares y muy diferentes, seducen al visitante con una excepcional oferta turística en la que encontrará los mejores alojamientos y una propuesta cultural y de ocio de la mayor calidad.Inmensas dunas blancas y aguas cristalinas, paisajes de aspecto lunar, bosques selváticos, recónditas calas de brillante arena negra, volcanes, extensas playas doradas… y todo el sabor de pueblos que han sabido conservar su patrimonio histórico y cultural con fiestas populares de gran arraigo y una gastronomía tan sencilla como exquisita.'),
    (15, N'PT', N'OFERTA', N'DTIlhasCanarias', N'Sobre as azuis e refrescantes, águas do Océano Atlantico, as Ilhas Canarias, também conhecidas como “ As Ilhas com Sorte” , nos mostram toda a sua magia num arquipélogo único em um mundo com tanta diversidade e riqueza sub-tropical, Todo um universo de que pode disfrutar num clima primaveral que esta 12 meses ao ano. O Hierro, Palma, Gomera, Tenerife, Gran Canaria, Fuerteventura e Lanzarote, todos destinos espetaculares e super diferentes, seduzem ao visitante que com uma ecepcional oferta turística, na qual encontra os melhores alojamentos e uma proposta cultural e ócio de mayor qualidade. Imensas dunas blancas e águas cristalinas, paisagens de aspecto lunar, bosques selvagens, pequenas praias escondidas com brillante areia negra, vulcoes, extensas praias douradas… e todo com o sabor de povos que souberam conservar o seu patrimonio histórico, cultural com festas populares de grande arraigo e uma gastronomia tao simples como exótica.'),
    (16, N'ES', N'COMBINADO', N'DTCombinados', N'<p>Si quieres hacerlo todo en tus vacaciones pero no sabes c&oacute;mo, aqu&iacute; te proponemos varias opciones para que disfrutes tanto de la playa, el sol y la fiesta como de la cultura, la gente y la ciudad del destino, nada m&aacute;s completo para tus vacaciones!</p>
'),
    (16, N'EX', N'COMBINADO', N'EXCombinados', N'EXCombinados'),
    (16, N'FR', N'COMBINADO', N'FRCombinados', N'FRCombinados'),
    (16, N'IT', N'COMBINADO', N'ITCombinados', N'ITCombinados'),
    (16, N'PT', N'COMBINADO', N'PTCombinados', N'<p>Se voc&ecirc; quiser fazer tudo nas suas f&eacute;rias, mas nao sabe como, aqu&iacute; te fazemos a proposta de varias op&ccedil;oes para que possa aproveitar tanto da praia , do sol e da festa cultural, da gente e da cidade de destino, nada mais completo para as suas f&eacute;rias!&quot;</p>

<p>&quot;</p>
'),
    (16, N'RO', N'COMBINADO', N'ROCombinados', N'ROCombinados'),
    (16, N'US', N'COMBINADO', N'USCombinados', N'USCombinados'),
    (17, N'ES', N'CIRCUITO', N'DTCircuitos', N'Qué mejor forma que conocer un destino, otro país, otra forma de vida que ¡recorriéndolo!, déjate llevar por los guías especializados en los destinos y conoce los mejores rincones del mundo. No lo dejes escapar.'),
    (17, N'EX', N'CIRCUITO', N'EXCircuitos', NULL),
    (17, N'FR', N'CIRCUITO', N'FRCircuitos', NULL),
    (17, N'IT', N'CIRCUITO', N'ITCircuitos', NULL),
    (17, N'PT', N'CIRCUITO', N'DTPTCircuitos', N'Qual é a melhor maneira de conhecer uma cidade em outro país, outra forma de vida, que deixando-se levar por guias especializados, os quais conhecem os melhores lugares do mundo. Nao deixe escapar esta oportunidade.'),
    (17, N'RO', N'CIRCUITO', N'ROCircuitos', NULL),
    (17, N'US', N'CIRCUITO', N'USCircuitos', NULL),
    (91, N'ES', N'OFERTA', N'DTDirectoBCN', NULL),
    (91, N'PT', N'OFERTA', N'DTDirectoBCN-PT', NULL),
    (95, N'ES', N'OFERTA', N'DTInvierno', NULL),
    (95, N'PT', N'OFERTA', N'DTInverno', NULL),
    (99, N'ES', N'Oferta', N'General-DT', N'DT'),
    (199, N'EX', N'Oferta', N'General-EX', N'EX'),
    (299, N'FR', N'Oferta', N'General-FR', N'FR'),
    (399, N'US', N'Oferta', N'General-US', N'US'),
    (922, N'IT', N'Oferta', N'General-IT', N'IT'),
    (991, N'PT', N'Oferta', N'General-PT', N'PT'),
    (992, N'RO', N'Oferta', N'General-RO', N'RO'),
    (1035, N'ES', N'OFERTA', N'Especial Navidad DT-PT', N'
<p>En esta sección de ofertas les ofrecemos una selección de los mejores destinos al mejor precio del mercado. Porque como de costumbre, nos sentimos comprometidos con hacer de su viaje  de Navidad y Fin de Año una experiencia inolvidable. <strong>Viaja con VDT.</strong></p>
<p>
<br />
</p>
'),
    (1035, N'EX', N'OFERTA', N'Especial Navidad DT-PT', N'
<p>En esta sección de ofertas les ofrecemos una selección de los mejores destinos al mejor precio del mercado. Porque como de costumbre, nos sentimos comprometidos con hacer de su viaje  de Navidad y Fin de Año una experiencia inolvidable. <strong>Viaja con VDT.</strong></p>
<p>
<br />
</p>
'),
    (1035, N'FR', N'OFERTA', N'Spécial Noël DT-PT', N'
<p>Dans cette section, nous proposons une sélection propose les meilleurs hôtels meilleur prix du marché. Parce que, comme d''habitude, nous nous engageons à rendre votre séjour à Noël et Nouvel An une expérience inoubliable. <strong>Voyage avec VDT.</strong></p>
<p>
<br />
</p>
'),
    (1035, N'IT', N'OFERTA', N'Speciale Natale DT-PT', N'
<p>In questa sezione vi proponiamo una selezione offre le migliori proposte miglior prezzo di mercato. Perché, come al solito, ci siamo impegnati per rendere il vostro viaggio a Natale e Capodanno un''esperienza indimenticabile. <strong>Viaggiare con VDT.</strong></p>
<p>
<br />
</p>
'),
    (1035, N'PT', N'OFERTA', N'Especial de Natal DT-PT', N'
<div>Nesta seção, oferecemos uma seleção oferece as melhores escolhas para melhor preço do mercado. Porque, como de costume, temos o compromisso de fazer a sua viagem de Natal e Ano Novo uma experiência inesquecível. <strong>Viaje com VDT.</strong></div>
<div>
<strong>
</strong> </div>
<div>
<br />
</div>
'),
    (1035, N'RO', N'OFERTA', N'Speciala de Craciun DT-PT', N'
<p>În aceasta sec?iune va oferim o selec?ie ofera cele mai bune ponturi pentru cel mai bun pret pe pia?a. Deoarece, ca de obicei, ne-am angajat pentru a face calatoria dvs. de Craciun ?i de Anul Nou o experienta de neuitat. <strong>Calatorii cu VDT.</strong></p>
<p>
<br />
</p>
'),
    (1035, N'US', N'OFERTA', N'Special Christmas DT-PT', N'
<p>In this section we offer a selection offers the best picks for best market price. Because as usual, we are committed to make your trip to Christmas and New Year an unforgettable experience. <strong>Travel with VDT.</strong></p>
<p>
<br />
</p>
'),
    (1114, N'PT', N'Oferta', N'BalearesPT', N'...'),
    (1199, N'PT', N'OFERTA', N'GeralDT-PT', N'





<p>&quot;</p>
'),
    (1230, N'ES', N'OFERTA', N'DTVerano', N'<p>Arena o asfalto..? Nosotros te lo ponemos f&aacute;cil, elijas el destino que elijas este verano te garantizamos el mejor precio. No esperes a que te lo cuenten, se t&uacute; el primero en contarlo...&nbsp;</p>

<div>&nbsp;</div>

<div><strong>Reserva YA tus vacaciones de VERANO!</strong></div>
'),
    (1230, N'PT', N'OFERTA', N'DTVerao', N'<p>Areia ou asfalto..? Deixamos f&aacute;cil, seja o que seja, o destino que escolhas, este ver&atilde;o garantimos o mejor pre&ccedil;o. N&atilde;o esperes que te digam depois, seja tu o primeiro em contar...</p>

<div>&nbsp;
<div><strong>Reserva J&Aacute; tuas f&eacute;rias de VER&Atilde;O!&nbsp;</strong></div>

<div>&nbsp;</div>

<div>&nbsp;</div>
</div>

<p>&quot;</p>
'),
    (1721, N'US', N'OFERTA', N'DTCANARIAS34', N'222'),
    (3164, N'ES', N'OFERTA', N'Especial Los Cabos DT-PT', NULL),
    (3164, N'PT', N'OFERTA', N'Especial Los Cabos DT-PT', NULL),
    (3166, N'ES', N'OFERTA', N'DTEspecialFiestas', NULL),
    (3166, N'PT', N'OFERTA', N'DTEspecialFestas', NULL),
    (3167, N'ES', N'OFERTA', N'DTDestinosEspeciales', NULL),
    (3167, N'PT', N'OFERTA', N'DTDestinosEspeciais', NULL),
    (3168, N'ES', N'OFERTA', N'DTFestivos', NULL),
    (3168, N'PT', N'OFERTA', N'DTFestivosPT', NULL),
    (3170, N'ES', N'OFERTA', N'DTGeneral', NULL),
    (3170, N'PT', N'OFERTA', N'DTGeralPT', NULL),
    (3171, N'ES', N'OFERTA', N'No usar BF ES', NULL),
    (3171, N'PT', N'OFERTA', N'No usar BF PT', NULL),
    (8131, N'PT', N'OFERTA', N'DTBlueMonday-PT', NULL),
    (8132, N'ES', N'OFERTA', N'DT Bahía Príncipe', NULL),
    (8132, N'PT', N'OFERTA', N'DT Bahía Príncipe PT', NULL),
    (8133, N'ES', N'OFERTA', N'Navidad', NULL),
    (8133, N'PT', N'OFERTA', N'Natal', NULL),
    (8134, N'ES', N'OFERTA', N'Luxury', NULL),
    (8134, N'PT', N'OFERTA', N'LuxuryPT', NULL),
    (8142, N'ES', N'Oferta', N'IT-General', N'IT-General'),
    (8146, N'PT', N'Oferta', N'Canarias-PT', N'Canarias-PT'),
    (8153, N'ES', N'Oferta', N'Vuelo Directo Barcelona', N'...'),
    (8154, N'FR', N'Oferta', N'FRBaleares', N'...'),
    (8155, N'US', N'Oferta', N'USBaleares', N'...'),
    (8156, N'IT', N'Oferta', N'ITBaleares', N'...'),
    (8157, N'PT', N'Oferta', N'PTBaleares', N'...'),
    (8158, N'RO', N'Oferta', N'ROBaleares', N'...'),
    (8160, N'ES', N'Oferta', N'Newblue Oferta DT', N'.'),
    (8161, N'EX', N'Oferta', N'Newblue Oferta EX', N'.'),
    (8162, N'FR', N'Oferta', N'Newblue Oferta FR', N'.'),
    (8163, N'US', N'Oferta', N'Newblue Oferta US', N'.'),
    (8164, N'IT', N'Oferta', N'Newblue Oferta IT', N'.'),
    (8167, N'RO', N'Oferta', N'Newblue Oferta RO', N'.'),
    (8168, N'PT', N'Oferta', N'Newblue Oferta PT', N'.'),
    (8171, N'IT', N'Oferta', N'IT-DatosDuplicados', N'it'),
    (8172, N'ES', N'Oferta', N'Inmediato.ES', N'.'),
    (8173, N'EX', N'Oferta', N'Inmediato.EX', N'.'),
    (8174, N'FR', N'Oferta', N'Inmediato.FR', N'.'),
    (8175, N'US', N'Oferta', N'Inmediato.US', N'.'),
    (8176, N'IT', N'Oferta', N'Inmediato.IT', N'.'),
    (8177, N'PT', N'Oferta', N'Inmediato.PT', N'.'),
    (8178, N'RO', N'Oferta', N'Inmediato.RO', N'.'),
    (8179, N'ES', N'Oferta', N'Navidad-ES', N'.'),
    (8180, N'EX', N'Oferta', N'Navidad-EX', N'.'),
    (8181, N'FR', N'Oferta', N'Navidad-FR', N'.'),
    (8182, N'US', N'Oferta', N'Navidad-US', N'.'),
    (8183, N'IT', N'Oferta', N'Navidad-IT', N'.'),
    (8184, N'PT', N'Oferta', N'Navidad-PT', N'.'),
    (8185, N'RO', N'Oferta', N'Navidad-RO', N'.'),
    (8186, N'FR', N'Oferta', N'p', N'p'),
    (8187, N'ES', N'Oferta', N'Black-ES', N'.'),
    (8188, N'EX', N'Oferta', N'Black-EX', N'.'),
    (8189, N'FR', N'Oferta', N'Black-FR', N'.'),
    (8190, N'US', N'Oferta', N'Black-US', N'.'),
    (8191, N'IT', N'Oferta', N'Black-IT', N'.'),
    (8192, N'PT', N'Oferta', N'Black-PT', N'.'),
    (8193, N'RO', N'Oferta', N'Black-RO', N'.'),
    (8194, N'RO', N'Oferta', N'p1', N'p1'),
    (8195, N'ES', N'Oferta', N'SemanaS.DT', N'.'),
    (8196, N'EX', N'Oferta', N'SemanaS.EX', N'.')
) v (id_campanya, id_idioma, id_tipo_servicio, nombre, descripcion)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.campanya t WHERE t.id_campanya = v.id_campanya AND t.id_idioma = v.id_idioma);
INSERT INTO Eurovip.campanya (id_campanya, id_idioma, id_tipo_servicio, nombre, descripcion)
SELECT v.id_campanya, v.id_idioma, v.id_tipo_servicio, v.nombre, v.descripcion FROM (VALUES
    (8197, N'FR', N'Oferta', N'SemanaS.FR', N'.'),
    (8198, N'US', N'Oferta', N'SemanaS.US', N'.'),
    (8199, N'IT', N'Oferta', N'SemanaS.IT', N'.'),
    (8200, N'PT', N'Oferta', N'SemanaS.PT', N'.'),
    (8201, N'RO', N'Oferta', N'SemanaS.RO', N'.'),
    (8202, N'ES', N'Oferta', N'SliderZanzibar-ES', N'SliderZanzibar-ES'),
    (8203, N'EX', N'Oferta', N'SliderZanzibar-EX', NULL),
    (8204, N'FR', N'Oferta', N'SliderZanzibar-FR', NULL),
    (8205, N'US', N'Oferta', N'SliderZanzibar-US', NULL),
    (8206, N'IT', N'Oferta', N'SliderZanzibar-IT', NULL),
    (8207, N'PT', N'Oferta', N'SliderZanzibar-PT', NULL),
    (8208, N'RO', N'Oferta', N'SliderZanzibar-RO', NULL),
    (8209, N'ES', N'Oferta', N'BonoExcursivo-ES', NULL),
    (8210, N'EX', N'Oferta', N'BonoExcursivo-EX', NULL),
    (8211, N'FR', N'Oferta', N'BonoExcursivo-FR', NULL),
    (8212, N'US', N'Oferta', N'BonoExcursivo-US', NULL),
    (8213, N'IT', N'Oferta', N'BonoExcursivo-IT', NULL),
    (8214, N'PT', N'Oferta', N'BonoExcursivo-PT', NULL),
    (8215, N'RO', N'Oferta', N'BonoExcursivo-RO', NULL),
    (8216, N'ES', N'Oferta', N'Verano-ES', NULL),
    (8217, N'EX', N'Oferta', N'Verano-EX', NULL),
    (8218, N'FR', N'Oferta', N'Verano-FR', NULL),
    (8219, N'US', N'Oferta', N'Verano-US', NULL),
    (8220, N'IT', N'Oferta', N'Verano-IT', NULL),
    (8221, N'PT', N'Oferta', N'Verano-PT', NULL),
    (8222, N'RO', N'Oferta', N'Verano-RO', NULL)
) v (id_campanya, id_idioma, id_tipo_servicio, nombre, descripcion)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.campanya t WHERE t.id_campanya = v.id_campanya AND t.id_idioma = v.id_idioma);
GO


/* ==========================================================================================
   CAMBIO 11 — 12/10/2026 — Tabla de CONTENEDORES (secciones de la web)
   ------------------------------------------------------------------------------------------
   Qué se hizo: los bloques de la página web donde se muestran los productos
   (CARIBE, AMÉRICA, HOTEL, CIRCUITO…), con su orden (posición) por idioma.
   Copiada de: ViajeVDT.dbo.contenedores (estructura + sus 48 filas, con los mismos números).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.contenedores', 'U') IS NULL
CREATE TABLE Eurovip.contenedores (
    id_contenedor int IDENTITY(1,1) NOT NULL,
    nombre        varchar(255) NOT NULL,                                          -- nombre de la sección
    posicion      int          NOT NULL,                                          -- orden en la web
    estado        bit          NOT NULL,                                          -- 1 = visible
    id_idioma     varchar(2)   NULL CONSTRAINT DF_Eurovip_contenedores_idioma DEFAULT ('es'),
    CONSTRAINT PK_Eurovip_contenedores PRIMARY KEY CLUSTERED (id_contenedor)
);
GO
-- Datos copiados de ViajeVDT (no duplica si ya existen)
SET IDENTITY_INSERT Eurovip.contenedores ON;
INSERT INTO Eurovip.contenedores (id_contenedor, nombre, posicion, estado, id_idioma)
SELECT v.id_contenedor, v.nombre, v.posicion, v.estado, v.id_idioma FROM (VALUES
    (1, N'CARIBE', 1, 1, N'es'),
    (2, N'COMBINADO', 3, 1, N'es'),
    (5, N'ISLAS', 6, 1, N'es'),
    (6, N'AFRICA / ASIA / EUROPA', 7, 1, N'es'),
    (9, N'CIRCUITO', 5, 1, N'es'),
    (10, N'AMÉRICA', 2, 1, N'es'),
    (11, N'CARIBBEAN', 1, 1, N'us'),
    (12, N'AMERICA', 2, 1, N'us'),
    (13, N'COMBINED', 3, 1, N'us'),
    (14, N'CIRCUIT', 4, 1, N'us'),
    (15, N'SPANISH ISLANDS', 5, 1, N'us'),
    (16, N'AFRICA / ASIA / EUROPE', 6, 1, N'us'),
    (17, N'CARAÏBES', 1, 1, N'fr'),
    (18, N'AMÉRIQUE', 2, 1, N'fr'),
    (19, N'COMBINÉ', 3, 1, N'fr'),
    (20, N'CIRCUIT', 4, 1, N'fr'),
    (21, N'ÎLES ESPAGNOLES', 5, 1, N'fr'),
    (22, N'AFRIQUE / ASIE / EUROPE', 6, 1, N'fr'),
    (23, N'CARAÍBAS', 1, 1, N'pt'),
    (24, N'AMÉRICA', 2, 1, N'pt'),
    (25, N'COMBINADO', 3, 1, N'pt'),
    (26, N'CIRCUITO', 4, 1, N'pt'),
    (27, N'ILHAS ESPANHOLAS', 5, 1, N'pt'),
    (28, N'ÁFRICA / ÁSIA / EUROPA', 6, 1, N'pt'),
    (29, N'CARAIBE', 1, 1, N'ro'),
    (30, N'AMERICA', 2, 1, N'ro'),
    (31, N'COMBINAT', 3, 1, N'ro'),
    (32, N'CIRCUIT', 4, 1, N'ro'),
    (33, N'INSULE SPANIOLE', 5, 1, N'ro'),
    (34, N'AFRICA / ASIA / EUROPA', 6, 1, N'ro'),
    (35, N'CARAIBI', 1, 1, N'it'),
    (36, N'AMERICA', 2, 1, N'it'),
    (37, N'COMBINATO', 3, 1, N'it'),
    (38, N'CIRCUITO', 4, 1, N'it'),
    (39, N'ISOLE SPAGNOLE', 5, 1, N'it'),
    (40, N'AFRICA / ASIA / EUROPA', 6, 1, N'it'),
    (41, N'CARIBE', 1, 1, N'ex'),
    (42, N'AMÉRICA', 2, 1, N'ex'),
    (43, N'COMBINADO', 3, 1, N'ex'),
    (44, N'CIRCUITO', 4, 1, N'ex'),
    (45, N'ISLAS', 5, 1, N'ex'),
    (46, N'AFRICA / ASIA / EUROPA', 6, 1, N'ex'),
    (47, N'HOTEL', 4, 1, N'es'),
    (48, N'HOTEL', 7, 1, N'us'),
    (49, N'HOTEL', 7, 1, N'fr'),
    (50, N'HOTEL', 7, 1, N'pt'),
    (51, N'HOTEL', 7, 1, N'ro'),
    (52, N'HOTEL', 7, 1, N'it')
) v (id_contenedor, nombre, posicion, estado, id_idioma)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.contenedores t WHERE t.id_contenedor = v.id_contenedor);
SET IDENTITY_INSERT Eurovip.contenedores OFF;
GO


/* ==========================================================================================
   CAMBIO 12 — 12/10/2026 — Tabla INICIO (vitrina de la web — aquí se cargan los HOTELES)
   ------------------------------------------------------------------------------------------
   Qué se hizo: se creó igual que ViajeVDT.dbo.inicio. La pantalla "Carga de Hoteles" del
   BackOffice guarda aquí cada hotel (tipo_servicio = 'HOTEL'): nombre, precio desde,
   imagen, campaña, idioma, origen/destino y en qué sección de la web aparece.
   Se agregó un índice para buscar rápido por tipo de servicio e idioma.
   Copiada de: ViajeVDT.dbo.inicio (solo estructura, empieza vacía).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.inicio', 'U') IS NULL
CREATE TABLE Eurovip.inicio (
    id_servicio         int           NOT NULL,   -- número del producto publicado
    id_idioma           char(2)       NOT NULL,   -- idioma/mercado (cambio 7)
    tipo_servicio       varchar(20)   NOT NULL,   -- HOTEL, OFERTA, CIRCUITO…
    id_campanya         smallint      NOT NULL,   -- campaña (cambio 10)
    id_moneda           char(3)       NOT NULL,   -- moneda (cambio 9)
    id_pais_origen      char(2)       NOT NULL,   -- país de salida (ES, PT…)
    origen              varchar(30)   NOT NULL,   -- ciudad de salida
    id_paisDestino      char(2)       NOT NULL,   -- país de destino (DO = Rep. Dominicana)
    id_destino          varchar(1500) NOT NULL,   -- en hoteles: nombre del hotel
    caratulaDestino     varchar(300)  NULL,       -- imagen de portada (URL)
    descripcion         varchar(max)  NOT NULL,   -- texto (ej. régimen: Todo Incluido)
    latitud             varchar(50)   NULL,       -- ubicación en el mapa
    longitud            varchar(50)   NULL,
    precioWeb           money         NULL,       -- precio "desde" que se ve en la web
    id_contenedor       int           NULL,       -- sección de la web (cambio 11)
    contenedor          varchar(30)   NULL,       -- nombre de la sección
    posicion_contenedor int           NULL,       -- orden de la sección
    posicion_destino    int           NULL,       -- orden del hotel dentro de la sección
    estado              bit           NULL,       -- 1 = visible en la web, 0 = oculto
    notas               nvarchar(1000) NULL,
    noches              int           NULL,
    CONSTRAINT PK_Eurovip_inicio PRIMARY KEY CLUSTERED (id_servicio)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_inicio_tipo_idioma' AND object_id = OBJECT_ID('Eurovip.inicio'))
    CREATE NONCLUSTERED INDEX IX_Eurovip_inicio_tipo_idioma ON Eurovip.inicio (tipo_servicio, id_idioma) INCLUDE (estado, id_contenedor);
GO


/* ==========================================================================================
   CAMBIO 13 — 12/10/2026 — Tabla de AUDITORÍA (historial de cambios)
   ------------------------------------------------------------------------------------------
   Qué se hizo: cada vez que alguien crea, edita, activa/desactiva o elimina un hotel,
   se guarda aquí quién lo hizo, cuándo, desde qué IP y qué cambió.
   Copiada de: ViajeVDT.dbo.auditoria_consulta_oferta (solo estructura).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.auditoria_consulta_oferta', 'U') IS NULL
CREATE TABLE Eurovip.auditoria_consulta_oferta (
    id                 int IDENTITY(1,1) NOT NULL,
    ids_servicio       nvarchar(max) NOT NULL,                       -- productos afectados (JSON)
    cantidad_servicios int           NOT NULL,
    tipo_mantenimiento varchar(50)   NOT NULL,                       -- Hotel_Crear, Hotel_Editar…
    usuario            varchar(100)  NOT NULL,                       -- quién lo hizo
    fecha_hora         datetime      NOT NULL CONSTRAINT DF_Eurovip_auditoria_fecha DEFAULT (getdate()),
    cambios_realizados nvarchar(max) NULL,                           -- detalle del cambio (JSON)
    ip_usuario         varchar(50)   NULL,
    navegador          varchar(200)  NULL,
    CONSTRAINT PK_Eurovip_auditoria_consulta_oferta PRIMARY KEY CLUSTERED (id)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_Auditoria_FechaHora')
    CREATE NONCLUSTERED INDEX IX_Eurovip_Auditoria_FechaHora ON Eurovip.auditoria_consulta_oferta (fecha_hora);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_Auditoria_TipoMantenimiento')
    CREATE NONCLUSTERED INDEX IX_Eurovip_Auditoria_TipoMantenimiento ON Eurovip.auditoria_consulta_oferta (tipo_mantenimiento);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_Auditoria_Usuario')
    CREATE NONCLUSTERED INDEX IX_Eurovip_Auditoria_Usuario ON Eurovip.auditoria_consulta_oferta (usuario);
GO


/* ##########################################################################################
   FASE 3 — CIRCUITOS  (12/10/2026)
   ------------------------------------------------------------------------------------------
   Un CIRCUITO es un viaje por varios destinos (ej. "Costa Rica Esencial", 7 noches) con:
     • sus destinos, sus hoteles por categoría (Standard, Superior…) y su itinerario día a día;
   y se VENDE creando "servicios": uno por cada fecha de salida, con su precio, campaña,
   suplementos/tasas, notas y, SOLO SI TIENE, sus vuelos (en Eurovip los vuelos son opcionales).
   Todo se copió de ViajeVDT (10.10.10.29) sin tocar ese servidor, siempre en idioma ES.

   Diferencias a propósito con ViajeVDT (mejoras Eurovip):
     • Vuelos opcionales de verdad: no se graban vuelos "de relleno".
     • servicio_circuito guarda también el origen de venta (para circuitos sin vuelo).
     • spInsertaServicio ya no "se traga" el error: lo registra y lo devuelve (la carga
       completa se deshace si algo falla, nada queda a medias).
     • Se crea spInsertaCircuitoItinerario (en ViajeVDT se llamaba pero no existía).
   ########################################################################################## */


/* ==========================================================================================
   CAMBIO 14 — 12/10/2026 — Catálogos geográficos: PAÍSES y DESTINOS
   ------------------------------------------------------------------------------------------
   Qué se hizo: lista de países y de destinos/ciudades (Punta Cana, Madrid, El Cairo…).
   Se usan para elegir los destinos de un circuito y el origen de venta.
   Copiadas de: ViajeVDT.dbo.pais y dbo.destino — solo filas en español (ES).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.pais', 'U') IS NULL
CREATE TABLE Eurovip.pais (
    id_pais           char(2)     NOT NULL,   -- código (DO, ES…)
    id_idioma         char(2)     NOT NULL,
    nombre_continente varchar(15) NULL,
    nombre            varchar(30) NOT NULL,
    CONSTRAINT PK_Eurovip_pais PRIMARY KEY CLUSTERED (id_pais, id_idioma),
    CONSTRAINT FK_Eurovip_pais_idioma FOREIGN KEY (id_idioma) REFERENCES Eurovip.idioma (id_idioma) ON UPDATE CASCADE
);
GO
-- Datos copiados de ViajeVDT (142 filas; no duplica si ya existen)
INSERT INTO Eurovip.pais (id_pais, id_idioma, nombre_continente, nombre)
SELECT v.id_pais, v.id_idioma, v.nombre_continente, v.nombre FROM (VALUES
    (N'AB', N'ES', NULL, N'Antigua y Barbuda'),
    (N'AD', N'ES', NULL, N'Andorra'),
    (N'AE', N'ES', NULL, N'Emiratos Arabes Unidos'),
    (N'AF', N'ES', NULL, N'Antillas Lesser - Francia'),
    (N'AL', N'ES', N'Europa', N'Albania'),
    (N'AM', N'ES', NULL, N'Armenia'),
    (N'AR', N'ES', NULL, N'Argentina'),
    (N'AT', N'ES', NULL, N'Austria'),
    (N'AU', N'ES', NULL, N'Australia'),
    (N'AW', N'ES', NULL, N'Antillas Holandesas'),
    (N'BA', N'ES', NULL, N'Bahrain'),
    (N'BB', N'ES', NULL, N'Baleares'),
    (N'BD', N'ES', NULL, N'Barbados'),
    (N'BE', N'ES', NULL, N'Bélgica'),
    (N'BG', N'ES', NULL, N'Bulgaria'),
    (N'BH', N'ES', NULL, N'Bosnia y Herzegovina'),
    (N'BI', N'ES', NULL, N'Bielorrusia'),
    (N'BO', N'ES', NULL, N'Bolivia'),
    (N'BR', N'ES', NULL, N'Brasil'),
    (N'BS', N'ES', NULL, N'Bahamas'),
    (N'BZ', N'ES', NULL, N'Escocia'),
    (N'CA', N'ES', NULL, N'Canada'),
    (N'CC', N'ES', NULL, N'Islas Canarias'),
    (N'CH', N'ES', NULL, N'Suiza'),
    (N'CL', N'ES', NULL, N'Chile'),
    (N'CM', N'ES', NULL, N'Camboya'),
    (N'CN', N'ES', NULL, N'China'),
    (N'CO', N'ES', NULL, N'Colombia'),
    (N'CR', N'ES', NULL, N'Costa Rica'),
    (N'CU', N'ES', NULL, N'Cuba'),
    (N'CV', N'ES', NULL, N'Cabo Verde'),
    (N'CY', N'ES', NULL, N'Chipre'),
    (N'CZ', N'ES', NULL, N'Republica Checa'),
    (N'DE', N'ES', NULL, N'Alemania'),
    (N'DK', N'ES', NULL, N'Dinamarca'),
    (N'DO', N'ES', NULL, N'República Dominicana'),
    (N'E1', N'ES', NULL, N'Arabia Saudi'),
    (N'E3', N'ES', NULL, N'Bosnia-herzegovina'),
    (N'EC', N'ES', NULL, N'Ecuador'),
    (N'EE', N'ES', NULL, N'Estonia'),
    (N'EG', N'ES', NULL, N'Egipto'),
    (N'ES', N'ES', NULL, N'España'),
    (N'FI', N'ES', NULL, N'Finlandia'),
    (N'FJ', N'ES', NULL, N'Fiji'),
    (N'FR', N'ES', NULL, N'Francia'),
    (N'GB', N'ES', NULL, N'Gran Bretaña'),
    (N'GF', N'ES', NULL, N'Guayana Francesa'),
    (N'GH', N'ES', NULL, N'Ganha'),
    (N'GM', N'ES', NULL, N'Gambia'),
    (N'GP', N'ES', NULL, N'Guadalupe'),
    (N'GR', N'ES', NULL, N'Grecia'),
    (N'GT', N'ES', NULL, N'Guatemala'),
    (N'HN', N'ES', NULL, N'Honduras'),
    (N'HR', N'ES', NULL, N'Croacia'),
    (N'HU', N'ES', NULL, N'Hungria'),
    (N'ID', N'ES', NULL, N'Indonesia'),
    (N'IE', N'ES', NULL, N'Irlanda'),
    (N'IL', N'ES', NULL, N'Israel'),
    (N'IM', N'ES', NULL, N'Islas Mauricio'),
    (N'IN', N'ES', NULL, N'India'),
    (N'IR', N'ES', NULL, N'Irán'),
    (N'IS', N'ES', NULL, N'Islandia'),
    (N'IT', N'ES', NULL, N'Italia'),
    (N'JM', N'ES', N'', N'Jamaica'),
    (N'JO', N'ES', NULL, N'Jordania'),
    (N'JP', N'ES', NULL, N'Japón'),
    (N'KE', N'ES', NULL, N'Kenya'),
    (N'KR', N'ES', NULL, N'Corea del Sur'),
    (N'KW', N'ES', NULL, N'Kuwait'),
    (N'LB', N'ES', NULL, N'Libano'),
    (N'LI', N'ES', NULL, N'Liechtenstein'),
    (N'LK', N'ES', NULL, N'Sri Lanka'),
    (N'LT', N'ES', NULL, N'Lituania'),
    (N'LU', N'ES', NULL, N'Luxemburgo'),
    (N'LV', N'ES', NULL, N'Letonia'),
    (N'LY', N'ES', NULL, N'Libia'),
    (N'MA', N'ES', NULL, N'Marruecos'),
    (N'MC', N'ES', NULL, N'Mónaco'),
    (N'ME', N'ES', N'America', N'Puerto Morelos'),
    (N'MG', N'ES', NULL, N'Madagascar'),
    (N'MK', N'ES', NULL, N'Macedonia'),
    (N'MO', N'ES', NULL, N'República popular China'),
    (N'MQ', N'ES', NULL, N'Martinica - Francia'),
    (N'MR', N'ES', NULL, N'Myanmar'),
    (N'MT', N'ES', NULL, N'Malta'),
    (N'MU', N'es', N'Africa', N'Mauricio'),
    (N'MV', N'ES', NULL, N'Maldivas'),
    (N'MX', N'ES', NULL, N'México'),
    (N'MY', N'ES', NULL, N'Malaysia'),
    (N'MZ', N'ES', NULL, N'Mozambique'),
    (N'NC', N'ES', NULL, N'Nueva Caledonia - Francia'),
    (N'NG', N'ES', NULL, N'Nigeria'),
    (N'NI', N'ES', NULL, N'Nicaragua'),
    (N'NL', N'ES', NULL, N'Holanda'),
    (N'NO', N'ES', NULL, N'Noruega'),
    (N'NP', N'ES', NULL, N'Nepal'),
    (N'NY', N'ES', NULL, N'Nueva York'),
    (N'NZ', N'ES', NULL, N'Nueva Zelandia'),
    (N'OM', N'ES', NULL, N'Omán'),
    (N'PA', N'ES', NULL, N'Panamá')
) v (id_pais, id_idioma, nombre_continente, nombre)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.pais t WHERE t.id_pais = v.id_pais AND t.id_idioma = v.id_idioma);
INSERT INTO Eurovip.pais (id_pais, id_idioma, nombre_continente, nombre)
SELECT v.id_pais, v.id_idioma, v.nombre_continente, v.nombre FROM (VALUES
    (N'PE', N'ES', NULL, N'Perú'),
    (N'PF', N'ES', NULL, N'Polinesia Francesa'),
    (N'PH', N'ES', NULL, N'Filipinas'),
    (N'PL', N'ES', NULL, N'Polonia'),
    (N'PP', N'ES', NULL, N'Palermo'),
    (N'PR', N'ES', NULL, N'Puerto Rico'),
    (N'PT', N'ES', NULL, N'Portugal'),
    (N'PY', N'ES', NULL, N'Paraguay'),
    (N'QA', N'ES', NULL, N'Qatar'),
    (N'RO', N'ES', NULL, N'Rumania'),
    (N'RU', N'ES', NULL, N'Rusia'),
    (N'SA', N'ES', N'Europa', N'Arabia Saudi'),
    (N'SB', N'ES', NULL, N'Serbia'),
    (N'SC', N'ES', NULL, N'Seychelles'),
    (N'SE', N'ES', NULL, N'Suecia'),
    (N'SG', N'ES', NULL, N'República de Singapur'),
    (N'SI', N'ES', NULL, N'Eslovenia'),
    (N'SK', N'ES', NULL, N'Eslovaquia'),
    (N'SL', N'ES', NULL, N'St. Lucia'),
    (N'SM', N'ES', NULL, N'San Marino'),
    (N'SN', N'ES', NULL, N'Senegal'),
    (N'SU', N'ES', NULL, N'Sudán'),
    (N'SV', N'ES', NULL, N'El Salvador'),
    (N'SY', N'ES', NULL, N'Siria'),
    (N'TH', N'ES', NULL, N'Tailandia'),
    (N'TN', N'ES', NULL, N'Túnez'),
    (N'TR', N'ES', NULL, N'Turquia'),
    (N'TT', N'ES', NULL, N'Trinidad y Tobago'),
    (N'TW', N'ES', NULL, N'República de China'),
    (N'TZ', N'ES', NULL, N'Tanzania'),
    (N'UA', N'ES', NULL, N'Ucrania'),
    (N'US', N'ES', NULL, N'Estados Unidos'),
    (N'UY', N'ES', NULL, N'Uruguay'),
    (N'UZ', N'ES', NULL, N'Uzbekistán'),
    (N'VE', N'ES', NULL, N'Venezuela'),
    (N'VI', N'ES', NULL, N'St. Vincent y las Granadinas'),
    (N'VN', N'ES', NULL, N'Vietnam'),
    (N'ww', N'ES', N'Am', N'America'),
    (N'XX', N'ES', NULL, N'Desconocido'),
    (N'YU', N'ES', NULL, N'Yugoslavia'),
    (N'ZA', N'ES', NULL, N'Sudáfrica'),
    (N'ZW', N'ES', NULL, N'Zimbawe')
) v (id_pais, id_idioma, nombre_continente, nombre)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.pais t WHERE t.id_pais = v.id_pais AND t.id_idioma = v.id_idioma);
GO
IF OBJECT_ID('Eurovip.destino', 'U') IS NULL
CREATE TABLE Eurovip.destino (
    id_destino  varchar(30)  NOT NULL,   -- nombre del destino (ej. Punta Cana)
    id_idioma   char(2)      NOT NULL,
    id_pais     char(2)      NOT NULL,
    caratula    varchar(300) NULL,
    id          int          NULL,
    informacion varchar(max) NULL,
    latitud     varchar(50)  NULL,
    longitud    varchar(50)  NULL,
    CONSTRAINT PK_Eurovip_destino PRIMARY KEY CLUSTERED (id_destino, id_idioma, id_pais),
    CONSTRAINT FK_Eurovip_destino_pais FOREIGN KEY (id_pais, id_idioma) REFERENCES Eurovip.pais (id_pais, id_idioma) ON UPDATE CASCADE
);
GO
-- Datos: copia masiva desde ViajeVDT →  SELECT * FROM dbo.destino WHERE id_idioma = 'ES'   (≈7.320 filas)


/* ==========================================================================================
   CAMBIO 15 — 12/10/2026 — HOTELES (alojamientos)
   ------------------------------------------------------------------------------------------
   Qué se hizo: catálogo de hoteles y su tipo (Hotel / Crucero). Se usan para asignar los
   hoteles de cada circuito por categoría.
   Copiadas de: ViajeVDT.dbo.tipo_alojamiento y dbo.alojamiento — solo filas ES.
   ========================================================================================== */
IF OBJECT_ID('Eurovip.tipo_alojamiento', 'U') IS NULL
CREATE TABLE Eurovip.tipo_alojamiento (
    id_tipo_alojamiento varchar(20)  NOT NULL,
    descripcion         varchar(max) NOT NULL,
    CONSTRAINT PK_Eurovip_tipo_alojamiento PRIMARY KEY CLUSTERED (id_tipo_alojamiento)
);
GO
-- Datos copiados de ViajeVDT (2 filas; no duplica si ya existen)
INSERT INTO Eurovip.tipo_alojamiento (id_tipo_alojamiento, descripcion)
SELECT v.id_tipo_alojamiento, v.descripcion FROM (VALUES
    (N'CRUCERO', N'Crucero'),
    (N'HOTEL', N'Hotel')
) v (id_tipo_alojamiento, descripcion)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.tipo_alojamiento t WHERE t.id_tipo_alojamiento = v.id_tipo_alojamiento);
GO
IF OBJECT_ID('Eurovip.alojamiento', 'U') IS NULL
CREATE TABLE Eurovip.alojamiento (
    id_alojamiento      int           NOT NULL,   -- número del hotel
    id_idioma           char(2)       NOT NULL,
    id_tipo_alojamiento varchar(20)   NOT NULL,   -- HOTEL / CRUCERO
    id_destino          varchar(30)   NOT NULL,   -- destino donde está
    id_pais             char(2)       NOT NULL,
    nombre              varchar(100)  NOT NULL,
    caratula            varchar(300)  NULL,
    descripcion         varchar(max)  NULL,
    grupo               int           NULL,
    categoria           varchar(15)   NOT NULL,   -- estrellas / categoría del hotel
    latitude            nvarchar(100) NULL,
    longitude           nvarchar(100) NULL,
    categoria_circuito  varchar(50)   NULL,
    circuito            bit           NULL,
    activo              bit           NOT NULL CONSTRAINT DF_Eurovip_alojamiento_activo DEFAULT ((1)),
    CONSTRAINT PK_Eurovip_alojamiento PRIMARY KEY CLUSTERED (id_alojamiento, id_idioma),
    CONSTRAINT FK_Eurovip_alojamiento_tipo FOREIGN KEY (id_tipo_alojamiento) REFERENCES Eurovip.tipo_alojamiento (id_tipo_alojamiento) ON UPDATE CASCADE
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_alojamiento_nombre' AND object_id = OBJECT_ID('Eurovip.alojamiento'))
    CREATE NONCLUSTERED INDEX IX_Eurovip_alojamiento_nombre ON Eurovip.alojamiento (id_idioma, nombre) INCLUDE (id_destino, id_pais, categoria, caratula, activo);
GO
-- Datos: copia masiva desde ViajeVDT →  SELECT * FROM dbo.alojamiento WHERE id_idioma = 'ES'   (≈9.560 filas)


/* ==========================================================================================
   CAMBIO 16 — 12/10/2026 — PROVEEDORES, AEROPUERTOS, RÉGIMEN, TASAS y CATEGORÍAS
   ------------------------------------------------------------------------------------------
   Qué se hizo:
     • proveedor / tipo_proveedor: tour operadores (dueños del circuito) y aerolíneas.
     • estacion / tipo_estacion: aeropuertos (solo se usan si el circuito lleva vuelo).
     • regimen: Todo Incluido, Media Pensión… (para el itinerario día a día).
     • tasa: suplementos y tasas que se suman al precio (Tasas de Origen, Visado…).
     • categoria: categorías de hoteles del circuito (Standard, Superior, Deluxe…).
   Copiadas de: ViajeVDT (proveedor y estacion por copia masiva; el resto aquí abajo).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.tipo_proveedor', 'U') IS NULL
CREATE TABLE Eurovip.tipo_proveedor (
    id_tipo_proveedor varchar(20) NOT NULL,
    CONSTRAINT PK_Eurovip_tipo_proveedor PRIMARY KEY CLUSTERED (id_tipo_proveedor)
);
GO
-- Datos copiados de ViajeVDT (2 filas; no duplica si ya existen)
INSERT INTO Eurovip.tipo_proveedor (id_tipo_proveedor)
SELECT v.id_tipo_proveedor FROM (VALUES
    (N'Aerolinea'),
    (N'Tour Operador')
) v (id_tipo_proveedor)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.tipo_proveedor t WHERE t.id_tipo_proveedor = v.id_tipo_proveedor);
GO
IF OBJECT_ID('Eurovip.proveedor', 'U') IS NULL
CREATE TABLE Eurovip.proveedor (
    id_proveedor      varchar(10) NOT NULL,
    id_tipo_proveedor varchar(20) NOT NULL,   -- Aerolinea / Tour Operador
    nombre            varchar(50) NOT NULL,
    CONSTRAINT PK_Eurovip_proveedor PRIMARY KEY CLUSTERED (id_proveedor),
    CONSTRAINT FK_Eurovip_proveedor_tipo FOREIGN KEY (id_tipo_proveedor) REFERENCES Eurovip.tipo_proveedor (id_tipo_proveedor) ON UPDATE CASCADE
);
GO
-- Datos: copia masiva desde ViajeVDT →  SELECT * FROM dbo.proveedor   (≈1.335 filas)

IF OBJECT_ID('Eurovip.tipo_estacion', 'U') IS NULL
CREATE TABLE Eurovip.tipo_estacion (
    id_tipo_estacion varchar(15) NOT NULL,
    CONSTRAINT PK_Eurovip_tipo_estacion PRIMARY KEY CLUSTERED (id_tipo_estacion)
);
GO
-- Datos copiados de ViajeVDT (1 filas; no duplica si ya existen)
INSERT INTO Eurovip.tipo_estacion (id_tipo_estacion)
SELECT v.id_tipo_estacion FROM (VALUES
    (N'AEROPUERTO')
) v (id_tipo_estacion)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.tipo_estacion t WHERE t.id_tipo_estacion = v.id_tipo_estacion);
GO
IF OBJECT_ID('Eurovip.estacion', 'U') IS NULL
CREATE TABLE Eurovip.estacion (
    id_estacion      varchar(10) NOT NULL,   -- código del aeropuerto (PUJ, MAD…)
    id_destino       varchar(30) NOT NULL,   -- ciudad
    id_idioma        char(2)     NOT NULL,
    id_pais          char(2)     NOT NULL,
    id_tipo_estacion varchar(15) NOT NULL,
    nombre           varchar(80) NOT NULL,
    CONSTRAINT PK_Eurovip_estacion PRIMARY KEY CLUSTERED (id_estacion, id_destino, id_idioma, id_pais),
    CONSTRAINT FK_Eurovip_estacion_tipo FOREIGN KEY (id_tipo_estacion) REFERENCES Eurovip.tipo_estacion (id_tipo_estacion) ON UPDATE CASCADE
);
GO
-- Datos: copia masiva desde ViajeVDT →  SELECT * FROM dbo.estacion WHERE id_idioma = 'ES'   (≈3.715 filas)

IF OBJECT_ID('Eurovip.regimen', 'U') IS NULL
CREATE TABLE Eurovip.regimen (
    id_regimen  char(2)      NOT NULL,   -- TI, MP, AD…
    id_idioma   char(2)      NOT NULL,
    descripcion varchar(max) NOT NULL,
    CONSTRAINT PK_Eurovip_regimen PRIMARY KEY CLUSTERED (id_regimen, id_idioma),
    CONSTRAINT FK_Eurovip_regimen_idioma FOREIGN KEY (id_idioma) REFERENCES Eurovip.idioma (id_idioma) ON UPDATE CASCADE
);
GO
-- Datos copiados de ViajeVDT (11 filas; no duplica si ya existen)
INSERT INTO Eurovip.regimen (id_regimen, id_idioma, descripcion)
SELECT v.id_regimen, v.id_idioma, v.descripcion FROM (VALUES
    (N'AD', N'ES', N'Alojamiento Y Desayuno AD'),
    (N'HD', N'ES', N'Alojamiento Y Desayuno'),
    (N'MP', N'ES', N'Media Pensión MP'),
    (N'PC', N'ES', N'Pensión Completa PC'),
    (N'PT', N'ES', N'Solo Habitación.'),
    (N'RO', N'ES', N'Solo Habitación..'),
    (N'SA', N'ES', N'Solo Habitación'),
    (N'SH', N'ES', N'Solo Habitación SH'),
    (N'SI', N'ES', N'Súper Todo Incluído Pio Gusto'),
    (N'SP', N'ES', N'Según Programa SP'),
    (N'TI', N'ES', N'Todo Incluido TI')
) v (id_regimen, id_idioma, descripcion)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.regimen t WHERE t.id_regimen = v.id_regimen AND t.id_idioma = v.id_idioma);
GO
IF OBJECT_ID('Eurovip.tasa', 'U') IS NULL
CREATE TABLE Eurovip.tasa (
    id_tasa      varchar(50) NOT NULL,   -- nombre del suplemento/tasa
    id_idioma    char(2)     NOT NULL,
    id_tipo_tasa int         NULL,
    CONSTRAINT PK_Eurovip_tasa PRIMARY KEY CLUSTERED (id_tasa, id_idioma),
    CONSTRAINT FK_Eurovip_tasa_idioma FOREIGN KEY (id_idioma) REFERENCES Eurovip.idioma (id_idioma) ON UPDATE CASCADE
);
GO
-- Datos copiados de ViajeVDT (47 filas; no duplica si ya existen)
INSERT INTO Eurovip.tasa (id_tasa, id_idioma, id_tipo_tasa)
SELECT v.id_tasa, v.id_idioma, v.id_tipo_tasa FROM (VALUES
    (N'Anulación VDT PLUS', N'ES', 101),
    (N'Asistencia VDT PLUS', N'ES', 101),
    (N'Campaña', N'ES', 11),
    (N'Carburante', N'ES', 2),
    (N'Cargo Barco', N'ES', 9),
    (N'Categoría Gran Clase', N'ES', 3),
    (N'Categoría Superior', N'ES', 1),
    (N'Cena de Noche Vieja', N'ES', 5),
    (N'Conexion', N'ES', NULL),
    (N'Descuento general', N'ES', 32),
    (N'Descuento por Bebé', N'ES', 34),
    (N'Descuento por Niño', N'ES', 33),
    (N'excursión', N'ES', 100),
    (N'f', N'ES', 56),
    (N'Gala de Navidad', N'ES', 4),
    (N'Habitación cuadruple', N'ES', 31),
    (N'Habitación Individual', N'ES', 22),
    (N'Habitación Junior', N'ES', 23),
    (N'Habitación Suite', N'ES', 24),
    (N'Habitación triple', N'ES', 30),
    (N'ITBIS', N'ES', 14),
    (N'Multiasistencia Plus con Anulación Receptivo', N'ES', 101),
    (N'Multiasistencia VDT PLUS', N'ES', 101),
    (N'Prueba', N'ES', 111),
    (N'Prueba3', N'ES', NULL),
    (N'Seguro', N'ES', NULL),
    (N'Sin Seguro', N'ES', 101),
    (N'Sup. Origen BCN', N'ES', 41),
    (N'Sup. Origen MAD', N'ES', 40),
    (N'Sup. Origen OTR', N'ES', 42),
    (N'Suplemento Adicional', N'ES', 102),
    (N'Suplemento Ampliacion Estancia', N'ES', 6),
    (N'Suplemento de Origen', N'ES', 8),
    (N'Suplemento de Salida', N'ES', 12),
    (N'Suplemento Dia', N'ES', NULL),
    (N'Suplemento Individual', N'ES', NULL),
    (N'Suplemento Noche extra', N'ES', 21),
    (N'Tasa Entrada país Destino', N'ES', 16),
    (N'Tasa Origen BCN', N'ES', 51),
    (N'Tasa Origen MAD', N'ES', 50),
    (N'Tasa Origen OTR', N'ES', 52),
    (N'Tasa Salida País destino', N'ES', 15),
    (N'Tasas de Origen', N'ES', 7),
    (N'Traslado', N'ES', 25),
    (N'Visado', N'ES', 10),
    (N'Visado ES', N'ES', NULL),
    (N'Vistas', N'ES', 20)
) v (id_tasa, id_idioma, id_tipo_tasa)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.tasa t WHERE t.id_tasa = v.id_tasa AND t.id_idioma = v.id_idioma);
GO
IF OBJECT_ID('Eurovip.categoria', 'U') IS NULL
CREATE TABLE Eurovip.categoria (
    categoria   varchar(20)  NOT NULL,   -- Standard, Superior…
    descripcion varchar(200) NULL,
    CONSTRAINT PK_Eurovip_categoria PRIMARY KEY CLUSTERED (categoria)
);
GO
-- Datos copiados de ViajeVDT (4 filas; no duplica si ya existen)
INSERT INTO Eurovip.categoria (categoria, descripcion)
SELECT v.categoria, v.descripcion FROM (VALUES
    (N'Deluxe', N'Deluxe'),
    (N'Deluxe Plus', N'Deluxe Plus'),
    (N'Standard', N'Standard'),
    (N'Superior', N'Superior')
) v (categoria, descripcion)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.categoria t WHERE t.categoria = v.categoria);
GO


/* ==========================================================================================
   CAMBIO 17 — 12/10/2026 — CONFIGURACIÓN del sistema
   ------------------------------------------------------------------------------------------
   Qué se hizo: tabla clave/valor. Por ahora guarda "precio_base_minimo" = 300: ninguna salida
   de circuito se puede cargar con precio base menor (igual que en ViajeVDT).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.config_sistema', 'U') IS NULL
CREATE TABLE Eurovip.config_sistema (
    clave                varchar(100) NOT NULL,
    valor                varchar(500) NOT NULL,
    descripcion          varchar(300) NULL,
    fecha_modificacion   datetime     NULL CONSTRAINT DF_Eurovip_config_fecha DEFAULT (getdate()),
    usuario_modificacion varchar(100) NULL,
    CONSTRAINT PK_Eurovip_config_sistema PRIMARY KEY CLUSTERED (clave)
);
GO
IF NOT EXISTS (SELECT 1 FROM Eurovip.config_sistema WHERE clave = 'precio_base_minimo')
    INSERT INTO Eurovip.config_sistema (clave, valor, descripcion)
    VALUES ('precio_base_minimo', '300', 'Precio base mínimo aceptado al cargar salidas de circuitos');
GO


/* ==========================================================================================
   CAMBIO 18 — 12/10/2026 — CATÁLOGO DE CIRCUITOS
   ------------------------------------------------------------------------------------------
   Qué se hizo (4 tablas, empiezan vacías):
     • circuito ......................... el circuito: código, nombre, noches, proveedor, imagen.
     • circuito_destino ................. qué destinos recorre.
     • circuito_itinerario .............. qué se hace cada día (título, descripción, visitas…).
     • servicio_circuito_alojamiento .... qué hoteles usa, por categoría.
   Copiadas de: ViajeVDT (solo estructura). En Eurovip cada circuito es solo ES (1 nombre).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.circuito', 'U') IS NULL
CREATE TABLE Eurovip.circuito (
    nombre_circuito varchar(50)  NOT NULL,   -- nombre visible del circuito
    id_idioma       char(2)      NOT NULL,   -- siempre ES en Eurovip
    id_proveedor    varchar(10)  NOT NULL,   -- tour operador
    noches          smallint     NOT NULL,
    caratula        varchar(200) NULL,       -- imagen de portada
    itinerario      varchar(max) NULL,       -- resumen del recorrido
    id              varchar(100) NULL,       -- código único del circuito
    CONSTRAINT PK_Eurovip_circuito PRIMARY KEY CLUSTERED (nombre_circuito, id_idioma),
    CONSTRAINT FK_Eurovip_circuito_idioma    FOREIGN KEY (id_idioma)    REFERENCES Eurovip.idioma (id_idioma) ON UPDATE CASCADE,
    CONSTRAINT FK_Eurovip_circuito_proveedor FOREIGN KEY (id_proveedor) REFERENCES Eurovip.proveedor (id_proveedor) ON UPDATE CASCADE
);
GO
IF OBJECT_ID('Eurovip.circuito_destino', 'U') IS NULL
CREATE TABLE Eurovip.circuito_destino (
    nombre_circuito varchar(50)  NOT NULL,
    id_destino      varchar(30)  NOT NULL,
    id_idioma       char(2)      NOT NULL,
    id_pais         char(2)      NOT NULL,
    id              varchar(100) NULL,
    CONSTRAINT PK_Eurovip_circuito_destino PRIMARY KEY CLUSTERED (nombre_circuito, id_destino, id_idioma, id_pais),
    CONSTRAINT FK_Eurovip_circuito_destino_circuito FOREIGN KEY (nombre_circuito, id_idioma) REFERENCES Eurovip.circuito (nombre_circuito, id_idioma),
    CONSTRAINT FK_Eurovip_circuito_destino_destino  FOREIGN KEY (id_destino, id_idioma, id_pais) REFERENCES Eurovip.destino (id_destino, id_idioma, id_pais) ON UPDATE CASCADE
);
GO
IF OBJECT_ID('Eurovip.circuito_itinerario', 'U') IS NULL
CREATE TABLE Eurovip.circuito_itinerario (
    dia              int          NOT NULL,   -- 1, 2, 3…
    titulo           varchar(200) NOT NULL,
    nombre_circuito  varchar(50)  NOT NULL,
    id_idioma        char(2)      NOT NULL,
    descripcion      varchar(max) NOT NULL,
    visitas          varchar(max) NULL,
    regimen          varchar(200) NULL,
    transporte       varchar(200) NULL,
    tipo_alojamiento varchar(20)  NULL,
    caratula         varchar(200) NULL,       -- imagen del día
    CONSTRAINT PK_Eurovip_circuito_itinerario PRIMARY KEY CLUSTERED (nombre_circuito, id_idioma, dia),
    CONSTRAINT FK_Eurovip_circuito_itinerario_circuito FOREIGN KEY (nombre_circuito, id_idioma) REFERENCES Eurovip.circuito (nombre_circuito, id_idioma)
);
GO
IF OBJECT_ID('Eurovip.servicio_circuito_alojamiento', 'U') IS NULL
CREATE TABLE Eurovip.servicio_circuito_alojamiento (
    nombre_circuito varchar(50)  NOT NULL,
    id_idioma       char(2)      NOT NULL,
    id_alojamiento  int          NOT NULL,
    categoria       varchar(50)  NOT NULL,   -- Standard, Superior…
    caratula        varchar(200) NULL,
    CONSTRAINT PK_Eurovip_servicio_circuito_alojamiento PRIMARY KEY CLUSTERED (nombre_circuito, id_idioma, id_alojamiento, categoria),
    CONSTRAINT FK_Eurovip_sca_alojamiento FOREIGN KEY (id_alojamiento, id_idioma) REFERENCES Eurovip.alojamiento (id_alojamiento, id_idioma),
    CONSTRAINT FK_Eurovip_sca_circuito    FOREIGN KEY (nombre_circuito, id_idioma) REFERENCES Eurovip.circuito (nombre_circuito, id_idioma)
);
GO


/* ==========================================================================================
   CAMBIO 19 — 12/10/2026 — CARGA DE CIRCUITOS (lo que se vende)
   ------------------------------------------------------------------------------------------
   Qué se hizo (empiezan vacías):
     • servicio .............. una fila por SALIDA vendible (fecha, precio, campaña, descuento).
     • servicio_circuito ..... a qué circuito pertenece cada salida y sus fechas desde/hasta.
                               ➕ Eurovip: guarda país y ciudad de origen de venta (sirve
                               cuando el circuito NO lleva vuelo).
     • servicio_itinerario ... los VUELOS de la salida (solo si tiene; opcional).
     • servicio_tasa ......... suplementos/tasas de cada salida.
     • servicio_nota ......... notas (incluye, no incluye, condiciones…).
     • alojamiento_circuito .. vitrina de circuitos para la web (precio desde por circuito).
     • Log_Cargas ............ registro de cada carga (quién, cuándo, cuántas salidas, estado).
     • LogErroresServicio .... errores de base de datos al cargar.
   Copiadas de: ViajeVDT (solo estructura).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.servicio', 'U') IS NULL
CREATE TABLE Eurovip.servicio (
    id_servicio        int IDENTITY(1,1) NOT NULL,
    id_tipo_servicio   varchar(20)   NOT NULL,                     -- CIRCUITO
    id_idioma          char(2)       NOT NULL,
    id_moneda          char(3)       NOT NULL,
    id_campanya        smallint      NOT NULL,
    id_Proveedor       varchar(10)   NOT NULL,
    apertura           date          NOT NULL CONSTRAINT DF_Eurovip_servicio_apertura DEFAULT (getdate()),
    usuario            varchar(15)   NOT NULL CONSTRAINT DF_Eurovip_servicio_usuario DEFAULT ('eurovip'),
    precio_base        money         NOT NULL CONSTRAINT DF_Eurovip_servicio_precio_base DEFAULT ((0.00)),
    descuento          numeric(6,2)  NOT NULL CONSTRAINT DF_Eurovip_servicio_descuento DEFAULT ((0)),
    precio_mas_menos   money         NOT NULL CONSTRAINT DF_Eurovip_servicio_mas_menos DEFAULT ((0)),
    ajuste             money         NULL     CONSTRAINT DF_Eurovip_servicio_ajuste DEFAULT ((0)),
    nota               varchar(max)  NULL,
    noches             smallint      NOT NULL,
    codigo             varchar(30)   NULL,                         -- código de la carga
    baja               date          NULL,
    nombre             varchar(50)   NULL,                         -- nombre del circuito
    activo             bit           NOT NULL CONSTRAINT DF_Eurovip_servicio_activo DEFAULT ((1)),
    precio             AS ([precio_base]-[precio_base]*([descuento]/(100))),
    categoria_circuito varchar(20)   NULL,
    CONSTRAINT PK_Eurovip_servicio PRIMARY KEY CLUSTERED (id_servicio),
    CONSTRAINT FK_Eurovip_servicio_idioma    FOREIGN KEY (id_idioma) REFERENCES Eurovip.idioma (id_idioma) ON UPDATE CASCADE,
    CONSTRAINT FK_Eurovip_servicio_campanya  FOREIGN KEY (id_campanya, id_idioma) REFERENCES Eurovip.campanya (id_campanya, id_idioma),
    CONSTRAINT FK_Eurovip_servicio_moneda    FOREIGN KEY (id_moneda) REFERENCES Eurovip.moneda (id_moneda) ON UPDATE CASCADE,
    CONSTRAINT FK_Eurovip_servicio_tipo      FOREIGN KEY (id_tipo_servicio) REFERENCES Eurovip.tipo_servicio (id_tipo_servicio) ON UPDATE CASCADE,
    CONSTRAINT FK_Eurovip_servicio_proveedor FOREIGN KEY (id_Proveedor) REFERENCES Eurovip.proveedor (id_proveedor) ON UPDATE CASCADE,
    CONSTRAINT FK_Eurovip_servicio_categoria FOREIGN KEY (categoria_circuito) REFERENCES Eurovip.categoria (categoria),
    CONSTRAINT CK_Eurovip_servicio_categoria_circuito CHECK ([id_tipo_servicio]='CIRCUITO' AND [categoria_circuito] IS NOT NULL OR [id_tipo_servicio]<>'CIRCUITO')
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_servicio_busqueda' AND object_id = OBJECT_ID('Eurovip.servicio'))
    CREATE NONCLUSTERED INDEX IX_Eurovip_servicio_busqueda ON Eurovip.servicio (id_idioma, id_tipo_servicio, id_campanya, id_moneda, activo) INCLUDE (noches, precio_base, precio_mas_menos, descuento, id_Proveedor, nombre, codigo);
GO
IF OBJECT_ID('Eurovip.servicio_circuito', 'U') IS NULL
CREATE TABLE Eurovip.servicio_circuito (
    id_servicio     int         NOT NULL,
    nombre_circuito varchar(50) NOT NULL,
    id_idoma        char(2)     NOT NULL,   -- (se mantiene el nombre de ViajeVDT, con su errata)
    desde           date        NOT NULL,   -- fecha de salida
    hasta           date        NOT NULL,   -- fecha de regreso
    id_pais_origen  char(2)     NULL,       -- ➕ Eurovip: país de origen de venta
    origen          varchar(30) NULL,       -- ➕ Eurovip: ciudad de origen de venta
    CONSTRAINT PK_Eurovip_servicio_circuito PRIMARY KEY CLUSTERED (id_servicio, nombre_circuito, id_idoma),
    CONSTRAINT FK_Eurovip_servicio_circuito_circuito FOREIGN KEY (nombre_circuito, id_idoma) REFERENCES Eurovip.circuito (nombre_circuito, id_idioma) ON UPDATE CASCADE,
    CONSTRAINT FK_Eurovip_servicio_circuito_servicio FOREIGN KEY (id_servicio) REFERENCES Eurovip.servicio (id_servicio)
);
GO
IF OBJECT_ID('Eurovip.servicio_itinerario', 'U') IS NULL
CREATE TABLE Eurovip.servicio_itinerario (
    id_servicio         int         NOT NULL,
    segmento            smallint    NOT NULL,   -- 1, 2, 3… (orden del vuelo)
    id_proveedor        varchar(10) NOT NULL,   -- aerolínea
    id_estacion_origen  varchar(10) NOT NULL,   -- aeropuerto de salida
    origen              varchar(30) NOT NULL,
    id_idioma           char(2)     NOT NULL,
    id_pais_origen      char(2)     NOT NULL,
    id_estacion_destino varchar(10) NOT NULL,   -- aeropuerto de llegada
    destino             varchar(30) NOT NULL,
    id_pais_destino     char(2)     NOT NULL,
    vuelo               varchar(15) NULL,       -- número de vuelo
    fecha_salida        date        NOT NULL,
    hora_salida         varchar(10) NOT NULL,
    fecha_regreso       date        NOT NULL,   -- fecha de llegada del segmento
    hora_regreso        varchar(10) NOT NULL,
    clase               char(1)     NOT NULL,
    conexion            bit         NOT NULL,
    CONSTRAINT PK_Eurovip_servicio_itinerario PRIMARY KEY CLUSTERED (id_servicio, segmento),
    CONSTRAINT FK_Eurovip_servicio_itinerario_origen    FOREIGN KEY (id_estacion_origen, origen, id_idioma, id_pais_origen) REFERENCES Eurovip.estacion (id_estacion, id_destino, id_idioma, id_pais),
    CONSTRAINT FK_Eurovip_servicio_itinerario_destino   FOREIGN KEY (id_estacion_destino, destino, id_idioma, id_pais_destino) REFERENCES Eurovip.estacion (id_estacion, id_destino, id_idioma, id_pais),
    CONSTRAINT FK_Eurovip_servicio_itinerario_proveedor FOREIGN KEY (id_proveedor) REFERENCES Eurovip.proveedor (id_proveedor),
    CONSTRAINT FK_Eurovip_servicio_itinerario_servicio  FOREIGN KEY (id_servicio) REFERENCES Eurovip.servicio (id_servicio)
);
GO
IF OBJECT_ID('Eurovip.servicio_tasa', 'U') IS NULL
CREATE TABLE Eurovip.servicio_tasa (
    id_Servicio int         NOT NULL,
    id_tasa     varchar(50) NOT NULL,   -- suplemento (cambio 16)
    id_idioma   char(2)     NOT NULL,
    valor       money       NOT NULL,   -- importe que se suma al precio
    CONSTRAINT PK_Eurovip_servicio_tasa PRIMARY KEY CLUSTERED (id_Servicio, id_tasa, id_idioma),
    CONSTRAINT FK_Eurovip_servicio_tasa_servicio FOREIGN KEY (id_Servicio) REFERENCES Eurovip.servicio (id_servicio),
    CONSTRAINT FK_Eurovip_servicio_tasa_tasa     FOREIGN KEY (id_tasa, id_idioma) REFERENCES Eurovip.tasa (id_tasa, id_idioma) ON UPDATE CASCADE
);
GO
IF OBJECT_ID('Eurovip.servicio_nota', 'U') IS NULL
CREATE TABLE Eurovip.servicio_nota (
    id_servicio int          NOT NULL,
    secuencia   smallint     NOT NULL,
    id_idioma   char(3)      NOT NULL,
    tipo        varchar(50)  NOT NULL,   -- Incluye, No incluye, Condiciones…
    nota1       varchar(max) NOT NULL,
    nota2       varchar(max) NOT NULL,
    CONSTRAINT PK_Eurovip_servicio_nota PRIMARY KEY CLUSTERED (id_servicio, secuencia, id_idioma),
    CONSTRAINT FK_Eurovip_servicio_nota_servicio FOREIGN KEY (id_servicio) REFERENCES Eurovip.servicio (id_servicio)
);
GO
IF OBJECT_ID('Eurovip.alojamiento_circuito', 'U') IS NULL
CREATE TABLE Eurovip.alojamiento_circuito (
    id_servicio      int          NOT NULL,
    id_idioma        char(2)      NOT NULL,
    id_tipo_servicio varchar(30)  NOT NULL,
    id_campanya      smallint     NOT NULL,
    id_moneda        char(3)      NOT NULL,
    id_pais_origen   char(2)      NOT NULL,
    id_origen        varchar(30)  NOT NULL,
    id_pais_destino  varchar(max) NOT NULL,
    id_destino       varchar(max) NOT NULL,
    caratula         varchar(max) NULL,
    precio_web       money        NOT NULL,
    nombre_circuito  varchar(max) NOT NULL,
    CONSTRAINT PK_Eurovip_alojamiento_circuito PRIMARY KEY CLUSTERED (id_servicio)
);
GO
IF OBJECT_ID('Eurovip.Log_Cargas', 'U') IS NULL
CREATE TABLE Eurovip.Log_Cargas (
    id                      int IDENTITY(1,1) NOT NULL,
    tipo_carga              nvarchar(200)  NOT NULL,
    usuario                 nvarchar(400)  NOT NULL,
    ip                      nvarchar(100)  NULL,
    navegador               nvarchar(1000) NULL,
    fecha_hora              datetime       NOT NULL CONSTRAINT DF_Eurovip_Log_Cargas_fecha DEFAULT (getdate()),
    origen                  nvarchar(max)  NULL,
    destino                 nvarchar(1000) NULL,
    campanyas               nvarchar(max)  NULL,
    regimenes               nvarchar(max)  NULL,
    descuento_carga         int            NULL,
    descuento_ex            int            NULL,
    noches                  int            NULL,
    fecha_inicio            date           NULL,
    fecha_fin               date           NULL,
    dias                    nvarchar(400)  NULL,
    codigo_oferta           nvarchar(200)  NULL,
    habitacion              int            NULL,
    estatus                 nvarchar(100)  NULL CONSTRAINT DF_Eurovip_Log_Cargas_estatus DEFAULT (N'Sin Estatus'),
    mensaje                 nvarchar(max)  NULL,
    datos                   nvarchar(max)  NULL,
    fecha_hora_fin          datetime       NULL,
    duracion                datetime       NULL,
    registros_insertados    int            NULL,
    registros_no_insertados int            NULL,
    CONSTRAINT PK_Eurovip_Log_Cargas PRIMARY KEY CLUSTERED (id)
);
GO
IF OBJECT_ID('Eurovip.LogErroresServicio', 'U') IS NULL
CREATE TABLE Eurovip.LogErroresServicio (
    LogID          int IDENTITY(1,1) NOT NULL,
    ErrorNumber    int            NULL,
    ErrorMessage   nvarchar(4000) NULL,
    ErrorProcedure nvarchar(128)  NULL,
    ErrorLine      int            NULL,
    FechaError     datetime       NULL,
    UsuarioDB      nvarchar(128)  NULL,
    CONSTRAINT PK_Eurovip_LogErroresServicio PRIMARY KEY CLUSTERED (LogID)
);
GO


/* ==========================================================================================
   CAMBIO 20 — 12/10/2026 — FUNCIONES de cálculo
   ------------------------------------------------------------------------------------------
   Qué se hizo:
     • fnSegmento ........... siguiente número de vuelo de una salida.
     • fnTotalTasa .......... suma de suplementos/tasas de una salida.
     • fnPrecioWeb .......... precio final de una salida = base + más/menos + tasas − descuento.
                              (En ViajeVDT también suma hotel/habitación; en circuitos eso
                              siempre es 0, por eso aquí no se incluye.)
     • fnAlojamientoCircuito  la salida más barata de cada circuito para la vitrina web.
                              ➕ Eurovip: funciona CON o SIN vuelo (el origen sale del vuelo
                              si existe; si no, del origen de venta guardado en servicio_circuito).
   ========================================================================================== */
CREATE OR ALTER FUNCTION Eurovip.fnSegmento (@numero INT)
RETURNS SMALLINT
AS
BEGIN
    RETURN (SELECT ISNULL(MAX(segmento), 0) + 1 FROM Eurovip.servicio_itinerario WHERE id_servicio = @numero);
END
GO
CREATE OR ALTER FUNCTION Eurovip.fnTotalTasa (@id_servicio INT)
RETURNS MONEY
AS
BEGIN
    RETURN (SELECT ISNULL(SUM(valor), 0.00) FROM Eurovip.servicio_tasa WHERE id_Servicio = @id_servicio);
END
GO
CREATE OR ALTER FUNCTION Eurovip.fnPrecioWeb (@id_Servicio INT)
RETURNS MONEY
AS
BEGIN
    RETURN (
        SELECT s.precio_base + s.precio_mas_menos + Eurovip.fnTotalTasa(s.id_servicio) - s.precio_base * (s.descuento / 100)
        FROM Eurovip.servicio s WHERE s.id_servicio = @id_Servicio
    );
END
GO
CREATE OR ALTER FUNCTION Eurovip.fnAlojamientoCircuito
(
    @idIdioma CHAR(2), @idTipoServicio VARCHAR(30), @idCampanya SMALLINT, @idMoneda CHAR(3),
    @idPaisOrigen CHAR(2), @origen VARCHAR(30), @idPaisDestino VARCHAR(100)
)
RETURNS TABLE
AS
RETURN
    WITH salidas AS (
        SELECT s.id_servicio, s.id_idioma, s.id_tipo_servicio, s.id_campanya, s.id_moneda,
               COALESCE(si.id_pais_origen, sc.id_pais_origen) AS id_pais_origen,
               COALESCE(si.origen, sc.origen)                 AS id_origen,
               cd.id_pais                                     AS id_pais_destino,
               c.caratula, Eurovip.fnPrecioWeb(s.id_servicio) AS precio_web, c.nombre_circuito,
               ROW_NUMBER() OVER (PARTITION BY c.nombre_circuito ORDER BY Eurovip.fnPrecioWeb(s.id_servicio), sc.desde) AS n
        FROM Eurovip.servicio s
        JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio AND sc.id_idoma = s.id_idioma
        JOIN Eurovip.circuito c           ON c.nombre_circuito = sc.nombre_circuito AND c.id_idioma = sc.id_idoma
        JOIN Eurovip.circuito_destino cd  ON cd.nombre_circuito = c.nombre_circuito AND cd.id_idioma = c.id_idioma
        LEFT JOIN Eurovip.servicio_itinerario si ON si.id_servicio = s.id_servicio AND si.segmento = 1
        WHERE s.id_idioma = @idIdioma AND s.id_tipo_servicio = @idTipoServicio AND s.id_moneda = @idMoneda
          AND s.id_campanya = @idCampanya AND s.activo = 1 AND sc.desde > CAST(GETDATE() AS date)
          AND COALESCE(si.id_pais_origen, sc.id_pais_origen) = @idPaisOrigen
          AND COALESCE(si.origen, sc.origen) = @origen
          AND cd.id_pais = @idPaisDestino
    )
    SELECT id_servicio, id_idioma, id_tipo_servicio, id_campanya, id_moneda, id_pais_origen, id_origen,
           id_pais_destino, id_pais_destino AS id_destino, caratula, precio_web, nombre_circuito
    FROM salidas WHERE n = 1;
GO


/* ==========================================================================================
   CAMBIO 21 — 12/10/2026 — PROCEDIMIENTOS de carga (igual que ViajeVDT, en esquema Eurovip)
   ------------------------------------------------------------------------------------------
   Qué se hizo:
     • spInsertaServicio ................... crea una salida y devuelve su número.
                                             ➕ si falla, anota el error y lo devuelve.
     • spInsertaServicioItinerario ......... agrega un vuelo a la salida.
     • spInsertaServicioSuplemento ......... agrega un suplemento/tasa.
     • spInsertaServicioNota ............... agrega una nota.
     • spInsertarServicioCircuitoAlojamiento agrega un hotel al circuito.
     • spInsertaCircuitoItinerario ......... ➕ nuevo: guarda (o actualiza) un día del itinerario.
     • spActualizaAlojamientoCircuito ...... refresca la vitrina web de circuitos.
   ========================================================================================== */
CREATE OR ALTER PROCEDURE Eurovip.spInsertaServicio
    @tipoServicio VARCHAR(15), @idioma CHAR(2), @moneda CHAR(3), @campanya SMALLINT, @proveedor VARCHAR(10),
    @apertura DATE = NULL, @usuario VARCHAR(15), @precioBase MONEY, @descuento NUMERIC(6, 2), @nota VARCHAR(MAX),
    @activo BIT = 1, @noches SMALLINT, @precioMasMenos MONEY, @codigoServicio VARCHAR(30), @nombre VARCHAR(50),
    @categoriaCircuito VARCHAR(20) = NULL, @baja DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO Eurovip.servicio (id_tipo_servicio, id_idioma, id_moneda, id_campanya, id_Proveedor, apertura, usuario, precio_base,
                                      descuento, precio_mas_menos, nota, noches, codigo, categoria_circuito, baja, nombre, activo)
        VALUES (@tipoServicio, @idioma, @moneda, @campanya, @proveedor, ISNULL(@apertura, CAST(GETDATE() AS date)), @usuario, @precioBase,
                @descuento, @precioMasMenos, @nota, @noches, @codigoServicio, @categoriaCircuito, @baja, @nombre, @activo);
        RETURN SCOPE_IDENTITY();
    END TRY
    BEGIN CATCH
        IF XACT_STATE() = 1 OR @@TRANCOUNT = 0
            INSERT INTO Eurovip.LogErroresServicio (ErrorNumber, ErrorMessage, ErrorProcedure, ErrorLine, FechaError, UsuarioDB)
            VALUES (ERROR_NUMBER(), ERROR_MESSAGE(), ERROR_PROCEDURE(), ERROR_LINE(), GETDATE(), SYSTEM_USER);
        THROW;
    END CATCH
END
GO
CREATE OR ALTER PROCEDURE Eurovip.spInsertaServicioItinerario
    @numero INT, @proveedor VARCHAR(10), @estacionOrigen VARCHAR(10), @origen VARCHAR(30), @idioma CHAR(2), @PaisOrigen CHAR(2),
    @estacionDestino VARCHAR(10), @destino VARCHAR(30), @paisDestino CHAR(2), @vuelo VARCHAR(15), @fechaSalida DATE,
    @horaSalida CHAR(5), @fechaRegreso DATE, @horaRegreso CHAR(5), @clase CHAR(1), @conexion BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @segmento SMALLINT = Eurovip.fnSegmento(@numero);
    INSERT INTO Eurovip.servicio_itinerario (id_servicio, segmento, id_proveedor, id_estacion_origen, origen, id_idioma, id_pais_origen,
        id_estacion_destino, destino, id_pais_destino, vuelo, fecha_salida, hora_salida, fecha_regreso, hora_regreso, clase, conexion)
    VALUES (@numero, @segmento, @proveedor, @estacionOrigen, @origen, @idioma, @PaisOrigen, @estacionDestino, @destino, @paisDestino,
            @vuelo, @fechaSalida, @horaSalida, @fechaRegreso, @horaRegreso, @clase, @conexion);
END
GO
CREATE OR ALTER PROCEDURE Eurovip.spInsertaServicioSuplemento
    @numeroServicio INT, @nombreSuplemento VARCHAR(50), @codigoIdioma CHAR(2), @valor MONEY
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Eurovip.servicio_tasa (id_Servicio, id_tasa, id_idioma, valor) VALUES (@numeroServicio, @nombreSuplemento, @codigoIdioma, @valor);
END
GO
CREATE OR ALTER PROCEDURE Eurovip.spInsertaServicioNota
    @numero INT, @idIdioma CHAR(2), @nota1 VARCHAR(MAX), @nota2 VARCHAR(MAX), @tipo VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @secuencia SMALLINT = (SELECT ISNULL(MAX(secuencia), 0) + 1 FROM Eurovip.servicio_nota WHERE id_servicio = @numero);
    INSERT INTO Eurovip.servicio_nota (id_servicio, secuencia, id_idioma, nota1, nota2, tipo) VALUES (@numero, @secuencia, @idIdioma, @nota1, @nota2, @tipo);
END
GO
CREATE OR ALTER PROCEDURE Eurovip.spInsertarServicioCircuitoAlojamiento
    @nombre_circuito VARCHAR(50), @id_idioma CHAR(2), @id_alojamiento INT, @categoria VARCHAR(50), @caratula VARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM Eurovip.servicio_circuito_alojamiento
                   WHERE nombre_circuito = @nombre_circuito AND id_idioma = @id_idioma AND id_alojamiento = @id_alojamiento AND categoria = @categoria)
        INSERT INTO Eurovip.servicio_circuito_alojamiento (nombre_circuito, id_idioma, id_alojamiento, categoria, caratula)
        VALUES (@nombre_circuito, @id_idioma, @id_alojamiento, @categoria, @caratula);
END
GO
CREATE OR ALTER PROCEDURE Eurovip.spInsertaCircuitoItinerario
    @nombre_circuito VARCHAR(50), @idioma CHAR(2), @dia INT, @titulo VARCHAR(200), @descripcion VARCHAR(MAX),
    @visitas VARCHAR(MAX) = NULL, @regimen VARCHAR(200) = NULL, @transporte VARCHAR(200) = NULL,
    @tipo_alojamiento VARCHAR(20) = NULL, @caratula VARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM Eurovip.circuito_itinerario WHERE nombre_circuito = @nombre_circuito AND id_idioma = @idioma AND dia = @dia)
        UPDATE Eurovip.circuito_itinerario
        SET titulo = @titulo, descripcion = @descripcion, visitas = @visitas, regimen = @regimen, transporte = @transporte,
            tipo_alojamiento = @tipo_alojamiento, caratula = @caratula
        WHERE nombre_circuito = @nombre_circuito AND id_idioma = @idioma AND dia = @dia;
    ELSE
        INSERT INTO Eurovip.circuito_itinerario (dia, titulo, nombre_circuito, id_idioma, descripcion, visitas, regimen, transporte, tipo_alojamiento, caratula)
        VALUES (@dia, @titulo, @nombre_circuito, @idioma, @descripcion, @visitas, @regimen, @transporte, @tipo_alojamiento, @caratula);
END
GO
CREATE OR ALTER PROCEDURE Eurovip.spActualizaAlojamientoCircuito
    @idIdioma CHAR(2), @idTipoServicio VARCHAR(30), @idCampanya SMALLINT, @idMoneda CHAR(3),
    @idPaisOrigen CHAR(2), @idOrigen VARCHAR(30), @idPaisDestino VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM Eurovip.alojamiento_circuito
    WHERE id_idioma = @idIdioma AND id_tipo_servicio = @idTipoServicio AND id_campanya = @idCampanya AND id_moneda = @idMoneda
      AND id_pais_origen = @idPaisOrigen AND id_origen = @idOrigen AND id_pais_destino = @idPaisDestino;

    INSERT INTO Eurovip.alojamiento_circuito (id_servicio, id_idioma, id_tipo_servicio, id_campanya, id_moneda, id_pais_origen, id_origen,
                                              id_pais_destino, id_destino, caratula, precio_web, nombre_circuito)
    SELECT f.id_servicio, f.id_idioma, f.id_tipo_servicio, f.id_campanya, f.id_moneda, f.id_pais_origen, f.id_origen,
           f.id_pais_destino, f.id_destino, f.caratula, f.precio_web, f.nombre_circuito
    FROM Eurovip.fnAlojamientoCircuito(@idIdioma, @idTipoServicio, @idCampanya, @idMoneda, @idPaisOrigen, @idOrigen, @idPaisDestino) f
    WHERE NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento_circuito x WHERE x.id_servicio = f.id_servicio);
END
GO


/* ##########################################################################################
   FASE 4 — CARGA MANUAL DE OFERTAS DE HOTEL  (13/10/2026)
   ------------------------------------------------------------------------------------------
   Una OFERTA es un paquete de hotel que se vende por fechas: hotel + régimen + noches +
   precio por tipo de habitación, con campaña, suplementos/tasas, notas y (SOLO SI TIENE)
   vuelo. Cada fecha de salida es un "servicio" (igual que en circuitos), con:
     • servicio_detalle ..... el hotel, régimen, fechas y noches de esa salida;
     • servicio_habitacion .. el precio de cada tipo de habitación (Doble, Suite…).
   Todo se copió de ViajeVDT (10.10.10.29) sin tocar ese servidor, siempre en idioma ES.

   Diferencias a propósito con ViajeVDT (mejoras Eurovip):
     • servicio_origen (nueva): guarda desde dónde se vende cada oferta, para que una oferta
       SIN vuelo también se pueda publicar en la web.
     • fnAlojamientoDestino funciona CON o SIN vuelo (en ViajeVDT exigía vuelo).
   ########################################################################################## */


/* ==========================================================================================
   CAMBIO 22 — 13/10/2026 — TIPOS DE HABITACIÓN y HABITACIONES DE CADA HOTEL
   ------------------------------------------------------------------------------------------
   Qué se hizo: catálogo de tipos de habitación (Doble, Suite, Junior Suite…) y las
   habitaciones que tiene cada hotel. Se usan para poner precio por habitación en la oferta.
   Copiadas de: ViajeVDT.dbo.tipo_habitacion y dbo.alojamiento_habitacion — solo filas ES
   (copia masiva, ver bitácora).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.tipo_habitacion', 'U') IS NULL
CREATE TABLE Eurovip.tipo_habitacion (
    id_idioma          char(2)      NOT NULL,
    id_tipo_habitacion varchar(100) NOT NULL,   -- Doble, Suite…
    CONSTRAINT PK_Eurovip_tipo_habitacion PRIMARY KEY CLUSTERED (id_idioma, id_tipo_habitacion)
);
GO
IF OBJECT_ID('Eurovip.alojamiento_habitacion', 'U') IS NULL
CREATE TABLE Eurovip.alojamiento_habitacion (
    id_alojamiento     int           NOT NULL,   -- hotel (cambio 15)
    id_habitacion      int           NOT NULL,   -- número de la habitación
    id_idioma          char(2)       NOT NULL,
    id_tipo_habitacion varchar(100)  NOT NULL,
    codigo             varchar(50)   NOT NULL,
    nombre             varchar(255)  NOT NULL,   -- nombre visible (ej. Doble Vista Mar)
    grupo              int           NOT NULL,
    nota               varchar(max)  NULL,
    precio             money         NOT NULL CONSTRAINT DF_Eurovip_alojamiento_habitacion_precio DEFAULT ((0.00)),
    codigo_newblue     nvarchar(510) NULL,
    nombre_newblue     nvarchar(max) NULL,
    CONSTRAINT PK_Eurovip_alojamiento_habitacion PRIMARY KEY CLUSTERED (id_alojamiento, id_habitacion, id_idioma),
    CONSTRAINT FK_Eurovip_alojamiento_habitacion_alojamiento FOREIGN KEY (id_alojamiento, id_idioma) REFERENCES Eurovip.alojamiento (id_alojamiento, id_idioma),
    CONSTRAINT FK_Eurovip_alojamiento_habitacion_tipo FOREIGN KEY (id_idioma, id_tipo_habitacion) REFERENCES Eurovip.tipo_habitacion (id_idioma, id_tipo_habitacion)
);
GO
-- Datos: copia masiva desde ViajeVDT →
--   SELECT * FROM dbo.tipo_habitacion        WHERE id_idioma = 'ES'   (≈15.900 filas)
--   SELECT * FROM dbo.alojamiento_habitacion WHERE id_idioma = 'ES'   (≈130.000 filas)


/* ==========================================================================================
   CAMBIO 23 — 13/10/2026 — DETALLE DE HOTEL y PRECIO POR HABITACIÓN de cada salida
   ------------------------------------------------------------------------------------------
   Qué se hizo:
     • servicio_detalle ..... en cada salida: hotel, régimen, fecha de entrada y salida, noches.
     • servicio_habitacion .. en cada salida: precio de cada habitación del hotel.
     • servicio_origen ...... ➕ Eurovip: país y ciudad desde donde se vende (ofertas sin vuelo).
   Copiadas de: ViajeVDT (solo estructura; empiezan vacías).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.servicio_detalle', 'U') IS NULL
CREATE TABLE Eurovip.servicio_detalle (
    id_servicio    int          NOT NULL,
    secuencia      smallint     NOT NULL,   -- 1 = primer hotel de la salida
    id_alojamiento int          NOT NULL,
    id_regimen     char(2)      NOT NULL,   -- TI, MP, AD…
    id_idioma      char(2)      NOT NULL,
    desde          date         NOT NULL,   -- entrada al hotel
    hora_desde     varchar(10)  NOT NULL,
    hasta          date         NOT NULL,   -- salida del hotel
    hora_hasta     varchar(10)  NOT NULL,
    noches         smallint     NULL,
    categoria      varchar(50)  NULL,
    precio         money        NULL,
    CONSTRAINT PK_Eurovip_servicio_detalle PRIMARY KEY CLUSTERED (id_servicio, secuencia),
    CONSTRAINT FK_Eurovip_servicio_detalle_servicio FOREIGN KEY (id_servicio) REFERENCES Eurovip.servicio (id_servicio),
    CONSTRAINT FK_Eurovip_servicio_detalle_alojamiento FOREIGN KEY (id_alojamiento, id_idioma) REFERENCES Eurovip.alojamiento (id_alojamiento, id_idioma),
    CONSTRAINT FK_Eurovip_servicio_detalle_regimen FOREIGN KEY (id_regimen, id_idioma) REFERENCES Eurovip.regimen (id_regimen, id_idioma)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_servicio_detalle_aloj' AND object_id = OBJECT_ID('Eurovip.servicio_detalle'))
    CREATE NONCLUSTERED INDEX IX_Eurovip_servicio_detalle_aloj ON Eurovip.servicio_detalle (id_alojamiento, id_idioma, desde) INCLUDE (id_servicio, secuencia, id_regimen, noches);
GO
IF OBJECT_ID('Eurovip.servicio_habitacion', 'U') IS NULL
CREATE TABLE Eurovip.servicio_habitacion (
    id_servicio    int      NOT NULL,
    secuencia      smallint NOT NULL,   -- hotel (servicio_detalle) al que pertenece
    id_alojamiento int      NOT NULL,
    id_idioma      char(2)  NOT NULL,
    id_habitacion  int      NOT NULL,
    precio         money    NOT NULL,   -- importe que suma esta habitación al precio
    CONSTRAINT PK_Eurovip_servicio_habitacion PRIMARY KEY CLUSTERED (id_servicio, secuencia, id_alojamiento, id_idioma, id_habitacion),
    CONSTRAINT FK_Eurovip_servicio_habitacion_detalle FOREIGN KEY (id_servicio, secuencia) REFERENCES Eurovip.servicio_detalle (id_servicio, secuencia),
    CONSTRAINT FK_Eurovip_servicio_habitacion_habitacion FOREIGN KEY (id_alojamiento, id_habitacion, id_idioma) REFERENCES Eurovip.alojamiento_habitacion (id_alojamiento, id_habitacion, id_idioma)
);
GO
IF OBJECT_ID('Eurovip.servicio_origen', 'U') IS NULL
CREATE TABLE Eurovip.servicio_origen (
    id_servicio    int         NOT NULL,
    id_pais_origen char(2)     NOT NULL,
    origen         varchar(30) NOT NULL,
    CONSTRAINT PK_Eurovip_servicio_origen PRIMARY KEY CLUSTERED (id_servicio),
    CONSTRAINT FK_Eurovip_servicio_origen_servicio FOREIGN KEY (id_servicio) REFERENCES Eurovip.servicio (id_servicio)
);
GO


/* ==========================================================================================
   CAMBIO 24 — 13/10/2026 — VITRINA WEB DE HOTELES (alojamiento_destino)
   ------------------------------------------------------------------------------------------
   Qué se hizo: tabla con la oferta más barata de cada hotel y régimen por destino, que es la
   que muestra la web. Se refresca sola al terminar cada carga manual.
   Copiada de: ViajeVDT.dbo.alojamiento_destino (solo estructura).
   ========================================================================================== */
IF OBJECT_ID('Eurovip.alojamiento_destino', 'U') IS NULL
CREATE TABLE Eurovip.alojamiento_destino (
    id_servicio         int            NOT NULL,
    id_idioma           char(2)        NOT NULL,
    id_tipo_servicio    varchar(30)    NOT NULL,
    id_campanya         smallint       NOT NULL,
    id_moneda           char(3)        NOT NULL,
    id_pais_origen      char(2)        NOT NULL,
    id_origen           varchar(30)    NOT NULL,
    id_pais_destino     varchar(200)   NOT NULL,
    id_destino          varchar(400)   NOT NULL,
    id_alojamiento      varchar(200)   NOT NULL,
    nombre_alojamiento  varchar(400)   NOT NULL,
    descripcion_regimen varchar(500)   NOT NULL,
    grupo               varchar(200)   NULL,
    categoria           varchar(200)   NULL,
    caratula            varchar(500)   NULL,
    latitude            nvarchar(1000) NULL,
    longitude           nvarchar(1000) NULL,
    desde               varchar(200)   NOT NULL,
    noches              varchar(100)   NOT NULL,
    id_habitacion       int            NOT NULL,
    precio_web          money          NOT NULL,
    CONSTRAINT PK_Eurovip_alojamiento_destino PRIMARY KEY CLUSTERED (id_servicio)
);
GO


/* ==========================================================================================
   CAMBIO 25 — 13/10/2026 — FUNCIONES y PROCEDIMIENTOS de la carga manual
   ------------------------------------------------------------------------------------------
   Qué se hizo:
     • fnSecuencia .................. siguiente número de hotel dentro de una salida
                                       (en OFERTA solo puede haber 1 hotel, igual que ViajeVDT).
     • fnPrecioWeb (actualizada) .... precio final = base + más/menos + tasas + habitación
                                       más barata + detalle − descuento (igual que ViajeVDT).
     • spInsertaServicioDetalle ..... agrega el hotel de la salida.
     • InsertarServicioHabitacion ... agrega el precio de una habitación (valida el hotel).
     • fnAlojamientoDestino ......... oferta más barata por hotel y régimen (➕ con o sin vuelo).
     • spActualizaAlojamientoDestino  refresca la vitrina de hoteles de un destino.
     • spEliminaServicio ............ borra una salida completa en el orden correcto.
   ========================================================================================== */
CREATE OR ALTER FUNCTION Eurovip.fnSecuencia (@numero INT)
RETURNS SMALLINT
AS
BEGIN
    DECLARE @tipo VARCHAR(20) = (SELECT id_tipo_servicio FROM Eurovip.servicio WHERE id_servicio = @numero);
    IF @tipo = 'OFERTA'
        RETURN CASE WHEN EXISTS (SELECT 1 FROM Eurovip.servicio_detalle WHERE id_servicio = @numero) THEN -1 ELSE 1 END;
    RETURN (SELECT ISNULL(MAX(secuencia), 0) + 1 FROM Eurovip.servicio_detalle WHERE id_servicio = @numero);
END
GO
CREATE OR ALTER FUNCTION Eurovip.fnPrecioWeb (@id_Servicio INT)
RETURNS MONEY
AS
BEGIN
    RETURN (
        SELECT s.precio_base + s.precio_mas_menos + Eurovip.fnTotalTasa(s.id_servicio)
             + ISNULL((SELECT MIN(precio) FROM Eurovip.servicio_habitacion WHERE id_servicio = s.id_servicio), 0)
             + ISNULL((SELECT MIN(precio) FROM Eurovip.servicio_detalle WHERE id_servicio = s.id_servicio), 0)
             - s.precio_base * (s.descuento / 100)
        FROM Eurovip.servicio s WHERE s.id_servicio = @id_Servicio
    );
END
GO
CREATE OR ALTER PROCEDURE Eurovip.spInsertaServicioDetalle
    @numero INT, @alojamiento INT, @idioma CHAR(2), @regimen CHAR(2), @desde DATE, @horaDesde CHAR(5),
    @hasta DATE, @horaHasta CHAR(5), @noches SMALLINT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @secuencia SMALLINT = Eurovip.fnSecuencia(@numero);
    IF @secuencia = -1
        THROW 50001, 'Una oferta solo puede tener un hotel.', 1;
    INSERT INTO Eurovip.servicio_detalle (id_servicio, secuencia, id_alojamiento, id_idioma, id_regimen, desde, hora_desde, hasta, hora_hasta, noches)
    VALUES (@numero, @secuencia, @alojamiento, @idioma, @regimen, @desde, @horaDesde, @hasta, @horaHasta, @noches);
    RETURN @secuencia;
END
GO
CREATE OR ALTER PROCEDURE Eurovip.InsertarServicioHabitacion
    @Id_Servicio INT, @Secuencia INT, @Id_Alojamiento INT, @Id_Idioma CHAR(2), @Id_Habitacion INT, @Precio DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento WHERE id_alojamiento = @Id_Alojamiento AND id_idioma = @Id_Idioma)
        THROW 50002, 'No existe el hotel con ese número e idioma.', 1;
    IF NOT EXISTS (SELECT 1 FROM Eurovip.servicio_habitacion
                   WHERE id_servicio = @Id_Servicio AND secuencia = @Secuencia AND id_alojamiento = @Id_Alojamiento
                     AND id_idioma = @Id_Idioma AND id_habitacion = @Id_Habitacion)
        INSERT INTO Eurovip.servicio_habitacion (id_servicio, secuencia, id_alojamiento, id_idioma, id_habitacion, precio)
        VALUES (@Id_Servicio, @Secuencia, @Id_Alojamiento, @Id_Idioma, @Id_Habitacion, @Precio);
END
GO
CREATE OR ALTER FUNCTION Eurovip.fnAlojamientoDestino
(
    @idIdioma CHAR(2), @idTipoServicio VARCHAR(30), @idCampanya SMALLINT, @idMoneda CHAR(3),
    @idPaisOrigen CHAR(2), @origen VARCHAR(30), @idPaisDestino CHAR(2), @idDestino VARCHAR(30)
)
RETURNS TABLE
AS
RETURN
    WITH ofertas AS (
        SELECT s.id_servicio, s.id_idioma, s.id_tipo_servicio AS tipo_servicio, s.id_campanya, s.id_moneda,
               COALESCE(si.id_pais_origen, so.id_pais_origen) AS id_pais_origen,
               COALESCE(si.origen, so.origen)                 AS id_origen,
               a.id_pais AS id_pais_destino, a.id_destino, a.id_alojamiento, a.nombre AS nombre_alojamiento,
               CONCAT(sd.id_regimen, ' ', r.descripcion) AS descripcion_regimen,
               a.grupo, a.categoria, a.caratula, a.latitude, a.longitude, sd.desde, sd.noches,
               hab.id_habitacion, Eurovip.fnPrecioWeb(s.id_servicio) AS precio_web,
               ROW_NUMBER() OVER (PARTITION BY a.id_alojamiento, sd.id_regimen
                                  ORDER BY Eurovip.fnPrecioWeb(s.id_servicio), sd.desde) AS n
        FROM Eurovip.servicio s
        JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.desde > CAST(GETDATE() AS date)
        JOIN Eurovip.alojamiento a       ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
        JOIN Eurovip.regimen r           ON r.id_regimen = sd.id_regimen AND r.id_idioma = s.id_idioma
        CROSS APPLY (SELECT TOP 1 sh.id_habitacion FROM Eurovip.servicio_habitacion sh
                     WHERE sh.id_servicio = sd.id_servicio AND sh.secuencia = sd.secuencia ORDER BY sh.precio, sh.id_habitacion) hab
        LEFT JOIN Eurovip.servicio_itinerario si ON si.id_servicio = s.id_servicio AND si.segmento = 1
        LEFT JOIN Eurovip.servicio_origen so     ON so.id_servicio = s.id_servicio
        WHERE s.id_idioma = @idIdioma AND s.id_tipo_servicio = @idTipoServicio AND s.id_moneda = @idMoneda
          AND s.id_campanya = @idCampanya AND s.activo = 1
          AND COALESCE(si.id_pais_origen, so.id_pais_origen) = @idPaisOrigen
          AND COALESCE(si.origen, so.origen) = @origen
          AND a.id_pais = @idPaisDestino AND a.id_destino = @idDestino
    )
    SELECT id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, id_origen, id_pais_destino, id_destino,
           id_alojamiento, nombre_alojamiento, descripcion_regimen, grupo, categoria, caratula, latitude, longitude,
           desde, noches, id_habitacion, precio_web
    FROM ofertas WHERE n = 1;
GO
CREATE OR ALTER PROCEDURE Eurovip.spActualizaAlojamientoDestino
    @idIdioma CHAR(2), @idTipoServicio VARCHAR(30), @idCampanya SMALLINT, @idMoneda CHAR(3),
    @idPaisOrigen CHAR(2), @idOrigen VARCHAR(30), @idPaisDestino CHAR(2), @idDestino VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM Eurovip.alojamiento_destino
    WHERE id_idioma = @idIdioma AND id_tipo_servicio = @idTipoServicio AND id_campanya = @idCampanya AND id_moneda = @idMoneda
      AND id_pais_origen = @idPaisOrigen AND id_origen = @idOrigen AND id_pais_destino = @idPaisDestino AND id_destino = @idDestino;

    INSERT INTO Eurovip.alojamiento_destino (id_servicio, id_idioma, id_tipo_servicio, id_campanya, id_moneda, id_pais_origen, id_origen,
        id_pais_destino, id_destino, id_alojamiento, nombre_alojamiento, descripcion_regimen, grupo, categoria, caratula, latitude, longitude,
        desde, noches, id_habitacion, precio_web)
    SELECT f.id_servicio, f.id_idioma, f.tipo_servicio, f.id_campanya, f.id_moneda, f.id_pais_origen, f.id_origen, f.id_pais_destino,
           f.id_destino, f.id_alojamiento, f.nombre_alojamiento, f.descripcion_regimen, f.grupo, f.categoria, f.caratula, f.latitude,
           f.longitude, CONVERT(varchar(10), f.desde, 23), CAST(f.noches AS varchar(10)), f.id_habitacion, f.precio_web
    FROM Eurovip.fnAlojamientoDestino(@idIdioma, @idTipoServicio, @idCampanya, @idMoneda, @idPaisOrigen, @idOrigen, @idPaisDestino, @idDestino) f
    WHERE NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento_destino x WHERE x.id_servicio = f.id_servicio);
END
GO
CREATE OR ALTER PROCEDURE Eurovip.spEliminaServicio @idServicio INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @codigo VARCHAR(50) = (SELECT codigo FROM Eurovip.servicio WHERE id_servicio = @idServicio);   -- CAMBIO 45 (FASE 12)
    DELETE FROM Eurovip.servicio_tasa        WHERE id_Servicio = @idServicio;
    DELETE FROM Eurovip.servicio_itinerario  WHERE id_servicio = @idServicio;
    DELETE FROM Eurovip.servicio_habitacion  WHERE id_servicio = @idServicio;
    DELETE FROM Eurovip.servicio_nota        WHERE id_servicio = @idServicio;
    DELETE FROM Eurovip.servicio_detalle     WHERE id_servicio = @idServicio;
    DELETE FROM Eurovip.servicio_circuito    WHERE id_servicio = @idServicio;
    DELETE FROM Eurovip.servicio_origen      WHERE id_servicio = @idServicio;
    IF OBJECT_ID('Eurovip.servicio_destino', 'U') IS NOT NULL   -- CAMBIO 44 (FASE 11)
        DELETE FROM Eurovip.servicio_destino WHERE id_servicio = @idServicio;
    DELETE FROM Eurovip.alojamiento_circuito WHERE id_servicio = @idServicio;
    DELETE FROM Eurovip.alojamiento_destino  WHERE id_servicio = @idServicio;
    DELETE FROM Eurovip.servicio             WHERE id_servicio = @idServicio;
    -- CAMBIO 45 (FASE 12): la descripción (incluye/no incluye/notas) es de la carga (codigo):
    -- se borra cuando ya no queda ninguna oferta/salida con ese código.
    IF OBJECT_ID('Eurovip.servicio_descripcion', 'U') IS NOT NULL AND @codigo IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio WHERE codigo = @codigo)
        DELETE FROM Eurovip.servicio_descripcion WHERE codigo = @codigo;
END
GO


/* ##########################################################################################
   FASE 5 — HOME DE LA WEB: INICIO + CARRUSEL  (13/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: organizar todo lo que el cliente ve en la Home de la web de Eurovip.
     • CARRUSEL = el slider grande de arriba (imágenes web / tablet / móvil, con su orden).
     • INICIO   = las tarjetas de la Home, agrupadas en secciones (CARIBE, AMÉRICA, HOTEL…).
   Copiado de ViajeVDT (10.10.10.29, solo lectura), solo idioma ES, todo en el esquema Eurovip.

   CAMBIO 26  inicio: nueva columna "id" (número propio). Antes la clave era id_servicio y un
              hotel cargado a mano (#1, #2…) podía chocar con el número de una oferta real.
   CAMBIO 27  home_contenido: recuerda en qué sección y posición va cada destino, para que al
              refrescar precios desde las ofertas no se pierda el orden (176 filas copiadas).
   CAMBIO 28  carrusel: los slides (53 filas copiadas: 15 web, 18 tablet, 20 móvil).
   CAMBIO 29  carrucelDestino: precio "desde" que muestra un slide (36 filas copiadas).
   CAMBIO 30  fnServicioMinimo: la oferta más barata de un destino. En ViajeVDT exigía vuelo;
              aquí usa el origen de venta (servicio_origen), así funciona con o sin vuelo.
   CAMBIO 31  fnServicioMinimoCircuito: igual para circuitos (usa servicio_circuito.origen).
   CAMBIO 32  spActualizaInicio: pone en la Home la oferta/circuito más barato. Corrige un fallo
              de ViajeVDT (el bloque de circuitos se ejecutaba SIEMPRE por faltar un ELSE) y
              conserva lo que el usuario ordenó (estado, sección y posiciones).
   CAMBIO 33  SP de lectura para la web: sp_ListarCarruselActivos y usp_GetDestinos.
   ########################################################################################## */
GO

/* ---------- CAMBIO 26: inicio con identificador propio ---------- */
-- La clave era id_servicio: un hotel cargado a mano (#1, #2…) chocaba con el número de una oferta real.
IF COL_LENGTH('Eurovip.inicio', 'id') IS NULL
BEGIN
    ALTER TABLE Eurovip.inicio DROP CONSTRAINT PK_Eurovip_inicio;
    ALTER TABLE Eurovip.inicio ADD id INT IDENTITY(1,1) NOT NULL;
    ALTER TABLE Eurovip.inicio ADD CONSTRAINT PK_Eurovip_inicio PRIMARY KEY CLUSTERED (id);
    CREATE INDEX IX_Eurovip_inicio_servicio ON Eurovip.inicio (id_servicio, tipo_servicio);
END
GO

/* ---------- CAMBIO 27: home_contenido (qué destino va en qué contenedor y posición) ---------- */
IF OBJECT_ID('Eurovip.home_contenido') IS NULL
CREATE TABLE Eurovip.home_contenido (
    id_home_contenido INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_home_contenido PRIMARY KEY,
    id_idioma         CHAR(2)      NOT NULL,
    id_contenedor     INT          NOT NULL,
    id_campanya       INT          NULL,
    id_moneda         CHAR(3)      NOT NULL,
    id_pais_origen    CHAR(2)      NOT NULL,
    origen            VARCHAR(30)  NOT NULL,
    id_destino        VARCHAR(MAX) NOT NULL,
    posicion          INT          NOT NULL,
    oferta_especial   BIT          NOT NULL CONSTRAINT DF_Eurovip_home_contenido_oe DEFAULT (0)
);
GO

/* ---------- CAMBIO 28: carrusel (slider de la Home) ---------- */
IF OBJECT_ID('Eurovip.carrusel') IS NULL
CREATE TABLE Eurovip.carrusel (
    id_carrusel SMALLINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_carrusel PRIMARY KEY,
    id_idioma   CHAR(2)      NOT NULL,
    orden       SMALLINT     NOT NULL,
    orientacion CHAR(2)      NOT NULL,
    ruta        VARCHAR(100) NOT NULL,
    activo      BIT          NOT NULL,
    url         VARCHAR(500) NULL,
    formato     VARCHAR(50)  NULL          -- 1 = web, 2 = tablet, 3 = móvil
);
GO

/* ---------- CAMBIO 29: carrucelDestino (precio "desde" que se muestra en un slide) ---------- */
IF OBJECT_ID('Eurovip.carrucelDestino') IS NULL
CREATE TABLE Eurovip.carrucelDestino (
    id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_carrucelDestino PRIMARY KEY,
    id_carrusel INT          NOT NULL,
    origen      VARCHAR(50)  NOT NULL,
    destino     VARCHAR(50)  NOT NULL,
    precio      MONEY        NOT NULL,
    id_campanya INT          NOT NULL,
    regimen     VARCHAR(100) NULL
);
GO

/* ---------- CAMBIO 30: fnServicioMinimo (oferta más barata de un destino) — sin vuelo obligatorio ---------- */
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
        INNER JOIN Eurovip.servicio_origen so ON s.id_servicio = so.id_servicio          -- antes: servicio_itinerario segmento 1 (vuelo)
        INNER JOIN Eurovip.regimen r ON sd.id_regimen = r.id_regimen AND sd.id_idioma = r.id_idioma
        LEFT JOIN Eurovip.home_contenido hc
            ON hc.id_idioma = s.id_idioma AND hc.id_campanya = s.id_campanya AND hc.id_moneda = s.id_moneda
           AND hc.id_pais_origen = so.id_pais_origen AND hc.origen = so.origen AND hc.id_destino = a.id_destino
        LEFT JOIN Eurovip.contenedores cont ON cont.id_contenedor = hc.id_contenedor
    WHERE s.activo = 1
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

/* ---------- CAMBIO 31: fnServicioMinimoCircuito — sin vuelo obligatorio ---------- */
CREATE OR ALTER FUNCTION Eurovip.fnServicioMinimoCircuito
(
    @idCampanya SMALLINT, @idIdioma CHAR(2), @idMoneda CHAR(3), @origen VARCHAR(30), @idPaisOrigen CHAR(2), @idPaisDestino CHAR(2)
)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1
        s.id_servicio, s.id_idioma, s.id_tipo_servicio, s.id_campanya, s.id_moneda, sc.id_pais_origen, sc.origen,
        cd.id_pais, cd.id_pais AS paisDestino, c.caratula, sc.nombre_circuito, NULL AS latitud, NULL AS longitud,
        Eurovip.fnPrecioWeb(s.id_servicio) AS precioWeb, NULL AS id_contenedor, NULL AS contenedor,
        NULL AS posicion_contenedor, NULL AS posicion_destino
    FROM Eurovip.servicio s
        INNER JOIN Eurovip.servicio_circuito sc ON s.id_servicio = sc.id_servicio       -- origen guardado en servicio_circuito
        INNER JOIN Eurovip.circuito_destino cd ON sc.nombre_circuito = cd.nombre_circuito AND sc.id_idoma = cd.id_idioma
        INNER JOIN Eurovip.circuito c ON c.nombre_circuito = sc.nombre_circuito AND c.id_idioma = sc.id_idoma
    WHERE s.activo = 1
      AND s.id_tipo_servicio = 'CIRCUITO'
      AND s.id_campanya = @idCampanya
      AND s.id_idioma = @idIdioma
      AND s.id_moneda = @idMoneda
      AND sc.origen = @origen
      AND sc.id_pais_origen = @idPaisOrigen
      AND cd.id_pais = @idPaisDestino
      AND sc.desde > GETDATE()
    GROUP BY s.id_servicio, s.id_idioma, s.id_tipo_servicio, s.id_campanya, s.id_moneda, sc.id_pais_origen, sc.origen,
             sc.nombre_circuito, cd.id_pais, Eurovip.fnPrecioWeb(s.id_servicio), c.caratula
    ORDER BY Eurovip.fnPrecioWeb(s.id_servicio) ASC
);
GO

/* ---------- CAMBIO 32: spActualizaInicio — pone en la Home la oferta/circuito más barato ----------
   Diferencias con ViajeVDT:
     • Corrige que el bloque de CIRCUITO se ejecutaba SIEMPRE (faltaba el ELSE).
     • Conserva lo que el usuario ordenó en el editor (estado, contenedor y posiciones) si la fila ya existía.
     • Todo en una transacción. Sin combinados (Eurovip no los tiene). */
CREATE OR ALTER PROCEDURE Eurovip.spActualizaInicio
    @tipoServicio VARCHAR(30), @idCampanya SMALLINT, @destino VARCHAR(MAX), @idIdioma CHAR(2),
    @idMoneda CHAR(3), @origen VARCHAR(30), @idPaisOrigen CHAR(2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;

    DECLARE @prev TABLE (estado BIT, id_contenedor INT, contenedor VARCHAR(50), posicion_contenedor INT, posicion_destino INT);
    INSERT INTO @prev
    SELECT TOP 1 estado, id_contenedor, contenedor, posicion_contenedor, posicion_destino
    FROM Eurovip.inicio
    WHERE tipo_servicio = @tipoServicio AND id_campanya = @idCampanya AND id_destino = @destino AND id_idioma = @idIdioma
      AND id_moneda = @idMoneda AND origen = @origen AND id_pais_origen = @idPaisOrigen;

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

    -- restaurar lo que el usuario había organizado
    UPDATE i SET i.estado = p.estado,
                 i.id_contenedor = ISNULL(p.id_contenedor, i.id_contenedor), i.contenedor = ISNULL(p.contenedor, i.contenedor),
                 i.posicion_contenedor = ISNULL(p.posicion_contenedor, i.posicion_contenedor),
                 i.posicion_destino = ISNULL(p.posicion_destino, i.posicion_destino)
    FROM Eurovip.inicio i CROSS JOIN @prev p
    WHERE i.tipo_servicio = @tipoServicio AND i.id_campanya = @idCampanya AND i.id_destino = @destino AND i.id_idioma = @idIdioma
      AND i.id_moneda = @idMoneda AND i.origen = @origen AND i.id_pais_origen = @idPaisOrigen;

    COMMIT;
END
GO

/* ---------- CAMBIO 33: SP de lectura para la web (copiados de ViajeVDT) ---------- */
CREATE OR ALTER PROCEDURE Eurovip.sp_ListarCarruselActivos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.id_carrusel, c.id_idioma, c.orden, c.orientacion, c.ruta, c.url, c.formato,
           cd.regimen, cd.origen, cd.destino, cd.precio, cd.id_campanya,
           i.contenedor, i.id_contenedor
    FROM Eurovip.carrusel c
        LEFT JOIN Eurovip.carrucelDestino cd ON cd.id_carrusel = c.id_carrusel
        LEFT JOIN Eurovip.inicio i
            ON i.id_campanya = cd.id_campanya AND i.id_idioma = c.id_idioma AND i.origen = cd.origen AND i.id_destino = cd.destino
    WHERE c.activo = 1
    ORDER BY c.formato, c.orden;
END
GO

CREATE OR ALTER PROCEDURE Eurovip.usp_GetDestinos
    @Campanya INT, @Idioma VARCHAR(50), @FiltrarCampanya BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    IF @FiltrarCampanya = 1
    BEGIN
        WITH CTE_MasEconomico AS (
            SELECT *, ROW_NUMBER() OVER (PARTITION BY id_paisDestino ORDER BY precioWeb ASC) AS rn
            FROM Eurovip.inicio WHERE id_campanya = @Campanya AND id_idioma = @Idioma
        )
        SELECT id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino, id_destino,
               caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor, posicion_contenedor,
               posicion_destino, estado, noches, notas
        FROM Eurovip.inicio WHERE id_campanya <> @Campanya AND id_idioma = @Idioma
        UNION ALL
        SELECT id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino, id_destino,
               caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor, posicion_contenedor,
               posicion_destino, estado, noches, notas
        FROM CTE_MasEconomico WHERE rn = 1;
    END
    ELSE
        SELECT id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino, id_destino,
               caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor, posicion_contenedor,
               posicion_destino, estado, noches, notas
        FROM Eurovip.inicio WHERE id_idioma = @Idioma;
END
GO

/* ##########################################################################################
   FASE 6 — HERRAMIENTAS (mantenimientos básicos)  (13/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: dar de alta, editar y eliminar los catálogos que usa Eurovip (destinos, monedas,
   campañas, países, idiomas, tasas, regímenes, estaciones, proveedores, alojamientos y
   habitaciones). Las TABLAS ya estaban copiadas de ViajeVDT (fases 2-4, solo ES).
   De ViajeVDT solo existen dos procedimientos de mantenimiento; se copiaron al esquema Eurovip.
   El resto de catálogos BackTiTan los mantiene con SQL directo; Eurovip hace lo mismo desde
   la pantalla Herramientas (siempre sobre Eurovip.*, nunca dbo).

   CAMBIO 34  InsertarOActualizarAlojamiento: crea o edita un hotel. Diferencias con ViajeVDT:
              número nuevo calculado con bloqueo (dos usuarios a la vez no chocan) y, si el
              hotel ya existe, ahora SE ACTUALIZA (antes no hacía nada); guarda "activo".
   CAMBIO 35  InsertarAlojamientoHabitacion: crea o edita una habitación de un hotel (y su tipo
              si no existe). Diferencia: número nuevo con bloqueo y en una transacción.
   CAMBIO 36  Índices de búsqueda en alojamiento y alojamiento_habitacion (solo columnas que se
              buscan): buscar en 130.000 habitaciones bajó de ~2,5 s a ~1,2 s.
   ########################################################################################## */
/* ---------- CAMBIO 34: InsertarOActualizarAlojamiento (copiado de ViajeVDT) ----------
   Diferencias: tablas Eurovip; el número nuevo se calcula con bloqueo (dos usuarios a la vez no
   obtienen el mismo); si el hotel ya existe en ese idioma, ahora SE ACTUALIZA (en ViajeVDT se
   devolvía sin hacer nada); guarda también "activo". */
CREATE OR ALTER PROCEDURE Eurovip.InsertarOActualizarAlojamiento
    @in_id_alojamiento   INT          = NULL,
    @id_idioma           CHAR(2),
    @id_tipo_alojamiento VARCHAR(20),
    @id_destino          VARCHAR(30),
    @id_pais             CHAR(2),
    @nombre              VARCHAR(100),
    @caratula            VARCHAR(300) = NULL,
    @descripcion         VARCHAR(MAX) = NULL,
    @grupo               INT          = NULL,
    @categoria           VARCHAR(15),
    @latitude            NVARCHAR(50) = NULL,
    @longitude           NVARCHAR(50) = NULL,
    @categoria_circuito  VARCHAR(50)  = NULL,
    @circuito            BIT          = NULL,
    @activo              BIT          = 1,
    @id_alojamiento      INT          OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;

    IF @in_id_alojamiento IS NOT NULL
       AND EXISTS (SELECT 1 FROM Eurovip.alojamiento WHERE id_alojamiento = @in_id_alojamiento AND id_idioma = @id_idioma)
    BEGIN
        UPDATE Eurovip.alojamiento SET
            id_tipo_alojamiento = @id_tipo_alojamiento, id_destino = @id_destino, id_pais = @id_pais, nombre = @nombre,
            caratula = @caratula, descripcion = @descripcion, grupo = @grupo, categoria = @categoria,
            latitude = @latitude, longitude = @longitude, categoria_circuito = @categoria_circuito,
            circuito = @circuito, activo = @activo
        WHERE id_alojamiento = @in_id_alojamiento AND id_idioma = @id_idioma;
        SET @id_alojamiento = @in_id_alojamiento;
    END
    ELSE
    BEGIN
        IF @in_id_alojamiento IS NOT NULL
           AND EXISTS (SELECT 1 FROM Eurovip.alojamiento WITH (UPDLOCK, HOLDLOCK) WHERE id_alojamiento = @in_id_alojamiento)
            SET @id_alojamiento = @in_id_alojamiento;      -- mismo hotel, otro idioma
        ELSE
            SELECT @id_alojamiento = ISNULL(MAX(id_alojamiento), 0) + 1 FROM Eurovip.alojamiento WITH (UPDLOCK, HOLDLOCK);

        INSERT INTO Eurovip.alojamiento (id_alojamiento, id_idioma, id_tipo_alojamiento, id_destino, id_pais, nombre,
               caratula, descripcion, grupo, categoria, latitude, longitude, categoria_circuito, circuito, activo)
        VALUES (@id_alojamiento, @id_idioma, @id_tipo_alojamiento, @id_destino, @id_pais, @nombre,
               @caratula, @descripcion, @grupo, @categoria, @latitude, @longitude, @categoria_circuito, @circuito, @activo);
    END

    COMMIT;
END
GO

/* ---------- CAMBIO 35: InsertarAlojamientoHabitacion (copiado de ViajeVDT) ----------
   Diferencias: tablas Eurovip; número nuevo con bloqueo y dentro de una transacción. */
CREATE OR ALTER PROCEDURE Eurovip.InsertarAlojamientoHabitacion
    @id_alojamiento     INT,
    @id_idioma          NVARCHAR(10),
    @id_tipo_habitacion NVARCHAR(100),
    @codigo             NVARCHAR(50),
    @nombre             NVARCHAR(255),
    @grupo              INT,
    @nota               NVARCHAR(MAX),
    @precio             DECIMAL(18,2),
    @codigo_newblue     NVARCHAR(50),
    @nombre_newblue     NVARCHAR(255),
    @id_habitacion      INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @nuevo_id_habitacion INT;
    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1 FROM Eurovip.tipo_habitacion WHERE id_idioma = @id_idioma AND id_tipo_habitacion = @id_tipo_habitacion)
        INSERT INTO Eurovip.tipo_habitacion (id_idioma, id_tipo_habitacion) VALUES (@id_idioma, @id_tipo_habitacion);

    IF @id_habitacion IS NOT NULL AND EXISTS (
        SELECT 1 FROM Eurovip.alojamiento_habitacion
        WHERE id_habitacion = @id_habitacion AND id_alojamiento = @id_alojamiento AND id_idioma = @id_idioma)
    BEGIN
        UPDATE Eurovip.alojamiento_habitacion SET
            codigo = @codigo, nombre = @nombre, grupo = @grupo, nota = @nota, precio = @precio,
            id_tipo_habitacion = @id_tipo_habitacion, codigo_newblue = @codigo_newblue, nombre_newblue = @nombre_newblue
        WHERE id_habitacion = @id_habitacion AND id_alojamiento = @id_alojamiento AND id_idioma = @id_idioma;
        SET @nuevo_id_habitacion = @id_habitacion;
    END
    ELSE
    BEGIN
        IF @id_habitacion IS NOT NULL
            SET @nuevo_id_habitacion = @id_habitacion;
        ELSE
            SELECT @nuevo_id_habitacion = ISNULL(MAX(id_habitacion), 0) + 1 FROM Eurovip.alojamiento_habitacion WITH (UPDLOCK, HOLDLOCK);

        INSERT INTO Eurovip.alojamiento_habitacion (id_alojamiento, id_habitacion, id_idioma, id_tipo_habitacion, codigo, nombre,
               grupo, nota, precio, codigo_newblue, nombre_newblue)
        VALUES (@id_alojamiento, @nuevo_id_habitacion, @id_idioma, @id_tipo_habitacion, @codigo, @nombre,
               @grupo, @nota, @precio, @codigo_newblue, @nombre_newblue);
    END

    COMMIT;
    SELECT @nuevo_id_habitacion AS nuevo_id_habitacion;
END
GO

/* ---------- CAMBIO 36: índices para buscar rápido en Herramientas ----------
   Buscar "suite" en 130.000 habitaciones obligaba a leer la tabla entera (con sus notas largas).
   Estos índices solo guardan las columnas que se buscan, así la lectura es mucho más chica. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_aloj_hab_buscar' AND object_id = OBJECT_ID('Eurovip.alojamiento_habitacion'))
    CREATE NONCLUSTERED INDEX IX_Eurovip_aloj_hab_buscar ON Eurovip.alojamiento_habitacion (id_idioma, nombre)
        INCLUDE (id_tipo_habitacion, codigo, precio);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_alojamiento_buscar' AND object_id = OBJECT_ID('Eurovip.alojamiento'))
    CREATE NONCLUSTERED INDEX IX_Eurovip_alojamiento_buscar ON Eurovip.alojamiento (id_idioma, nombre)
        INCLUDE (id_destino, id_pais, id_tipo_alojamiento, categoria, activo);
GO

/* ##########################################################################################
   FASE 7 — PERFIL DE USUARIO y NOTIFICACIONES  (13/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: en la barra de arriba, la campana muestra avisos (cargas terminadas o con error,
   cambios de otros usuarios, ofertas que salen esta semana, tarjetas de la Home vencidas) y el
   nombre abre "Mi perfil", donde cada usuario ve sus datos, sube su foto y edita nombre,
   apellido y teléfono (correo, usuario y contraseña NO se editan desde ahí).
   Los avisos se arman leyendo tablas que ya existían (Log_Cargas, auditoría, servicio, inicio);
   no se creó una tabla de notificaciones.

   CAMBIO 37  usuario.notif_leidas_hasta: hasta cuándo el usuario ya vio sus avisos.
   ########################################################################################## */
/* ---------- CAMBIO 37: usuario.notif_leidas_hasta ----------
   Para qué: la campana de notificaciones. Guarda hasta qué fecha/hora el usuario ya vio sus
   avisos; lo que sea más nuevo se cuenta como "no leído". Vacío = nunca abrió la campana. */
IF COL_LENGTH('Eurovip.usuario', 'notif_leidas_hasta') IS NULL
    ALTER TABLE Eurovip.usuario ADD notif_leidas_hasta DATETIME NULL;
GO

/* ##########################################################################################
   FASE 8 — CONSULTAR OFERTAS  (13/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: la pantalla "Consultar ofertas" (como /ConsultarOferta/Index de BackTiTan) busca
   cualquier servicio cargado (ofertas de hotel y salidas de circuito) con muchos filtros, y
   permite editar, activar/desactivar y duplicar uno o muchos a la vez.
   Copiado de ViajeVDT (10.10.10.29, solo lectura) y adaptado al esquema Eurovip:

   CAMBIO 38  fnFiltro: el buscador. En ViajeVDT exigía vuelo; aquí funciona con o sin vuelo.
   CAMBIO 39  spEditaServicio: edita moneda, campaña, proveedor, precio, descuento, ajuste,
              activo y aerolínea (lo que llega vacío no se toca).
   CAMBIO 40  spDuplicaServicio: copia un servicio completo. Corregido (se tragaba los errores)
              y mejorado: se puede duplicar a otras fechas (mover N días) y con otro origen.
   ########################################################################################## */
/* ---------- CAMBIO 38: fnFiltro — buscador general de servicios (ofertas y circuitos) ----------
   Copiado de ViajeVDT.dbo.fnFiltro y adaptado:
     • En ViajeVDT exigía vuelo (INNER JOIN con servicio_itinerario): aquí el vuelo es opcional.
     • Usaba funciones que Eurovip no tiene (fnUneDestino, fnFechaInicio…): se calcula todo aquí.
     • Las fechas filtran por la ENTRADA del servicio (hotel o salida del circuito), no por el vuelo.
     • Devuelve también si lleva vuelo, el circuito y el país de destino. */
CREATE OR ALTER FUNCTION Eurovip.fnFiltro
(
    @idServicio INT, @idIdioma CHAR(2), @idTipoServicio VARCHAR(15), @idMoneda CHAR(3), @idCampanya SMALLINT,
    @idProveedor VARCHAR(10), @idPaisOrigen CHAR(2), @nombreOrigen VARCHAR(30), @idPaisDestino CHAR(2), @idDestino VARCHAR(MAX),
    @inicioDesde DATE, @salidaDesde DATE, @aperturaDesde DATE, @aperturaHasta DATE, @usuario VARCHAR(15),
    @precio_baseDesde MONEY, @precio_baseHasta MONEY, @descuentoDesde NUMERIC(6, 2), @descuentoHasta NUMERIC(6, 2),
    @idAlojamiento INT, @noches SMALLINT, @activo BIT, @codigo VARCHAR(30)
)
RETURNS TABLE
AS
RETURN
(
    SELECT x.* FROM (
        SELECT
            s.id_servicio, s.id_tipo_servicio, s.id_idioma, s.id_moneda, s.id_campanya, s.codigo, c.nombre AS nombreCampanya,
            COALESCE(so.origen, sc.origen, si.origen) AS origen,
            COALESCE(so.id_pais_origen, sc.id_pais_origen, si.id_pais_origen) AS id_pais_origen,
            CASE WHEN sc.id_servicio IS NOT NULL THEN sc.nombre_circuito ELSE a.id_destino END AS destino,
            COALESCE(a.id_pais, (SELECT TOP 1 cd.id_pais FROM Eurovip.circuito_destino cd WHERE cd.nombre_circuito = sc.nombre_circuito AND cd.id_idioma = sc.id_idoma)) AS id_pais_destino,
            CAST(COALESCE(sd.desde, sc.desde) AS DATE) AS inicio, CAST(COALESCE(sd.hasta, sc.hasta) AS DATE) AS regreso,
            s.noches, s.id_Proveedor AS codigoOperador, p.nombre AS nombreProveedor, s.apertura, s.usuario, s.nota, s.activo,
            sd.id_alojamiento, a.nombre AS nombreAlojamiento, r.descripcion AS regimen, sd.id_regimen,
            hab.nombre AS habitacion, sc.nombre_circuito,
            CAST(CASE WHEN si.id_servicio IS NULL THEN 0 ELSE 1 END AS BIT) AS conVuelo,
            si.id_proveedor AS codigoAerolinea, ae.nombre AS nombreAerolinea, si.vuelo, si.id_estacion_origen, si.id_estacion_destino,
            si.fecha_salida, si.hora_salida AS horaSalidaVuelo,
            s.precio_base, s.descuento, s.precio_mas_menos, Eurovip.fnTotalTasa(s.id_servicio) AS totalTasa, Eurovip.fnPrecioWeb(s.id_servicio) AS precioWeb
        FROM Eurovip.servicio s
            LEFT JOIN Eurovip.campanya c ON c.id_campanya = s.id_campanya AND c.id_idioma = s.id_idioma
            LEFT JOIN Eurovip.proveedor p ON p.id_proveedor = s.id_Proveedor
            OUTER APPLY (SELECT TOP 1 * FROM Eurovip.servicio_detalle d WHERE d.id_servicio = s.id_servicio ORDER BY d.secuencia) sd
            LEFT JOIN Eurovip.alojamiento a ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
            LEFT JOIN Eurovip.regimen r ON r.id_regimen = sd.id_regimen AND r.id_idioma = sd.id_idioma
            LEFT JOIN Eurovip.servicio_circuito sc ON sc.id_servicio = s.id_servicio
            LEFT JOIN Eurovip.servicio_origen so ON so.id_servicio = s.id_servicio
            OUTER APPLY (SELECT TOP 1 * FROM Eurovip.servicio_itinerario i WHERE i.id_servicio = s.id_servicio AND i.segmento = 1) si
            LEFT JOIN Eurovip.proveedor ae ON ae.id_proveedor = si.id_proveedor
            OUTER APPLY (SELECT TOP 1 ah.nombre FROM Eurovip.servicio_habitacion h
                           JOIN Eurovip.alojamiento_habitacion ah ON ah.id_alojamiento = h.id_alojamiento AND ah.id_habitacion = h.id_habitacion AND ah.id_idioma = h.id_idioma
                         WHERE h.id_servicio = s.id_servicio ORDER BY h.precio, h.secuencia) hab
        WHERE (@idServicio IS NULL OR s.id_servicio = @idServicio)
          AND (@idIdioma IS NULL OR s.id_idioma = @idIdioma)
          AND (@idTipoServicio IS NULL OR s.id_tipo_servicio = @idTipoServicio)
          AND (@idMoneda IS NULL OR s.id_moneda = @idMoneda)
          AND (@idCampanya IS NULL OR s.id_campanya = @idCampanya)
          AND (@idProveedor IS NULL OR s.id_Proveedor = @idProveedor)
          AND (@aperturaDesde IS NULL OR s.apertura >= @aperturaDesde)
          AND (@aperturaHasta IS NULL OR s.apertura < DATEADD(DAY, 1, @aperturaHasta))
          AND (@usuario IS NULL OR s.usuario = @usuario)
          AND (@precio_baseDesde IS NULL OR s.precio_base >= @precio_baseDesde)
          AND (@precio_baseHasta IS NULL OR s.precio_base <= @precio_baseHasta)
          AND (@descuentoDesde IS NULL OR s.descuento >= @descuentoDesde)
          AND (@descuentoHasta IS NULL OR s.descuento <= @descuentoHasta)
          AND (@idAlojamiento IS NULL OR sd.id_alojamiento = @idAlojamiento)
          AND (@noches IS NULL OR s.noches = @noches)
          AND (@activo IS NULL OR s.activo = @activo)
          AND (@codigo IS NULL OR s.codigo = @codigo)
    ) x
    WHERE (@idPaisOrigen IS NULL OR x.id_pais_origen = @idPaisOrigen)
      AND (@nombreOrigen IS NULL OR x.origen = @nombreOrigen)
      AND (@idPaisDestino IS NULL OR x.id_pais_destino = @idPaisDestino)
      AND (@idDestino IS NULL OR x.destino = @idDestino)
      AND (@inicioDesde IS NULL OR x.inicio >= @inicioDesde)
      AND (@salidaDesde IS NULL OR x.inicio <= @salidaDesde)
);
GO

/* ---------- CAMBIO 39: spEditaServicio — editar uno o muchos servicios ----------
   Copiado de ViajeVDT. Lo que llega vacío (NULL) no se toca. Diferencias: el parámetro @baja de
   ViajeVDT en realidad cambiaba "activo": aquí se llama @activo; la aerolínea solo se cambia si
   el servicio tiene vuelo; todo en una transacción. */
CREATE OR ALTER PROCEDURE Eurovip.spEditaServicio
    @numeroServicio INT, @idMoneda CHAR(3) = NULL, @idCampanya SMALLINT = NULL, @idProveedor VARCHAR(10) = NULL,
    @precioBase MONEY = NULL, @descuento NUMERIC(6, 2) = NULL, @precioMasMenos MONEY = NULL, @activo BIT = NULL,
    @idAerolinea VARCHAR(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    UPDATE Eurovip.servicio SET
        id_moneda = ISNULL(@idMoneda, id_moneda), id_campanya = ISNULL(@idCampanya, id_campanya),
        id_Proveedor = ISNULL(@idProveedor, id_Proveedor), precio_base = ISNULL(@precioBase, precio_base),
        descuento = ISNULL(@descuento, descuento), precio_mas_menos = ISNULL(@precioMasMenos, precio_mas_menos),
        activo = ISNULL(@activo, activo)
    WHERE id_servicio = @numeroServicio;
    IF @idAerolinea IS NOT NULL
        UPDATE Eurovip.servicio_itinerario SET id_proveedor = @idAerolinea WHERE id_servicio = @numeroServicio;
    COMMIT;
END
GO

/* ---------- CAMBIO 40: spDuplicaServicio — copia un servicio completo ----------
   Copiado de ViajeVDT y corregido: devolvía una consulta de más, se "tragaba" los errores (la
   pantalla decía OK aunque fallara) y copiaba tablas que Eurovip no tiene. NUEVO: se puede
   duplicar a OTRAS FECHAS (@diasDesplazar mueve entrada, salida, circuito y vuelos) y con otro
   origen de venta. Copia: servicio, detalle, habitaciones, tasas, notas, vuelos, origen y circuito. */
CREATE OR ALTER PROCEDURE Eurovip.spDuplicaServicio
    @numeroServicio INT, @idCampanya SMALLINT = NULL, @diasDesplazar INT = 0,
    @idPaisOrigen CHAR(2) = NULL, @origen VARCHAR(30) = NULL, @codigoEstacionOrigen VARCHAR(10) = NULL,
    @usuario VARCHAR(15) = NULL, @nuevoId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM Eurovip.servicio WHERE id_servicio = @numeroServicio)
        THROW 50001, 'El servicio a duplicar no existe.', 1;
    DECLARE @d INT = ISNULL(@diasDesplazar, 0);
    BEGIN TRAN;

    INSERT INTO Eurovip.servicio (id_tipo_servicio, id_idioma, id_moneda, id_campanya, id_Proveedor, apertura, usuario, precio_base, descuento,
                                  precio_mas_menos, ajuste, nota, noches, codigo, baja, nombre, activo, categoria_circuito)   -- "precio" es columna calculada
    SELECT id_tipo_servicio, id_idioma, id_moneda, ISNULL(@idCampanya, id_campanya), id_Proveedor, GETDATE(), ISNULL(@usuario, usuario), precio_base, descuento,
           precio_mas_menos, ajuste, LTRIM(ISNULL(nota, '') + ' (duplicada de #' + CAST(@numeroServicio AS VARCHAR(12)) + ')'), noches, codigo, baja, nombre, activo, categoria_circuito
    FROM Eurovip.servicio WHERE id_servicio = @numeroServicio;
    SET @nuevoId = SCOPE_IDENTITY();

    INSERT INTO Eurovip.servicio_detalle (id_servicio, secuencia, id_alojamiento, id_regimen, id_idioma, desde, hora_desde, hasta, hora_hasta, noches, categoria, precio)
    SELECT @nuevoId, secuencia, id_alojamiento, id_regimen, id_idioma, DATEADD(DAY, @d, desde), hora_desde, DATEADD(DAY, @d, hasta), hora_hasta, noches, categoria, precio
    FROM Eurovip.servicio_detalle WHERE id_servicio = @numeroServicio;

    INSERT INTO Eurovip.servicio_habitacion (id_servicio, secuencia, id_alojamiento, id_idioma, id_habitacion, precio)
    SELECT @nuevoId, secuencia, id_alojamiento, id_idioma, id_habitacion, precio FROM Eurovip.servicio_habitacion WHERE id_servicio = @numeroServicio;

    INSERT INTO Eurovip.servicio_tasa (id_Servicio, id_tasa, id_idioma, valor)
    SELECT @nuevoId, id_tasa, id_idioma, valor FROM Eurovip.servicio_tasa WHERE id_Servicio = @numeroServicio;

    INSERT INTO Eurovip.servicio_nota (id_servicio, secuencia, id_idioma, tipo, nota1, nota2)
    SELECT @nuevoId, secuencia, id_idioma, tipo, nota1, nota2 FROM Eurovip.servicio_nota WHERE id_servicio = @numeroServicio;

    INSERT INTO Eurovip.servicio_itinerario (id_servicio, segmento, id_proveedor, id_estacion_origen, origen, id_idioma, id_pais_origen, id_estacion_destino,
                                             destino, id_pais_destino, vuelo, fecha_salida, hora_salida, fecha_regreso, hora_regreso, clase, conexion)
    SELECT @nuevoId, segmento, id_proveedor,
           CASE WHEN segmento = 1 AND @codigoEstacionOrigen IS NOT NULL THEN @codigoEstacionOrigen ELSE id_estacion_origen END,
           CASE WHEN segmento = 1 AND @origen IS NOT NULL THEN @origen ELSE origen END, id_idioma,
           CASE WHEN segmento = 1 AND @idPaisOrigen IS NOT NULL THEN @idPaisOrigen ELSE id_pais_origen END,
           id_estacion_destino, destino, id_pais_destino, vuelo, DATEADD(DAY, @d, fecha_salida), hora_salida, DATEADD(DAY, @d, fecha_regreso), hora_regreso, clase, conexion
    FROM Eurovip.servicio_itinerario WHERE id_servicio = @numeroServicio;

    INSERT INTO Eurovip.servicio_origen (id_servicio, id_pais_origen, origen)
    SELECT @nuevoId, ISNULL(@idPaisOrigen, id_pais_origen), ISNULL(@origen, origen) FROM Eurovip.servicio_origen WHERE id_servicio = @numeroServicio;

    -- CAMBIO 44 (FASE 11): la copia lleva el mismo destino (el de sus hoteles)
    IF OBJECT_ID('Eurovip.servicio_destino', 'U') IS NOT NULL
        INSERT INTO Eurovip.servicio_destino (id_servicio, id_pais_destino, destino)
        SELECT @nuevoId, id_pais_destino, destino FROM Eurovip.servicio_destino WHERE id_servicio = @numeroServicio;

    INSERT INTO Eurovip.servicio_circuito (id_servicio, nombre_circuito, id_idoma, desde, hasta, id_pais_origen, origen)
    SELECT @nuevoId, nombre_circuito, id_idoma, DATEADD(DAY, @d, desde), DATEADD(DAY, @d, hasta), ISNULL(@idPaisOrigen, id_pais_origen), ISNULL(@origen, origen)
    FROM Eurovip.servicio_circuito WHERE id_servicio = @numeroServicio;

    COMMIT;
END
GO

/* ##########################################################################################
   FASE 9 — FLYERS  (13/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: la pantalla "Flyers" (como /Flyer/Hub de BackTiTan) arma imágenes con los precios
   más bajos de cada destino, las guarda en una galería y las envía por WhatsApp a una lista de
   contactos (cuenta de WhatsApp de BackTiTan; las claves van SOLO en appsettings.Local.json).
   Decisión de la usuaria (13/10/2026): copiar también los contactos de ViajeVDT.

   CAMBIO 41  Tablas copiadas de ViajeVDT (solo lectura), con sus datos:
              flyer_config 11 · flyer_datos 25 · flyer_contactos 178 · flyer_envios 1 ·
              flyer_oferta 1 · flyer_oferta_token 0 · contactos_vdt 1.
              (Se conservaron los números de cada fila.)
   ########################################################################################## */
/* ---------- CAMBIO 41: tablas de FLYERS (copiadas de ViajeVDT) ---------- */
IF OBJECT_ID('Eurovip.flyer_config') IS NULL
CREATE TABLE Eurovip.flyer_config (            -- qué destinos/campañas salen en los flyers
    id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_flyer_config PRIMARY KEY,
    nombre_config    NVARCHAR(100) NULL,
    id_tipo_servicio VARCHAR(30)   NOT NULL,
    id_campanya      INT           NOT NULL,
    id_idioma        VARCHAR(10)   NOT NULL,
    id_moneda        VARCHAR(10)   NOT NULL,
    activo_servicio  INT           NOT NULL,
    origen           NVARCHAR(100) NOT NULL,
    id_regimen       VARCHAR(20)   NOT NULL,
    id_destino       NVARCHAR(100) NOT NULL,
    nombre_destino   NVARCHAR(200) NULL,
    fecha_creacion   DATETIME      NULL CONSTRAINT DF_Eurovip_flyer_config_fecha DEFAULT (GETDATE()),
    url_destino      NVARCHAR(500) NULL,       -- imagen de fondo del flyer
    tipo             NVARCHAR(20)  NULL
);
GO
IF OBJECT_ID('Eurovip.flyer_datos') IS NULL
CREATE TABLE Eurovip.flyer_datos (             -- precios calculados que se pintan en cada flyer
    id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_flyer_datos PRIMARY KEY,
    id_destino          VARCHAR(30)   NULL,
    nombre_destino      NVARCHAR(200) NULL,
    nombre_hotel        NVARCHAR(300) NULL,
    nombre_habitacion   NVARCHAR(200) NULL,
    regimen             NVARCHAR(150) NULL,
    precio_final        INT           NULL,
    posicion            TINYINT       NULL,
    fecha_actualizacion DATETIME      NULL,
    activo              BIT           NULL
);
GO
IF OBJECT_ID('Eurovip.flyer_contactos') IS NULL
CREATE TABLE Eurovip.flyer_contactos (         -- a quién se envían por WhatsApp
    id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_flyer_contactos PRIMARY KEY,
    activo          BIT           NOT NULL CONSTRAINT DF_Eurovip_flyer_contactos_activo DEFAULT (1),
    created_at      DATETIME      NOT NULL CONSTRAINT DF_Eurovip_flyer_contactos_fecha DEFAULT (GETDATE()),
    nombre_completo NVARCHAR(200) NULL,
    numero          NVARCHAR(30)  NOT NULL,
    email           NVARCHAR(200) NULL
);
GO
IF OBJECT_ID('Eurovip.flyer_envios') IS NULL
CREATE TABLE Eurovip.flyer_envios (            -- historial de envíos
    id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_flyer_envios PRIMARY KEY,
    fecha_envio     DATETIME      NOT NULL,
    carpeta_fecha   NVARCHAR(20)  NOT NULL,
    total_contactos INT           NOT NULL,
    enviados_ok     INT           NOT NULL,
    errores         INT           NOT NULL,
    imagenes_count  INT           NOT NULL,
    texto_mensaje   NVARCHAR(MAX) NULL,
    estado          NVARCHAR(50)  NULL
);
GO
IF OBJECT_ID('Eurovip.flyer_oferta') IS NULL
CREATE TABLE Eurovip.flyer_oferta (
    id_oferta      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_flyer_oferta PRIMARY KEY,
    titulo         NVARCHAR(200) NOT NULL,
    subtitulo      NVARCHAR(300) NULL,
    fecha_desde    DATE          NULL,
    fecha_hasta    DATE          NULL,
    notas          NVARCHAR(MAX) NULL,
    hoteles_json   NVARCHAR(MAX) NOT NULL,
    estado         INT           NOT NULL,
    fecha_creacion DATETIME      NOT NULL
);
GO
IF OBJECT_ID('Eurovip.flyer_oferta_token') IS NULL
CREATE TABLE Eurovip.flyer_oferta_token (
    id_token         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_flyer_oferta_token PRIMARY KEY,
    id_oferta        INT           NOT NULL,
    token            NVARCHAR(100) NOT NULL,
    nombre_usuario   NVARCHAR(200) NULL,
    email_usuario    NVARCHAR(200) NULL,
    fecha_creacion   DATETIME      NOT NULL,
    fecha_expiracion DATETIME      NULL,
    activo           INT           NOT NULL
);
GO
IF OBJECT_ID('Eurovip.contactos_vdt') IS NULL
CREATE TABLE Eurovip.contactos_vdt (
    id_contacto    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_contactos_vdt PRIMARY KEY,
    nombre         NVARCHAR(200) NOT NULL,
    telefono       NVARCHAR(50)  NOT NULL,
    correo         NVARCHAR(200) NULL,
    activo         INT           NOT NULL,
    fecha_creacion DATETIME      NOT NULL
);
GO

/* ##########################################################################################
   FASE 10 — USUARIOS Y ACCESOS  (13/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: la pantalla "Usuarios y accesos" (como /Usuario2 + /Usuario2/Accesos de BackTiTan,
   todo en una) permite crear usuarios, activarlos/desactivarlos, cambiarles la contraseña y
   decir qué módulos de Eurovip puede abrir cada uno.

   Explicado fácil:
   • Cada pantalla de Eurovip es un "módulo" (tabla UsuarioAcceso). Los módulos de BackTiTan NO
     se copian: son otras pantallas. Eurovip tiene su propia lista (CAMBIO 43).
   • UsuarioModulos dice "este usuario SÍ / NO puede entrar a este módulo".
   • El rol "Superadministrador" entra a todo siempre (no necesita marcar módulos).
   • Si un usuario no tiene un módulo, no lo ve en el menú y si entra por la dirección le sale
     "Acceso denegado". Inicio y "Mi perfil" los ven todos.

   CAMBIO 42  UsuarioAcceso: columnas nuevas clave, controladores, icono, descripcion, orden.
   CAMBIO 43  Módulos de Eurovip y roles (Superadministrador, Administrador, Usuario).
   ########################################################################################## */
/* ---------- CAMBIO 42: UsuarioAcceso con datos para el menú y para saber qué pantallas cubre ---------- */
IF COL_LENGTH('Eurovip.UsuarioAcceso', 'clave') IS NULL
    ALTER TABLE Eurovip.UsuarioAcceso ADD
        clave         VARCHAR(40)   NULL,      -- código fijo que usa el programa (ej. 'flyers_enviar')
        controladores VARCHAR(300)  NULL,      -- pantallas que cubre, separadas por coma (ej. 'Ofertas,CargaManual')
        icono         VARCHAR(40)   NULL,      -- icono de Bootstrap Icons (ej. 'bi-building')
        descripcion   NVARCHAR(300) NULL,      -- explicación corta que ve el administrador
        orden         INT           NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Eurovip_UsuarioAcceso_clave')
    CREATE UNIQUE INDEX UX_Eurovip_UsuarioAcceso_clave ON Eurovip.UsuarioAcceso (clave) WHERE clave IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Eurovip_UsuarioModulos_usuario_modulo')
    CREATE UNIQUE INDEX UX_Eurovip_UsuarioModulos_usuario_modulo ON Eurovip.UsuarioModulos (usuario, id_modulo);
GO

/* ---------- CAMBIO 43: módulos de Eurovip y roles ---------- */
MERGE Eurovip.UsuarioAcceso AS d
USING (VALUES
    ('hoteles',           N'Hoteles',                    N'Cargas',         'Hoteles',                  'bi-building',               N'Ver y editar los hoteles y sus contenedores.', 10),
    ('carga_manual',      N'Paquetes',                   N'Cargas',         'Ofertas,CargaManual',      'bi-box-seam',               N'Cargar paquetes (Express y Clásica) y editar la lista de paquetes.', 20),
    ('circuitos',         N'Circuitos',                  N'Cargas',         'Circuitos,CargaCircuitos', 'bi-signpost-split',         N'Cargar y editar circuitos.', 30),
    ('historial_cargas',  N'Historial de cargas',        N'Cargas',         'Cargas',                   'bi-clock-history',          N'Ver quién cargó qué y cuándo.', 40),
    ('home_web',          N'Home de la web y carrusel',  N'Web',            'HomeWeb',                  'bi-window-sidebar',         N'Ordenar la portada de la web y el slider.', 50),
    ('consultar_ofertas', N'Consultar ofertas',          N'Web',            'ConsultarOferta',          'bi-search-heart',           N'Buscar, editar, duplicar y exportar ofertas a la venta.', 60),
    ('flyers',            N'Flyers',                     N'Web',            'Flyer',                    'bi-file-earmark-richtext',  N'Generar flyers y manejar los contactos.', 70),
    ('flyers_enviar',     N'Enviar flyers por WhatsApp', N'Web',            NULL,                       'bi-whatsapp',               N'Permiso especial: enviar los flyers a los contactos.', 75),
    ('herramientas',      N'Herramientas',               N'Administración', 'Herramientas',             'bi-tools',                  N'Mantenimientos básicos: destinos, monedas, campañas, etc.', 80),
    ('reportes',          N'Reportes',                   N'Administración', 'Reportes',                 'bi-bar-chart-line',         N'Tablero y reportes de salidas.', 90),
    ('usuarios',          N'Usuarios y accesos',         N'Administración', 'Usuarios',                 'bi-people',                 N'Crear usuarios y decidir qué ve cada uno.', 100)
) AS o (clave, nombre, categoria, controladores, icono, descripcion, orden)
ON d.clave = o.clave
WHEN MATCHED THEN UPDATE SET nombre_modulo = o.nombre, Categoria = o.categoria, controladores = o.controladores,
                             icono = o.icono, descripcion = o.descripcion, orden = o.orden
WHEN NOT MATCHED THEN INSERT (nombre_modulo, Categoria, clave, controladores, icono, descripcion, orden)
                      VALUES (o.nombre, o.categoria, o.clave, o.controladores, o.icono, o.descripcion, o.orden);
GO
INSERT INTO Eurovip.rol (codigoRol)
SELECT r FROM (VALUES ('Superadministrador'), ('Administrador'), ('Usuario')) v (r)
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.rol x WHERE x.codigoRol = v.r);
GO

/* ##########################################################################################
   FASE 11 — DESTINO DE LA OFERTA  (14/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: la base guardaba el ORIGEN de venta de cada oferta (servicio_origen) pero no su
   DESTINO. Pedido de la usuaria (14/10/2026): "así como colocaste el origen coloca el destino".

   Explicado fácil:
   • El destino de una oferta es el destino de sus hoteles (alojamiento.id_destino).
   • En una carga, TODOS los hoteles tienen que ser del mismo destino: si todos son de
     Punta Cana, la oferta es destino = Punta Cana. La pantalla y el servidor no dejan
     mezclar hoteles de destinos distintos.
   • servicio_destino es igual que servicio_origen: una fila por oferta.

   CAMBIO 44  Tabla servicio_destino + se llena para las ofertas que ya existen (todas tenían
              un solo destino por sus hoteles: 122 de 122).
              Además se actualizaron en su sitio (más arriba en este archivo):
              • spEliminaServicio: borra también servicio_destino.
              • spDuplicaServicio: la copia lleva el mismo destino.
   ########################################################################################## */
/* ---------- CAMBIO 44: servicio_destino ---------- */
IF OBJECT_ID('Eurovip.servicio_destino', 'U') IS NULL
CREATE TABLE Eurovip.servicio_destino (
    id_servicio     INT         NOT NULL,
    id_pais_destino CHAR(2)     NOT NULL,      -- país del destino (alojamiento.id_pais)
    destino         VARCHAR(30) NOT NULL,      -- destino de los hoteles (alojamiento.id_destino / destino.id_destino)
    CONSTRAINT PK_Eurovip_servicio_destino PRIMARY KEY CLUSTERED (id_servicio),
    CONSTRAINT FK_Eurovip_servicio_destino_servicio FOREIGN KEY (id_servicio) REFERENCES Eurovip.servicio (id_servicio)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_servicio_destino_destino')
    CREATE INDEX IX_Eurovip_servicio_destino_destino ON Eurovip.servicio_destino (destino, id_pais_destino);
GO
-- Llenar las ofertas que ya existen con el destino de su hotel (solo las que tienen UN destino)
INSERT INTO Eurovip.servicio_destino (id_servicio, id_pais_destino, destino)
SELECT s.id_servicio, MIN(a.id_pais), MIN(a.id_destino)
FROM Eurovip.servicio s
    JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.id_idioma = s.id_idioma
    JOIN Eurovip.alojamiento a       ON a.id_alojamiento = sd.id_alojamiento AND a.id_idioma = sd.id_idioma
WHERE s.id_tipo_servicio = 'OFERTA'
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_destino x WHERE x.id_servicio = s.id_servicio)
GROUP BY s.id_servicio
HAVING COUNT(DISTINCT a.id_destino) = 1;
GO

/* ##########################################################################################
   FASE 12 — INCLUYE / NO INCLUYE / NOTAS EN servicio_descripcion  (15/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: pedido de la usuaria: "quiero que sea servicio_descripcion, cópiala de ViajeVDT
   (10.10.10.29) al esquema Eurovip y ahí guardarás las notas, incluye, no incluye; haz el
   cambio para oferta manual y carga de circuitos".

   Explicado fácil:
   • Es la misma tabla que usa BackTiTan (ViajeVDT.dbo.servicio_descripcion), copiada SOLO la
     estructura: sus 16 filas son de ofertas de ViajeVDT que no existen en Eurovip.
   • UNA fila por carga y por idioma, unida a la oferta por servicio.codigo (todas las
     salidas/fechas de una misma carga comparten el mismo código y, por tanto, los textos).
   • Columnas: incluye · no_incluye · nota (condiciones, importante, documentación…) ·
     oferta_especial · itinerario · imagen. Las de texto se crean como VARCHAR(MAX) (en ViajeVDT
     son TEXT, un tipo antiguo; guardan lo mismo).
   • Antes Eurovip guardaba estos textos en servicio_nota (una fila por texto y por salida).
     Esa tabla se deja como está (estaba vacía) pero las cargas ya no la usan.

   CAMBIO 45  Tabla servicio_descripcion + procedimiento spInsertaServicioDescripcion (copia del
              de ViajeVDT, en el esquema Eurovip).
              Además se actualizó en su sitio (más arriba en este archivo):
              • spEliminaServicio: si ya no queda ninguna oferta con ese código, borra también
                su descripción. (spDuplicaServicio no cambia: la copia lleva el mismo código y
                comparte la descripción.)
   ########################################################################################## */
/* ---------- CAMBIO 45: servicio_descripcion ---------- */
IF OBJECT_ID('Eurovip.servicio_descripcion', 'U') IS NULL
CREATE TABLE Eurovip.servicio_descripcion (
    codigo          VARCHAR(50)  NOT NULL,     -- servicio.codigo (el código de la carga)
    id_idioma       CHAR(2)      NOT NULL,
    nota            VARCHAR(MAX) NULL,         -- condiciones, importante, documentación…
    incluye         VARCHAR(MAX) NULL,
    no_incluye      VARCHAR(MAX) NULL,
    itinerario      VARCHAR(MAX) NULL,
    imagen          VARCHAR(MAX) NULL,
    oferta_especial VARCHAR(MAX) NULL,
    CONSTRAINT PK_Eurovip_servicio_descripcion PRIMARY KEY CLUSTERED (codigo, id_idioma)
);
GO
CREATE OR ALTER PROCEDURE Eurovip.spInsertaServicioDescripcion
    @codigo VARCHAR(50),
    @id_idioma CHAR(2),
    @nota VARCHAR(MAX),
    @incluye VARCHAR(MAX),
    @no_incluye VARCHAR(MAX),
    @itinerario VARCHAR(MAX),
    @imagen VARCHAR(MAX),
    @oferta_especial VARCHAR(MAX)
AS
BEGIN
    -- Copia de ViajeVDT.dbo.spInsertaServicioDescripcion (Mariana, agosto 2025), en el esquema Eurovip.
    SET NOCOUNT ON;
    INSERT INTO Eurovip.servicio_descripcion (codigo, id_idioma, nota, incluye, no_incluye, itinerario, imagen, oferta_especial)
    VALUES (@codigo, @id_idioma, @nota, @incluye, @no_incluye, @itinerario, @imagen, @oferta_especial);
END
GO
/* ##########################################################################################
   FASE 13 — EXCURSIONES  (15/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: pedido de la usuaria: "haz el mantenimiento de excursión; la tabla es servicio_excursion,
   cópiala de ViajeVDT (10.10.10.29) al esquema Eurovip con los datos cuando id_destino = 'Punta Cana'".

   Explicado fácil:
   • Misma tabla que ViajeVDT.dbo.servicio_excursion (id_Excursion autonumérico, id_destino,
     id_tipo_servicio, nombre, descripcion, cancelable, precio).
   • Se copiaron las 24 excursiones de Punta Cana con su MISMO número (id_Excursion). Al copiar:
     el destino se escribe como en Destinos ('Punta Cana'; en ViajeVDT estaba 'Punta cana') y los
     nombres sin espacios de más.
   • La foto NO va en la base: como BackTiTan, se relaciona por el nombre de la excursión sin
     espacios ni acentos → wwwroot/imagenes/excursiones/{Nombre}.webp (se convierte a WebP).
   • Módulo nuevo 'excursiones' en Usuarios y accesos (Superadministrador lo ve siempre; a los
     demás hay que dárselo desde Usuarios y accesos).

   CAMBIO 46  Tabla servicio_excursion + datos de Punta Cana.
   CAMBIO 47  Módulo 'excursiones'.
   ########################################################################################## */
/* ---------- CAMBIO 46: servicio_excursion ---------- */
IF OBJECT_ID('Eurovip.servicio_excursion', 'U') IS NULL
CREATE TABLE Eurovip.servicio_excursion (
    id_Excursion     INT IDENTITY(1,1) NOT NULL,
    id_destino       VARCHAR(50)  NULL,      -- destino.id_destino (ej. Punta Cana)
    id_tipo_servicio VARCHAR(20)  NULL,      -- 'Excursion'
    nombre           VARCHAR(500) NULL,
    descripcion      VARCHAR(MAX) NULL,      -- puede traer HTML sencillo (<p>…), como en ViajeVDT
    cancelable       BIT          NULL,
    precio           MONEY        NULL,
    CONSTRAINT PK_Eurovip_servicio_excursion PRIMARY KEY CLUSTERED (id_Excursion)
);
GO
-- Datos de Punta Cana copiados de ViajeVDT.dbo.servicio_excursion (leídos el 15/10/2026).
DECLARE @json NVARCHAR(MAX) = N'[{"id_Excursion":1,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Catamarán (Party Boat)","descripcion":"<p>Una jornada diferente a bordo de un verdadero catamarán en el mar Caribe, bordeando la costa de Punta Cana, con animación, posibilidad de practicar esnórquel entre hermosos peces de colores y disfrutando de un buen ambiente hasta llegar a la piscina natural. A bordo de nuestro barco contamos con un fantástico equipo de animación, barra libre de bebidas nacionales, snack, refrescos, agua, y, para ganar fuerza, nuestra famosa bebida \"energizante\", la mamajuana. Nuestra primera parada será en el banco de peces, allí dispondremos de 30 minutos para hacer esnórquel, si así lo desea o simplemente disfrutar de nuestra música caribeña, bachata y merengue, a bordo. Continuaremos bailando y tomando tragos en esta fascinante embarcación hasta llegar a nuestro segundo destino: una piscina natural, formada por acumulación de arena en medio del mar. Súmate al equipo de animación y disfruta de un reparador baño en las calurosas aguas del Caribe. Regreso al club de playa Marina Caribe con animación y barra libre. Al terminar la aventura, nuestro transporte le llevará de regreso a su hotel. Incluye: Traslados, navegación en catamarán, snack y bebidas descritas.<\/p>","cancelable":false,"precio":54.5000},{"id_Excursion":2,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Aventura en Buggies\/Quads Dobles","descripcion":"","cancelable":false,"precio":59.0000},{"id_Excursion":3,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Isla Saona (desde Playa Bávaro)","descripcion":"<p>Desde la playa de Bayahibe nos embarcaremos en un bellísimo catamarán con todas las medidas de seguridad y acompañados por guías expertos . Durante la trayectoria hacia Isla Saona en el catamarán podrán disfrutar de snacksny bebidas nacionales alcohólicas y agua, refrescos así como probar las exquisitas frutas tropicales. Haremos una parada en la Piscina Natural, considerada la más grande de Rep. Dominicana, es un banco de arenas blancas con una profundidad de no más de un metro, y en la que abundan las Estrellas de mar; donde podrásdarte un refrescante baño. Una vez en la isla, contamos con un área de restaurante, cubierto con hermosas palapas y palmeras, donde almorzaremos un delicioso bufé con bebidas locales incluidas. Después de 3 horas en la playa, nuevamente tomamos el catamarán para continuar disfrutando de la música, el baile, el hermoso paisaje y la diversión de regreso a la playa de Bayahibe para retornar a sus hoteles. Incluye: - Guía profesional - Navegación en Catamarán. - Baño en piscina natural - Almuerzo y bebidas<\/p>","cancelable":false,"precio":77.5000},{"id_Excursion":4,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Supreme VIP Safari","descripcion":"","cancelable":false,"precio":82.0000},{"id_Excursion":5,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Saona Laguna Azul","descripcion":null,"cancelable":false,"precio":102.0000},{"id_Excursion":6,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Dolphin Experience","descripcion":"","cancelable":false,"precio":109.5000},{"id_Excursion":7,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Seaquarium","descripcion":"","cancelable":false,"precio":114.0000},{"id_Excursion":8,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Scape Park Full Day","descripcion":"","cancelable":false,"precio":113.0000},{"id_Excursion":9,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Bavaro Adventure Park","descripcion":"","cancelable":false,"precio":126.5000},{"id_Excursion":10,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Dolphin Action","descripcion":"","cancelable":false,"precio":141.0000},{"id_Excursion":11,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Dolphin Royal","descripcion":null,"cancelable":false,"precio":177.5000},{"id_Excursion":12,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Santo Domingo City Tour (desde Playa Bávaro)","descripcion":null,"cancelable":false,"precio":83.0000},{"id_Excursion":13,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Coco Bongo","descripcion":"","cancelable":false,"precio":92.0000},{"id_Excursion":14,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Fun Park","descripcion":null,"cancelable":false,"precio":90.0000},{"id_Excursion":15,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Isla Catalina","descripcion":"","cancelable":false,"precio":108.5000},{"id_Excursion":16,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Samaná, Cascada y Cayo","descripcion":"","cancelable":false,"precio":154.0000},{"id_Excursion":17,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Coco Bongo Gold Member","descripcion":"","cancelable":false,"precio":155.0000},{"id_Excursion":208,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión MC Catamarán","descripcion":null,"cancelable":false,"precio":60.0000},{"id_Excursion":209,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Saona Paradise Clásica  ","descripcion":null,"cancelable":false,"precio":78.0000},{"id_Excursion":210,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Supreme Safari","descripcion":null,"cancelable":false,"precio":83.0000},{"id_Excursion":211,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Parque Domitai Full Day","descripcion":null,"cancelable":false,"precio":113.0000},{"id_Excursion":212,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión El Dorado Water Park","descripcion":"","cancelable":false,"precio":120.0000},{"id_Excursion":213,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Samaná, Cascada El Limón e Isla Barcardi","descripcion":null,"cancelable":false,"precio":163.5000},{"id_Excursion":214,"id_destino":"Punta cana","id_tipo_servicio":"Excursion","nombre":"Excursión Samaná, Cascada El Limón e Isla Bacardi","descripcion":"","cancelable":false,"precio":157.0000}]';
IF NOT EXISTS (SELECT 1 FROM Eurovip.servicio_excursion)
BEGIN
    SET IDENTITY_INSERT Eurovip.servicio_excursion ON;
    INSERT INTO Eurovip.servicio_excursion (id_Excursion, id_destino, id_tipo_servicio, nombre, descripcion, cancelable, precio)
    SELECT j.id_Excursion,
           ISNULL((SELECT TOP 1 d.id_destino FROM Eurovip.destino d WHERE d.id_destino = j.id_destino AND d.id_idioma = 'ES'), j.id_destino),
           j.id_tipo_servicio,
           LTRIM(RTRIM(REPLACE(REPLACE(j.nombre, '   ', ' '), '  ', ' '))),
           NULLIF(LTRIM(RTRIM(j.descripcion)), ''),
           j.cancelable, j.precio
    FROM OPENJSON(@json) WITH (id_Excursion INT, id_destino VARCHAR(50), id_tipo_servicio VARCHAR(20), nombre VARCHAR(500),
                               descripcion VARCHAR(MAX), cancelable BIT, precio DECIMAL(19,4)) j;
    SET IDENTITY_INSERT Eurovip.servicio_excursion OFF;
END
GO
/* ---------- CAMBIO 47: módulo excursiones ---------- */
IF NOT EXISTS (SELECT 1 FROM Eurovip.UsuarioAcceso WHERE clave = 'excursiones')
    INSERT INTO Eurovip.UsuarioAcceso (nombre_modulo, Categoria, clave, controladores, icono, descripcion, orden)
    VALUES (N'Excursiones', N'Cargas', 'excursiones', 'Excursiones', 'bi-compass', N'Buscar, cargar, editar y eliminar excursiones con su foto.', 35);
GO

/* ##########################################################################################
   FASE 14 — EXCURSIONES EN LA HOME  (16/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: pedido de la usuaria: "que al cargar una excursión se cargue en la HOME; en inicio
   guardamos una excursión por destino y al dar clic salen todas las excursiones de ese destino
   (Punta Cana…), tomándolas de Eurovip.servicio_excursion (sin tabla nueva)".
   Cómo queda (explicado fácil):
     • Eurovip.inicio tiene UNA tarjeta por destino con tipo_servicio = 'EXCURSION'
       ("Excursiones en Punta Cana", precio "desde" = la excursión más barata, foto del destino),
       en la sección nueva de la Home «EXCURSIONES».
     • La pantalla de Excursiones llama a spActualizaInicioExcursion al guardar, duplicar o
       eliminar: crea, actualiza o borra la tarjeta del destino sola. Lo que el usuario ordene en
       "Home de la web" (sección, posición, visible) se respeta.
     • La web: spPortalListarInicio ya trae la tarjeta; al pulsarla llama a
       spPortalListarExcursiones @Destino (todas las de ese destino) y spPortalObtenerExcursion.
   Cómo se aplicó: CAMBIO 48 a 51 y luego la carga inicial (al final de esta fase).
   ########################################################################################## */
/* ---------- CAMBIO 48: sección «EXCURSIONES» de la Home ----------
   Las tarjetas de excursión (una por destino) van en su propia sección de la web. */
IF NOT EXISTS (SELECT 1 FROM Eurovip.contenedores WHERE nombre = 'EXCURSIONES' AND UPPER(id_idioma) = 'ES')
    INSERT INTO Eurovip.contenedores (nombre, posicion, estado, id_idioma) VALUES ('EXCURSIONES', 4, 1, 'es');
GO

/* ---------- CAMBIO 49: nombre del archivo de imagen (igual que ImagenesLocales.NombreCircuito en C#) ----------
   "Excursión Isla Saona (desde Playa Bávaro)" -> "ExcursionIslaSaonadesdePlayaBavaro": sin acentos, solo letras y números. */
CREATE OR ALTER FUNCTION Eurovip.fnNombreImagen (@s nvarchar(500))
RETURNS varchar(500)
AS
BEGIN
    DECLARE @t nvarchar(500) = TRANSLATE(ISNULL(@s, N''), N'áéíóúàèìòùäëïöüâêîôûñçÁÉÍÓÚÀÈÌÒÙÄËÏÖÜÂÊÎÔÛÑÇ',
                                                      N'aeiouaeiouaeiouaeiouncAEIOUAEIOUAEIOUAEIOUNC');
    DECLARE @r varchar(500) = '', @i int = 1, @c nchar(1);
    WHILE @i <= LEN(@t)
    BEGIN
        SET @c = SUBSTRING(@t, @i, 1);
        IF @c COLLATE Latin1_General_BIN LIKE N'[A-Za-z0-9]' SET @r += CAST(@c AS varchar(1));
        SET @i += 1;
    END
    RETURN @r;
END
GO

/* ---------- CAMBIO 50: spActualizaInicioExcursion — pone en la Home la tarjeta de excursiones de un destino ----------
   Como spActualizaInicio hace con OFERTA / CIRCUITO, pero para servicio_excursion:
     • Una sola tarjeta por destino en Eurovip.inicio (tipo_servicio = 'EXCURSION', id_destino = el destino).
     • id_servicio = la excursión más barata del destino (id_Excursion) y precioWeb = su precio ("desde ...").
     • Si el destino ya tenía tarjeta se ACTUALIZA (se conserva lo que el usuario ordenó: sección, posición, visible).
     • Si el destino ya no tiene excursiones, la tarjeta se borra.
   La pantalla de Excursiones lo llama cada vez que se guarda, duplica o elimina una excursión. */
CREATE OR ALTER PROCEDURE Eurovip.spActualizaInicioExcursion
    @destino varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @destino = LTRIM(RTRIM(@destino));
    IF ISNULL(@destino, '') = '' RETURN;

    BEGIN TRAN;
    DECLARE @id int, @precio money, @pais char(2), @imagen varchar(300), @cont int;

    SELECT TOP 1 @id = id_Excursion, @precio = NULLIF(precio, 0)
    FROM Eurovip.servicio_excursion
    WHERE LTRIM(RTRIM(id_destino)) = @destino
    ORDER BY CASE WHEN ISNULL(precio, 0) > 0 THEN 0 ELSE 1 END, precio, id_Excursion;

    IF @id IS NULL
    BEGIN
        DELETE FROM Eurovip.inicio WHERE tipo_servicio = 'EXCURSION' AND id_destino = @destino AND id_idioma = 'ES';
        COMMIT;
        RETURN;
    END

    SELECT TOP 1 @pais = id_pais, @imagen = caratula FROM Eurovip.destino WHERE id_destino = @destino AND id_idioma = 'ES';
    SELECT TOP 1 @cont = id_contenedor FROM Eurovip.contenedores WHERE nombre = 'EXCURSIONES' AND UPPER(id_idioma) = 'ES';

    IF EXISTS (SELECT 1 FROM Eurovip.inicio WHERE tipo_servicio = 'EXCURSION' AND id_destino = @destino AND id_idioma = 'ES')
        UPDATE Eurovip.inicio
           SET id_servicio = @id, precioWeb = @precio, id_moneda = 'DOP', id_paisDestino = ISNULL(@pais, id_paisDestino),
               caratulaDestino = ISNULL(@imagen, caratulaDestino), descripcion = 'Excursiones en ' + @destino
         WHERE tipo_servicio = 'EXCURSION' AND id_destino = @destino AND id_idioma = 'ES';
    ELSE
        INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino,
                                    id_destino, caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor,
                                    posicion_contenedor, posicion_destino, estado, notas, noches)
        SELECT @id, 'ES', 'EXCURSION', 99, 'DOP', 'ES', 'Madrid', ISNULL(@pais, 'DO'),
               @destino, @imagen, 'Excursiones en ' + @destino, NULL, NULL, @precio, @cont, CASE WHEN @cont IS NULL THEN NULL ELSE 'EXCURSIONES' END,
               (SELECT posicion FROM Eurovip.contenedores WHERE id_contenedor = @cont),
               (SELECT ISNULL(MAX(posicion_destino), 0) + 1 FROM Eurovip.inicio WHERE id_contenedor = @cont), 1, NULL, NULL;
    COMMIT;
END
GO

/* ---------- CAMBIO 51: consultas para la WEB ----------
   La Home lee las tarjetas con spPortalListarInicio (sale la de EXCURSION de cada destino, igual que OFERTA / CIRCUITO / HOTEL).
   Al pulsar la tarjeta de excursiones (tipo_servicio = 'EXCURSION') la web llama a:
     • spPortalListarExcursiones @Destino = la columna "destino" de la tarjeta  -> todas las excursiones de ese destino
     • spPortalObtenerExcursion @IdExcursion                                    -> el detalle de una
   imagen = /imagenes/excursiones/{nombre sin espacios ni acentos}.webp (lo que sube la pantalla de Excursiones). */
CREATE OR ALTER PROCEDURE Eurovip.spPortalListarExcursiones
    @Destino varchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.id_Excursion AS id_excursion, LTRIM(RTRIM(e.id_destino)) AS destino, d.id_pais, e.nombre, e.descripcion,
           e.cancelable, CAST(e.precio AS decimal(19,4)) AS precio, 'DOP' AS id_moneda,
           '/imagenes/excursiones/' + Eurovip.fnNombreImagen(e.nombre) + '.webp' AS imagen
      FROM Eurovip.servicio_excursion e
      OUTER APPLY (SELECT TOP 1 id_pais FROM Eurovip.destino x WHERE x.id_destino = LTRIM(RTRIM(e.id_destino)) AND x.id_idioma = 'ES') d
     WHERE @Destino IS NULL OR LTRIM(RTRIM(e.id_destino)) = LTRIM(RTRIM(@Destino))
     ORDER BY destino, CASE WHEN ISNULL(e.precio, 0) > 0 THEN 0 ELSE 1 END, e.precio, e.nombre;
END
GO

CREATE OR ALTER PROCEDURE Eurovip.spPortalObtenerExcursion
    @IdExcursion int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.id_Excursion AS id_excursion, LTRIM(RTRIM(e.id_destino)) AS destino, e.nombre, e.descripcion, e.cancelable,
           CAST(e.precio AS decimal(19,4)) AS precio, 'DOP' AS id_moneda,
           '/imagenes/excursiones/' + Eurovip.fnNombreImagen(e.nombre) + '.webp' AS imagen
      FROM Eurovip.servicio_excursion e
     WHERE e.id_Excursion = @IdExcursion;
END
GO

/* Carga inicial (16/10/2026): una tarjeta EXCURSION por cada destino que ya tiene excursiones.
   Resultado: tarjeta id 43 "Excursiones en Punta Cana", desde 54,50, sección EXCURSIONES (id 54). */
DECLARE @d varchar(50);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT LTRIM(RTRIM(id_destino)) FROM Eurovip.servicio_excursion WHERE ISNULL(id_destino, '') <> '';
OPEN c; FETCH NEXT FROM c INTO @d;
WHILE @@FETCH_STATUS = 0 BEGIN EXEC Eurovip.spActualizaInicioExcursion @d; FETCH NEXT FROM c INTO @d; END
CLOSE c; DEALLOCATE c;
GO


/* ##########################################################################################
   FASE 15 — TRASLADOS  (16/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: pedido de la usuaria: "debo hacer un mantenimiento para que el usuario cargue Traslados
   para la web, con sus imágenes y datos, en base a la búsqueda de traslados del tour operador
   (Hotelbeds): ver, crear, editar, cargar imagen, eliminar, desactivar".
   No había nada de traslados ni en Eurovip ni en ViajeVDT: se crea desde cero.
   Cómo funciona (explicado fácil):
     • El cliente busca en la web: Desde (aeropuerto, ej. PUJ) · A (su hotel) · ida y vuelta o solo
       ida · viajeros. La web le muestra las opciones activas de ese aeropuerto al destino del hotel.
     • Cada fila de Eurovip.traslado es UNA opción: Compartido o Privado, categoría (Estándar,
       Premium, Lujo), vehículo, maletas, duración, pasajeros mínimo/máximo, precio y foto.
     • Se asocia al DESTINO (la zona de los hoteles) y al aeropuerto de salida; por defecto vale para
       todos los hoteles del destino y, si no, solo para los hoteles de Eurovip.traslado_alojamiento.
       No se asocia a ofertas: el cliente lo añade a su reserva del hotel.
     • Pantalla: /Traslados/Index (módulo «traslados»). La web: Eurovip.spPortalBuscarTraslados.
   Cómo se aplicó: CAMBIO 52 a 55, tal cual aquí. Probado con 4 traslados de ejemplo dentro de una
   transacción con ROLLBACK (no quedó ninguno).
   ########################################################################################## */
/* ---------- CAMBIO 52: Eurovip.traslado — un traslado que se vende en la web ----------
   Igual que en la búsqueda de traslados del tour operador (Hotelbeds): "Desde" un aeropuerto, "A" un destino
   (la zona de los hoteles) o solo a algunos hoteles, ida y vuelta o solo ida, y por cada opción:
   tipo (Compartido / Privado), categoría (Estándar / Premium / Lujo), vehículo, maletas, duración,
   pasajeros mínimo y máximo, precio y foto del vehículo.
     • cobro = 'VEHICULO'  -> el precio es para todos los viajeros ("Precio ida y vuelta, para todos los viajeros")
       cobro = 'PERSONA'   -> el precio se multiplica por la cantidad de pasajeros
     • precio_ida / precio_ida_vuelta: al menos uno. Moneda DOP como todo Eurovip.
     • todos_hoteles = 1 -> sirve para cualquier hotel del destino; 0 -> solo los de Eurovip.traslado_alojamiento.
     • imagen = nombre del archivo en wwwroot/imagenes/traslados (lo pone la pantalla al subir la foto).
     • activo = 0 -> no sale en la web (desactivado), pero no se borra. */
IF OBJECT_ID('Eurovip.traslado') IS NULL
CREATE TABLE Eurovip.traslado (
    id_traslado         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Eurovip_traslado PRIMARY KEY,
    nombre              varchar(150) NOT NULL,
    tipo                varchar(20)  NOT NULL CONSTRAINT CK_Eurovip_traslado_tipo CHECK (tipo IN ('COMPARTIDO', 'PRIVADO')),
    categoria           varchar(20)  NOT NULL CONSTRAINT DF_Eurovip_traslado_categoria DEFAULT ('ESTANDAR')
                                     CONSTRAINT CK_Eurovip_traslado_categoria CHECK (categoria IN ('ESTANDAR', 'PREMIUM', 'LUJO')),
    vehiculo            varchar(20)  NOT NULL CONSTRAINT CK_Eurovip_traslado_vehiculo CHECK (vehiculo IN ('BUS', 'MINIBUS', 'VAN', 'COCHE', 'LIMUSINA')),
    id_estacion_origen  varchar(10)  NOT NULL,
    id_destino          varchar(50)  NOT NULL,
    id_pais             char(2)      NULL,
    maletas             smallint     NOT NULL CONSTRAINT DF_Eurovip_traslado_maletas DEFAULT (1),
    duracion_min        smallint     NULL,
    pasajeros_min       smallint     NOT NULL CONSTRAINT DF_Eurovip_traslado_pmin DEFAULT (1),
    pasajeros_max       smallint     NOT NULL,
    cobro               varchar(10)  NOT NULL CONSTRAINT DF_Eurovip_traslado_cobro DEFAULT ('VEHICULO')
                                     CONSTRAINT CK_Eurovip_traslado_cobro CHECK (cobro IN ('VEHICULO', 'PERSONA')),
    precio_ida          money        NULL,
    precio_ida_vuelta   money        NULL,
    id_moneda           char(3)      NOT NULL CONSTRAINT DF_Eurovip_traslado_moneda DEFAULT ('DOP'),
    cancelable          bit          NOT NULL CONSTRAINT DF_Eurovip_traslado_cancelable DEFAULT (1),
    horas_cancelacion   smallint     NULL,
    descripcion         varchar(max) NULL,
    imagen              varchar(200) NULL,
    todos_hoteles       bit          NOT NULL CONSTRAINT DF_Eurovip_traslado_todos DEFAULT (1),
    activo              bit          NOT NULL CONSTRAINT DF_Eurovip_traslado_activo DEFAULT (1),
    usuario             varchar(50)  NULL,
    fecha_alta          datetime2(0) NOT NULL CONSTRAINT DF_Eurovip_traslado_alta DEFAULT (SYSDATETIME()),
    fecha_modificacion  datetime2(0) NULL,
    CONSTRAINT CK_Eurovip_traslado_precio CHECK (precio_ida IS NOT NULL OR precio_ida_vuelta IS NOT NULL),
    CONSTRAINT CK_Eurovip_traslado_pasajeros CHECK (pasajeros_min >= 1 AND pasajeros_max >= pasajeros_min)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Eurovip_traslado_ruta' AND object_id = OBJECT_ID('Eurovip.traslado'))
    CREATE INDEX IX_Eurovip_traslado_ruta ON Eurovip.traslado (id_estacion_origen, id_destino, activo);
GO

/* ---------- CAMBIO 53: Eurovip.traslado_alojamiento — hoteles a los que llega un traslado ----------
   Solo se usa cuando traslado.todos_hoteles = 0. Al borrar el traslado se borran sus filas. */
IF OBJECT_ID('Eurovip.traslado_alojamiento') IS NULL
CREATE TABLE Eurovip.traslado_alojamiento (
    id_traslado     int NOT NULL CONSTRAINT FK_Eurovip_traslado_alojamiento_traslado REFERENCES Eurovip.traslado (id_traslado) ON DELETE CASCADE,
    id_alojamiento  int NOT NULL,
    CONSTRAINT PK_Eurovip_traslado_alojamiento PRIMARY KEY (id_traslado, id_alojamiento)
);
GO

/* ---------- CAMBIO 54: módulo «traslados» (Usuarios y accesos) ---------- */
IF NOT EXISTS (SELECT 1 FROM Eurovip.UsuarioAcceso WHERE clave = 'traslados')
    INSERT INTO Eurovip.UsuarioAcceso (nombre_modulo, Categoria, clave, controladores, icono, descripcion, orden)
    VALUES (N'Traslados', N'Cargas', 'traslados', 'Traslados', 'bi-bus-front', N'Ver, cargar, editar, desactivar y eliminar traslados con su foto.', 36);
GO

/* ---------- CAMBIO 55: consulta para la WEB — buscar traslados ----------
   Como el buscador del tour operador: Desde (aeropuerto) · A (hotel o destino) · Viajeros · Ida y vuelta / Sólo ida.
     @Origen       = código del aeropuerto (ej. 'PUJ')
     @IdAlojamiento= hotel elegido (opcional; si viene, el destino es el del hotel)
     @Destino      = destino (si no se eligió hotel)
     @Pasajeros    = adultos + niños
     @IdaVuelta    = 1 ida y vuelta, 0 solo ida
   Devuelve solo los activos con precio para esa opción y cupo para esos pasajeros, del más barato al más caro.
   precio_total = precio (si se cobra por vehículo) o precio × pasajeros (si se cobra por persona). */
CREATE OR ALTER PROCEDURE Eurovip.spPortalBuscarTraslados
    @Origen        varchar(10),
    @IdAlojamiento int = NULL,
    @Destino       varchar(50) = NULL,
    @Pasajeros     int = 1,
    @IdaVuelta     bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    IF @IdAlojamiento IS NOT NULL
        SELECT TOP 1 @Destino = LTRIM(RTRIM(id_destino)) FROM Eurovip.alojamiento WHERE id_alojamiento = @IdAlojamiento;
    SET @Pasajeros = CASE WHEN ISNULL(@Pasajeros, 0) < 1 THEN 1 ELSE @Pasajeros END;

    SELECT t.id_traslado, t.nombre, t.tipo, t.categoria, t.vehiculo, t.id_estacion_origen AS origen,
           (SELECT TOP 1 e.nombre FROM Eurovip.estacion e WHERE e.id_estacion = t.id_estacion_origen) AS origen_nombre,
           t.id_destino AS destino, t.maletas, t.duracion_min, t.pasajeros_min, t.pasajeros_max, t.cobro,
           CAST(CASE WHEN @IdaVuelta = 1 THEN t.precio_ida_vuelta ELSE t.precio_ida END AS decimal(19,4)) AS precio,
           CAST(CASE WHEN @IdaVuelta = 1 THEN t.precio_ida_vuelta ELSE t.precio_ida END
                * CASE WHEN t.cobro = 'PERSONA' THEN @Pasajeros ELSE 1 END AS decimal(19,4)) AS precio_total,
           t.id_moneda, t.cancelable, t.horas_cancelacion, t.descripcion,
           CASE WHEN t.imagen IS NULL THEN NULL ELSE '/imagenes/traslados/' + t.imagen END AS imagen
      FROM Eurovip.traslado t
     WHERE t.activo = 1
       AND t.id_estacion_origen = @Origen
       AND (@Destino IS NULL OR t.id_destino = @Destino)
       AND (CASE WHEN @IdaVuelta = 1 THEN t.precio_ida_vuelta ELSE t.precio_ida END) IS NOT NULL
       AND @Pasajeros BETWEEN t.pasajeros_min AND t.pasajeros_max
       AND (t.todos_hoteles = 1 OR @IdAlojamiento IS NULL
            OR EXISTS (SELECT 1 FROM Eurovip.traslado_alojamiento a WHERE a.id_traslado = t.id_traslado AND a.id_alojamiento = @IdAlojamiento))
     ORDER BY precio_total, t.tipo, t.categoria;
END
GO

/* ##########################################################################################
   FASE 16 — TRASLADOS EN LA HOME  (20/10/2026)
   ------------------------------------------------------------------------------------------
   Para qué: pedido de la usuaria: "agregar los traslados a la tabla inicio organizados por destino:
   en inicio una sola vez Punta Cana y al entrar salen todos los de Punta Cana; cuando el usuario
   cargue un traslado, que se cargue en inicio si es un destino nuevo y, si no, que quede en la
   tabla intermedia".
   Cómo queda (igual que las excursiones, FASE 14):
     • Eurovip.inicio: UNA tarjeta por destino con tipo_servicio = 'TRASLADO' ("Traslados en Punta
       Cana", "desde" = el traslado activo más barato, foto del destino), en la sección «TRASLADOS».
     • La "tabla intermedia" es Eurovip.traslado: ahí están todos los traslados de cada destino.
     • La pantalla de Traslados llama a spActualizaInicioTraslado al guardar, duplicar, activar/
       desactivar y eliminar: crea la tarjeta si el destino es nuevo, la actualiza si ya estaba
       (respetando sección/posición/visible) y la borra si el destino se queda sin traslados activos.
     • Web: al pulsar la tarjeta → spPortalListarTraslados @Destino (todos los activos de ese destino).
   Cómo se aplicó: CAMBIO 56 a 58 y la carga inicial (al final de esta fase).
   ########################################################################################## */
/* ---------- CAMBIO 56: sección «TRASLADOS» de la Home ---------- */
IF NOT EXISTS (SELECT 1 FROM Eurovip.contenedores WHERE nombre = 'TRASLADOS' AND UPPER(id_idioma) = 'ES')
    INSERT INTO Eurovip.contenedores (nombre, posicion, estado, id_idioma) VALUES ('TRASLADOS', 5, 1, 'es');
GO

/* ---------- CAMBIO 57: spActualizaInicioTraslado — tarjeta de traslados de un destino en la Home ----------
   Igual que spActualizaInicioExcursion, pero con Eurovip.traslado (que hace de tabla intermedia):
     • Una sola tarjeta por destino en Eurovip.inicio (tipo_servicio = 'TRASLADO', id_destino = el destino).
     • Solo cuentan los traslados ACTIVOS. id_servicio = el más barato (id_traslado) y precioWeb = su precio
       más bajo (ida y vuelta o solo ida), para el "desde …".
     • Si el destino ya tenía tarjeta se ACTUALIZA (se respeta sección, posición y visible del editor de la Home).
     • Si el destino se queda sin traslados activos, la tarjeta se borra.
   La pantalla de Traslados lo llama al guardar, duplicar, activar/desactivar y eliminar. */
CREATE OR ALTER PROCEDURE Eurovip.spActualizaInicioTraslado
    @destino varchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @destino = LTRIM(RTRIM(@destino));
    IF ISNULL(@destino, '') = '' RETURN;

    BEGIN TRAN;
    DECLARE @id int, @precio money, @pais char(2), @imagen varchar(300), @cont int;

    SELECT TOP 1 @id = id_traslado,
                 @precio = CASE WHEN precio_ida_vuelta IS NULL THEN precio_ida
                                WHEN precio_ida IS NULL OR precio_ida_vuelta <= precio_ida THEN precio_ida_vuelta ELSE precio_ida END
    FROM Eurovip.traslado
    WHERE activo = 1 AND id_destino = @destino
    ORDER BY CASE WHEN precio_ida_vuelta IS NULL THEN precio_ida
                  WHEN precio_ida IS NULL OR precio_ida_vuelta <= precio_ida THEN precio_ida_vuelta ELSE precio_ida END, id_traslado;

    IF @id IS NULL
    BEGIN
        DELETE FROM Eurovip.inicio WHERE tipo_servicio = 'TRASLADO' AND id_destino = @destino AND id_idioma = 'ES';
        COMMIT;
        RETURN;
    END

    SELECT TOP 1 @pais = id_pais, @imagen = caratula FROM Eurovip.destino WHERE id_destino = @destino AND id_idioma = 'ES';
    SELECT TOP 1 @cont = id_contenedor FROM Eurovip.contenedores WHERE nombre = 'TRASLADOS' AND UPPER(id_idioma) = 'ES';

    IF EXISTS (SELECT 1 FROM Eurovip.inicio WHERE tipo_servicio = 'TRASLADO' AND id_destino = @destino AND id_idioma = 'ES')
        UPDATE Eurovip.inicio
           SET id_servicio = @id, precioWeb = @precio, id_moneda = 'DOP', id_paisDestino = ISNULL(@pais, id_paisDestino),
               caratulaDestino = ISNULL(@imagen, caratulaDestino), descripcion = 'Traslados en ' + @destino
         WHERE tipo_servicio = 'TRASLADO' AND id_destino = @destino AND id_idioma = 'ES';
    ELSE
        INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino,
                                    id_destino, caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor,
                                    posicion_contenedor, posicion_destino, estado, notas, noches)
        SELECT @id, 'ES', 'TRASLADO', 99, 'DOP', 'ES', 'Madrid', ISNULL(@pais, 'DO'),
               @destino, @imagen, 'Traslados en ' + @destino, NULL, NULL, @precio, @cont, CASE WHEN @cont IS NULL THEN NULL ELSE 'TRASLADOS' END,
               (SELECT posicion FROM Eurovip.contenedores WHERE id_contenedor = @cont),
               (SELECT ISNULL(MAX(posicion_destino), 0) + 1 FROM Eurovip.inicio WHERE id_contenedor = @cont), 1, NULL, NULL;
    COMMIT;
END
GO

/* ---------- CAMBIO 58: consulta para la WEB — todos los traslados de un destino ----------
   Al pulsar la tarjeta TRASLADO de la Home (columna "destino" de spPortalListarInicio) la web llama a
   spPortalListarTraslados @Destino: todos los traslados activos de ese destino, agrupables por aeropuerto,
   del más barato al más caro. Para buscar con viajeros e ida y vuelta sigue estando spPortalBuscarTraslados. */
CREATE OR ALTER PROCEDURE Eurovip.spPortalListarTraslados
    @Destino varchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.id_traslado, t.nombre, t.tipo, t.categoria, t.vehiculo, t.id_estacion_origen AS origen,
           (SELECT TOP 1 e.nombre FROM Eurovip.estacion e WHERE e.id_estacion = t.id_estacion_origen) AS origen_nombre,
           t.id_destino AS destino, t.maletas, t.duracion_min, t.pasajeros_min, t.pasajeros_max, t.cobro,
           CAST(t.precio_ida AS decimal(19,4)) AS precio_ida, CAST(t.precio_ida_vuelta AS decimal(19,4)) AS precio_ida_vuelta,
           t.id_moneda, t.cancelable, t.horas_cancelacion, t.descripcion, t.todos_hoteles,
           CASE WHEN t.imagen IS NULL THEN NULL ELSE '/imagenes/traslados/' + t.imagen END AS imagen
      FROM Eurovip.traslado t
     WHERE t.activo = 1 AND (@Destino IS NULL OR t.id_destino = LTRIM(RTRIM(@Destino)))
     ORDER BY t.id_destino, t.id_estacion_origen,
              CASE WHEN t.precio_ida_vuelta IS NULL THEN t.precio_ida ELSE t.precio_ida_vuelta END, t.tipo, t.categoria;
END
GO

/* Carga inicial (20/10/2026): una tarjeta TRASLADO por cada destino que ya tiene traslados.
   Resultado: tarjeta id 92 "Traslados en Punta Cana", desde 20,00, sección TRASLADOS (id 55). */
DECLARE @d varchar(50);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT id_destino FROM Eurovip.traslado;
OPEN c; FETCH NEXT FROM c INTO @d;
WHILE @@FETCH_STATUS = 0 BEGIN EXEC Eurovip.spActualizaInicioTraslado @d; FETCH NEXT FROM c INTO @d; END
CLOSE c; DEALLOCATE c;
GO

/* ##########################################################################################
   BITÁCORA — cambios de datos puntuales
   (Se anotan aquí para que quede registro. No se guardan contraseñas ni datos cifrados.)
   ##########################################################################################

   12/10/2026  Se creó desde la pantalla "Crear usuario" la cuenta "Nachely" (#1) como
               Superadministrador. Luego se reemplazó por ARodriguez (ver CAMBIO 6).

   12/10/2026  Se asignó una contraseña TEMPORAL al usuario ARodriguez, solo en
               Eurovip.usuario, porque no se recordaba la original.
               La contraseña de Tropitours (dbo.usuario) NO se tocó.
               Pendiente: cambiarla desde la pantalla de perfil cuando exista.

   12/10/2026  Prueba de la pantalla "Carga de Hoteles": se creó el hotel de prueba
               "Hotel Prueba Bávaro" (#1), se ocultó, se editó, se volvió a mostrar y se
               ELIMINÓ. Eurovip.inicio quedó vacía. En Eurovip.auditoria_consulta_oferta
               quedan las 6 filas de esa prueba (registros 1 a 6) como evidencia.
               Nota: el próximo hotel que se cargue tendrá el número #1 otra vez.

   12/10/2026  A pedido de la usuaria se cambió la contraseña temporal de ARodriguez
               (solo Eurovip.usuario) por una corta y fácil de recordar.
               ⚠ Antes de publicar el sistema se debe cambiar por una segura.

   12/10/2026  REGLA: los hoteles de Eurovip se cargan SOLO en idioma ES (español).
               No cambia la base de datos: lo garantiza el BackOffice (siempre guarda
               id_idioma = 'ES' y solo lista, edita y elimina hoteles ES). Las tablas
               idioma/campanya/contenedores conservan los demás idiomas por si se
               necesitan en el futuro.
               Prueba: se creó un hotel enviando idioma 'FR' → se guardó 'ES' → se eliminó.
               Eurovip.inicio quedó vacía (auditoría registros 7 y 8).

   12/10/2026  FASE 3 — se copiaron por copia masiva desde ViajeVDT (solo lectura, solo ES):
               destino 7.320 · alojamiento 9.563 · estacion 3.715 · proveedor 1.335 filas.

   12/10/2026  Prueba de Circuitos: se creó el circuito "Prueba Eurovip Caribe" y se cargaron
               salidas con la Carga Express (4, sin vuelo), la Carga Clásica (1 con vuelo) y el
               Excel (2 con vuelo); luego "Prueba Express Nuevo" (circuito nuevo + 2 salidas).
               Todo se ELIMINÓ desde la pantalla. Las tablas de circuitos y servicio quedaron
               vacías. Quedan como evidencia: Eurovip.Log_Cargas (4 registros) y 7 filas de
               auditoría "Circuito_*". Nota: los números de servicio siguen desde el #10
               (es un contador automático, no vuelve a empezar).

   13/10/2026  Pantallas "Cargas" y "Reportes" (no crean tablas: leen Log_Cargas, servicio,
               servicio_circuito, inicio y auditoría). Para probar los gráficos se crearon y luego
               se ELIMINARON los circuitos "Reporte Prueba Uno" (14 salidas) y "Reporte Prueba Dos"
               (6 salidas). Quedan sus registros en Log_Cargas (#6 y #7) como evidencia.
               No se tocó el circuito "Prueba" cargado por la usuaria.

   13/10/2026  FASE 4 — copia masiva desde ViajeVDT (solo lectura, solo ES): tipo_habitacion 15.877 ·
               alojamiento_habitacion 129.943 filas. fnPrecioWeb ahora suma también la habitación
               más barata y el detalle (en circuitos siguen siendo 0, no cambia su precio).

   13/10/2026  Pantalla "Carga manual" (ofertas de hotel: lista + Carga Express + Carga Clásica con
               Excel). No crea tablas nuevas: usa las de la FASE 4 (servicio_detalle,
               servicio_habitacion, servicio_origen, alojamiento_destino).
               Prueba: Express con 2 hoteles (Riu Palace Bavaro y Barcelo Bavaro Palace) = 9 ofertas;
               Clásica desde la plantilla Excel = 10 ofertas. Se probaron editar, activar/desactivar,
               ajuste de precio en lote, exportar y el aviso de duplicados. Las 19 ofertas
               (#44 a #62) se ELIMINARON desde la pantalla: servicio y sus tablas hijas quedaron
               vacías y la vitrina alojamiento_destino volvió a 0. Quedan como evidencia
               Log_Cargas #8 y #9 y las filas de auditoría "Oferta_*".

   13/10/2026  Clásica de ofertas y de circuitos rehechas como BackTiTan actual (solo INICIO,
               VUELOS y CARGAR EXCEL). Prueba de la Clásica de ofertas: 5 ofertas (#63 a #67)
               cargadas desde la plantilla y ELIMINADAS después. Queda Log_Cargas #10.

   13/10/2026  FASE 5 — copia masiva desde ViajeVDT (solo lectura, solo ES): carrusel 53 ·
               carrucelDestino 36 · home_contenido 176 filas (se conservaron los números de
               carrusel para que los precios de cada slide sigan enlazados).

   13/10/2026  Pantalla "Herramientas" (11 catálogos). Prueba: se crearon y luego se ELIMINARON
               desde la pantalla: destino "Prueba Eurovip", hotel #10450 con 2 habitaciones
               (#368833 y #368834), campaña #8223, estación ZZQ y moneda USD. Las tablas quedaron
               como estaban (tipo_habitacion sigue en 15.877). Quedan como evidencia las filas de
               auditoría "Herramienta_*".

   13/10/2026  "Mi perfil" y campana de notificaciones. Prueba con el usuario ARodriguez: se subió
               una foto de prueba, se cambió el teléfono y se intentó mandar correo/usuario (el
               servidor los ignoró). Después se quitó la foto (el archivo se borró del disco) y se
               dejaron los datos como estaban: teléfono 829000000 e imagen 'ARodriguez.jpg'
               (nombre de la foto antigua de ViajeVDT, restaurado con UPDATE en Eurovip.usuario).
               "Marcar todo como leído" dejó guardada notif_leidas_hasta para ARodriguez.

   13/10/2026  Pantalla "Home de la web" (Inicio2 + Carrusel). Cambios de datos al probar:
               • Carrusel: los 5 slides activos tenían posiciones repetidas (dos con orden 1, así
                 venían de ViajeVDT). Se renumeraron 1-5 desde la pantalla, respetando el orden.
               • Se creó y luego se quitó una tarjeta de prueba (inicio id 2). Quedó su "memoria"
                 de posición en home_contenido #3051 (Punta Cana · Madrid · campaña 3170 →
                 sección ISLAS, posición 1): si un día se carga esa combinación, aparecerá en ISLAS.
                 PENDIENTE: borrarla (DELETE FROM Eurovip.home_contenido WHERE id_home_contenido = 3051).
               • "Traer ofertas a la Home" puso el circuito "Prueba" de la usuaria (inicio id 3,
                 770 €, sin sección). Se dejó porque quitarlo requiere su confirmación.

   13/10/2026  Pantalla "Consultar ofertas". spDuplicaServicio y spEditaServicio se probaron dentro
               de una transacción deshecha (ROLLBACK) sobre el servicio #10 del circuito "Prueba":
               no quedó nada guardado (servicio sigue con 14 filas). Solo se "gastaron" los números
               automáticos 68 y 69 (el próximo servicio nuevo será #70 o mayor; es normal).

   13/10/2026  Pantalla "Flyers". Al probar "Generar flyers" se recalcularon los precios: las 25
               filas de flyer_datos copiadas de ViajeVDT (precios de las ofertas de ViajeVDT, no de
               Eurovip) se borraron porque Eurovip todavía no tiene ofertas activas a futuro. Es el
               comportamiento correcto: se llenará solo cuando haya ofertas cargadas.
               • Se corrigió que dos configuraciones del mismo destino (campañas 199 y 99) se pisaban:
                 ahora se juntan y quedan los 5 hoteles más baratos.
               • Se insertaron 3 filas de prueba ('PRUEBA ...') en flyer_datos y se borraron
                 enseguida (flyer_datos quedó con 0 filas).
               • NO se envió ningún WhatsApp.
               OJO: las configuraciones 1-5 (campaña 199, idioma 'EX') vienen de ViajeVDT y la
               campaña 199 no existe en Eurovip: revisarlas o borrarlas desde la pantalla.

   13/10/2026  FASE 10 aplicada (CAMBIO 42 y 43): 11 módulos de Eurovip y 3 roles.
               • La primera vez sqlcmd leyó el archivo sin UTF-8 y los acentos de los módulos quedaron
                 mal ("ClÃ¡sica"). Se volvió a correr el MERGE con UTF-8 (-f 65001) y quedaron bien.
               • Pantalla "Usuarios y accesos": se creó el usuario de prueba zzPruebaAcc (id 3,
                 correo zz.prueba.acc@eurovip.test), se le dieron y quitaron módulos, se le cambió la
                 contraseña, se desactivó/activó, se entró con él para comprobar que no ve lo que no
                 tiene, y se BORRÓ desde la pantalla. Quedan solo sus líneas en la auditoría
                 (tipo 'Usuario_*'). El próximo usuario nuevo tendrá número 4 o mayor.
               • ARodriguez (Superadministrador) no se tocó.

   13/10/2026  Campañas: a pedido de la usuaria se dejan solo la 99 "General-DT" y la 17 (la usa
               su circuito "Prueba": 14 salidas; decidió conservarla). Se borraron las otras 93
               campañas (118 filas contando idiomas), copiadas de ViajeVDT. Antes se comprobó que
               ninguna salida, alojamiento_circuito, inicio ni flyer las usaba.
                   DELETE FROM Eurovip.campanya WHERE id_campanya NOT IN (99, 17);
               Si hiciera falta recuperar alguna, se puede volver a copiar de ViajeVDT.dbo.campanya.
               PENDIENTE (a confirmar con la usuaria): home_contenido (64 filas) y carrucelDestino
               (27 filas) copiados de ViajeVDT siguen apuntando a campañas ya borradas.

   13/10/2026  Habitaciones repetidas: a pedido de la usuaria, en cada hotel queda UNA sola
               habitación por nombre (solo servidor 93, esquema Eurovip; ViajeVDT NO se tocó).
               • Repetida = mismo id_alojamiento + idioma + nombre (sin espacios de más, sin
                 distinguir mayúsculas). Se conserva la que tiene codigo_newblue y, si empatan, la
                 de id_habitacion más bajo.
               • Se borraron 46.834 filas de 1.724 hoteles; quedan 83.109 y 0 repetidas.
                 Ninguna salida (servicio_habitacion) usaba las borradas.
               • COPIA DE SEGURIDAD de lo borrado: Eurovip.alojamiento_habitacion_borradas_20261013.
                 Para devolver una: INSERT INTO Eurovip.alojamiento_habitacion SELECT * FROM
                 Eurovip.alojamiento_habitacion_borradas_20261013 WHERE id_alojamiento = … ;
                 Cuando se confirme que todo está bien, se puede borrar la copia (DROP TABLE).
               Script usado:
                   ;WITH r AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY id_alojamiento, id_idioma, UPPER(LTRIM(RTRIM(nombre)))
                               ORDER BY CASE WHEN ISNULL(codigo_newblue,'') <> '' THEN 0 ELSE 1 END, id_habitacion) rn
                               FROM Eurovip.alojamiento_habitacion)
                   SELECT <columnas> INTO Eurovip.alojamiento_habitacion_borradas_20261013 FROM r WHERE rn > 1;
                   DELETE h FROM Eurovip.alojamiento_habitacion h JOIN Eurovip.alojamiento_habitacion_borradas_20261013 b
                     ON b.id_alojamiento = h.id_alojamiento AND b.id_habitacion = h.id_habitacion AND b.id_idioma = h.id_idioma;

   13/10/2026  Carga de Hoteles (/Hoteles/Index) corregida para guardar como ViajeVDT.dbo.inicio
               (tipo_servicio = Hotel): id_destino = nombre del hotel, caratulaDestino = nombre del
               destino (antes la pantalla pedía una URL de imagen) y descripcion = "Régimen -Código".
               Los 2 hoteles ya cargados en Eurovip (inicio 1 y 12) no se tocaron: no tienen código
               y en el nombre tienen el destino; hay que corregirlos desde la pantalla.

   14/10/2026  FASE 11 aplicada (CAMBIO 44): servicio_destino creada y llenada con las 122 ofertas
               existentes (todas Punta Cana / DO, una sola por sus hoteles). spEliminaServicio y
               spDuplicaServicio actualizados. Prueba dentro de una transacción deshecha (ROLLBACK):
               se duplicó una oferta (#192, salió con destino Punta Cana/DO) y se eliminó con
               spEliminaServicio sin error; no quedó nada guardado (servicio_destino sigue con 122).
               Solo se "gastó" el número automático 192.

   14/10/2026  LIMPIEZA DE DATOS (a pedido de la usuaria): destinos, países y hoteles mal creados.
               Solo servidor 93, esquema Eurovip; ViajeVDT (10.10.10.29) NO se tocó. Casi todo
               venía así copiado de ViajeVDT. Script completo al final de este archivo
               ("LIMPIEZA 14/10/2026"); se ensayó antes con ROLLBACK y luego se aplicó.
               • Hoteles basura: 5 borrados ("222", "2222", "333", "3333", "6666", en Madrid).
               • Países repetidos: IM (Islas Mauricio) -> MU (Mauricio) y E1 (Arabia Saudi) -> SA
                 en todas las tablas; IM y E1 borrados de pais (quedan 140 países).
               • País que no concordaba: RIYADH y JEDDAH estaban en Argentina -> Arabia Saudí;
                 un hotel de Atenas (Grecia) apuntaba a "Athens" (EE.UU.) -> "Atenas";
                 11 hoteles con el país mal (Cartagena de Indias en Antigua y Barbuda, Cancún en
                 Colombia, Isla de Sal en Baleares…) toman el país de su destino.
               • Destino inexistente: "Havana" -> "La Habana"; "Mauricio" -> "Isla Mauricio";
                 4 hoteles de Barbados sin destino ni país -> "Barbados" (BD).
               • Espacios de más: 10 destinos (" Dubrovnik", "Bergen "…) y nombres de hotel;
                 " Dubrovnik" se borró porque ya existía "Dubrovnik".
                 130 hoteles con el destino en otras mayúsculas ("RIVIERA MAYA") -> igual que en
                 la tabla de destinos ("Riviera Maya").
               • Destinos basura borrados (13, ninguno con hoteles): "-pego", "65401", "Al", "t",
                 "falta" (x4), "Por cuenta del cliente" (x2), "Lima 41", "Orleans Cedex 1" y
                 "TROU D EAU DOUCE" de España (queda el de Mauricio). Corregidos:
                 "Miraflores 18.lima" -> "Miraflores", "Mazara del Vall0" -> "Mazara del Vallo",
                 "Ringko99bing" -> "Ringkobing".
               • HOTELES REPETIDOS: 162 hoteles estaban copiados ~107 veces. Queda 1 por hotel
                 (el activo; si no, el de número más bajo). Se borraron 7.446 copias y sus
                 35.818 habitaciones; antes, 540 habitaciones que solo tenía alguna copia se
                 pasaron al hotel que queda. El circuito "Prueba" apuntaba a una copia: ahora
                 apunta al hotel que queda. Ninguna oferta usaba copias.
               • Resultado: 2.109 hoteles, 47.831 habitaciones, 7.306 destinos, 140 países;
                 0 repetidos, 0 hoteles sin destino válido, 0 países inexistentes.
               • COPIAS DE SEGURIDAD (tablas completas de antes): Eurovip.destino_respaldo_20261014,
                 alojamiento_respaldo_20261014, alojamiento_habitacion_respaldo_20261014,
                 pais_respaldo_20261014, estacion_respaldo_20261014 y
                 servicio_circuito_alojamiento_respaldo_20261014. Cuando se confirme que todo
                 está bien, se pueden borrar (DROP TABLE).
               NO SE TOCÓ (a decidir): los destinos "MX" y "TH" (son códigos de país, no
               destinos) porque los usan 2 filas activas de la Home (home_contenido 2076 y 2857,
               campaña 17); y ~57 nombres de destino repetidos en dos países que pueden ser
               ciudades distintas (Toledo ES/US, Boston GB/US…) o Baleares/Canarias (BB/CC) frente
               a España (ES).

   14/10/2026  LIMPIEZA 2 (a pedido de la usuaria: "resolver" lo que marcaba "Datos a revisar").
               Solo servidor 93, esquema Eurovip. Script al final ("LIMPIEZA 2 14/10/2026");
               se ensayó con ROLLBACK y luego se aplicó.
               • Proveedores: espacios de más fuera; 44 repetidos borrados (las aerolíneas estaban
                 dos veces: "AIR EUROPA " tour operador y "Air Europa" UX); queda el que se usa o la
                 aerolínea con código IATA. Borrada la fila de relleno "Elegir un Proveedor".
               • Habitaciones: sin tabuladores, espacios dobles, "." delante ni " ." al final; las 25
                 que quedaron repetidas en el mismo hotel se borraron (queda la que usan las ofertas)
                 y 1 sin letras. Quedan 47.805.
               • Destinos "MX" y "TH" borrados con sus 2 filas de home_contenido (memoria de
                 posición de tarjetas que ya no existen).
               • 85 países que usaban las estaciones y no estaban en Países (Kazajistán, Pakistán,
                 Islas Caimán…) agregados con nombre en español; las de AG pasan a AB (Antigua y
                 Barbuda ya existía con ese código). Quedan 225 países.
               • Destinos en varios países: queda con su nombre el que más se usa; 22 de España que
                 eran el mismo sitio que Baleares/Canarias se unieron (Cala Millor, Maspalomas…);
                 32 sin uso se borraron (Toledo ES, Boston GB…); 2 en uso se renombraron
                 «Newcastle (GB)» y «Vitoria (BR)». Las referencias (hoteles, estaciones, ofertas,
                 Home…) se movieron igual.
               • Estaciones: se crearon 2.593 destinos que usaban y no existían; 197 estaciones de
                 ciudades que ya existían en otro país pasan a «Nombre (País)» (ej. «Alexandria (US)»).
               • Resultado: 0 proveedores repetidos, 0 habitaciones repetidas o con espacios,
                 0 destinos en varios países, 0 estaciones sin destino o país; 9.843 destinos.
               • COPIAS DE SEGURIDAD: tablas Eurovip.*_respaldo_20261014b.

   14/10/2026  MONEDA EUR -> DOP (a pedido de la usuaria: "todo lo cargado con EUR cambiarlo a DOP
               en todas las tablas"). Solo se cambió el código; los importes NO se convirtieron.
               310 filas: servicio 172, home_contenido 105, alojamiento_destino 18, inicio 6,
               flyer_config 6, alojamiento_circuito 3. Quedan 0 en EUR. La moneda EUR sigue en el
               catálogo "moneda". Copias: Eurovip.*_respaldo_20261014c. Script al final ("EUR -> DOP").
               OJO: varias pantallas (cargas de ofertas y circuitos, Flyers, HomeWeb) proponen EUR
               por defecto; si no se cambia, lo nuevo se seguirá cargando en EUR.

   15/10/2026  ESTACIONES MAL GUARDADAS (a pedido de la usuaria: "AA Sin Especificar / Desconocido no
               es un aeropuerto, borra todo lo que esté mal guardado"). Solo servidor 93, esquema
               Eurovip; ninguna se usaba (servicio_itinerario vacío). Ensayado con ROLLBACK y aplicado.
               Script al final ("ESTACIONES 15/10/2026").
               • Borradas 12: códigos que no son IATA (AA "Sin Especificar", AA1 copia de Cancún que
                 ya existe como CUN, W4 "Nuevo"); sin nombre, solo el código (DTZ, ETR, RZG); y
                 estaciones de tren guardadas como aeropuerto (Atocha, Sevilla-Santa Justa, Massy TGV,
                 Marsella, Xianyang, Maastricht ZYT).
               • Borrados 3 destinos que quedaron sin uso ("Desconocido", "ZYT" y el creado solo para
                 una de esas estaciones). Quedan 3.703 estaciones, todas con IATA de 3 letras y destino
                 válido en su país.
               • Copias: Eurovip.estacion_respaldo_20261015 y destino_respaldo_20261015.
               • En el código: Estaciones solo acepta IATA de 3 letras y un nombre real de aeropuerto
                 (no "Sin Especificar"/"Nuevo", ni el código, ni estaciones de tren/autobús); y
                 "Datos a revisar" de Estaciones tiene el control "No es un aeropuerto".

   15/10/2026  DATOS DE PRUEBA Y TABLAS DE RESPALDO (a pedido de la usuaria).
               • Tasas y suplementos: borradas "f", "Prueba" y "Prueba3" (ninguna oferta las usaba).
               • Hoteles de prueba sin ofertas ni circuitos borrados (5): "prueba 1",
                 "Prueba 15-05-2025", "Prueba 22-05-2025", "Prueba hoy 04-07-2025", "Prueba True 1".
                 Proveedor "prueba4" borrado.
               • TABLAS DE RESPALDO BORRADAS: todas las Eurovip.*_respaldo_* y
                 alojamiento_habitacion_borradas_20261013 (copias de seguridad de las limpiezas). Lo
                 que dicen las entradas anteriores sobre "COPIAS DE SEGURIDAD" ya no existe: esas
                 limpiezas ya no se pueden deshacer desde la base.
               • En el código: los nombres de prueba («Prueba3», «test», «xxx»…) ya no se dejan
                 guardar, y "Datos a revisar" tiene el control "Dato de prueba" en Tasas, Campañas,
                 Proveedores y Regímenes.

   15/10/2026  FASE 12 aplicada (CAMBIO 45): Eurovip.servicio_descripcion creada (copia de la
               estructura de ViajeVDT.dbo.servicio_descripcion, sin sus 16 filas que son de ofertas
               de ViajeVDT) + Eurovip.spInsertaServicioDescripcion; spEliminaServicio actualizado.
               Las cargas de oferta manual y de circuitos guardan ahí Incluye / No incluye /
               Notas / Oferta (una fila por código de carga) y ya no usan servicio_nota (vacía).
               Prueba dentro de una transacción deshecha (ROLLBACK): se insertó una descripción para
               la última oferta y Ofertas/Consultar la leen bien; no quedó nada guardado.

   15/10/2026  "Carga manual" pasa a llamarse "Paquetes" (a pedido de la usuaria). En la base solo
               cambia el nombre que se ve del módulo (la clave 'carga_manual' y los permisos no):
                   UPDATE Eurovip.UsuarioAcceso SET nombre_modulo = N'Paquetes', icono = 'bi-box-seam',
                          descripcion = N'Cargar paquetes (Express y Clásica) y editar la lista de paquetes.'
                   WHERE clave = 'carga_manual';
               El MERGE de CAMBIO 43 (más arriba) ya tiene el nombre nuevo.

   15/10/2026  FASE 13 aplicada (CAMBIO 46 y 47): Eurovip.servicio_excursion creada (copia de
               ViajeVDT.dbo.servicio_excursion) con las 24 excursiones de Punta Cana (mismo número;
               destino escrito 'Punta Cana'; 22 sin descripción) y módulo 'excursiones' (id 12).
               ViajeVDT solo se leyó. Nota técnica: sqlcmd devuelve el texto de ViajeVDT en la página
               de códigos 850; se convirtió a UTF-8 antes de copiarlo (los acentos quedaron bien).

   16/10/2026  TARJETAS DE HOTEL COPIADAS DE VIAJEVDT (a pedido de la usuaria: "copia de la tabla inicio
               de ViajeVDT todo lo que sea tipo_servicio = 'Hotel' en Eurovip.inicio"). ViajeVDT solo se
               leyó. Script al final ("INICIO HOTEL 16/10/2026"); se ensayó con ROLLBACK y se aplicó.
               • 5 tarjetas copiadas (id 26-30): Hotel Kristal, Costa Brava Blanes, Hotel Adsubia,
                 Hotel El Convento y 4R Salou Park Resort I (campaña 99, contenedor 47 HOTEL, visibles).
                 Con el mismo id_servicio que en ViajeVDT; tipo 'HOTEL' y moneda DOP como el resto de
                 Eurovip; sin espacios de más; descripción con el formato "Régimen -Código".
               • Destinos creados porque las tarjetas los usan y no existían (España): Costa del Sol,
                 Costa Brava, Costa Blanca, Costa de la Luz y Costa Dorada.
               • Ya estaban en Eurovip (no se tocaron): id 1, 4 y 5. La id 4 ("Costa villa sol") no
                 tiene destino: hay que completarlo desde la pantalla de Hoteles.
               • Nota: en ViajeVDT "Hotel El Convento" tiene origen Costa Dorada y destino Costa de la
                 Luz, y "4R Salou Park Resort I" al revés; se copió tal cual.

   16/10/2026  CIRCUITOS COPIADOS DE VIAJEVDT (a pedido de la usuaria: "copia los datos de circuitos de
               ViajeVDT de inicio, circuito, circuito_destino, circuito_itinerario, servicio, servicio_detalle,
               servicio_habitacion, servicio_tasa, todo lo relacionado a Circuito"). ViajeVDT solo se leyó.
               Script al final ("CIRCUITOS 16/10/2026"); se ensayó con ROLLBACK y se aplicó.
               • No hubo que crear tablas: todas existían en Eurovip con la misma estructura.
               • Solo idioma ES. Sin los circuitos de prueba «Hola 1» y «Prueba 2».
               • Copiado: 31 circuitos, 109 destinos de circuito, 274 días de itinerario, 125 hoteles de
                 circuito, 1.153 salidas (servicio) con su fecha (servicio_circuito), 2.096 vuelos
                 (servicio_itinerario), 884 tasas, 18 filas de alojamiento_circuito y 6 tarjetas CIRCUITO
                 en inicio (id 31-36). De las 1.153 salidas, las que salen después de hoy siguen a la venta.
               • servicio_detalle, servicio_habitacion y servicio_descripcion: ViajeVDT no tiene filas de
                 circuitos en esas tablas (nada que copiar).
               • Las salidas tienen número nuevo de Eurovip (no el de ViajeVDT); moneda DOP (mismo importe);
                 servicio_circuito con hasta = desde + noches y origen ES/Madrid, como las cargas de Eurovip.
               • Hoteles: 331 → 322 (Yodia Heritage) y 10219 → 10218 (Cartagena Plaza), porque las copias
                 repetidas se borraron en la limpieza; se creó el 10387 Hotel Arslan Boutique (Khiva).
               • Las tarjetas CIRCUITO usan la carátula «NombreSinEspacios.webp»: falta subir esas fotos
                 desde Circuitos. «Costa Rica Esencial» (ViajeVDT) y «Costa Rica Essencial» (ya en Eurovip)
                 quedaron como circuitos distintos.

   16/10/2026  FASE 14 aplicada (CAMBIO 48 a 51): excursiones en la Home (ver FASE 14).

   16/10/2026  FASE 15 aplicada (CAMBIO 52 a 55): traslados — tablas Eurovip.traslado y
               Eurovip.traslado_alojamiento, módulo 'traslados' (id 13; dárselo a los usuarios en
               "Usuarios y accesos") y Eurovip.spPortalBuscarTraslados para la web. Tablas vacías:
               los traslados se cargan desde la pantalla /Traslados/Index.

   16/10/2026  EXCURSIONES DE LA ROMANA Y SAMANÁ COPIADAS DE VIAJEVDT (a pedido de la usuaria: "La Romana y
               Samaná: copia las excursiones de estos 2 destinos de ViajeVDT a Eurovip.servicio_excursion").
               ViajeVDT solo se leyó. Script al final ("EXCURSIONES LA ROMANA Y SAMANÁ 16/10/2026"); se
               ensayó con ROLLBACK y se aplicó.
               • 24 excursiones: La Romana 18 (desde 68) y Samaná 6 (desde 48), todas con su MISMO número
                 de ViajeVDT (37-39, 188-193, 311-325: estaban libres). Ninguna trae descripción.
               • Tarjetas de la Home creadas solas (spActualizaInicioExcursion): id 90 "Excursiones en
                 La Romana" e id 91 "Excursiones en Samaná", en la sección EXCURSIONES.

   20/10/2026  FASE 16 aplicada (CAMBIO 56 a 58): traslados en la Home, una tarjeta por destino
               (ver FASE 16). Carga inicial: "Traslados en Punta Cana" (inicio id 92), con los 3 traslados
               que ya había cargado la usuaria.

   PENDIENTE   Copiar los demás usuarios de ViajeVDT (23). Los módulos y permisos de BackTiTan
               NO se copian (son otras pantallas): Eurovip tiene los suyos (CAMBIO 43) y los
               accesos se dan desde "Usuarios y accesos".
   ########################################################################################## */





/* ##########################################################################################
   LIMPIEZA 14/10/2026 — destinos, países y hoteles mal creados (explicación en la BITÁCORA).
   Script tal cual se aplicó (primero se ensayó con ROLLBACK en lugar de COMMIT).
   ########################################################################################## */
/* Limpieza de datos Eurovip (servidor 93, TropitoursVDT, esquema Eurovip) — 14/10/2026
   ViajeVDT (10.10.10.29) NO se toca. Todo en una transacción; al final COMMIT o ROLLBACK (ensayo). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

/* ---------- 0) Copias de seguridad (tablas completas, antes de tocar nada) ---------- */
IF OBJECT_ID('Eurovip.destino_respaldo_20261014') IS NULL SELECT * INTO Eurovip.destino_respaldo_20261014 FROM Eurovip.destino;
IF OBJECT_ID('Eurovip.alojamiento_respaldo_20261014') IS NULL SELECT * INTO Eurovip.alojamiento_respaldo_20261014 FROM Eurovip.alojamiento;
IF OBJECT_ID('Eurovip.alojamiento_habitacion_respaldo_20261014') IS NULL SELECT * INTO Eurovip.alojamiento_habitacion_respaldo_20261014 FROM Eurovip.alojamiento_habitacion;
IF OBJECT_ID('Eurovip.pais_respaldo_20261014') IS NULL SELECT * INTO Eurovip.pais_respaldo_20261014 FROM Eurovip.pais;
IF OBJECT_ID('Eurovip.estacion_respaldo_20261014') IS NULL SELECT * INTO Eurovip.estacion_respaldo_20261014 FROM Eurovip.estacion;
IF OBJECT_ID('Eurovip.servicio_circuito_alojamiento_respaldo_20261014') IS NULL SELECT * INTO Eurovip.servicio_circuito_alojamiento_respaldo_20261014 FROM Eurovip.servicio_circuito_alojamiento;
PRINT 'Copias de seguridad creadas (_respaldo_20261014).';

/* ---------- 1) Hoteles basura: nombre sin ninguna letra ("222", "2222", "333", "3333", "6666") ---------- */
DELETE h FROM Eurovip.alojamiento_habitacion h JOIN Eurovip.alojamiento a ON a.id_alojamiento = h.id_alojamiento WHERE a.nombre NOT LIKE '%[a-zA-Z]%';
DELETE FROM Eurovip.alojamiento WHERE nombre NOT LIKE '%[a-zA-Z]%';
PRINT CONCAT('1) Hoteles basura borrados: ', @@ROWCOUNT);

/* ---------- 2) Países duplicados: IM (Islas Mauricio) -> MU (Mauricio), E1 (Arabia Saudi) -> SA ---------- */
-- destino y estacion tienen el país en la clave: si ya existe la misma fila con el código bueno, sobra la vieja.
DELETE d FROM Eurovip.destino d WHERE d.id_pais IN ('IM','E1')
   AND EXISTS (SELECT 1 FROM Eurovip.destino x WHERE x.id_destino = d.id_destino AND x.id_idioma = d.id_idioma AND x.id_pais = CASE d.id_pais WHEN 'IM' THEN 'MU' ELSE 'SA' END);
UPDATE Eurovip.destino SET id_pais = CASE id_pais WHEN 'IM' THEN 'MU' ELSE 'SA' END WHERE id_pais IN ('IM','E1');
DELETE e FROM Eurovip.estacion e WHERE e.id_pais IN ('IM','E1')
   AND EXISTS (SELECT 1 FROM Eurovip.estacion x WHERE x.id_estacion = e.id_estacion AND x.id_destino = e.id_destino AND x.id_idioma = e.id_idioma AND x.id_pais = CASE e.id_pais WHEN 'IM' THEN 'MU' ELSE 'SA' END);
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'UPDATE Eurovip.' + QUOTENAME(TABLE_NAME) + N' SET ' + QUOTENAME(COLUMN_NAME) + N' = CASE ' + QUOTENAME(COLUMN_NAME)
            + N' WHEN ''IM'' THEN ''MU'' ELSE ''SA'' END WHERE ' + QUOTENAME(COLUMN_NAME) + N' IN (''IM'',''E1'');' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'Eurovip' AND COLUMN_NAME LIKE '%pais%' AND TABLE_NAME NOT IN ('pais','destino')
  AND TABLE_NAME NOT LIKE '%respaldo%' AND TABLE_NAME NOT LIKE '%borradas%' AND DATA_TYPE IN ('char','varchar','nchar','nvarchar');
EXEC sp_executesql @sql;
DELETE FROM Eurovip.pais WHERE id_pais IN ('IM','E1');
PRINT '2) Países IM -> MU y E1 -> SA unificados; IM y E1 borrados de pais.';

/* ---------- 3) País del hotel que no concuerda con su destino ---------- */
-- El destino estaba mal: Riyadh y Jeddah figuraban en Argentina (AR) y sus hoteles en Arabia Saudí.
UPDATE Eurovip.destino SET id_pais = 'SA' WHERE id_destino IN ('RIYADH','JEDDAH') AND id_pais = 'AR'
   AND NOT EXISTS (SELECT 1 FROM Eurovip.destino x WHERE x.id_destino = Eurovip.destino.id_destino AND x.id_idioma = Eurovip.destino.id_idioma AND x.id_pais = 'SA');
-- Hotel griego apuntando a "Athens" (Estados Unidos): pasa a "Atenas" (Grecia).
UPDATE Eurovip.alojamiento SET id_destino = 'Atenas' WHERE id_destino = 'Athens' AND id_pais = 'GR';
-- El resto: el hotel tenía mal el país (Cartagena de Indias en Antigua y Barbuda, Cancún en Colombia,
-- Isla de Sal en Baleares): toma el país de su destino.
UPDATE a SET id_pais = d.id_pais
FROM Eurovip.alojamiento a
JOIN Eurovip.destino d ON d.id_destino = a.id_destino AND d.id_idioma = a.id_idioma
WHERE ISNULL(a.id_pais,'') <> d.id_pais
  AND NOT EXISTS (SELECT 1 FROM Eurovip.destino d2 WHERE d2.id_destino = a.id_destino AND d2.id_idioma = a.id_idioma AND d2.id_pais = a.id_pais)
  AND (SELECT COUNT(*) FROM Eurovip.destino d3 WHERE d3.id_destino = a.id_destino AND d3.id_idioma = a.id_idioma) = 1;
PRINT CONCAT('3) Hoteles con el país corregido según su destino: ', @@ROWCOUNT);

/* ---------- 4) Hoteles con un destino que no existe ---------- */
UPDATE Eurovip.alojamiento SET id_destino = 'La Habana', id_pais = 'CU' WHERE id_destino = 'Havana';
UPDATE Eurovip.alojamiento SET id_destino = 'Isla Mauricio', id_pais = 'MU' WHERE id_destino = 'Mauricio';
UPDATE Eurovip.alojamiento SET id_destino = 'Barbados', id_pais = 'BD' WHERE id_alojamiento IN (9766, 9767, 9768, 9769) AND LTRIM(RTRIM(ISNULL(id_destino,''))) = '';
PRINT '4) Hoteles reasignados: Havana -> La Habana, Mauricio -> Isla Mauricio, 4 de Barbados sin destino -> Barbados.';

/* ---------- 5) Espacios de más y mayúsculas distintas ---------- */
-- " Dubrovnik" sobra: ya existe "Dubrovnik" (y no lo usa nadie).
DELETE d FROM Eurovip.destino d WHERE (d.id_destino LIKE ' %' OR d.id_destino LIKE '% ')
   AND EXISTS (SELECT 1 FROM Eurovip.destino x WHERE x.id_destino = LTRIM(RTRIM(d.id_destino)) AND DATALENGTH(x.id_destino) = DATALENGTH(LTRIM(RTRIM(d.id_destino))) AND x.id_idioma = d.id_idioma AND x.id_pais = d.id_pais);
UPDATE Eurovip.destino SET id_destino = LTRIM(RTRIM(id_destino)) WHERE id_destino LIKE ' %' OR id_destino LIKE '% ';
SET @sql = N'';
SELECT @sql += N'UPDATE Eurovip.' + QUOTENAME(TABLE_NAME) + N' SET id_destino = LTRIM(RTRIM(id_destino)) WHERE id_destino LIKE '' %'' OR id_destino LIKE ''% '';' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'Eurovip' AND COLUMN_NAME = 'id_destino' AND TABLE_NAME <> 'destino'
  AND TABLE_NAME NOT LIKE '%respaldo%' AND TABLE_NAME NOT LIKE '%borradas%';
EXEC sp_executesql @sql;
UPDATE Eurovip.alojamiento SET nombre = LTRIM(RTRIM(nombre)) WHERE nombre LIKE ' %' OR nombre LIKE '% ';
-- El destino del hotel se escribe igual que en la tabla de destinos ("RIVIERA MAYA" -> "Riviera Maya").
UPDATE a SET id_destino = d.id_destino
FROM Eurovip.alojamiento a
JOIN Eurovip.destino d ON d.id_destino = a.id_destino AND d.id_idioma = a.id_idioma AND d.id_pais = a.id_pais
WHERE a.id_destino COLLATE Latin1_General_CS_AS <> d.id_destino COLLATE Latin1_General_CS_AS;
PRINT CONCAT('5) Hoteles con el destino escrito como en la tabla de destinos: ', @@ROWCOUNT);

/* ---------- 6) Destinos basura (ninguno tiene hoteles) ---------- */
-- Nombres con error que no existen bien escritos: se corrigen.
UPDATE Eurovip.destino SET id_destino = 'Miraflores' WHERE id_destino = 'Miraflores 18.lima' AND NOT EXISTS (SELECT 1 FROM Eurovip.destino x WHERE x.id_destino = 'Miraflores' AND x.id_pais = 'PE');
UPDATE Eurovip.destino SET id_destino = 'Mazara del Vallo' WHERE id_destino = 'Mazara del Vall0' AND NOT EXISTS (SELECT 1 FROM Eurovip.destino x WHERE x.id_destino = 'Mazara del Vallo' AND x.id_pais = 'IT');
UPDATE Eurovip.destino SET id_destino = 'Ringkobing' WHERE id_destino = 'Ringko99bing' AND NOT EXISTS (SELECT 1 FROM Eurovip.destino x WHERE x.id_destino = 'Ringkobing' AND x.id_pais = 'DK');
-- Los que no son destinos o ya existen bien escritos: se borran (solo si nadie los usa).
DELETE d FROM Eurovip.destino d
WHERE (d.id_destino IN ('-pego', '65401', 'Al', 't', 'falta', 'Por cuenta del cliente', 'Lima 41', 'Orleans Cedex 1', 'Miraflores 18.lima', 'Mazara del Vall0', 'Ringko99bing')
       OR (d.id_destino = 'TROU D EAU DOUCE' AND d.id_pais = 'ES'))
  AND NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento a WHERE a.id_destino = d.id_destino)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.estacion e WHERE e.id_destino = d.id_destino AND e.id_pais = d.id_pais)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.home_contenido h WHERE h.id_destino = d.id_destino)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.circuito_destino c WHERE c.id_destino = d.id_destino);
PRINT CONCAT('6) Destinos basura borrados: ', @@ROWCOUNT);

/* ---------- 7) Hoteles repetidos: mismo nombre y destino -> queda 1 (el activo; si no, el de número más bajo) ---------- */
IF OBJECT_ID('tempdb..#copias') IS NOT NULL DROP TABLE #copias;
;WITH g AS (
    SELECT id_alojamiento,
           ROW_NUMBER()  OVER (PARTITION BY id_idioma, nombre, id_destino ORDER BY activo DESC, id_alojamiento) rn,
           FIRST_VALUE(id_alojamiento) OVER (PARTITION BY id_idioma, nombre, id_destino ORDER BY activo DESC, id_alojamiento) canon
    FROM Eurovip.alojamiento)
SELECT id_alojamiento AS copia, canon INTO #copias FROM g WHERE rn > 1;
DECLARE @nc int = (SELECT COUNT(*) FROM #copias), @nh int = (SELECT COUNT(DISTINCT canon) FROM #copias);
PRINT CONCAT('7) Copias encontradas: ', @nc, ' de ', @nh, ' hoteles');

-- 7a) Habitaciones que solo tenía alguna copia: se pasan al hotel que queda (una por nombre).
;WITH nuevas AS (
    SELECT c.canon, h.*, ROW_NUMBER() OVER (PARTITION BY c.canon, h.nombre ORDER BY h.id_alojamiento, h.id_habitacion) rn
    FROM Eurovip.alojamiento_habitacion h JOIN #copias c ON c.copia = h.id_alojamiento
    WHERE NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento_habitacion x WHERE x.id_alojamiento = c.canon AND x.id_idioma = h.id_idioma AND x.nombre = h.nombre)),
n2 AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY canon ORDER BY nombre) k FROM nuevas WHERE rn = 1)
INSERT INTO Eurovip.alojamiento_habitacion (id_alojamiento, id_habitacion, id_idioma, id_tipo_habitacion, codigo, nombre, grupo, nota, precio, codigo_newblue, nombre_newblue)
SELECT n2.canon, ISNULL((SELECT MAX(x.id_habitacion) FROM Eurovip.alojamiento_habitacion x WHERE x.id_alojamiento = n2.canon), 0) + n2.k,
       n2.id_idioma, n2.id_tipo_habitacion, n2.codigo, n2.nombre, n2.grupo, n2.nota, n2.precio, n2.codigo_newblue, n2.nombre_newblue
FROM n2;
PRINT CONCAT('7a) Habitaciones pasadas al hotel que queda: ', @@ROWCOUNT);

-- 7b) Lo que apunte a una copia pasa al hotel que queda.
UPDATE s SET id_alojamiento = c.canon FROM Eurovip.servicio_circuito_alojamiento s JOIN #copias c ON c.copia = s.id_alojamiento
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.servicio_circuito_alojamiento x WHERE x.nombre_circuito = s.nombre_circuito AND x.id_idioma = s.id_idioma AND x.categoria = s.categoria AND x.id_alojamiento = c.canon);
DELETE s FROM Eurovip.servicio_circuito_alojamiento s JOIN #copias c ON c.copia = s.id_alojamiento;
UPDATE s SET id_alojamiento = c.canon FROM Eurovip.servicio_detalle s JOIN #copias c ON c.copia = s.id_alojamiento;
UPDATE s SET id_alojamiento = c.canon FROM Eurovip.alojamiento_destino s JOIN #copias c ON c.copia = s.id_alojamiento;
IF EXISTS (SELECT 1 FROM Eurovip.servicio_habitacion s JOIN #copias c ON c.copia = s.id_alojamiento)
    THROW 50001, 'Hay salidas (servicio_habitacion) que usan habitaciones de una copia: revisar antes de borrar.', 1;

-- 7c) Se borran las copias y sus habitaciones.
DELETE h FROM Eurovip.alojamiento_habitacion h JOIN #copias c ON c.copia = h.id_alojamiento;
PRINT CONCAT('7c) Habitaciones de copias borradas: ', @@ROWCOUNT);
DELETE a FROM Eurovip.alojamiento a JOIN #copias c ON c.copia = a.id_alojamiento;
PRINT CONCAT('7c) Copias de hoteles borradas: ', @@ROWCOUNT);

/* ---------- Comprobación final ---------- */
SELECT 'hoteles' que, COUNT(*) n FROM Eurovip.alojamiento
UNION ALL SELECT 'habitaciones', COUNT(*) FROM Eurovip.alojamiento_habitacion
UNION ALL SELECT 'destinos', COUNT(*) FROM Eurovip.destino
UNION ALL SELECT 'paises', COUNT(*) FROM Eurovip.pais
UNION ALL SELECT 'hoteles repetidos', COUNT(*) FROM (SELECT 1 x FROM Eurovip.alojamiento GROUP BY id_idioma, nombre, id_destino HAVING COUNT(*) > 1) r
UNION ALL SELECT 'hoteles sin destino valido', COUNT(*) FROM Eurovip.alojamiento a WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = a.id_destino AND d.id_pais = a.id_pais)
UNION ALL SELECT 'hoteles con pais inexistente', COUNT(*) FROM Eurovip.alojamiento a WHERE NOT EXISTS (SELECT 1 FROM Eurovip.pais p WHERE p.id_pais = a.id_pais)
UNION ALL SELECT 'destinos con pais inexistente', COUNT(*) FROM Eurovip.destino d WHERE NOT EXISTS (SELECT 1 FROM Eurovip.pais p WHERE p.id_pais = d.id_pais)
UNION ALL SELECT 'destinos con espacios', COUNT(*) FROM Eurovip.destino WHERE id_destino LIKE ' %' OR id_destino LIKE '% '
UNION ALL SELECT 'hoteles basura', COUNT(*) FROM Eurovip.alojamiento WHERE nombre NOT LIKE '%[a-zA-Z]%';

SELECT a.id_alojamiento, a.nombre, a.id_destino, a.id_pais FROM Eurovip.alojamiento a WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = a.id_destino AND d.id_pais = a.id_pais);

COMMIT;


/* ##########################################################################################
   LIMPIEZA 2 14/10/2026 — proveedores, habitaciones, destinos en varios países y estaciones
   (explicación en la BITÁCORA). Script tal cual se aplicó (antes se ensayó con ROLLBACK).
   ########################################################################################## */
/* Limpieza 2 de datos Eurovip (servidor 93, TropitoursVDT, esquema Eurovip) — 14/10/2026
   Resuelve lo que marcaba "Datos a revisar": proveedores repetidos, habitaciones con espacios/símbolos,
   destinos "MX"/"TH", destinos en varios países y estaciones con destino o país inexistente.
   ViajeVDT (10.10.10.29) NO se toca. Todo en una transacción; al final COMMIT o ROLLBACK (ensayo). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

/* ---------- 0) Copias de seguridad de las tablas que se tocan ---------- */
DECLARE @t sysname, @sql nvarchar(max);
DECLARE r CURSOR LOCAL FAST_FORWARD FOR SELECT v FROM (VALUES ('proveedor'),('servicio'),('circuito'),('alojamiento_habitacion'),('servicio_habitacion'),('destino'),('estacion'),('pais'),
    ('home_contenido'),('alojamiento'),('circuito_destino'),('servicio_destino'),('servicio_origen'),('servicio_circuito'),('inicio'),('alojamiento_destino'),('alojamiento_circuito')) x(v);
OPEN r; FETCH NEXT FROM r INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID('Eurovip.' + @t + '_respaldo_20261014b') IS NULL
    BEGIN SET @sql = N'SELECT * INTO Eurovip.' + QUOTENAME(@t + '_respaldo_20261014b') + N' FROM Eurovip.' + QUOTENAME(@t) + N';'; EXEC sp_executesql @sql; END
    FETCH NEXT FROM r INTO @t;
END
CLOSE r; DEALLOCATE r;
PRINT 'Copias de seguridad creadas (_respaldo_20261014b).';

/* ---------- 1) Proveedores ---------- */
-- 1a) Nombres sin espacios de más.
WHILE EXISTS (SELECT 1 FROM Eurovip.proveedor WHERE nombre LIKE '%  %') UPDATE Eurovip.proveedor SET nombre = REPLACE(nombre, '  ', ' ') WHERE nombre LIKE '%  %';
UPDATE Eurovip.proveedor SET nombre = LTRIM(RTRIM(nombre)) WHERE nombre LIKE ' %' OR nombre LIKE '% ';
-- 1b) Repetidos (mismo nombre): queda el que se usa; si ninguno, la aerolínea (código IATA); lo que apunte a los otros pasa al que queda.
IF OBJECT_ID('tempdb..#prov') IS NOT NULL DROP TABLE #prov;
;WITH u AS (
    SELECT p.id_proveedor, p.nombre, p.id_tipo_proveedor,
           (SELECT COUNT(*) FROM Eurovip.servicio s WHERE s.id_Proveedor = p.id_proveedor) + (SELECT COUNT(*) FROM Eurovip.circuito c WHERE c.id_proveedor = p.id_proveedor)
         + (SELECT COUNT(*) FROM Eurovip.servicio_itinerario i WHERE i.id_proveedor = p.id_proveedor) AS usos
    FROM Eurovip.proveedor p),
g AS (SELECT *, FIRST_VALUE(id_proveedor) OVER (PARTITION BY nombre ORDER BY usos DESC, CASE WHEN id_tipo_proveedor = 'AEROLINEA' THEN 0 ELSE 1 END, id_proveedor) queda,
             COUNT(*) OVER (PARTITION BY nombre) n FROM u)
SELECT id_proveedor AS sobra, queda INTO #prov FROM g WHERE n > 1 AND id_proveedor <> queda;
UPDATE s SET id_Proveedor = x.queda FROM Eurovip.servicio s JOIN #prov x ON x.sobra = s.id_Proveedor;
UPDATE c SET id_proveedor = x.queda FROM Eurovip.circuito c JOIN #prov x ON x.sobra = c.id_proveedor;
UPDATE i SET id_proveedor = x.queda FROM Eurovip.servicio_itinerario i JOIN #prov x ON x.sobra = i.id_proveedor;
DELETE p FROM Eurovip.proveedor p JOIN #prov x ON x.sobra = p.id_proveedor;
PRINT CONCAT('1b) Proveedores repetidos borrados: ', @@ROWCOUNT);
-- 1c) Fila de relleno "Elegir un Proveedor" (no es un proveedor), si nadie la usa.
DELETE p FROM Eurovip.proveedor p WHERE p.nombre = 'Elegir un Proveedor'
   AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio s WHERE s.id_Proveedor = p.id_proveedor)
   AND NOT EXISTS (SELECT 1 FROM Eurovip.circuito c WHERE c.id_proveedor = p.id_proveedor)
   AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_itinerario i WHERE i.id_proveedor = p.id_proveedor);
PRINT CONCAT('1c) "Elegir un Proveedor" borrado: ', @@ROWCOUNT);

/* ---------- 2) Habitaciones ---------- */
-- 2a) Tabuladores/saltos -> espacio, sin espacios dobles ni al principio/final, sin "." o "-" delante ni " ." al final.
UPDATE Eurovip.alojamiento_habitacion SET nombre = REPLACE(REPLACE(REPLACE(nombre, CHAR(9), ' '), CHAR(13), ' '), CHAR(10), ' ')
WHERE nombre LIKE '%' + CHAR(9) + '%' OR nombre LIKE '%' + CHAR(13) + '%' OR nombre LIKE '%' + CHAR(10) + '%';
WHILE EXISTS (SELECT 1 FROM Eurovip.alojamiento_habitacion WHERE nombre LIKE '%  %') UPDATE Eurovip.alojamiento_habitacion SET nombre = REPLACE(nombre, '  ', ' ') WHERE nombre LIKE '%  %';
UPDATE Eurovip.alojamiento_habitacion SET nombre = LTRIM(RTRIM(nombre)) WHERE nombre LIKE ' %' OR nombre LIKE '% ';
WHILE EXISTS (SELECT 1 FROM Eurovip.alojamiento_habitacion WHERE nombre LIKE '[.-]%') UPDATE Eurovip.alojamiento_habitacion SET nombre = LTRIM(STUFF(nombre, 1, 1, '')) WHERE nombre LIKE '[.-]%';
UPDATE Eurovip.alojamiento_habitacion SET nombre = RTRIM(LEFT(nombre, LEN(nombre) - 1)) WHERE nombre LIKE '% .';
PRINT '2a) Nombres de habitación limpiados.';
-- 2b) Al limpiar pueden quedar repetidas en el mismo hotel: queda la que usan las ofertas; si no, la que tiene código Newblue; si no, la de número más bajo.
IF OBJECT_ID('tempdb..#hab') IS NOT NULL DROP TABLE #hab;
;WITH g AS (
    SELECT h.id_alojamiento, h.id_idioma, h.id_habitacion,
           FIRST_VALUE(h.id_habitacion) OVER (PARTITION BY h.id_alojamiento, h.id_idioma, h.nombre ORDER BY
               CASE WHEN EXISTS (SELECT 1 FROM Eurovip.servicio_habitacion s WHERE s.id_alojamiento = h.id_alojamiento AND s.id_habitacion = h.id_habitacion AND s.id_idioma = h.id_idioma) THEN 0 ELSE 1 END,
               CASE WHEN ISNULL(h.codigo_newblue, '') <> '' THEN 0 ELSE 1 END, h.id_habitacion) queda
    FROM Eurovip.alojamiento_habitacion h)
SELECT * INTO #hab FROM g WHERE id_habitacion <> queda;
UPDATE s SET id_habitacion = x.queda FROM Eurovip.servicio_habitacion s
JOIN #hab x ON x.id_alojamiento = s.id_alojamiento AND x.id_habitacion = s.id_habitacion AND x.id_idioma = s.id_idioma;
DELETE h FROM Eurovip.alojamiento_habitacion h JOIN #hab x ON x.id_alojamiento = h.id_alojamiento AND x.id_habitacion = h.id_habitacion AND x.id_idioma = h.id_idioma;
PRINT CONCAT('2b) Habitaciones repetidas tras limpiar, borradas: ', @@ROWCOUNT);
-- 2c) Habitaciones sin ninguna letra (ej. "12") que ninguna oferta usa.
DELETE h FROM Eurovip.alojamiento_habitacion h
WHERE h.nombre NOT LIKE '%[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]%'
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_habitacion s WHERE s.id_alojamiento = h.id_alojamiento AND s.id_habitacion = h.id_habitacion AND s.id_idioma = h.id_idioma);
PRINT CONCAT('2c) Habitaciones sin letras borradas: ', @@ROWCOUNT);

/* ---------- 3) Destinos "MX" y "TH" (códigos de país, no destinos) ---------- */
-- Solo los usaban 2 filas de home_contenido (memoria de posición de tarjetas que no existen: ninguna tarjeta de inicio los usa).
DELETE FROM Eurovip.home_contenido WHERE id_destino IN ('MX', 'TH') AND NOT EXISTS (SELECT 1 FROM Eurovip.inicio i WHERE i.id_destino IN ('MX', 'TH'));
DELETE d FROM Eurovip.destino d WHERE d.id_destino IN ('MX', 'TH')
   AND NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento a WHERE a.id_destino = d.id_destino)
   AND NOT EXISTS (SELECT 1 FROM Eurovip.estacion e WHERE e.id_destino = d.id_destino)
   AND NOT EXISTS (SELECT 1 FROM Eurovip.home_contenido h WHERE h.id_destino = d.id_destino);
PRINT CONCAT('3) Destinos MX/TH borrados: ', @@ROWCOUNT);

/* ---------- 4) Países que usaban las estaciones y no estaban en Países ---------- */
DECLARE @nuevos TABLE (id_pais char(2), nombre nvarchar(30), continente nvarchar(15));
INSERT @nuevos VALUES
('HK', N'Hong Kong', N'Asia'), ('UM', N'Islas Ultramarinas de EE.UU.', N'Oceania'), ('KZ', N'Kazajistán', N'Asia'), ('UG', N'Uganda', N'Africa'),
('FK', N'Islas Malvinas', N'America'), ('AS', N'Samoa Americana', N'Oceania'), ('RW', N'Ruanda', N'Africa'), ('AI', N'Anguila', N'America'),
('KH', N'Camboya', N'Asia'), ('FM', N'Micronesia', N'Oceania'), ('KN', N'San Cristóbal y Nieves', N'America'), ('KY', N'Islas Caimán', N'America'),
('BY', N'Bielorrusia', N'Europa'), ('SD', N'Sudán', N'Africa'), ('GY', N'Guyana', N'America'), ('WS', N'Samoa', N'Oceania'),
('ML', N'Malí', N'Africa'), ('VG', N'Islas Vírgenes Británicas', N'America'), ('PS', N'Palestina', N'Asia'), ('KI', N'Kiribati', N'Oceania'),
('TM', N'Turkmenistán', N'Asia'), ('BM', N'Bermudas', N'America'), ('TV', N'Tuvalu', N'Oceania'), ('ER', N'Eritrea', N'Africa'),
('GE', N'Georgia', N'Asia'), ('NU', N'Niue', N'Oceania'), ('RS', N'Serbia', N'Europa'), ('ZM', N'Zambia', N'Africa'),
('BF', N'Burkina Faso', N'Africa'), ('GA', N'Gabón', N'Africa'), ('LS', N'Lesoto', N'Africa'), ('MM', N'Myanmar', N'Asia'),
('BN', N'Brunéi', N'Asia'), ('TJ', N'Tayikistán', N'Asia'), ('NF', N'Isla Norfolk', N'Oceania'), ('DZ', N'Argelia', N'Africa'),
('MD', N'Moldavia', N'Europa'), ('GW', N'Guinea-Bisáu', N'Africa'), ('NA', N'Namibia', N'Africa'), ('PK', N'Pakistán', N'Asia'),
('PW', N'Palaos', N'Oceania'), ('MN', N'Mongolia', N'Asia'), ('SX', N'Sint Maarten', N'America'), ('IQ', N'Irak', N'Asia'),
('BW', N'Botsuana', N'Africa'), ('GI', N'Gibraltar', N'Europa'), ('TG', N'Togo', N'Africa'), ('ET', N'Etiopía', N'Africa'),
('CF', N'República Centroafricana', N'Africa'), ('AO', N'Angola', N'Africa'), ('LC', N'Santa Lucía', N'America'), ('MW', N'Malaui', N'Africa'),
('CK', N'Islas Cook', N'Oceania'), ('YT', N'Mayotte', N'Africa'), ('GL', N'Groenlandia', N'America'), ('GU', N'Guam', N'Oceania'),
('NR', N'Nauru', N'Oceania'), ('TD', N'Chad', N'Africa'), ('YE', N'Yemen', N'Asia'), ('KM', N'Comoras', N'Africa'),
('GD', N'Granada', N'America'), ('MP', N'Islas Marianas del Norte', N'Oceania'), ('TO', N'Tonga', N'Oceania'), ('LR', N'Liberia', N'Africa'),
('VC', N'San Vicente y las Granadinas', N'America'), ('CD', N'Congo (Rep. Democrática)', N'Africa'), ('KG', N'Kirguistán', N'Asia'), ('AN', N'Antillas Neerlandesas', N'America'),
('DJ', N'Yibuti', N'Africa'), ('MH', N'Islas Marshall', N'Oceania'), ('NE', N'Níger', N'Africa'), ('TC', N'Islas Turcas y Caicos', N'America'),
('GQ', N'Guinea Ecuatorial', N'Africa'), ('RE', N'Reunión', N'Africa'), ('BJ', N'Benín', N'Africa'), ('BT', N'Bután', N'Asia'),
('CI', N'Costa de Marfil', N'Africa'), ('TL', N'Timor Oriental', N'Asia'), ('AG', N'Antigua y Barbuda', N'America'), ('CG', N'República del Congo', N'Africa'),
('KP', N'Corea del Norte', N'Asia'), ('DM', N'Dominica', N'America'), ('GN', N'Guinea', N'Africa'), ('LA', N'Laos', N'Asia'),
('SR', N'Surinam', N'America'), ('PG', N'Papúa Nueva Guinea', N'Oceania'), ('VU', N'Vanuatu', N'Oceania'), ('HT', N'Haití', N'America'),
('AZ', N'Azerbaiyán', N'Asia'), ('CX', N'Isla de Navidad', N'Oceania'), ('SZ', N'Esuatini', N'Africa')
;
-- Si el país ya existe con otro código (ej. Antigua y Barbuda = AB), las estaciones pasan a ese código.
UPDATE e SET id_pais = p.id_pais
FROM Eurovip.estacion e JOIN @nuevos n ON n.id_pais = e.id_pais
JOIN Eurovip.pais p ON p.nombre = n.nombre AND p.id_idioma = 'ES' AND p.id_pais <> n.id_pais
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.estacion x WHERE x.id_estacion = e.id_estacion AND x.id_destino = e.id_destino AND x.id_idioma = e.id_idioma AND x.id_pais = p.id_pais);
INSERT INTO Eurovip.pais (id_pais, id_idioma, nombre_continente, nombre)
SELECT n.id_pais, 'ES', n.continente, n.nombre FROM @nuevos n
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.pais p WHERE p.id_pais = n.id_pais AND p.id_idioma = 'ES')
  AND NOT EXISTS (SELECT 1 FROM Eurovip.pais p WHERE p.nombre = n.nombre AND p.id_idioma = 'ES')
  AND EXISTS (SELECT 1 FROM Eurovip.estacion e WHERE e.id_pais = n.id_pais);
PRINT CONCAT('4) Países agregados: ', @@ROWCOUNT);

/* ---------- Dónde se usa un destino (nombre + país) en otras tablas ---------- */
DECLARE @refs TABLE (tabla sysname, colNombre sysname, colPais sysname);
INSERT @refs VALUES ('alojamiento','id_destino','id_pais'), ('estacion','id_destino','id_pais'), ('circuito_destino','id_destino','id_pais'),
    ('servicio_destino','destino','id_pais_destino'), ('servicio_origen','origen','id_pais_origen'), ('servicio_circuito','origen','id_pais_origen'),
    ('inicio','id_destino','id_paisDestino'), ('inicio','origen','id_pais_origen'), ('alojamiento_destino','id_destino','id_pais_destino'),
    ('alojamiento_destino','id_origen','id_pais_origen'), ('alojamiento_circuito','id_destino','id_pais_destino'), ('alojamiento_circuito','id_origen','id_pais_origen'),
    ('home_contenido','origen','id_pais_origen'), ('servicio_itinerario','origen','id_pais_origen'), ('servicio_itinerario','destino','id_pais_destino');
DECLARE @tab sysname, @cn sysname, @cp sysname;

/* ---------- 5) Destinos con el mismo nombre en varios países ---------- */
-- Queda con su nombre el que más se usa. Los demás: si es el mismo sitio (España frente a Baleares/Canarias) se unen;
-- si nadie los usa se borran; si se usan, pasan a llamarse «Nombre (País)», ej. «Toledo (US)».
IF OBJECT_ID('tempdb..#dup') IS NOT NULL DROP TABLE #dup;
CREATE TABLE #dup (id_destino varchar(30), id_pais char(2), n int DEFAULT 0, queda_pais char(2) NULL, accion varchar(10) NULL, nuevo varchar(30) NULL);
INSERT #dup (id_destino, id_pais) SELECT d.id_destino, d.id_pais FROM Eurovip.destino d
WHERE d.id_idioma = 'ES' AND EXISTS (SELECT 1 FROM Eurovip.destino x WHERE x.id_destino = d.id_destino AND x.id_idioma = 'ES' AND x.id_pais <> d.id_pais);
DECLARE c1 CURSOR LOCAL FAST_FORWARD FOR SELECT tabla, colNombre, colPais FROM @refs;
OPEN c1; FETCH NEXT FROM c1 INTO @tab, @cn, @cp;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'UPDATE u SET n = n + (SELECT COUNT(*) FROM Eurovip.' + QUOTENAME(@tab) + N' x WHERE x.' + QUOTENAME(@cn) + N' = u.id_destino AND x.' + QUOTENAME(@cp) + N' = u.id_pais) FROM #dup u;';
    EXEC sp_executesql @sql;
    FETCH NEXT FROM c1 INTO @tab, @cn, @cp;
END
CLOSE c1; DEALLOCATE c1;
;WITH k AS (SELECT *, FIRST_VALUE(id_pais) OVER (PARTITION BY id_destino ORDER BY n DESC,
               CASE WHEN id_pais IN ('BB','CC') THEN 0 WHEN id_pais = 'ES' THEN 1 ELSE 2 END, id_pais) qp FROM #dup)
UPDATE k SET queda_pais = qp;
UPDATE #dup SET accion = CASE
        WHEN id_pais = queda_pais THEN 'queda'
        WHEN (id_pais = 'ES' AND queda_pais IN ('BB','CC')) OR (queda_pais = 'ES' AND id_pais IN ('BB','CC')) THEN 'unir'
        WHEN n = 0 THEN 'borrar'
        ELSE 'renombrar' END;
UPDATE #dup SET nuevo = LEFT(RTRIM(id_destino), 25) + ' (' + id_pais + ')' WHERE accion = 'renombrar';
-- unir: las referencias pasan al país del que queda (en estaciones, si ya existe igual con ese país, sobra la vieja)
DELETE e FROM Eurovip.estacion e JOIN #dup u ON u.accion = 'unir' AND e.id_destino = u.id_destino AND e.id_pais = u.id_pais
WHERE EXISTS (SELECT 1 FROM Eurovip.estacion x WHERE x.id_estacion = e.id_estacion AND x.id_destino = e.id_destino AND x.id_idioma = e.id_idioma AND x.id_pais = u.queda_pais);
DECLARE c2 CURSOR LOCAL FAST_FORWARD FOR SELECT tabla, colNombre, colPais FROM @refs;
OPEN c2; FETCH NEXT FROM c2 INTO @tab, @cn, @cp;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'UPDATE x SET ' + QUOTENAME(@cp) + N' = u.queda_pais FROM Eurovip.' + QUOTENAME(@tab) + N' x JOIN #dup u ON u.accion = ''unir'' AND x.' + QUOTENAME(@cn) + N' = u.id_destino AND x.' + QUOTENAME(@cp) + N' = u.id_pais;'
             + N'UPDATE x SET ' + QUOTENAME(@cn) + N' = u.nuevo FROM Eurovip.' + QUOTENAME(@tab) + N' x JOIN #dup u ON u.accion = ''renombrar'' AND x.' + QUOTENAME(@cn) + N' = u.id_destino AND x.' + QUOTENAME(@cp) + N' = u.id_pais;';
    EXEC sp_executesql @sql;
    FETCH NEXT FROM c2 INTO @tab, @cn, @cp;
END
CLOSE c2; DEALLOCATE c2;
UPDATE d SET id_destino = u.nuevo FROM Eurovip.destino d JOIN #dup u ON u.accion = 'renombrar' AND d.id_destino = u.id_destino AND d.id_pais = u.id_pais AND d.id_idioma = 'ES';
DELETE d FROM Eurovip.destino d JOIN #dup u ON u.accion IN ('unir','borrar') AND d.id_destino = u.id_destino AND d.id_pais = u.id_pais AND d.id_idioma = 'ES';
DECLARE @r1 int = (SELECT COUNT(*) FROM #dup WHERE accion = 'unir'), @r2 int = (SELECT COUNT(*) FROM #dup WHERE accion = 'borrar'), @r3 int = (SELECT COUNT(*) FROM #dup WHERE accion = 'renombrar');
PRINT CONCAT('5) Destinos en varios países: unidos ', @r1, ', borrados (sin uso) ', @r2, ', renombrados ', @r3);

/* ---------- 6) Estaciones cuyo destino no existe en su país ---------- */
WHILE EXISTS (SELECT 1 FROM Eurovip.estacion WHERE id_destino LIKE '%  %') UPDATE Eurovip.estacion SET id_destino = REPLACE(id_destino, '  ', ' ') WHERE id_destino LIKE '%  %';
UPDATE Eurovip.estacion SET id_destino = LTRIM(RTRIM(id_destino)) WHERE id_destino LIKE ' %' OR id_destino LIKE '% ';
IF OBJECT_ID('tempdb..#est') IS NOT NULL DROP TABLE #est;
SELECT e.id_destino, e.id_pais, COUNT(*) n, CAST(NULL AS varchar(30)) nuevo INTO #est FROM Eurovip.estacion e
WHERE e.id_idioma = 'ES' AND NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = e.id_destino AND d.id_pais = e.id_pais AND d.id_idioma = 'ES')
GROUP BY e.id_destino, e.id_pais;
;WITH k AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY id_destino ORDER BY n DESC, id_pais) rn FROM #est)
UPDATE k SET nuevo = CASE WHEN rn = 1 AND NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = k.id_destino AND d.id_idioma = 'ES')
                          THEN id_destino ELSE LEFT(id_destino, 25) + ' (' + id_pais + ')' END;
DECLARE @maxid int = ISNULL((SELECT MAX(id) FROM Eurovip.destino), 0);
INSERT INTO Eurovip.destino (id_destino, id_idioma, id_pais, id)
SELECT f.nuevo, 'ES', f.id_pais, @maxid + ROW_NUMBER() OVER (ORDER BY f.nuevo) FROM #est f
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = f.nuevo AND d.id_pais = f.id_pais AND d.id_idioma = 'ES');
PRINT CONCAT('6) Destinos creados para estaciones: ', @@ROWCOUNT);
UPDATE e SET id_destino = f.nuevo FROM Eurovip.estacion e JOIN #est f ON f.id_destino = e.id_destino AND f.id_pais = e.id_pais WHERE f.nuevo <> f.id_destino;
PRINT CONCAT('6) Estaciones con el destino renombrado «Nombre (País)»: ', @@ROWCOUNT);

/* ---------- Comprobación final ---------- */
SELECT 'proveedores repetidos' que, COUNT(*) n FROM (SELECT nombre FROM Eurovip.proveedor GROUP BY nombre HAVING COUNT(*) > 1) x
UNION ALL SELECT 'proveedores', COUNT(*) FROM Eurovip.proveedor
UNION ALL SELECT 'habitaciones con espacios', COUNT(*) FROM Eurovip.alojamiento_habitacion WHERE nombre LIKE ' %' OR nombre LIKE '% ' OR nombre LIKE '%  %'
UNION ALL SELECT 'habitaciones repetidas', COUNT(*) FROM (SELECT 1 x FROM Eurovip.alojamiento_habitacion GROUP BY id_alojamiento, id_idioma, nombre HAVING COUNT(*) > 1) y
UNION ALL SELECT 'habitaciones', COUNT(*) FROM Eurovip.alojamiento_habitacion
UNION ALL SELECT 'destinos en varios paises', COUNT(*) FROM Eurovip.destino d WHERE EXISTS (SELECT 1 FROM Eurovip.destino x WHERE x.id_destino = d.id_destino AND x.id_idioma = d.id_idioma AND x.id_pais <> d.id_pais)
UNION ALL SELECT 'destinos', COUNT(*) FROM Eurovip.destino
UNION ALL SELECT 'estaciones sin destino en su pais', COUNT(*) FROM Eurovip.estacion e WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = e.id_destino AND d.id_pais = e.id_pais AND d.id_idioma = 'ES')
UNION ALL SELECT 'estaciones con pais inexistente', COUNT(*) FROM Eurovip.estacion e WHERE NOT EXISTS (SELECT 1 FROM Eurovip.pais p WHERE p.id_pais = e.id_pais)
UNION ALL SELECT 'paises', COUNT(*) FROM Eurovip.pais
UNION ALL SELECT 'hoteles sin destino valido', COUNT(*) FROM Eurovip.alojamiento a WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = a.id_destino AND d.id_pais = a.id_pais)
UNION ALL SELECT 'ofertas con destino invalido', COUNT(*) FROM Eurovip.servicio_destino s WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = s.destino AND d.id_pais = s.id_pais_destino);
SELECT accion, COUNT(*) n, STRING_AGG(CAST(id_destino + ' ' + id_pais + ISNULL(' -> ' + nuevo, '') AS nvarchar(max)), ' | ') ejemplos FROM (SELECT TOP 100 PERCENT * FROM #dup) d GROUP BY accion;

COMMIT;


/* ##########################################################################################
   EUR -> DOP 14/10/2026 (explicación en la BITÁCORA). Script tal cual se aplicó.
   ########################################################################################## */
/* EUR -> DOP en todas las tablas de Eurovip (servidor 93) — 14/10/2026. Solo cambia el código de moneda;
   los importes NO se convierten. La moneda EUR sigue existiendo en el catálogo "moneda". */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @t sysname, @c sysname, @sql nvarchar(max);
DECLARE r CURSOR LOCAL FAST_FORWARD FOR
    SELECT c.TABLE_NAME, c.COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS c
    JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_TYPE = 'BASE TABLE'
    WHERE c.TABLE_SCHEMA = 'Eurovip' AND c.COLUMN_NAME LIKE '%moneda%' AND c.TABLE_NAME NOT IN ('moneda')
      AND c.TABLE_NAME NOT LIKE '%respaldo%' AND c.TABLE_NAME NOT LIKE '%borradas%';
OPEN r; FETCH NEXT FROM r INTO @t, @c;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID('Eurovip.' + @t + '_respaldo_20261014c') IS NULL
    BEGIN SET @sql = N'SELECT * INTO Eurovip.' + QUOTENAME(@t + '_respaldo_20261014c') + N' FROM Eurovip.' + QUOTENAME(@t) + N';'; EXEC sp_executesql @sql; END
    SET @sql = N'UPDATE Eurovip.' + QUOTENAME(@t) + N' SET ' + QUOTENAME(@c) + N' = ''DOP'' WHERE ' + QUOTENAME(@c) + N' = ''EUR''; PRINT CONCAT(''' + @t + N'.' + @c + N': '', @@ROWCOUNT);';
    EXEC sp_executesql @sql;
    FETCH NEXT FROM r INTO @t, @c;
END
CLOSE r; DEALLOCATE r;
COMMIT;


/* ##########################################################################################
   ESTACIONES 15/10/2026 (explicación en la BITÁCORA). Script tal cual se aplicó.
   ########################################################################################## */
/* Estaciones mal guardadas — 15/10/2026 (servidor 93, esquema Eurovip). Ninguna se usa (servicio_itinerario vacío). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
IF OBJECT_ID('Eurovip.estacion_respaldo_20261015') IS NULL SELECT * INTO Eurovip.estacion_respaldo_20261015 FROM Eurovip.estacion;
IF OBJECT_ID('Eurovip.destino_respaldo_20261015') IS NULL SELECT * INTO Eurovip.destino_respaldo_20261015 FROM Eurovip.destino;

IF OBJECT_ID('tempdb..#mal') IS NOT NULL DROP TABLE #mal;
SELECT e.id_estacion, e.id_destino, e.id_pais, e.id_idioma, e.nombre,
       CASE WHEN e.id_estacion NOT LIKE '[A-Z][A-Z][A-Z]' THEN 'código que no es IATA'
            WHEN e.nombre IN ('Sin Especificar', 'Desconocido', 'Nuevo', 'Prueba', 'Test') OR e.id_destino IN ('Desconocido') THEN 'no es un aeropuerto'
            WHEN e.nombre = e.id_estacion OR e.id_destino = e.id_estacion THEN 'sin nombre (solo el código)'
            ELSE 'estación de tren, no aeropuerto' END AS motivo
INTO #mal
FROM Eurovip.estacion e
WHERE e.id_estacion NOT LIKE '[A-Z][A-Z][A-Z]'
   OR e.nombre IN ('Sin Especificar', 'Desconocido', 'Nuevo', 'Prueba', 'Test') OR e.id_destino IN ('Desconocido')
   OR e.nombre = e.id_estacion OR e.id_destino = e.id_estacion
   OR e.nombre LIKE '%estaci_n de tren%' OR e.nombre LIKE '%estaci_n de ferrocarril%' OR e.nombre LIKE '% rail %' OR e.nombre LIKE '%rail stn%'
   OR e.nombre IN ('ESTACIÓN DE ATOCHA', 'ESTACIÓN DE SEVILLA-SANTA JUSTA', 'ESTACIÓN DE MARSELLA-SAN CARLOS');
IF EXISTS (SELECT 1 FROM Eurovip.servicio_itinerario i JOIN #mal m ON m.id_estacion IN (i.id_estacion_origen, i.id_estacion_destino))
    THROW 50001, 'Alguna estación a borrar se usa en vuelos cargados.', 1;
SELECT id_estacion, nombre, id_destino, id_pais, motivo FROM #mal ORDER BY motivo, id_estacion;

DELETE e FROM Eurovip.estacion e JOIN #mal m ON m.id_estacion = e.id_estacion AND m.id_destino = e.id_destino AND m.id_pais = e.id_pais AND m.id_idioma = e.id_idioma;
PRINT CONCAT('Estaciones borradas: ', @@ROWCOUNT);

-- Destinos que quedaron sin uso y no son destinos reales ("Desconocido", "ZYT"…) o que solo existían por esas estaciones.
DELETE d FROM Eurovip.destino d
WHERE EXISTS (SELECT 1 FROM #mal m WHERE m.id_destino = d.id_destino AND m.id_pais = d.id_pais)
  AND (d.id_destino IN ('Desconocido') OR d.id_destino LIKE '[A-Z][A-Z][A-Z]' COLLATE Latin1_General_CS_AS
       OR NOT EXISTS (SELECT 1 FROM Eurovip.destino_respaldo_20261014b b WHERE b.id_destino = d.id_destino AND b.id_pais = d.id_pais))
  AND NOT EXISTS (SELECT 1 FROM Eurovip.estacion x WHERE x.id_destino = d.id_destino AND x.id_pais = d.id_pais)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento x WHERE x.id_destino = d.id_destino)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.circuito_destino x WHERE x.id_destino = d.id_destino)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_destino x WHERE x.destino = d.id_destino)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_origen x WHERE x.origen = d.id_destino)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.inicio x WHERE x.id_destino = d.id_destino OR x.origen = d.id_destino)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.home_contenido x WHERE x.id_destino = d.id_destino OR x.origen = d.id_destino);
PRINT CONCAT('Destinos sin uso borrados: ', @@ROWCOUNT);

SELECT COUNT(*) estaciones, (SELECT COUNT(*) FROM Eurovip.estacion WHERE id_estacion NOT LIKE '[A-Z][A-Z][A-Z]') codigos_raros,
       (SELECT COUNT(*) FROM Eurovip.estacion e WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = e.id_destino AND d.id_pais = e.id_pais)) sin_destino
FROM Eurovip.estacion;
COMMIT;


/* ##########################################################################################
   DATOS DE PRUEBA Y RESPALDOS 15/10/2026 (explicación en la BITÁCORA). Scripts tal cual se aplicaron.
   ########################################################################################## */
/* 15/10/2026 — Tasas de prueba y tablas de respaldo (servidor 93, esquema Eurovip). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
-- Tasas de prueba que ninguna oferta usa.
DELETE t FROM Eurovip.tasa t
WHERE t.id_tasa IN ('f', 'Prueba', 'Prueba3')
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_tasa s WHERE s.id_tasa = t.id_tasa);
PRINT CONCAT('Tasas de prueba borradas: ', @@ROWCOUNT);
-- Tablas de respaldo de las limpiezas (a pedido de la usuaria).
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'DROP TABLE Eurovip.' + QUOTENAME(name) + N';' + CHAR(10)
FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'Eurovip' AND (name LIKE '%[_]respaldo[_]%' OR name LIKE '%[_]borradas[_]%');
EXEC sp_executesql @sql;
PRINT CONCAT('Tablas de respaldo borradas: ', (LEN(@sql) - LEN(REPLACE(@sql, 'DROP TABLE', ''))) / 10);
COMMIT;
SELECT COUNT(*) quedan_respaldos FROM sys.tables WHERE SCHEMA_NAME(schema_id) = 'Eurovip' AND (name LIKE '%respaldo%' OR name LIKE '%borradas%');
SELECT COUNT(*) quedan_tasas_prueba FROM Eurovip.tasa WHERE id_tasa IN ('f', 'Prueba', 'Prueba3');

/* 15/10/2026 — Datos de prueba que nadie usa (servidor 93, esquema Eurovip). */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
SELECT a.id_alojamiento, a.nombre, a.id_destino,
       (SELECT COUNT(*) FROM Eurovip.servicio_detalle s WHERE s.id_alojamiento = a.id_alojamiento) ofertas,
       (SELECT COUNT(*) FROM Eurovip.servicio_circuito_alojamiento s WHERE s.id_alojamiento = a.id_alojamiento) circuitos,
       (SELECT COUNT(*) FROM Eurovip.alojamiento_habitacion h WHERE h.id_alojamiento = a.id_alojamiento) habitaciones
FROM Eurovip.alojamiento a WHERE a.nombre LIKE 'prueba%' OR a.nombre LIKE 'test%';
-- Hoteles de prueba sin ofertas ni circuitos (con sus habitaciones).
IF OBJECT_ID('tempdb..#h') IS NOT NULL DROP TABLE #h;
SELECT a.id_alojamiento INTO #h FROM Eurovip.alojamiento a
WHERE (a.nombre LIKE 'prueba%' OR a.nombre LIKE 'test%')
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_detalle s WHERE s.id_alojamiento = a.id_alojamiento)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_habitacion s WHERE s.id_alojamiento = a.id_alojamiento)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_circuito_alojamiento s WHERE s.id_alojamiento = a.id_alojamiento)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento_destino s WHERE s.id_alojamiento = a.id_alojamiento);
DELETE h FROM Eurovip.alojamiento_habitacion h JOIN #h x ON x.id_alojamiento = h.id_alojamiento;
PRINT CONCAT('Habitaciones de hoteles de prueba borradas: ', @@ROWCOUNT);
DELETE a FROM Eurovip.alojamiento a JOIN #h x ON x.id_alojamiento = a.id_alojamiento;
PRINT CONCAT('Hoteles de prueba borrados: ', @@ROWCOUNT);
-- Proveedor de prueba sin uso.
DELETE p FROM Eurovip.proveedor p WHERE (p.nombre LIKE 'prueba%' OR p.nombre LIKE 'test%')
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio s WHERE s.id_Proveedor = p.id_proveedor)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.circuito c WHERE c.id_proveedor = p.id_proveedor)
  AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_itinerario i WHERE i.id_proveedor = p.id_proveedor);
PRINT CONCAT('Proveedores de prueba borrados: ', @@ROWCOUNT);
COMMIT;
SELECT 'hotel' t, nombre FROM Eurovip.alojamiento WHERE nombre LIKE 'prueba%' OR nombre LIKE 'test%' UNION ALL SELECT 'proveedor', nombre FROM Eurovip.proveedor WHERE nombre LIKE 'prueba%' OR nombre LIKE 'test%';


/* ##########################################################################################
   INICIO HOTEL 16/10/2026 (explicación en la BITÁCORA). Script tal cual se aplicó.
   ########################################################################################## */
/* Copia de ViajeVDT.dbo.inicio (tipo_servicio = 'Hotel') a TropitoursVDT.Eurovip.inicio — 16/10/2026.
   ViajeVDT (10.10.10.29) solo se leyó. Al copiar:
   • tipo_servicio 'HOTEL' (como los que ya tiene Eurovip) y moneda DOP (todo Eurovip pasó de EUR a DOP el 14/10).
   • Espacios de más fuera; descripción con el formato de la pantalla de Hoteles: "Régimen -Código".
   • El destino de cada tarjeta (caratulaDestino) tiene que existir en Destinos: se crean los que faltaban
     (Costa del Sol, Costa Brava, Costa Blanca, Costa de la Luz, Costa Dorada — España) y se escriben igual.
   • No se repite: si ya hay una tarjeta HOTEL con el mismo id_servicio, se salta. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @json NVARCHAR(MAX) = N'[{"id_servicio":25033693,"id_idioma":"ES","tipo_servicio":"HOTEL","id_campanya":99,"id_moneda":"EUR","id_pais_origen":"ES","origen":"Málaga","id_paisDestino":"ES","id_destino":"Hotel Kristal","caratulaDestino":"Costa del sol ","descripcion":"Solo Habitación-603258","latitud":"184861","longitud":"-699312","precioWeb":62.0000,"id_contenedor":47,"contenedor":"HOTEL","posicion_contenedor":4,"posicion_destino":1,"estado":true,"notas":null,"noches":null},{"id_servicio":25050144,"id_idioma":"ES","tipo_servicio":"Hotel","id_campanya":99,"id_moneda":"EUR","id_pais_origen":"ES","origen":"Costa Brava","id_paisDestino":"ES","id_destino":"Costa Brava Blanes","caratulaDestino":"Costa Brava ","descripcion":"Alojamiento y Desayuno -6023506","latitud":null,"longitud":null,"precioWeb":77.0000,"id_contenedor":47,"contenedor":"HOTEL","posicion_contenedor":4,"posicion_destino":2,"estado":true,"notas":null,"noches":null},{"id_servicio":25050145,"id_idioma":"ES","tipo_servicio":"Hotel","id_campanya":99,"id_moneda":"EUR","id_pais_origen":"ES","origen":"Costa Blanca","id_paisDestino":"ES","id_destino":"Hotel Adsubia","caratulaDestino":"Costa Blanca ","descripcion":"Solo Habitación -6221339","latitud":null,"longitud":null,"precioWeb":56.0000,"id_contenedor":47,"contenedor":"HOTEL","posicion_contenedor":4,"posicion_destino":3,"estado":true,"notas":null,"noches":null},{"id_servicio":25050146,"id_idioma":"ES","tipo_servicio":"Hotel","id_campanya":99,"id_moneda":"EUR","id_pais_origen":"ES","origen":"Costa Dorada","id_paisDestino":"ES","id_destino":"Hotel El Convento","caratulaDestino":"Costa de la Luz","descripcion":"Solo Habitación -6046406","latitud":null,"longitud":null,"precioWeb":55.0000,"id_contenedor":47,"contenedor":"HOTEL","posicion_contenedor":4,"posicion_destino":4,"estado":true,"notas":null,"noches":null},{"id_servicio":25050147,"id_idioma":"ES","tipo_servicio":"Hotel","id_campanya":99,"id_moneda":"EUR","id_pais_origen":"ES","origen":"Costa de la Luz","id_paisDestino":"ES","id_destino":"4R Salou Park Resort I   ","caratulaDestino":"Costa Dorada","descripcion":"Room Only -6025856","latitud":null,"longitud":null,"precioWeb":89.0000,"id_contenedor":47,"contenedor":"HOTEL","posicion_contenedor":4,"posicion_destino":5,"estado":true,"notas":null,"noches":null}]';

-- 1) Destinos que faltan
DECLARE @nuevos TABLE (id_destino varchar(30));
INSERT @nuevos VALUES ('Costa del Sol'), ('Costa Brava'), ('Costa Blanca'), ('Costa de la Luz'), ('Costa Dorada');
DECLARE @maxid int = ISNULL((SELECT MAX(id) FROM Eurovip.destino), 0);
INSERT INTO Eurovip.destino (id_destino, id_idioma, id_pais, id)
SELECT n.id_destino, 'ES', 'ES', @maxid + ROW_NUMBER() OVER (ORDER BY n.id_destino)
FROM @nuevos n WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = n.id_destino AND d.id_idioma = 'ES');
PRINT CONCAT('Destinos creados: ', @@ROWCOUNT);

-- 2) Tarjetas de hotel
;WITH j AS (
    SELECT * FROM OPENJSON(@json) WITH (
        id_servicio int, id_idioma char(2), tipo_servicio varchar(20), id_campanya smallint, id_moneda char(3),
        id_pais_origen char(2), origen varchar(30), id_paisDestino char(2), id_destino varchar(1500), caratulaDestino varchar(300),
        descripcion varchar(max), latitud varchar(50), longitud varchar(50), precioWeb decimal(19,4), id_contenedor int,
        contenedor varchar(30), posicion_contenedor int, posicion_destino int, estado bit, notas nvarchar(1000), noches int)
), limpio AS (
    SELECT j.*,
           LTRIM(RTRIM(REPLACE(REPLACE(j.id_destino, '   ', ' '), '  ', ' '))) AS nombre,
           LTRIM(RTRIM(j.caratulaDestino)) AS dest,
           -- "Solo Habitación-603258" / "Solo Habitación -603258" -> "Solo Habitación -603258"
           CASE WHEN CHARINDEX('-', REVERSE(j.descripcion)) > 0
                THEN LTRIM(RTRIM(LEFT(j.descripcion, LEN(j.descripcion) - CHARINDEX('-', REVERSE(j.descripcion))))) + ' -'
                     + LTRIM(RTRIM(RIGHT(j.descripcion, CHARINDEX('-', REVERSE(j.descripcion)) - 1)))
                ELSE LTRIM(RTRIM(j.descripcion)) END AS descr
    FROM j
)
INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino,
                            id_destino, caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor,
                            posicion_contenedor, posicion_destino, estado, notas, noches)
SELECT l.id_servicio, l.id_idioma, 'HOTEL', l.id_campanya, 'DOP', l.id_pais_origen, LTRIM(RTRIM(l.origen)), l.id_paisDestino,
       l.nombre,
       ISNULL((SELECT TOP 1 d.id_destino FROM Eurovip.destino d WHERE d.id_destino = l.dest AND d.id_idioma = 'ES'), l.dest),
       l.descr, l.latitud, l.longitud, l.precioWeb, l.id_contenedor, l.contenedor, l.posicion_contenedor, l.posicion_destino,
       l.estado, l.notas, l.noches
FROM limpio l
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.inicio i WHERE i.id_servicio = l.id_servicio AND i.id_idioma = l.id_idioma AND i.tipo_servicio = 'HOTEL');
PRINT CONCAT('Tarjetas de hotel copiadas: ', @@ROWCOUNT);

SELECT id, id_servicio, id_moneda, origen, id_destino, caratulaDestino, descripcion, CAST(precioWeb AS decimal(10,2)) precio, id_contenedor, estado,
       CASE WHEN EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = i.caratulaDestino AND d.id_idioma = 'ES') THEN 'ok' ELSE 'DESTINO NO EXISTE' END destino_ok
FROM Eurovip.inicio i WHERE tipo_servicio = 'HOTEL' ORDER BY id;
COMMIT;


/* ##########################################################################################
   CIRCUITOS 16/10/2026 (explicación en la BITÁCORA). Script tal cual se aplicó; los datos (JSON) se
   leyeron de ViajeVDT con las consultas indicadas en cada variable y no se repiten aquí por tamaño (1,7 MB).
   ########################################################################################## */
/* Copia de los CIRCUITOS de ViajeVDT (10.10.10.29, idioma ES) a TropitoursVDT.Eurovip — 16/10/2026.
   ViajeVDT solo se leyó. Tablas: circuito, circuito_destino, circuito_itinerario, servicio_circuito_alojamiento,
   servicio (tipo CIRCUITO), servicio_circuito, servicio_itinerario (vuelos), servicio_tasa, alojamiento_circuito, inicio.
   servicio_detalle / servicio_habitacion / servicio_descripcion: en ViajeVDT no tienen filas de circuitos (nada que copiar).
   Todas las tablas ya existían en Eurovip con la misma estructura: no hubo que crear ninguna.
   Al copiar:
   • Solo idioma ES (Eurovip trabaja en ES). Fuera los circuitos de prueba «Hola 1» y «Prueba 2».
   • Moneda DOP (como todo Eurovip desde el 14/10); los importes no cambian.
   • id_servicio nuevo (el contador de Eurovip); la relación vieja→nueva se guarda en @map para las tablas hijas.
   • servicio_circuito: hasta = desde + noches y origen ES/Madrid, como las cargas de Eurovip.
   • Hoteles: 331 (Yodia Heritage) → 322 y 10219 (Cartagena Plaza) → 10218 (las copias repetidas se borraron en la
     limpieza); 10387 Hotel Arslan Boutique (Khiva) no existía y se crea.
   • inicio: 6 tarjetas CIRCUITO; si su id_servicio no está entre los copiados se usa la salida más barata de ese circuito.
     caratulaDestino = nombre del circuito sin espacios + .webp (regla de carátulas de circuito).
   • No se repite: lo que ya existe (mismo nombre / mismo código de carga) se salta. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @jCirc NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.circuito (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jDest NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.circuito_destino (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jItin NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.circuito_itinerario (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jSca  NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.servicio_circuito_alojamiento (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jServ NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.servicio (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jSc   NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.servicio_circuito (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jSi   NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.servicio_itinerario (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jTasa NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.servicio_tasa (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jAc   NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.alojamiento_circuito (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jIni  NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.inicio (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @jHot  NVARCHAR(MAX) = N'/* datos de ViajeVDT: SELECT ... FROM dbo.alojamiento (id 10387) (ES) FOR JSON PATH, INCLUDE_NULL_VALUES */';
DECLARE @n int;

-- 0) Hotel que falta (Hotel Arslan Boutique, Khiva)
INSERT INTO Eurovip.alojamiento (id_alojamiento, id_idioma, id_tipo_alojamiento, id_destino, id_pais, nombre, caratula, descripcion,
                                 grupo, categoria, latitude, longitude, categoria_circuito, circuito, activo)
SELECT h.id_alojamiento, h.id_idioma, h.id_tipo_alojamiento, LTRIM(RTRIM(h.id_destino)), h.id_pais, LTRIM(RTRIM(h.nombre)), h.caratula,
       h.descripcion, h.grupo, h.categoria, h.latitude, h.longitude, h.categoria_circuito, h.circuito, 1
FROM OPENJSON(@jHot) WITH (id_alojamiento int, id_idioma char(2), id_tipo_alojamiento varchar(30), id_destino varchar(100), id_pais char(2),
     nombre varchar(200), caratula varchar(500), descripcion varchar(max), grupo varchar(50), categoria varchar(20), latitude varchar(50),
     longitude varchar(50), categoria_circuito varchar(50), circuito bit) h
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento a WHERE a.id_alojamiento = h.id_alojamiento);
PRINT CONCAT('Hoteles creados: ', @@ROWCOUNT);

-- 1) circuito
INSERT INTO Eurovip.circuito (nombre_circuito, id_idioma, id_proveedor, noches, caratula, itinerario, id)
SELECT LTRIM(RTRIM(c.nombre_circuito)), c.id_idioma, c.id_proveedor, c.noches, c.caratula, c.itinerario, LTRIM(RTRIM(c.id))
FROM OPENJSON(@jCirc) WITH (nombre_circuito varchar(100), id_idioma char(2), id_proveedor varchar(10), noches smallint,
     caratula varchar(500), itinerario varchar(max), id varchar(100)) c
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.circuito e WHERE e.nombre_circuito = LTRIM(RTRIM(c.nombre_circuito)) AND e.id_idioma = c.id_idioma);
PRINT CONCAT('Circuitos copiados: ', @@ROWCOUNT);

-- 2) circuito_destino (destino escrito como en Eurovip.destino)
INSERT INTO Eurovip.circuito_destino (nombre_circuito, id_destino, id_idioma, id_pais, id)
SELECT LTRIM(RTRIM(x.nombre_circuito)), ISNULL(d.id_destino, LTRIM(RTRIM(x.id_destino))), x.id_idioma, x.id_pais, x.id
FROM OPENJSON(@jDest) WITH (nombre_circuito varchar(100), id_destino varchar(100), id_idioma char(2), id_pais char(2), id varchar(100)) x
OUTER APPLY (SELECT TOP 1 id_destino FROM Eurovip.destino d WHERE d.id_destino = LTRIM(RTRIM(x.id_destino)) AND d.id_idioma = 'ES') d
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.circuito_destino e WHERE e.nombre_circuito = LTRIM(RTRIM(x.nombre_circuito))
                  AND e.id_destino = LTRIM(RTRIM(x.id_destino)) AND e.id_idioma = x.id_idioma);
PRINT CONCAT('Destinos de circuito: ', @@ROWCOUNT);

-- 3) circuito_itinerario (día a día)
INSERT INTO Eurovip.circuito_itinerario (dia, titulo, nombre_circuito, id_idioma, descripcion, visitas, regimen, transporte, tipo_alojamiento, caratula)
SELECT x.dia, LTRIM(RTRIM(x.titulo)), LTRIM(RTRIM(x.nombre_circuito)), x.id_idioma, x.descripcion, x.visitas, x.regimen, x.transporte, x.tipo_alojamiento, x.caratula
FROM OPENJSON(@jItin) WITH (dia int, titulo varchar(500), nombre_circuito varchar(100), id_idioma char(2), descripcion varchar(max),
     visitas varchar(max), regimen varchar(200), transporte varchar(200), tipo_alojamiento varchar(200), caratula varchar(500)) x
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.circuito_itinerario e WHERE e.nombre_circuito = LTRIM(RTRIM(x.nombre_circuito)) AND e.dia = x.dia AND e.id_idioma = x.id_idioma);
PRINT CONCAT('Días de itinerario: ', @@ROWCOUNT);

-- 4) servicio_circuito_alojamiento (hoteles del circuito)
;WITH x AS (
    SELECT LTRIM(RTRIM(nombre_circuito)) nombre_circuito, id_idioma,
           CASE id_alojamiento WHEN 331 THEN 322 WHEN 10219 THEN 10218 ELSE id_alojamiento END id_alojamiento, categoria, caratula
    FROM OPENJSON(@jSca) WITH (nombre_circuito varchar(100), id_idioma char(2), id_alojamiento int, categoria varchar(50), caratula varchar(500))
)
INSERT INTO Eurovip.servicio_circuito_alojamiento (nombre_circuito, id_idioma, id_alojamiento, categoria, caratula)
SELECT DISTINCT x.nombre_circuito, x.id_idioma, x.id_alojamiento, x.categoria, x.caratula FROM x
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.servicio_circuito_alojamiento e WHERE e.nombre_circuito = x.nombre_circuito AND e.id_idioma = x.id_idioma
                  AND e.id_alojamiento = x.id_alojamiento AND e.categoria = x.categoria);
PRINT CONCAT('Hoteles de circuito: ', @@ROWCOUNT);

-- 5) servicio (salidas) con id nuevo; @map = viejo -> nuevo
DECLARE @map TABLE (viejo int PRIMARY KEY, nuevo int);
MERGE Eurovip.servicio AS t
USING (
    SELECT s.* FROM OPENJSON(@jServ) WITH (id_servicio int, id_tipo_servicio varchar(20), id_idioma char(2), id_campanya smallint,
           id_Proveedor varchar(10), apertura date, usuario varchar(50), precio_base money, descuento numeric(18,2), precio_mas_menos money,
           ajuste money, nota varchar(max), noches smallint, codigo varchar(50), baja date, nombre varchar(200), activo bit,
           precio numeric(18,2), categoria_circuito varchar(50)) s
    WHERE NOT EXISTS (SELECT 1 FROM Eurovip.servicio e WHERE e.codigo = s.codigo)
) AS s ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (id_tipo_servicio, id_idioma, id_moneda, id_campanya, id_Proveedor, apertura, usuario, precio_base, descuento, precio_mas_menos,
            ajuste, nota, noches, codigo, baja, nombre, activo, categoria_circuito)
    VALUES (s.id_tipo_servicio, s.id_idioma, 'DOP', s.id_campanya, s.id_Proveedor, s.apertura, s.usuario, s.precio_base, s.descuento,
            s.precio_mas_menos, s.ajuste, LTRIM(RTRIM(s.nota)), s.noches, s.codigo, s.baja, LTRIM(RTRIM(s.nombre)), s.activo, s.categoria_circuito)
OUTPUT s.id_servicio, inserted.id_servicio INTO @map (viejo, nuevo);
SELECT @n = COUNT(*) FROM @map; PRINT CONCAT('Salidas (servicio) copiadas: ', @n);

-- 6) servicio_circuito (fecha de salida)
INSERT INTO Eurovip.servicio_circuito (id_servicio, nombre_circuito, id_idoma, desde, hasta, id_pais_origen, origen)
SELECT m.nuevo, LTRIM(RTRIM(x.nombre_circuito)), x.id_idoma, x.desde,
       DATEADD(day, ISNULL((SELECT s.noches FROM Eurovip.servicio s WHERE s.id_servicio = m.nuevo), 0), x.desde), 'ES', 'Madrid'
FROM OPENJSON(@jSc) WITH (id_servicio int, nombre_circuito varchar(100), id_idoma char(2), desde date, hasta date) x
JOIN @map m ON m.viejo = x.id_servicio;
PRINT CONCAT('Fechas de salida: ', @@ROWCOUNT);

-- 7) servicio_itinerario (vuelos)
INSERT INTO Eurovip.servicio_itinerario (id_servicio, segmento, id_proveedor, id_estacion_origen, origen, id_idioma, id_pais_origen,
            id_estacion_destino, destino, id_pais_destino, vuelo, fecha_salida, hora_salida, fecha_regreso, hora_regreso, clase, conexion)
SELECT m.nuevo, x.segmento, x.id_proveedor, x.id_estacion_origen, LTRIM(RTRIM(x.origen)), x.id_idioma, x.id_pais_origen,
       x.id_estacion_destino, LTRIM(RTRIM(x.destino)), x.id_pais_destino, LTRIM(RTRIM(x.vuelo)), x.fecha_salida, x.hora_salida,
       x.fecha_regreso, x.hora_regreso, x.clase, x.conexion
FROM OPENJSON(@jSi) WITH (id_servicio int, segmento smallint, id_proveedor varchar(10), id_estacion_origen varchar(10), origen varchar(100),
     id_idioma char(2), id_pais_origen char(2), id_estacion_destino varchar(10), destino varchar(100), id_pais_destino char(2), vuelo varchar(20),
     fecha_salida date, hora_salida varchar(10), fecha_regreso date, hora_regreso varchar(10), clase char(1), conexion bit) x
JOIN @map m ON m.viejo = x.id_servicio;
PRINT CONCAT('Vuelos: ', @@ROWCOUNT);

-- 8) servicio_tasa
INSERT INTO Eurovip.servicio_tasa (id_Servicio, id_tasa, id_idioma, valor)
SELECT m.nuevo, x.id_tasa, x.id_idioma, x.valor
FROM OPENJSON(@jTasa) WITH (id_Servicio int, id_tasa varchar(100), id_idioma char(2), valor money) x
JOIN @map m ON m.viejo = x.id_Servicio;
PRINT CONCAT('Tasas: ', @@ROWCOUNT);

-- 9) alojamiento_circuito
INSERT INTO Eurovip.alojamiento_circuito (id_servicio, id_idioma, id_tipo_servicio, id_campanya, id_moneda, id_pais_origen, id_origen,
            id_pais_destino, id_destino, caratula, precio_web, nombre_circuito)
SELECT m.nuevo, x.id_idioma, x.id_tipo_servicio, x.id_campanya, 'DOP', x.id_pais_origen, LTRIM(RTRIM(x.id_origen)), x.id_pais_destino,
       LTRIM(RTRIM(x.id_destino)), x.caratula, x.precio_web, LTRIM(RTRIM(x.nombre_circuito))
FROM OPENJSON(@jAc) WITH (id_servicio int, id_idioma char(2), id_tipo_servicio varchar(20), id_campanya smallint, id_pais_origen char(2),
     id_origen varchar(50), id_pais_destino varchar(10), id_destino varchar(100), caratula varchar(500), precio_web money, nombre_circuito varchar(100)) x
JOIN @map m ON m.viejo = x.id_servicio;
PRINT CONCAT('Alojamiento_circuito: ', @@ROWCOUNT);

-- 10) inicio: tarjetas CIRCUITO de la HOME
;WITH j AS (
    SELECT * FROM OPENJSON(@jIni) WITH (id_servicio int, id_idioma char(2), id_campanya smallint, id_pais_origen char(2), origen varchar(30),
           id_paisDestino char(2), id_destino varchar(1500), descripcion varchar(max), latitud varchar(50), longitud varchar(50),
           precioWeb decimal(19,4), id_contenedor int, contenedor varchar(30), posicion_contenedor int, posicion_destino int, estado bit,
           notas nvarchar(1000), noches int)
)
INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino,
                            id_destino, caratulaDestino, descripcion, latitud, longitud, precioWeb, id_contenedor, contenedor,
                            posicion_contenedor, posicion_destino, estado, notas, noches)
SELECT COALESCE(m.nuevo, barata.id_servicio), j.id_idioma, 'CIRCUITO', j.id_campanya, 'DOP', j.id_pais_origen, LTRIM(RTRIM(j.origen)),
       j.id_paisDestino, LTRIM(RTRIM(j.id_destino)), REPLACE(LTRIM(RTRIM(j.descripcion)), ' ', '') + '.webp', LTRIM(RTRIM(j.descripcion)),
       j.latitud, j.longitud, j.precioWeb, j.id_contenedor, j.contenedor, j.posicion_contenedor, j.posicion_destino, j.estado, j.notas, j.noches
FROM j
LEFT JOIN @map m ON m.viejo = j.id_servicio
OUTER APPLY (SELECT TOP 1 s.id_servicio FROM Eurovip.servicio s JOIN Eurovip.servicio_circuito c ON c.id_servicio = s.id_servicio
             WHERE c.nombre_circuito = LTRIM(RTRIM(j.descripcion)) AND s.id_servicio IN (SELECT nuevo FROM @map) ORDER BY s.precio) barata
WHERE COALESCE(m.nuevo, barata.id_servicio) IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Eurovip.inicio i WHERE i.tipo_servicio = 'CIRCUITO' AND i.descripcion = LTRIM(RTRIM(j.descripcion)));
PRINT CONCAT('Tarjetas CIRCUITO en inicio: ', @@ROWCOUNT);

-- Comprobación
SELECT 'circuitos' t, COUNT(*) n FROM Eurovip.circuito
UNION ALL SELECT 'salidas', COUNT(*) FROM Eurovip.servicio WHERE id_tipo_servicio = 'CIRCUITO'
UNION ALL SELECT 'salidas futuras', COUNT(*) FROM Eurovip.servicio_circuito WHERE desde > CAST(GETDATE() AS date)
UNION ALL SELECT 'salidas sin fecha', COUNT(*) FROM Eurovip.servicio s WHERE id_tipo_servicio = 'CIRCUITO' AND NOT EXISTS (SELECT 1 FROM Eurovip.servicio_circuito c WHERE c.id_servicio = s.id_servicio)
UNION ALL SELECT 'circuitos sin destino', COUNT(*) FROM Eurovip.circuito c WHERE NOT EXISTS (SELECT 1 FROM Eurovip.circuito_destino d WHERE d.nombre_circuito = c.nombre_circuito)
UNION ALL SELECT 'destinos no existen', COUNT(*) FROM Eurovip.circuito_destino cd WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = cd.id_destino AND d.id_idioma = 'ES')
UNION ALL SELECT 'moneda no DOP', COUNT(*) FROM Eurovip.servicio WHERE id_moneda <> 'DOP';
SELECT id, id_servicio, descripcion, id_destino, caratulaDestino, CAST(precioWeb AS decimal(10,2)) precio FROM Eurovip.inicio WHERE tipo_servicio = 'CIRCUITO' ORDER BY id;
COMMIT;


/* ##########################################################################################
   EXCURSIONES LA ROMANA Y SAMANÁ 16/10/2026 (explicación en la BITÁCORA). Script tal cual se aplicó.
   ########################################################################################## */
/* Copia de las excursiones de La Romana y Samaná de ViajeVDT.dbo.servicio_excursion a TropitoursVDT.Eurovip.servicio_excursion
   — 16/10/2026. ViajeVDT (10.10.10.29) solo se leyó. 24 excursiones: La Romana 18 y Samaná 6 (ninguna trae descripción).
   • Mismo número (id_Excursion) que en ViajeVDT cuando está libre en Eurovip; si ya lo usa otra excursión, número nuevo.
   • Destino escrito como en Eurovip.destino ('La Romana', 'Samaná'); nombres sin espacios de más.
   • No se repite: si ya hay una excursión con el mismo nombre en ese destino, se salta.
   • Al final se actualizan las tarjetas de la Home (una por destino) con spActualizaInicioExcursion. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @json NVARCHAR(MAX) = N'[{"id_Excursion":37,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Isla Saona (desde Bayahibe\/La Romana)","descripcion":"","cancelable":false,"precio":68.0000},{"id_Excursion":38,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Santo Domingo City Tour (desde Bayahibe\/La Romana)","descripcion":"","cancelable":false,"precio":82.0000},{"id_Excursion":39,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Bayahibe Runners","descripcion":null,"cancelable":false,"precio":82.0000},{"id_Excursion":188,"id_destino":"Samaná","id_tipo_servicio":"Excursion","nombre":"Excursión Limón a Caballo","descripcion":null,"cancelable":false,"precio":48.0000},{"id_Excursion":189,"id_destino":"Samaná","id_tipo_servicio":"Excursion","nombre":"Excursión Monkeyland","descripcion":null,"cancelable":false,"precio":63.0000},{"id_Excursion":190,"id_destino":"Samaná","id_tipo_servicio":"Excursion","nombre":"Excursión Tirolesas","descripcion":"","cancelable":false,"precio":73.0000},{"id_Excursion":191,"id_destino":"Samaná","id_tipo_servicio":"Excursion","nombre":"Excursión Playa Rincón Samaná","descripcion":null,"cancelable":false,"precio":82.0000},{"id_Excursion":192,"id_destino":"Samaná","id_tipo_servicio":"Excursion","nombre":"Excursión Cascada el Limón","descripcion":null,"cancelable":false,"precio":82.0000},{"id_Excursion":193,"id_destino":"Samaná","id_tipo_servicio":"Excursion","nombre":"Excursión Tirolesas + Monkeyland ","descripcion":null,"cancelable":false,"precio":87.0000},{"id_Excursion":311,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Zipline Adventure","descripcion":"","cancelable":false,"precio":87.5000},{"id_Excursion":312,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Saona Blue Lagoon","descripcion":null,"cancelable":false,"precio":99.0000},{"id_Excursion":313,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Zipline Adventure","descripcion":"","cancelable":false,"precio":87.5000},{"id_Excursion":314,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Saona Blue Lagoon","descripcion":null,"cancelable":false,"precio":99.0000},{"id_Excursion":315,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Coco Bongo Regular Entrance","descripcion":null,"cancelable":false,"precio":101.0000},{"id_Excursion":316,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Zipline Adventure","descripcion":"","cancelable":false,"precio":87.5000},{"id_Excursion":317,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Saona Blue Lagoon","descripcion":null,"cancelable":false,"precio":99.0000},{"id_Excursion":318,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Zipline Adventure","descripcion":"","cancelable":false,"precio":87.5000},{"id_Excursion":319,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Saona Blue Lagoon","descripcion":null,"cancelable":false,"precio":99.0000},{"id_Excursion":320,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Zipline Adventure","descripcion":"","cancelable":false,"precio":87.5000},{"id_Excursion":321,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Saona Blue Lagoon","descripcion":null,"cancelable":false,"precio":99.0000},{"id_Excursion":322,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Zipline Adventure","descripcion":"","cancelable":false,"precio":87.5000},{"id_Excursion":323,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Saona Blue Lagoon","descripcion":null,"cancelable":false,"precio":99.0000},{"id_Excursion":324,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Coco Bongo Regular Entrance","descripcion":null,"cancelable":false,"precio":101.0000},{"id_Excursion":325,"id_destino":"La Romana","id_tipo_servicio":"Excursion","nombre":"Excursión Samaná, Cascada El Limón e Isla Bacardi desde Bayahibe","descripcion":"","cancelable":false,"precio":163.5000}]';
DECLARE @j TABLE (id int, destino varchar(50), tipo varchar(20), nombre varchar(500), descripcion varchar(max), cancelable bit, precio money);
INSERT @j
SELECT j.id_Excursion,
       ISNULL((SELECT TOP 1 d.id_destino FROM Eurovip.destino d WHERE d.id_destino = LTRIM(RTRIM(j.id_destino)) AND d.id_idioma = 'ES'), LTRIM(RTRIM(j.id_destino))),
       ISNULL(j.id_tipo_servicio, 'Excursion'),
       LTRIM(RTRIM(REPLACE(REPLACE(j.nombre, '   ', ' '), '  ', ' '))), NULLIF(LTRIM(RTRIM(j.descripcion)), ''), j.cancelable, j.precio
FROM OPENJSON(@json) WITH (id_Excursion int, id_destino varchar(50), id_tipo_servicio varchar(20), nombre varchar(500), descripcion varchar(max), cancelable bit, precio money) j;
DELETE x FROM @j x WHERE EXISTS (SELECT 1 FROM Eurovip.servicio_excursion e WHERE e.nombre = x.nombre AND e.id_destino = x.destino);
PRINT CONCAT('Ya estaban (se saltan): ', @@ROWCOUNT);

-- 1) Con el mismo número (libre en Eurovip)
SET IDENTITY_INSERT Eurovip.servicio_excursion ON;
INSERT INTO Eurovip.servicio_excursion (id_Excursion, id_destino, id_tipo_servicio, nombre, descripcion, cancelable, precio)
SELECT x.id, x.destino, x.tipo, x.nombre, x.descripcion, x.cancelable, x.precio FROM @j x
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.servicio_excursion e WHERE e.id_Excursion = x.id);
PRINT CONCAT('Copiadas con el mismo número: ', @@ROWCOUNT);
SET IDENTITY_INSERT Eurovip.servicio_excursion OFF;

-- 2) Con número nuevo (el suyo ya lo usa otra excursión)
INSERT INTO Eurovip.servicio_excursion (id_destino, id_tipo_servicio, nombre, descripcion, cancelable, precio)
SELECT x.destino, x.tipo, x.nombre, x.descripcion, x.cancelable, x.precio FROM @j x
WHERE NOT EXISTS (SELECT 1 FROM Eurovip.servicio_excursion e WHERE e.nombre = x.nombre AND e.id_destino = x.destino)
ORDER BY x.id;
PRINT CONCAT('Copiadas con número nuevo: ', @@ROWCOUNT);

-- 3) Tarjetas de la Home
EXEC Eurovip.spActualizaInicioExcursion 'La Romana';
EXEC Eurovip.spActualizaInicioExcursion 'Samaná';

SELECT id_destino, COUNT(*) n, MIN(precio) desde FROM Eurovip.servicio_excursion GROUP BY id_destino;
SELECT id, id_servicio, id_destino, descripcion, CAST(precioWeb AS decimal(10,2)) precio, contenedor FROM Eurovip.inicio WHERE tipo_servicio = 'EXCURSION';
SELECT 'destino no existe' t, COUNT(*) FROM Eurovip.servicio_excursion e WHERE NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = e.id_destino AND d.id_idioma = 'ES');
COMMIT;
