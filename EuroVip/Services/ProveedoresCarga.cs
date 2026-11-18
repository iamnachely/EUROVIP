using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using EuroVip.Models;
using EuroVip.Repositories;

namespace EuroVip.Services
{
    // =====================================================================================================================
    // CAMBIO 140 · Cargas de PROVEEDORES (fase 1).
    // Pedido de la usuaria: «usuarios Proveedor (empresas externas) cargan productos en Solo Hotel, Paquetes, Circuitos,
    // Excursiones, Traslados y Cruceros. Super-Admin y Super-Comercial (y el personal del BackEurovip) crean los proveedores,
    // les habilitan qué pueden cargar, revisan, aprueban/rechazan (con motivo e instrucciones) y dan de alta en la web al
    // instante o programado. Cada carga se agrupa con un CÓDIGO; al enviarla a revisión se genera SIEMPRE un FLYER automático
    // con los 4 productos más baratos. El personal recibe ALERTAS. El proveedor solo ve y edita lo suyo.»
    //
    // Decisión de diseño: lo que carga un proveedor NO se escribe en las tablas de la web hasta aprobarse. Se guarda como JSON
    // (los MISMOS datos que mandan los formularios: CargaOfertaDto, CargaCircuitoDto, ExcursionGuardarDto, TrasladoGuardarDto,
    // CruceroDto) en Eurovip.carga_proveedor.datos. Al publicar se usa el MISMO camino de guardado de cada pantalla
    // (CargaOfertaService, CargaCircuitoService, GuardadoProductos) y sus mismas reglas (precio mínimo, código interno…).
    // =====================================================================================================================

    /// <summary>Los módulos que puede cargar un proveedor (los mismos nombres del precio mínimo, CAMBIO 139).</summary>
    public static class ProveedorModulos
    {
        public const string Ofertas = "OFERTAS", Paquetes = "PAQUETES", Circuitos = "CIRCUITOS", Excursiones = "EXCURSIONES", Traslados = "TRASLADOS", Cruceros = "CRUCEROS";

        /// <summary>Nombre en pantalla, plural para el flyer («Hoteles», «Circuitos»…) e icono.</summary>
        public static readonly IReadOnlyDictionary<string, (string Nombre, string Plural, string Icono)> Todos = new Dictionary<string, (string, string, string)>
        {
            [Ofertas] = ("Solo Hotel", "Hoteles", "bi-building"),
            [Paquetes] = ("Paquetes", "Paquetes", "bi-suitcase2"),
            [Circuitos] = ("Circuitos", "Circuitos", "bi-signpost-split"),
            [Excursiones] = ("Excursiones", "Excursiones", "bi-compass"),
            [Traslados] = ("Traslados", "Traslados", "bi-bus-front"),
            [Cruceros] = ("Cruceros", "Cruceros", "bi-water"),
        };

        public static string? Normalizar(string? m) => m?.Trim().ToUpperInvariant() is { } x && Todos.ContainsKey(x) ? x : null;

        /// <summary>«OFERTAS,TRASLADOS» → lista válida sin repetidos (en el orden de Todos).</summary>
        public static List<string> Lista(string? csv)
        {
            var l = (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Normalizar).OfType<string>().ToHashSet();
            return Todos.Keys.Where(l.Contains).ToList();
        }
    }

    public static class EstadosCargaProveedor
    {
        public const string Borrador = "BORRADOR", Pendiente = "PENDIENTE", Rechazada = "RECHAZADA", Programada = "PROGRAMADA", Activa = "ACTIVA";
        public static readonly string[] Todos = { Borrador, Pendiente, Rechazada, Programada, Activa };
        public static string? Normalizar(string? e) => e?.Trim().ToUpperInvariant() is { } x && Todos.Contains(x) ? x : null;
    }

    /// <summary>Una cuenta de proveedor (Eurovip.proveedor_cuenta).</summary>
    public class ProveedorCuenta
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Telefono { get; set; }
        public string Id_Proveedor { get; set; } = "";
        /// <summary>Nombre del proveedor del catálogo (Eurovip.proveedor).</summary>
        public string? Proveedor { get; set; }
        public string Modulos { get; set; } = "";
        public bool Activo { get; set; }
        public int? Id_Gestion { get; set; }
        public string? Usuario { get; set; }
        public DateTime Fecha_Alta { get; set; }
        public string? Usuario_Modificacion { get; set; }
        public DateTime? Fecha_Modificacion { get; set; }
        public int Cargas { get; set; }
        public int Pendientes { get; set; }
        public List<string> ListaModulos => ProveedorModulos.Lista(Modulos);
    }

    /// <summary>Alta o edición de una cuenta (Herramientas › Proveedores).</summary>
    public class ProveedorCuentaDto
    {
        public int? Id { get; set; }
        [Required(ErrorMessage = "Escribe el nombre de la empresa"), StringLength(150, ErrorMessage = "El nombre admite como máximo 150 caracteres")]
        public string Nombre { get; set; } = "";
        [Required(ErrorMessage = "Escribe el correo"), EmailAddress(ErrorMessage = "El correo no es válido"), StringLength(250)]
        public string Email { get; set; } = "";
        [StringLength(30, ErrorMessage = "El teléfono admite como máximo 30 caracteres")] public string? Telefono { get; set; }
        [Required(ErrorMessage = "Elige el proveedor del catálogo"), StringLength(10)] public string Id_Proveedor { get; set; } = "";
        public List<string> Modulos { get; set; } = new();
        public bool Activo { get; set; } = true;
    }

    /// <summary>Una carga de proveedor (Eurovip.carga_proveedor) con el nombre de su cuenta y proveedor.</summary>
    public class CargaProveedor
    {
        public int Id { get; set; }
        public int Id_Cuenta { get; set; }
        public string? Cuenta { get; set; }
        public string? Email { get; set; }
        public string? Id_Proveedor { get; set; }
        public string? Proveedor { get; set; }
        public string Modulo { get; set; } = "";
        public string Codigo { get; set; } = "";
        public string? Titulo { get; set; }
        public string Estado { get; set; } = "";
        public string? Motivo { get; set; }
        public string? Instrucciones { get; set; }
        public DateTime? Activar_En { get; set; }
        public string? Flyer { get; set; }
        public int Productos { get; set; }
        public decimal? Precio_Desde { get; set; }
        public string? Moneda { get; set; }
        public DateTime? Fecha_Solicitud { get; set; }
        public DateTime? Fecha_Revision { get; set; }
        public string? Revisado_Por { get; set; }
        public DateTime? Fecha_Activacion { get; set; }
        public string? Error { get; set; }
        public DateTime Creado_En { get; set; }
        public DateTime? Actualizado_En { get; set; }
        public int Publicados { get; set; }
        [System.Text.Json.Serialization.JsonIgnore] public string Datos { get; set; } = "[]";
    }

    /// <summary>Un producto de la carga tal como se ve en la revisión (leído del JSON).</summary>
    public class CargaProducto
    {
        public int Indice { get; set; }
        public string Nombre { get; set; } = "";
        public string? Detalle { get; set; }
        public string? Destino { get; set; }
        public decimal? Precio { get; set; }
        public string? Moneda { get; set; }
    }

    /// <summary>Lo que se creó en la web al publicar (Eurovip.carga_proveedor_item).</summary>
    public class CargaProveedorItem
    {
        public int Id_Carga { get; set; }
        public string Tabla { get; set; } = "";
        public int Id_Registro { get; set; }
        public string? Nombre { get; set; }
        public decimal? Precio { get; set; }
        public string? Moneda { get; set; }
        /// <summary>¿Sigue a la venta? (existe y está activo).</summary>
        public bool? Activo { get; set; }
        public string? Codigo { get; set; }
        public string? Modulo { get; set; }
    }

    /// <summary>Crear o editar una carga (proveedor). Datos = lista JSON de los DTO del módulo.</summary>
    public class CargaProveedorDto
    {
        public int? Id { get; set; }
        public string Modulo { get; set; } = "";
        public string Codigo { get; set; } = "";
        [StringLength(150, ErrorMessage = "El título admite como máximo 150 caracteres")] public string? Titulo { get; set; }
        public JsonElement Datos { get; set; }
    }

    public class CargaProveedorFiltro
    {
        public string? Estado { get; set; }
        public int? Cuenta { get; set; }
        public string? Modulo { get; set; }
        public string? Q { get; set; }
        public int Pagina { get; set; } = 1;
        public int Tam { get; set; } = 25;
    }

    /// <summary>Un problema de un producto de la carga (Indice empieza en 1; 0 = de la carga entera).</summary>
    public sealed record ErrorProducto(int Indice, string? Producto, string? Campo, string Mensaje);

    public class ProveedoresCarga
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private static readonly Regex CodigoValido = new(@"^[A-Za-z0-9]{1,30}$", RegexOptions.Compiled);
        public const int MaxProductos = 300;

        private readonly ICircuitoRepository _db;
        private readonly ICargaOfertaService _ofertas;
        private readonly ICargaCircuitoService _circuitos;
        private readonly GuardadoProductos _guardado;
        private readonly IFlyerImagenService _flyer;
        private readonly IConfiguration _cfg;
        private readonly ILogger<ProveedoresCarga> _log;

        public ProveedoresCarga(ICircuitoRepository db, ICargaOfertaService ofertas, ICargaCircuitoService circuitos, GuardadoProductos guardado,
                                IFlyerImagenService flyer, IConfiguration cfg, ILogger<ProveedoresCarga> log)
        {
            _db = db; _ofertas = ofertas; _circuitos = circuitos; _guardado = guardado; _flyer = flyer; _cfg = cfg; _log = log;
        }

        // ============================================ CUENTAS ============================================

        private const string SelCuenta = @"SELECT pc.id, pc.nombre, pc.email, pc.telefono, pc.id_proveedor, LTRIM(RTRIM(p.nombre)) AS Proveedor, pc.modulos, pc.activo,
                pc.id_gestion, pc.usuario, pc.fecha_alta, pc.usuario_modificacion, pc.fecha_modificacion,
                (SELECT COUNT(*) FROM Eurovip.carga_proveedor c WHERE c.id_cuenta = pc.id) AS Cargas,
                (SELECT COUNT(*) FROM Eurovip.carga_proveedor c WHERE c.id_cuenta = pc.id AND c.estado = 'PENDIENTE') AS Pendientes
            FROM Eurovip.proveedor_cuenta pc LEFT JOIN Eurovip.proveedor p ON p.id_proveedor = pc.id_proveedor";

        public async Task<List<ProveedorCuenta>> CuentasAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<ProveedorCuenta>(SelCuenta + " ORDER BY pc.activo DESC, pc.nombre")).ToList();
        }

        public async Task<ProveedorCuenta?> CuentaAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<ProveedorCuenta>(SelCuenta + " WHERE pc.id = @id", new { id });
        }

        public async Task<ProveedorCuenta?> CuentaPorEmailAsync(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<ProveedorCuenta>(SelCuenta + " WHERE pc.email = @email", new { email = email.Trim().ToLowerInvariant() });
        }

        /// <summary>Crea o edita una cuenta. Devuelve (id, null) o (null, error).</summary>
        public async Task<(int? Id, string? Error)> GuardarCuentaAsync(ProveedorCuentaDto d, string usuario)
        {
            var errores = ErroresModelo(d);
            if (errores.Count > 0) return (null, errores[0]);
            d.Nombre = ReglasDatos.Limpiar(d.Nombre);
            d.Email = d.Email.Trim().ToLowerInvariant();
            d.Telefono = string.IsNullOrWhiteSpace(d.Telefono) ? null : d.Telefono.Trim();
            d.Id_Proveedor = d.Id_Proveedor.Trim();
            var modulos = ProveedorModulos.Lista(string.Join(",", d.Modulos));
            if (d.Modulos.Any(m => ProveedorModulos.Normalizar(m) is null)) return (null, $"Módulo desconocido. Usa {string.Join(", ", ProveedorModulos.Todos.Keys)}.");
            if (ReglasDatos.Nombre("nombre de la empresa", d.Nombre, 2) is string malNombre) return (null, malNombre);
            using var cn = _db.Conexion();
            if (await CatalogoCargas.ErrorProveedorAsync(cn, d.Id_Proveedor) is string malProv) return (null, malProv);
            if (await cn.ExecuteScalarAsync<int?>("SELECT id FROM Eurovip.proveedor_cuenta WHERE email = @Email AND (@Id IS NULL OR id <> @Id)", new { d.Email, d.Id }) is int otra)
                return (null, $"Ya hay otra cuenta de proveedor con el correo «{d.Email}» (n.º {otra}).");
            var p = new { d.Id, d.Nombre, d.Email, d.Telefono, d.Id_Proveedor, modulos = string.Join(",", modulos), d.Activo, usuario };
            if (d.Id is int id)
            {
                var n = await cn.ExecuteAsync(@"UPDATE Eurovip.proveedor_cuenta SET nombre = @Nombre, email = @Email, telefono = @Telefono, id_proveedor = @Id_Proveedor,
                    modulos = @modulos, activo = @Activo, usuario_modificacion = @usuario, fecha_modificacion = SYSDATETIME() WHERE id = @Id", p);
                return n == 0 ? (null, "Esa cuenta ya no existe. Recarga la página.") : (id, null);
            }
            return (await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.proveedor_cuenta (nombre, email, telefono, id_proveedor, modulos, activo, usuario)
                VALUES (@Nombre, @Email, @Telefono, @Id_Proveedor, @modulos, @Activo, @usuario); SELECT CAST(SCOPE_IDENTITY() AS int);", p), null);
        }

        // ============================================ CARGAS: LECTURA ============================================

        private const string SelCarga = @"SELECT c.id, c.id_cuenta, pc.nombre AS Cuenta, pc.email AS Email, pc.id_proveedor AS Id_Proveedor, LTRIM(RTRIM(p.nombre)) AS Proveedor,
                c.modulo, c.codigo, c.titulo, c.estado, c.motivo, c.instrucciones, c.activar_en, c.flyer, c.productos, c.precio_desde, c.moneda,
                c.fecha_solicitud, c.fecha_revision, c.revisado_por, c.fecha_activacion, c.error, c.creado_en, c.actualizado_en, c.publicados, c.datos
            FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta LEFT JOIN Eurovip.proveedor p ON p.id_proveedor = pc.id_proveedor";

        public async Task<(List<CargaProveedor> Items, int Total)> CargasAsync(CargaProveedorFiltro f)
        {
            var tam = Math.Clamp(f.Tam, 5, 100);
            var pagina = Math.Max(1, f.Pagina);
            var p = new
            {
                estado = EstadosCargaProveedor.Normalizar(f.Estado), cuenta = f.Cuenta, modulo = ProveedorModulos.Normalizar(f.Modulo),
                q = string.IsNullOrWhiteSpace(f.Q) ? null : f.Q.Trim(), salto = (pagina - 1) * tam, tam
            };
            const string donde = @" WHERE (@estado IS NULL OR c.estado = @estado) AND (@cuenta IS NULL OR c.id_cuenta = @cuenta) AND (@modulo IS NULL OR c.modulo = @modulo)
                AND (@q IS NULL OR c.codigo LIKE '%' + @q + '%' OR c.titulo LIKE '%' + @q + '%' OR pc.nombre LIKE '%' + @q + '%' OR pc.email LIKE '%' + @q + '%')";
            using var cn = _db.Conexion();
            var items = (await cn.QueryAsync<CargaProveedor>(SelCarga + donde + @"
                ORDER BY CASE c.estado WHEN 'PENDIENTE' THEN 0 WHEN 'PROGRAMADA' THEN 1 ELSE 2 END, ISNULL(c.actualizado_en, c.creado_en) DESC, c.id DESC
                OFFSET @salto ROWS FETCH NEXT @tam ROWS ONLY", p)).ToList();
            var total = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.carga_proveedor c JOIN Eurovip.proveedor_cuenta pc ON pc.id = c.id_cuenta" + donde, p);
            return (items, total);
        }

        /// <summary>Cuántas cargas hay en cada estado (contadores de Herramientas).</summary>
        public async Task<Dictionary<string, int>> ConteoAsync(int? cuenta = null)
        {
            using var cn = _db.Conexion();
            var filas = (await cn.QueryAsync<(string Estado, int N)>("SELECT estado, COUNT(*) FROM Eurovip.carga_proveedor WHERE @cuenta IS NULL OR id_cuenta = @cuenta GROUP BY estado", new { cuenta }))
                .ToDictionary(x => x.Estado, x => x.N);
            return EstadosCargaProveedor.Todos.ToDictionary(e => e, e => filas.GetValueOrDefault(e));
        }

        public async Task<CargaProveedor?> CargaAsync(int id)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<CargaProveedor>(SelCarga + " WHERE c.id = @id", new { id });
        }

        public async Task<List<CargaProveedorItem>> ItemsAsync(int? idCarga, int? idCuenta = null)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<CargaProveedorItem>(@"
                SELECT i.id_carga, i.tabla, i.id_registro, i.nombre, i.precio, i.moneda, c.codigo, c.modulo,
                       CAST(CASE i.tabla
                            WHEN 'SERVICIO' THEN (SELECT TOP 1 CAST(s.activo AS int) FROM Eurovip.servicio s WITH (NOLOCK) WHERE s.id_servicio = i.id_registro)
                            WHEN 'EXCURSION' THEN (SELECT TOP 1 1 FROM Eurovip.servicio_excursion e WITH (NOLOCK) WHERE e.id_Excursion = i.id_registro)
                            WHEN 'TRASLADO' THEN (SELECT TOP 1 CAST(t.activo AS int) FROM Eurovip.traslado t WITH (NOLOCK) WHERE t.id_traslado = i.id_registro)
                            WHEN 'CRUCERO' THEN (SELECT TOP 1 CAST(x.activo AS int) FROM Eurovip.crucero x WITH (NOLOCK) WHERE x.id_crucero = i.id_registro) END AS bit) AS Activo   -- NOLOCK: solo informativo; evita bloqueos mientras se publica
                FROM Eurovip.carga_proveedor_item i JOIN Eurovip.carga_proveedor c ON c.id = i.id_carga
                WHERE (@idCarga IS NULL OR i.id_carga = @idCarga) AND (@idCuenta IS NULL OR c.id_cuenta = @idCuenta)
                ORDER BY i.id_carga DESC, i.tabla, i.id_registro", new { idCarga, idCuenta })).ToList();
        }

        /// <summary>Detalle para la revisión: la carga, sus datos (JSON), el resumen de productos y lo que ya se publicó.</summary>
        public async Task<object?> DetalleAsync(int id, int? soloCuenta = null)
        {
            var c = await CargaAsync(id);
            if (c is null || (soloCuenta is int cu && c.Id_Cuenta != cu)) return null;
            List<CargaProducto> productos;
            try { productos = await ProductosAsync(c.Modulo, Leer(c.Modulo, c.Datos)); }
            catch (Exception ex) { _log.LogWarning(ex, "Carga de proveedor {Id}: no se pudo leer el JSON", id); productos = new(); }
            return new
            {
                carga = c, productos, items = await ItemsAsync(id),
                datos = JsonDocument.Parse(string.IsNullOrWhiteSpace(c.Datos) ? "[]" : c.Datos).RootElement,
                modulo = new { clave = c.Modulo, nombre = ProveedorModulos.Todos[c.Modulo].Nombre, icono = ProveedorModulos.Todos[c.Modulo].Icono }
            };
        }

        // ============================================ JSON ⇄ DTO ============================================

        private static Type TipoDe(string modulo) => modulo switch
        {
            ProveedorModulos.Ofertas or ProveedorModulos.Paquetes => typeof(CargaOfertaDto),
            ProveedorModulos.Circuitos => typeof(CargaCircuitoDto),
            ProveedorModulos.Excursiones => typeof(ExcursionGuardarDto),
            ProveedorModulos.Traslados => typeof(TrasladoGuardarDto),
            ProveedorModulos.Cruceros => typeof(CruceroDto),
            _ => throw new InvalidOperationException($"Módulo desconocido: {modulo}")
        };

        /// <summary>Lee el JSON guardado (lista de DTO del módulo).</summary>
        public static List<object> Leer(string modulo, string? json)
        {
            var t = TipoDe(modulo);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            return doc.RootElement.EnumerateArray().Select(e => e.Deserialize(t, Json)!).ToList();
        }

        /// <summary>Lee lo que manda el proveedor. Devuelve los DTO o los errores («el producto 2 tiene un dato que no se entiende»).</summary>
        public static (List<object> Lista, List<ErrorProducto> Errores) Leer(string modulo, JsonElement datos)
        {
            var lista = new List<object>();
            var errores = new List<ErrorProducto>();
            if (datos.ValueKind != JsonValueKind.Array) { errores.Add(new(0, null, "datos", "Los datos tienen que ser una lista de productos.")); return (lista, errores); }
            var t = TipoDe(modulo);
            var i = 0;
            foreach (var e in datos.EnumerateArray())
            {
                i++;
                try { lista.Add(e.Deserialize(t, Json) ?? throw new JsonException("vacío")); }
                catch (JsonException ex) { errores.Add(new(i, null, ex.Path, $"El producto {i} tiene un dato que no se entiende ({ex.Path ?? "formato"}). Revísalo.")); }
            }
            if (lista.Count + errores.Count == 0) errores.Add(new(0, null, "datos", "La carga no tiene ningún producto."));
            if (lista.Count > MaxProductos) errores.Add(new(0, null, "datos", $"Una carga admite como máximo {MaxProductos} productos. Divídela en varias."));
            return (lista, errores);
        }

        /// <summary>
        /// Lo que el proveedor NO puede decidir: siempre es su proveedor del catálogo, el código de la carga agrupa lo que se crea
        /// (servicio.codigo en Solo Hotel, Paquetes y Circuitos), siempre son productos NUEVOS y se publican activos en la web.
        /// </summary>
        public static void Forzar(string modulo, object dto, string idProveedor, string codigo)
        {
            switch (dto)
            {
                case CargaOfertaDto o:
                    o.Id_Proveedor = idProveedor; o.CodigoServicio = codigo; o.PublicarEnWeb = true;
                    o.Tipo = modulo == ProveedorModulos.Paquetes ? OfertaRepository.TipoPaquete : OfertaRepository.Tipo;
                    if (o.Modo is not ("express" or "excel" or "clasica")) o.Modo = "express";
                    break;
                case CargaCircuitoDto c:
                    c.Id_Proveedor = idProveedor; c.CodigoServicio = codigo; c.PublicarEnWeb = true;
                    if (c.Modo is not ("express" or "excel" or "clasica")) c.Modo = "express";
                    if (c.NuevoCircuito is not null) { c.NuevoCircuito.Id_Proveedor = idProveedor; c.NuevoCircuito.CodigoOriginal = null; }
                    break;
                case ExcursionGuardarDto e: e.Id_Excursion = null; e.Id_Proveedor = idProveedor; break;
                case TrasladoGuardarDto t: t.Id_Traslado = null; t.Id_Proveedor = idProveedor; t.Activo = true; break;
                case CruceroDto x: x.Id_Crucero = null; x.Id_Proveedor = idProveedor; x.Activo = true; break;
            }
        }

        private static string Serializar(IEnumerable<object> lista) => JsonSerializer.Serialize(lista.ToList(), Json);

        // ============================================ VALIDAR (sin guardar) ============================================

        /// <summary>
        /// Revisa cada producto con la validación del módulo, SIN guardar nada (la misma de los formularios: datos obligatorios,
        /// catálogos, fechas, duplicados, código interno, precio mínimo CAMBIO 139…). «desde» = los que ya se publicaron no se revisan.
        /// </summary>
        public async Task<List<ErrorProducto>> ValidarAsync(string modulo, List<object> lista, string codigo, int? idCarga = null, int desde = 0)
        {
            var errores = new List<ErrorProducto>();
            // Se revisa una COPIA: las reglas dejan los datos «limpios» y no queremos cambiar lo que escribió el proveedor.
            var copia = Leer(modulo, Serializar(lista));
            var productos = await ProductosAsync(modulo, copia);
            string? Nombre(int i) => productos.FirstOrDefault(p => p.Indice == i + 1)?.Nombre;
            void Mal(int i, string? campo, string m) => errores.Add(new(i + 1, Nombre(i), campo, m));

            // código servicio (Solo Hotel, Paquetes y Circuitos): no puede chocar con lo que cargó otra persona en la web
            if (modulo is ProveedorModulos.Ofertas or ProveedorModulos.Paquetes or ProveedorModulos.Circuitos)
            {
                using var cn = _db.Conexion();
                var usado = await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM Eurovip.servicio s WHERE s.codigo = @codigo
                    AND NOT EXISTS (SELECT 1 FROM Eurovip.carga_proveedor_item i WHERE i.id_carga = @idCarga AND i.tabla = 'SERVICIO' AND i.id_registro = s.id_servicio)",
                    new { codigo, idCarga = idCarga ?? 0 });
                if (usado > 0) errores.Add(new(0, null, "codigo", $"El código «{codigo}» ya lo usan {usado} producto(s) de la web cargados por otra persona. Usa otro código para esta carga."));
            }

            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fechas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = desde; i < copia.Count; i++)
            {
                var dto = copia[i];
                foreach (var m in ErroresModelo(dto)) Mal(i, null, m);
                if (errores.Any(e => e.Indice == i + 1)) continue;
                switch (dto)
                {
                    case CargaOfertaDto o:
                        foreach (var m in await _ofertas.ValidarAsync(o)) Mal(i, null, m);
                        // la misma fecha del mismo hotel no puede venir dos veces en la carga
                        foreach (var h in o.Hoteles)
                            foreach (var s in h.Salidas)
                                foreach (var c in o.Campanyas)
                                    if (!fechas.Add($"{h.Id_Alojamiento}|{h.Id_Regimen?.Trim()}|{s.Desde:yyyyMMdd}|{c.Id_Campanya}"))
                                        Mal(i, null, $"El hotel #{h.Id_Alojamiento} con la fecha {s.Desde:dd/MM/yyyy} (campaña #{c.Id_Campanya}) está repetido en la carga.");
                        break;
                    case CargaCircuitoDto c:
                        foreach (var m in await _circuitos.ValidarAsync(c)) Mal(i, null, m);
                        if (c.NuevoCircuito is { } n && !vistos.Add("CIR|" + n.Codigo.Trim())) Mal(i, "NuevoCircuito", $"El circuito nuevo «{n.Codigo}» está dos veces en la carga.");
                        foreach (var s in c.Salidas)
                            foreach (var cp in c.Campanyas)
                                if (!fechas.Add($"{c.NuevoCircuito?.Codigo ?? c.CodigoCircuito}|{c.Categoria}|{s.Desde:yyyyMMdd}|{cp.Id_Campanya}"))
                                    Mal(i, null, $"La salida del {s.Desde:dd/MM/yyyy} (campaña #{cp.Id_Campanya}) está repetida en la carga.");
                        break;
                    case ExcursionGuardarDto e:
                        if (await _guardado.RevisarExcursionAsync(e) is var (ce, me)) Mal(i, ce, me);
                        else if (await _guardado.MinimoExcursionAsync(e) is { } bajo) foreach (var m in bajo.Mensajes()) Mal(i, bajo.Campo, m);
                        else if (!vistos.Add($"{e.Id_Destino}|{e.Nombre}")) Mal(i, "Nombre", $"La excursión «{e.Nombre}» en {e.Id_Destino} está dos veces en la carga.");
                        if (e.Codigo is not null && !codigos.Add(e.Codigo)) Mal(i, "Codigo", $"El código «{e.Codigo}» está repetido en la carga.");
                        break;
                    case TrasladoGuardarDto t:
                        if (await _guardado.RevisarTrasladoAsync(t) is var (ct, mt)) Mal(i, ct, mt);
                        else if (await _guardado.MinimoTrasladoAsync(t) is { } bajo) foreach (var m in bajo.Mensajes()) Mal(i, bajo.Campo, m);
                        else if (!vistos.Add($"{t.Id_Estacion_Origen}|{t.Id_Destino}|{t.Tipo}|{t.Categoria}|{t.Vehiculo}"))
                            Mal(i, "Nombre", $"El traslado «{t.Nombre}» (misma ruta, tipo, categoría y vehículo) está dos veces en la carga.");
                        if (t.Codigo is not null && !codigos.Add(t.Codigo)) Mal(i, "Codigo", $"El código «{t.Codigo}» está repetido en la carga.");
                        break;
                    case CruceroDto x:
                        if (await _guardado.RevisarCruceroAsync(x, conMinimo: true) is var (cx, mx)) Mal(i, cx, mx);
                        else if (!vistos.Add($"{x.Titulo}|{x.Id_Barco}|{x.Barco_Nombre}")) Mal(i, "Titulo", $"El crucero «{x.Titulo}» está dos veces en la carga.");
                        if (x.Codigo_Interno is not null && !codigos.Add(x.Codigo_Interno)) Mal(i, "Codigo_Interno", $"El código «{x.Codigo_Interno}» está repetido en la carga.");
                        break;
                }
            }
            return errores;
        }

        /// <summary>Validación de DataAnnotations (como el model binding de los formularios), también en las listas de adentro.</summary>
        public static List<string> ErroresModelo(object? o, int nivel = 0)
        {
            var res = new List<string>();
            if (o is null || nivel > 5) return res;
            var r = new List<ValidationResult>();
            Validator.TryValidateObject(o, new ValidationContext(o), r, validateAllProperties: true);
            res.AddRange(r.Select(x => x.ErrorMessage ?? "Hay un dato inválido."));
            foreach (var p in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0 || p.PropertyType == typeof(string) || p.PropertyType.IsValueType) continue;
                var v = p.GetValue(o);
                if (v is System.Collections.IEnumerable lista)
                {
                    foreach (var x in lista)
                        if (x is not null && x is not string && !x.GetType().IsValueType && x.GetType().Namespace?.StartsWith("EuroVip") == true) res.AddRange(ErroresModelo(x, nivel + 1));
                }
                else if (v is not null && v.GetType().Namespace?.StartsWith("EuroVip") == true) res.AddRange(ErroresModelo(v, nivel + 1));
            }
            return res.Distinct().ToList();
        }

        // ============================================ RESUMEN DE PRODUCTOS ============================================

        /// <summary>Nombre, detalle, destino, precio y moneda de cada producto (para la revisión, la lista y el flyer).</summary>
        public async Task<List<CargaProducto>> ProductosAsync(string modulo, List<object> lista)
        {
            var res = new List<CargaProducto>();
            using var cn = _db.Conexion();
            var hoteles = new Dictionary<int, (string Nombre, string? Destino)>();
            var idsHotel = lista.OfType<CargaOfertaDto>().SelectMany(o => o.Hoteles.Select(h => h.Id_Alojamiento)).Distinct().ToList();
            if (idsHotel.Count > 0)
                foreach (var h in await cn.QueryAsync<(int Id, string Nombre, string? Destino)>(
                    "SELECT id_alojamiento, nombre, id_destino FROM Eurovip.alojamiento WHERE id_idioma = 'ES' AND id_alojamiento IN @ids", new { ids = idsHotel }))
                    hoteles[h.Id] = (h.Nombre, h.Destino);
            for (var i = 0; i < lista.Count; i++)
            {
                var n = i + 1;
                switch (lista[i])
                {
                    case CargaOfertaDto o:
                        foreach (var h in o.Hoteles)
                        {
                            var (nom, des) = hoteles.TryGetValue(h.Id_Alojamiento, out var x) ? x : (h.Nombre ?? $"Hotel #{h.Id_Alojamiento}", null);
                            var precio = h.Salidas.Where(s => s.PrecioTotal > 0).Select(s => (decimal?)s.PrecioTotal).Min();
                            res.Add(new CargaProducto
                            {
                                Indice = n, Nombre = nom.Trim(), Destino = des?.Trim(), Precio = precio, Moneda = o.Divisa?.Trim().ToUpperInvariant(),
                                Detalle = string.Join(" · ", new[] { h.Id_Regimen, $"{o.Noches} noches", $"{h.Salidas.Count} fecha{(h.Salidas.Count == 1 ? "" : "s")}" }.Where(s => !string.IsNullOrWhiteSpace(s)))
                            });
                        }
                        break;
                    case CargaCircuitoDto c:
                        {
                            var nombre = c.NuevoCircuito?.Nombre ?? (string.IsNullOrWhiteSpace(c.CodigoCircuito) ? null
                                : await cn.ExecuteScalarAsync<string?>("SELECT TOP 1 nombre_circuito FROM Eurovip.circuito WHERE LTRIM(RTRIM(id)) = @c AND id_idioma = 'ES'", new { c = c.CodigoCircuito.Trim() }));
                            string? destino = null;   // circuito nuevo: sus destinos se ven en el detalle de los datos
                            if (nombre is not null && c.NuevoCircuito is null)
                                destino = await cn.ExecuteScalarAsync<string?>("SELECT TOP 1 id_destino FROM Eurovip.circuito_destino WHERE nombre_circuito = @n AND id_idioma = 'ES'", new { n = nombre });
                            res.Add(new CargaProducto
                            {
                                Indice = n, Nombre = (nombre ?? c.CodigoCircuito ?? "Circuito").Trim(), Destino = destino?.Trim(), Moneda = c.Divisa?.Trim().ToUpperInvariant(),
                                Precio = c.Salidas.Where(s => s.PrecioTotal > 0).Select(s => (decimal?)s.PrecioTotal).Min(),
                                Detalle = string.Join(" · ", new[] { c.Noches is short nc ? $"{nc} noches" : null, $"{c.Salidas.Count} salida{(c.Salidas.Count == 1 ? "" : "s")}", c.Categoria }.Where(s => !string.IsNullOrWhiteSpace(s)))
                            });
                            break;
                        }
                    case ExcursionGuardarDto e:
                        res.Add(new CargaProducto { Indice = n, Nombre = e.Nombre.Trim(), Destino = e.Id_Destino?.Trim(), Precio = e.Precio, Moneda = (e.Id_Moneda ?? "DOP").Trim().ToUpperInvariant(),
                                                    Detalle = e.Cancelable ? "Cancelable" : "Excursión" });
                        break;
                    case TrasladoGuardarDto t:
                        res.Add(new CargaProducto
                        {
                            Indice = n, Nombre = t.Nombre.Trim(), Destino = t.Id_Destino?.Trim(), Moneda = (t.Id_Moneda ?? "DOP").Trim().ToUpperInvariant(),
                            Precio = new[] { t.Precio_Ida_Vuelta, t.Precio_Ida }.Where(x => x is > 0).Min(),
                            Detalle = $"{t.Id_Estacion_Origen?.Trim().ToUpperInvariant()} → {t.Id_Destino?.Trim()} · {(TrasladoValores.Tipos.TryGetValue((t.Tipo ?? "").Trim().ToUpperInvariant(), out var tt) ? tt : t.Tipo)}"
                        });
                        break;
                    case CruceroDto x:
                        res.Add(new CargaProducto
                        {
                            Indice = n, Nombre = x.Titulo.Trim(), Destino = x.Puerto_Salida?.Trim(), Moneda = (x.Id_Moneda ?? "DOP").Trim().ToUpperInvariant(),
                            Precio = x.Precios.Where(p => p.Precio_Pp > 0).Select(p => (decimal?)p.Precio_Pp).Min(),
                            Detalle = string.Join(" · ", new[] { x.Noches is short nx ? $"{nx} noches" : null, x.Barco_Nombre, x.Naviera_Nombre }.Where(s => !string.IsNullOrWhiteSpace(s)))
                        });
                        break;
                }
            }
            return res;
        }

        /// <summary>Precio «desde» y su moneda (la moneda con más productos; si hay varias monedas no se comparan entre sí).</summary>
        private static (decimal? Desde, string? Moneda) Desde(List<CargaProducto> productos)
        {
            var moneda = productos.Where(p => p.Precio is > 0 && p.Moneda is not null).GroupBy(p => p.Moneda!).OrderByDescending(g => g.Count()).ThenBy(g => g.Min(p => p.Precio)).FirstOrDefault()?.Key;
            return (productos.Where(p => p.Moneda == moneda && p.Precio is > 0).Min(p => p.Precio), moneda);
        }

        // ============================================ CARGAS: PROVEEDOR ============================================

        /// <summary>
        /// Crear (Id null) o editar una carga en BORRADOR o RECHAZADA. No revisa las reglas de cada producto (eso es Validar y
        /// Enviar a revisión): solo que los datos se entiendan, el módulo esté habilitado y el código sea válido y único.
        /// </summary>
        public async Task<(int? Id, List<ErrorProducto> Errores)> GuardarCargaAsync(ProveedorCuenta cuenta, CargaProveedorDto d)
        {
            var errores = new List<ErrorProducto>();
            ErrorProducto Mal(string campo, string m) => new(0, null, campo, m);
            var modulo = ProveedorModulos.Normalizar(d.Modulo);
            if (modulo is null) return (null, new() { Mal("modulo", $"Elige el módulo: {string.Join(", ", ProveedorModulos.Todos.Keys)}.") });
            if (!cuenta.ListaModulos.Contains(modulo)) return (null, new() { Mal("modulo", $"Tu cuenta no tiene habilitado {ProveedorModulos.Todos[modulo].Nombre}. Pídeselo a Eurovip.") });
            var codigo = (d.Codigo ?? "").Trim();
            if (!CodigoValido.IsMatch(codigo)) return (null, new() { Mal("codigo", "El código de la carga es obligatorio: solo letras y números (sin espacios, tildes ni símbolos), hasta 30.") });
            var titulo = string.IsNullOrWhiteSpace(d.Titulo) ? null : ReglasDatos.Limpiar(d.Titulo);
            if (titulo?.Length > 150) return (null, new() { Mal("titulo", "El título admite como máximo 150 caracteres.") });

            var (lista, malos) = Leer(modulo, d.Datos);
            if (malos.Count > 0) return (null, malos);
            foreach (var x in lista) Forzar(modulo, x, cuenta.Id_Proveedor, codigo);
            var productos = await ProductosAsync(modulo, lista);
            var (desde, moneda) = Desde(productos);

            using var cn = _db.Conexion();
            if (await cn.ExecuteScalarAsync<int?>("SELECT id FROM Eurovip.carga_proveedor WHERE id_cuenta = @cuenta AND codigo = @codigo AND (@id IS NULL OR id <> @id)",
                    new { cuenta = cuenta.Id, codigo, id = d.Id }) is int otra)
                return (null, new() { Mal("codigo", $"Ya tienes otra carga con el código «{codigo}» (n.º {otra}). Usa otro código.") });
            var p = new { id = d.Id, cuenta = cuenta.Id, modulo, codigo, titulo, datos = Serializar(lista), productos = productos.Count, desde, moneda };
            if (d.Id is int id)
            {
                var actual = await cn.QueryFirstOrDefaultAsync<(int Cuenta, string Estado)>("SELECT id_cuenta, estado FROM Eurovip.carga_proveedor WHERE id = @id", new { id });
                if (actual == default || actual.Cuenta != cuenta.Id) return (null, new() { Mal("id", "Esa carga no existe.") });
                if (actual.Estado is not (EstadosCargaProveedor.Borrador or EstadosCargaProveedor.Rechazada))
                    return (null, new() { Mal("estado", $"La carga está {actual.Estado}: solo se puede cambiar en borrador o si fue rechazada.") });
                await cn.ExecuteAsync(@"UPDATE Eurovip.carga_proveedor SET modulo = @modulo, codigo = @codigo, titulo = @titulo, datos = @datos, productos = @productos,
                    precio_desde = @desde, moneda = @moneda, actualizado_en = SYSDATETIME() WHERE id = @id", p);
                return (id, errores);
            }
            return (await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.carga_proveedor (id_cuenta, modulo, codigo, titulo, datos, estado, productos, precio_desde, moneda)
                VALUES (@cuenta, @modulo, @codigo, @titulo, @datos, 'BORRADOR', @productos, @desde, @moneda); SELECT CAST(SCOPE_IDENTITY() AS int);", p), errores);
        }

        /// <summary>Validar lo que manda el proveedor antes de guardarlo (sin guardar nada).</summary>
        public async Task<(List<CargaProducto> Productos, List<ErrorProducto> Errores)> ValidarDatosAsync(ProveedorCuenta cuenta, string? moduloTexto, string? codigo, JsonElement datos)
        {
            var modulo = ProveedorModulos.Normalizar(moduloTexto);
            if (modulo is null) return (new(), new() { new(0, null, "modulo", $"Elige el módulo: {string.Join(", ", ProveedorModulos.Todos.Keys)}.") });
            if (!cuenta.ListaModulos.Contains(modulo)) return (new(), new() { new(0, null, "modulo", $"Tu cuenta no tiene habilitado {ProveedorModulos.Todos[modulo].Nombre}.") });
            var cod = string.IsNullOrWhiteSpace(codigo) ? "VALIDAR" : codigo.Trim();
            var (lista, malos) = Leer(modulo, datos);
            if (malos.Count > 0) return (new(), malos);
            foreach (var x in lista) Forzar(modulo, x, cuenta.Id_Proveedor, cod);
            var productos = await ProductosAsync(modulo, lista);
            var errores = await ValidarAsync(modulo, lista, cod);
            if (!string.IsNullOrWhiteSpace(codigo) && !CodigoValido.IsMatch(cod)) errores.Insert(0, new(0, null, "codigo", "El código de la carga: solo letras y números, hasta 30."));
            return (productos, errores);
        }

        /// <summary>Enviar a revisión: se revisa todo; si está bien pasa a PENDIENTE y se genera el flyer (siempre, cada vez).</summary>
        public async Task<(bool Ok, string Mensaje, List<ErrorProducto> Errores)> EnviarAsync(ProveedorCuenta cuenta, int id)
        {
            var c = await CargaAsync(id);
            if (c is null || c.Id_Cuenta != cuenta.Id) return (false, "Esa carga no existe.", new());
            if (c.Estado is not (EstadosCargaProveedor.Borrador or EstadosCargaProveedor.Rechazada))
                return (false, $"La carga ya está {c.Estado}: no se puede volver a enviar.", new());
            if (!cuenta.ListaModulos.Contains(c.Modulo)) return (false, $"Tu cuenta ya no tiene habilitado {ProveedorModulos.Todos[c.Modulo].Nombre}.", new());
            var lista = Leer(c.Modulo, c.Datos);
            foreach (var x in lista) Forzar(c.Modulo, x, cuenta.Id_Proveedor, c.Codigo);
            var errores = await ValidarAsync(c.Modulo, lista, c.Codigo, c.Id);
            if (errores.Count > 0) return (false, $"La carga tiene {errores.Count} problema{(errores.Count == 1 ? "" : "s")}: corrígelos y vuelve a enviarla.", errores);
            var flyer = await FlyerAsync(c, cuenta, lista);
            var productos = await ProductosAsync(c.Modulo, lista);
            var (desde, moneda) = Desde(productos);
            using var cn = _db.Conexion();
            await cn.ExecuteAsync(@"UPDATE Eurovip.carga_proveedor SET estado = 'PENDIENTE', fecha_solicitud = SYSDATETIME(), flyer = @flyer, productos = @n, precio_desde = @desde,
                moneda = @moneda, error = NULL, activar_en = NULL, actualizado_en = SYSDATETIME() WHERE id = @id AND estado IN ('BORRADOR','RECHAZADA')",
                new { id, flyer, n = productos.Count, desde, moneda });
            return (true, "¡Listo! La carga se envió a revisión. Eurovip te avisará cuando esté aprobada.", new());
        }

        // ============================================ FLYER ============================================

        /// <summary>
        /// Flyer automático con los 4 productos más baratos de la carga (en la moneda con más productos). Título: nombre del
        /// proveedor + tipo de producto. Se guarda en uploads/flyers/proveedores/ y se devuelve la ruta pública.
        /// </summary>
        public async Task<string?> FlyerAsync(CargaProveedor c, ProveedorCuenta cuenta, List<object>? lista = null)
        {
            try
            {
                var productos = await ProductosAsync(c.Modulo, lista ?? Leer(c.Modulo, c.Datos));
                var (_, moneda) = Desde(productos);
                var top = productos.Where(p => p.Precio is > 0 && p.Moneda == moneda).OrderBy(p => p.Precio).Take(4).ToList();
                if (top.Count == 0) top = productos.Take(4).ToList();
                var (_, plural, _) = ProveedorModulos.Todos[c.Modulo];
                var datos = new FlyerDestino
                {
                    Id_Destino = cuenta.Nombre, Nombre_Destino = cuenta.Nombre, Moneda = moneda, Gancho = $"{plural} · {cuenta.Nombre}",
                    Hoteles = top.Select((p, k) => new FlyerHotel
                    {
                        Nombre_Hotel = p.Nombre, Nombre_Habitacion = string.Join(" · ", new[] { p.Destino, p.Detalle }.Where(s => !string.IsNullOrWhiteSpace(s))),
                        Precio_Final = (int)Math.Round(p.Precio ?? 0), Posicion = k + 1
                    }).ToList()
                };
                // Foto: el destino que más se repite entre los productos del flyer (si tiene fondo de flyer), si no Punta Cana
                var destino = top.Where(p => !string.IsNullOrWhiteSpace(p.Destino)).GroupBy(p => p.Destino!.Trim(), StringComparer.OrdinalIgnoreCase)
                                 .OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
                return await _flyer.GenerarProveedorAsync(datos, destino, c.Codigo);
            }
            catch (Exception ex)
            {
                // El flyer nunca impide enviar la carga: se avisa en el log y la carga sigue (se puede regenerar desde la revisión)
                _log.LogError(ex, "No se pudo generar el flyer de la carga de proveedor {Id}", c.Id);
                return null;
            }
        }

        /// <summary>Vuelve a generar el flyer de una carga (botón de la revisión).</summary>
        public async Task<string?> RegenerarFlyerAsync(int id)
        {
            var c = await CargaAsync(id);
            if (c is null) return null;
            var cuenta = await CuentaAsync(c.Id_Cuenta);
            if (cuenta is null) return null;
            var lista = Leer(c.Modulo, c.Datos);
            foreach (var x in lista) Forzar(c.Modulo, x, cuenta.Id_Proveedor, c.Codigo);
            var flyer = await FlyerAsync(c, cuenta, lista);
            if (flyer is not null)
                using (var cn = _db.Conexion()) await cn.ExecuteAsync("UPDATE Eurovip.carga_proveedor SET flyer = @flyer, actualizado_en = SYSDATETIME() WHERE id = @id", new { id, flyer });
            return flyer;
        }

        // ============================================ REVISIÓN (personal de Eurovip) ============================================

        /// <summary>Rechazar (con motivo e instrucciones): el proveedor la puede corregir y volver a enviar.</summary>
        public async Task<string?> RechazarAsync(int id, string? motivo, string? instrucciones, string usuario)
        {
            motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            instrucciones = string.IsNullOrWhiteSpace(instrucciones) ? null : instrucciones.Trim();
            if (motivo is null) return "Escribe el motivo del rechazo: el proveedor lo verá para corregir la carga.";
            if (motivo.Length > 1000 || instrucciones?.Length > 1000) return "El motivo y las instrucciones admiten como máximo 1000 caracteres cada uno.";
            using var cn = _db.Conexion();
            var n = await cn.ExecuteAsync(@"UPDATE Eurovip.carga_proveedor SET estado = 'RECHAZADA', motivo = @motivo, instrucciones = @instrucciones, activar_en = NULL,
                revisado_por = @usuario, fecha_revision = SYSDATETIME(), error = NULL, actualizado_en = SYSDATETIME() WHERE id = @id AND estado IN ('PENDIENTE','PROGRAMADA')",
                new { id, motivo, instrucciones, usuario });
            return n == 0 ? "Solo se rechazan cargas pendientes o programadas (recarga la página: quizá ya la revisó otra persona)." : null;
        }

        /// <summary>Programar el alta en la web (fecha y hora de República Dominicana). Antes se revisa todo otra vez.</summary>
        public async Task<(string? Error, List<ErrorProducto> Errores)> ProgramarAsync(int id, DateTime cuando, string usuario)
        {
            var ahora = AgenciaProgramador.Ahora(_cfg);
            if (cuando <= ahora.AddMinutes(1)) return ($"La fecha del alta tiene que ser en el futuro (ahora en República Dominicana son las {ahora:dd/MM/yyyy HH:mm}).", new());
            if (cuando > ahora.AddYears(2)) return ("La fecha del alta no puede ser a más de 2 años.", new());
            var c = await CargaAsync(id);
            if (c is null) return ("Esa carga no existe.", new());
            if (c.Estado is not (EstadosCargaProveedor.Pendiente or EstadosCargaProveedor.Programada)) return ($"La carga está {c.Estado}: solo se programan las pendientes.", new());
            var lista = Leer(c.Modulo, c.Datos);
            foreach (var x in lista) Forzar(c.Modulo, x, c.Id_Proveedor ?? "", c.Codigo);
            var errores = await ValidarAsync(c.Modulo, lista, c.Codigo, c.Id, c.Publicados);
            if (errores.Count > 0) return ("La carga ya no cumple las reglas (algo cambió en la web desde que se envió). Recházala para que el proveedor la corrija.", errores);
            using var cn = _db.Conexion();
            var n = await cn.ExecuteAsync(@"UPDATE Eurovip.carga_proveedor SET estado = 'PROGRAMADA', activar_en = @cuando, revisado_por = @usuario, fecha_revision = SYSDATETIME(),
                error = NULL, actualizado_en = SYSDATETIME() WHERE id = @id AND estado IN ('PENDIENTE','PROGRAMADA')", new { id, cuando, usuario });
            return n == 0 ? ("Recarga la página: quizá ya la revisó otra persona.", new()) : (null, new());
        }

        /// <summary>
        /// Publicar en la web (Aprobar ahora o al llegar la fecha programada): el MISMO guardado de cada pantalla. Cada producto se
        /// guarda en su propia transacción (la del servicio de ese módulo); si uno falla se anota el error, la carga NO pasa a ACTIVA
        /// y al volver a intentar se sigue desde el que falló (carga_proveedor.publicados), sin duplicar lo ya publicado.
        /// </summary>
        public async Task<(bool Ok, string Mensaje, List<ErrorProducto> Errores)> PublicarAsync(int id, string usuario)
        {
            // Bloqueo por carga (por si el botón y el programador coinciden): la otra petición no espera, avisa.
            using var cerrojo = _db.Conexion();
            await cerrojo.OpenAsync();
            var bloqueo = await cerrojo.ExecuteScalarAsync<int>(@"DECLARE @r int; EXEC @r = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r",
                new { res = $"Eurovip.carga_proveedor.{id}" });
            if (bloqueo < 0) return (false, "Esta carga ya se está publicando. Espera un momento y recarga.", new());
            try
            {
                var c = await CargaAsync(id);
                if (c is null) return (false, "Esa carga no existe.", new());
                if (c.Estado is not (EstadosCargaProveedor.Pendiente or EstadosCargaProveedor.Programada))
                    return (false, $"La carga está {c.Estado}: solo se publican las pendientes o programadas.", new());
                var lista = Leer(c.Modulo, c.Datos);
                foreach (var x in lista) Forzar(c.Modulo, x, c.Id_Proveedor ?? "", c.Codigo);

                var errores = await ValidarAsync(c.Modulo, lista, c.Codigo, c.Id, c.Publicados);
                if (errores.Count > 0)
                {
                    var msg = $"No se publicó: {errores.Count} problema{(errores.Count == 1 ? "" : "s")} (p. ej. {errores[0].Mensaje}). Recházala para que el proveedor la corrija.";
                    await AnotarErrorAsync(id, msg);
                    return (false, msg, errores);
                }

                var creados = 0;
                for (var i = c.Publicados; i < lista.Count; i++)
                {
                    List<CargaProveedorItem> items;
                    try { items = await PublicarUnoAsync(c, lista[i], usuario); }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Carga de proveedor {Id}: falló al publicar el producto {N}", id, i + 1);
                        var msg = $"Se cortó en el producto {i + 1} de {lista.Count}: {ex.Message}" + (i > 0 ? $" Los {i} primeros ya están en la web; al volver a aprobar se sigue desde el {i + 1}." : "");
                        await AnotarErrorAsync(id, msg);
                        return (false, msg, new());
                    }
                    using var cn = _db.Conexion();
                    await cn.OpenAsync();
                    using var tx = cn.BeginTransaction();
                    foreach (var it in items)
                        await cn.ExecuteAsync(@"IF NOT EXISTS (SELECT 1 FROM Eurovip.carga_proveedor_item WHERE id_carga = @id AND tabla = @Tabla AND id_registro = @Id_Registro)
                            INSERT INTO Eurovip.carga_proveedor_item (id_carga, tabla, id_registro, nombre, precio, moneda) VALUES (@id, @Tabla, @Id_Registro, @Nombre, @Precio, @Moneda)",
                            new { id, it.Tabla, it.Id_Registro, Nombre = Corta(it.Nombre, 200), it.Precio, it.Moneda }, tx);
                    await cn.ExecuteAsync("UPDATE Eurovip.carga_proveedor SET publicados = @n, actualizado_en = SYSDATETIME() WHERE id = @id", new { id, n = i + 1 }, tx);
                    tx.Commit();
                    creados += items.Count;
                }

                using (var cn = _db.Conexion())
                    await cn.ExecuteAsync(@"UPDATE Eurovip.carga_proveedor SET estado = 'ACTIVA', fecha_activacion = SYSDATETIME(), error = NULL, revisado_por = ISNULL(revisado_por, @usuario),
                        fecha_revision = ISNULL(fecha_revision, SYSDATETIME()), actualizado_en = SYSDATETIME() WHERE id = @id", new { id, usuario });
                await _db.AuditarAsync("Proveedor_Carga_Publicar", (await ItemsAsync(id)).Select(x => x.Id_Registro), new { carga = id, c.Codigo, c.Modulo, c.Cuenta, creados },
                    usuario, null, "Cargas de proveedores");
                return (true, $"¡Publicada! Se crearon {creados} registro{(creados == 1 ? "" : "s")} en la web ({ProveedorModulos.Todos[c.Modulo].Nombre}).", new());
            }
            finally
            {
                try { await cerrojo.ExecuteAsync("EXEC sp_releaseapplock @Resource = @res, @LockOwner = 'Session'", new { res = $"Eurovip.carga_proveedor.{id}" }); } catch { /* se libera al cerrar */ }
            }
        }

        private async Task AnotarErrorAsync(int id, string mensaje)
        {
            using var cn = _db.Conexion();
            await cn.ExecuteAsync("UPDATE Eurovip.carga_proveedor SET error = @m, actualizado_en = SYSDATETIME() WHERE id = @id", new { id, m = Corta(mensaje, 1000) });
        }

        /// <summary>Guarda UN producto con el camino de su pantalla y devuelve lo que se creó (para carga_proveedor_item).</summary>
        private async Task<List<CargaProveedorItem>> PublicarUnoAsync(CargaProveedor c, object dto, string usuario)
        {
            var nav = $"Carga de proveedor #{c.Id} ({c.Codigo})";
            switch (dto)
            {
                case CargaOfertaDto o:
                    {
                        var (r, errs) = await _ofertas.CargarAsync(o, usuario, null, nav);
                        if (r is null) throw new InvalidOperationException(errs.FirstOrDefault() ?? "No se pudo cargar.");
                        await _db.AuditarAsync(OfertaRepository.TipoDe(o.Tipo) == OfertaRepository.TipoPaquete ? "Paquete_Carga" : "Oferta_Carga", r.Ids,
                            new { o.Modo, r.Codigo, r.LogId, cargaProveedor = c.Id }, usuario, null, nav);
                        // Mismo orden en que CargaOfertaService crea los servicios: hotel × campaña × fecha
                        var nombres = await NombresHotelesAsync(o.Hoteles.Select(h => h.Id_Alojamiento));
                        var orden = (from h in o.Hoteles from cp in o.Campanyas.DistinctBy(x => x.Id_Campanya) from s in h.Salidas.OrderBy(s => s.Desde)
                                     select (Nombre: $"{nombres.GetValueOrDefault(h.Id_Alojamiento, $"Hotel #{h.Id_Alojamiento}")} · {s.Desde:dd/MM/yyyy}", s.PrecioTotal)).ToList();
                        return r.Ids.Select((idS, k) => new CargaProveedorItem { Tabla = "SERVICIO", Id_Registro = idS, Nombre = orden.ElementAtOrDefault(k).Nombre ?? r.Circuito,
                                                                                  Precio = k < orden.Count ? orden[k].PrecioTotal : null, Moneda = o.Divisa }).ToList();
                    }
                case CargaCircuitoDto ci:
                    {
                        var (r, errs) = await _circuitos.CargarAsync(ci, usuario, null, nav);
                        if (r is null) throw new InvalidOperationException(errs.FirstOrDefault() ?? "No se pudo cargar.");
                        await _db.AuditarAsync("Circuito_Carga", r.Ids, new { ci.Modo, r.Circuito, r.Codigo, r.LogId, cargaProveedor = c.Id }, usuario, null, nav);
                        // Mismo orden que CargaCircuitoService: campaña × fecha
                        var orden = (from cp in ci.Campanyas.DistinctBy(x => x.Id_Campanya) from s in ci.Salidas.OrderBy(s => s.Desde) select s).ToList();
                        return r.Ids.Select((idS, k) => new CargaProveedorItem { Tabla = "SERVICIO", Id_Registro = idS,
                            Nombre = k < orden.Count ? $"{r.Circuito} · {orden[k].Desde:dd/MM/yyyy}" : r.Circuito, Precio = k < orden.Count ? orden[k].PrecioTotal : null, Moneda = ci.Divisa }).ToList();
                    }
                case ExcursionGuardarDto e:
                    {
                        // las reglas dejan nombre y destino como en Eurovip (igual que el formulario antes de guardar)
                        if (await _guardado.RevisarExcursionAsync(e) is var (_, mal)) throw new InvalidOperationException(mal);
                        var idE = await _guardado.GuardarExcursionAsync(e);
                        await _db.AuditarAsync("Excursion_Crear", new[] { idE }, new { despues = e, cargaProveedor = c.Id }, usuario, null, nav);
                        return new() { new CargaProveedorItem { Tabla = "EXCURSION", Id_Registro = idE, Nombre = e.Nombre, Precio = e.Precio, Moneda = e.Id_Moneda } };
                    }
                case TrasladoGuardarDto t:
                    {
                        if (await _guardado.RevisarTrasladoAsync(t) is var (_, mal)) throw new InvalidOperationException(mal);
                        var idT = await _guardado.GuardarTrasladoAsync(t, usuario);
                        await _db.AuditarAsync("Traslado_Crear", new[] { idT }, new { despues = t, cargaProveedor = c.Id }, usuario, null, nav);
                        return new() { new CargaProveedorItem { Tabla = "TRASLADO", Id_Registro = idT, Nombre = t.Nombre, Precio = new[] { t.Precio_Ida_Vuelta, t.Precio_Ida }.Where(x => x is > 0).Min(), Moneda = t.Id_Moneda } };
                    }
                case CruceroDto x:
                    {
                        if (await _guardado.RevisarCruceroAsync(x) is var (_, mal)) throw new InvalidOperationException(mal);
                        var idC = await _guardado.GuardarCruceroAsync(x, usuario);
                        await _db.AuditarAsync("Crucero_Crear", new[] { idC }, new { x.Titulo, x.Id_Barco, x.Noches, salidas = x.Salidas.Count, cabinas = x.Cabinas.Count, precios = x.Precios.Count, cargaProveedor = c.Id }, usuario, null, nav);
                        return new() { new CargaProveedorItem { Tabla = "CRUCERO", Id_Registro = idC, Nombre = x.Titulo, Precio = x.Precios.Where(p => p.Precio_Pp > 0).Select(p => (decimal?)p.Precio_Pp).Min(), Moneda = x.Id_Moneda } };
                    }
                default: throw new InvalidOperationException("Tipo de producto desconocido.");
            }
        }

        private async Task<Dictionary<int, string>> NombresHotelesAsync(IEnumerable<int> ids)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<(int Id, string Nombre)>("SELECT id_alojamiento, nombre FROM Eurovip.alojamiento WHERE id_idioma = 'ES' AND id_alojamiento IN @ids", new { ids = ids.Distinct().ToList() }))
                .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Nombre.Trim());
        }

        /// <summary>Las programadas cuya fecha ya llegó (hora de República Dominicana) y que no fallaron antes.</summary>
        public async Task<List<(int Id, string? Usuario)>> ProgramadasParaPublicarAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<(int, string?)>(@"SELECT id, revisado_por FROM Eurovip.carga_proveedor
                WHERE estado = 'PROGRAMADA' AND activar_en <= @ahora AND error IS NULL ORDER BY activar_en, id", new { ahora = AgenciaProgramador.Ahora(_cfg) })).ToList();
        }

        private static string? Corta(string? s, int n) => s is null ? null : s.Length <= n ? s : s[..n];
    }

    /// <summary>
    /// CAMBIO 140: cada 60 s publica las cargas de proveedor PROGRAMADA cuya fecha de alta ya llegó (hora de República Dominicana).
    /// Igual que los envíos programados de Agencia: encendido en el servidor y APAGADO en local (Development), porque la copia local
    /// usa la misma base de datos y publicaría lo mismo. Se fuerza con Eurovip:Proveedores:Publicar = true/false.
    /// </summary>
    public class ProveedoresCargaWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _cfg;
        private readonly ILogger<ProveedoresCargaWorker> _log;
        private readonly IHostEnvironment _env;

        public ProveedoresCargaWorker(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<ProveedoresCargaWorker> log, IHostEnvironment env)
        { _scopes = scopes; _cfg = cfg; _log = log; _env = env; }

        public static bool Encendido(IConfiguration cfg, IHostEnvironment env)
        {
            var conf = cfg["Eurovip:Proveedores:Publicar"];
            return conf is { Length: > 0 } ? string.Equals(conf, "true", StringComparison.OrdinalIgnoreCase) : !env.IsDevelopment();
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            if (!Encendido(_cfg, _env))
            {
                _log.LogInformation("Publicación programada de cargas de proveedores apagada en este servidor (Eurovip:Proveedores:Publicar).");
                return;
            }
            try { await Task.Delay(TimeSpan.FromSeconds(25), ct); } catch (OperationCanceledException) { return; }
            while (!ct.IsCancellationRequested)
            {
                try { await RevisarAsync(); }
                catch (Exception ex) { _log.LogError(ex, "Cargas de proveedores programadas: error al revisar"); }
                try { await Task.Delay(TimeSpan.FromSeconds(60), ct); } catch (OperationCanceledException) { return; }
            }
        }

        private async Task RevisarAsync()
        {
            using var scope = _scopes.CreateScope();
            var srv = scope.ServiceProvider.GetRequiredService<ProveedoresCarga>();
            foreach (var (id, usuario) in await srv.ProgramadasParaPublicarAsync())
            {
                var (ok, msg, _) = await srv.PublicarAsync(id, string.IsNullOrWhiteSpace(usuario) ? "programado" : usuario);
                if (ok) _log.LogInformation("Carga de proveedor {Id} publicada (programada): {Msg}", id, msg);
                else _log.LogWarning("Carga de proveedor {Id} programada no se publicó: {Msg}", id, msg);
            }
        }
    }
}
