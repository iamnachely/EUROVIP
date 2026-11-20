using System.Text.Json;
using EuroVip.Services;
using Dapper;
using EuroVip.Models;
using EuroVip.Repositories;

namespace EuroVip.Services
{
    /// <summary>
    /// Solicitudes de creación de datos de los PROVEEDORES (20/11/2026). Pedido de la usuaria: «que en cada input el proveedor pueda
    /// pedir crear un dato nuevo (un hotel que no existe…), con el mismo formulario del BackOffice, y que ya lo pueda usar en su carga».
    /// Se usan los MISMOS catálogos y reglas de Herramientas (HrCatalogos + HerramientasRepository.GuardarAsync): el dato se crea al
    /// momento y queda anotado en Eurovip.proveedor_solicitud (Database/proveedor-solicitudes.sql). El hotel nuevo queda INACTIVO
    /// (no sale en la web ni en las listas de nadie más) y Eurovip lo aprueba en Herramientas › Cargas de proveedores › Solicitudes.
    /// </summary>
    public class ProveedoresSolicitudes
    {
        private readonly ICircuitoRepository _db;
        private readonly IHerramientasRepository _hr;
        private readonly IMultimedia _mm;
        private readonly IWebHostEnvironment _env;
        public ProveedoresSolicitudes(ICircuitoRepository db, IHerramientasRepository hr, IMultimedia mm, IWebHostEnvironment env) { _db = db; _hr = hr; _mm = mm; _env = env; }

        // ---------------------------------------------------------------- imágenes que pide agregar el proveedor
        private const string CarpetaImagenes = "uploads/proveedores/imagenes";
        private string DirImagenes(int id) => Path.Combine(_env.WebRootPath ?? "wwwroot", CarpetaImagenes.Replace('/', Path.DirectorySeparatorChar), id.ToString());

        public sealed record ArchivoSol(int N, string Ext, int? Ancho, int? Alto, long Bytes, string? Original);

        /// <summary>
        /// El proveedor pide agregar fotos a un hotel o destino. Se guardan TAL CUAL (sin tocar la calidad) en una carpeta aparte y NO salen en la web
        /// hasta que Eurovip las aprueba: entonces pasan a Eurovip.multimedia con el mismo proceso de siempre (sin perder calidad).
        /// </summary>
        public async Task<(object? Resultado, string? Error)> CrearImagenesAsync(ProveedorCuenta cuenta, string? tipo, string? clave, string? nombre, string? uso, IList<Microsoft.AspNetCore.Http.IFormFile> archivos)
        {
            tipo = (tipo ?? "").Trim().ToLowerInvariant(); uso = (uso ?? "GALERIA").Trim().ToUpperInvariant();
            if (tipo is not ("hotel" or "destino")) return (null, "Elige si las fotos son de un hotel o de un destino.");
            if (string.IsNullOrWhiteSpace(clave)) return (null, tipo == "hotel" ? "Elige el hotel." : "Elige el destino.");
            if (uso is not ("PORTADA" or "GALERIA")) uso = "GALERIA";
            if (archivos is null || archivos.Count == 0) return (null, "Elige al menos una imagen.");
            if (archivos.Count > 12) return (null, "Puedes pedir hasta 12 fotos cada vez.");
            using (var cn0 = _db.Conexion()) if (!await HayTablaAsync(cn0)) return (null, "Las solicitudes todavía no están disponibles en este servidor. Escribe a Eurovip.");
            var validos = new List<(Microsoft.AspNetCore.Http.IFormFile F, string Ext)>();
            foreach (var f in archivos)
            {
                if (f.Length == 0) continue;
                if (f.Length > 15_000_000) return (null, $"«{f.FileName}» pesa más de 15 MB.");
                var ext = await EuroVip.Helpers.ImagenSegura.ExtensionAsync(f);
                if (ext is null) return (null, $"«{f.FileName}» no es una imagen JPG, PNG o WebP.");
                validos.Add((f, ext));
            }
            if (validos.Count == 0) return (null, "Elige al menos una imagen.");
            var etiqueta = (nombre ?? "").Trim(); if (etiqueta.Length > 150) etiqueta = etiqueta[..150];
            using var cn = _db.Conexion();
            var id = await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.proveedor_solicitud (id_cuenta, tipo, nombre) OUTPUT INSERTED.id_solicitud VALUES (@c, 'imagen', @n)", new { c = cuenta.Id, n = $"{(etiqueta.Length > 0 ? etiqueta : clave)} · {validos.Count} foto(s)" });
            var dir = DirImagenes(id); Directory.CreateDirectory(dir);
            var lista = new List<ArchivoSol>(); var n = 0;
            foreach (var (f, ext) in validos)
            {
                n++; int? w = null, h = null;
                await using (var fs = File.Create(Path.Combine(dir, $"{n}.{ext}"))) await f.CopyToAsync(fs);
                try { using var st = File.OpenRead(Path.Combine(dir, $"{n}.{ext}")); var info = SkiaSharp.SKBitmap.DecodeBounds(st); if (info.Width > 0) { w = info.Width; h = info.Height; } } catch (Exception) { }
                lista.Add(new ArchivoSol(n, ext, w, h, f.Length, Path.GetFileName(f.FileName)));
            }
            await cn.ExecuteAsync("UPDATE Eurovip.proveedor_solicitud SET datos = @d WHERE id_solicitud = @id", new { id, d = JsonSerializer.Serialize(new { destinoTipo = tipo, clave = clave!.Trim(), nombre = etiqueta, uso, archivos = lista }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
            return (new { ok = true, id, mensaje = $"Listo: pediste agregar {validos.Count} foto(s). Eurovip las revisa y, si están bien, salen en la web." }, null);
        }

        // ---------------------------------------------------------------- sugerencias y comentarios del proveedor
        public static readonly string[] CategoriasSugerencia = { "sugerencia", "problema", "pregunta", "felicitacion" };

        /// <summary>El proveedor deja una sugerencia, un problema, una pregunta o un comentario para Eurovip (se guarda como una solicitud más, sin cambiar la base).</summary>
        public async Task<(object? Resultado, string? Error)> CrearSugerenciaAsync(ProveedorCuenta cuenta, string? categoria, string? asunto, string? texto, Dictionary<string, int>? valoracion = null)
        {
            categoria = (categoria ?? "sugerencia").Trim().ToLowerInvariant();
            if (!CategoriasSugerencia.Contains(categoria)) categoria = "sugerencia";
            var claves = new[] { "general", "facilidad", "velocidad", "claridad", "atencion" };
            valoracion = (valoracion ?? new()).Where(x => claves.Contains(x.Key) && x.Value is >= 1 and <= 5).ToDictionary(x => x.Key, x => x.Value);
            asunto = (asunto ?? "").Trim(); texto = (texto ?? "").Trim();
            if (valoracion.Count > 0 && asunto.Length < 3) asunto = "Mi valoración"; if (valoracion.Count > 0 && texto.Length < 10) texto = "Valoración con estrellas.";
            if (asunto.Length < 3) return (null, "Escribe un título corto (al menos 3 letras).");
            if (texto.Length < 10) return (null, "Cuéntanos un poco más (al menos 10 letras).");
            if (asunto.Length > 150) asunto = asunto[..150]; if (texto.Length > 3000) texto = texto[..3000];
            using var cn = _db.Conexion();
            if (!await HayTablaAsync(cn)) return (null, "Las sugerencias todavía no están disponibles en este servidor. Escribe a Eurovip.");
            if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.proveedor_solicitud WHERE id_cuenta = @c AND tipo = 'sugerencia' AND creado_en > DATEADD(MINUTE, -1, SYSDATETIME())", new { c = cuenta.Id }) >= 3)
                return (null, "Enviaste varios mensajes seguidos. Espera un minuto y vuelve a intentarlo.");
            var id = await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.proveedor_solicitud (id_cuenta, tipo, nombre, datos) OUTPUT INSERTED.id_solicitud VALUES (@c, 'sugerencia', @n, @d)",
                new { c = cuenta.Id, n = asunto, d = JsonSerializer.Serialize(new { categoria, texto, valoracion }) });
            return (new { ok = true, id, mensaje = "¡Gracias! Eurovip recibió tu mensaje y te responderá aquí." }, null);
        }

        private async Task<string?> AprobarImagenesAsync(int id, string? datos)
        {
            if (string.IsNullOrWhiteSpace(datos)) return "La solicitud no tiene fotos.";
            using var doc = JsonDocument.Parse(datos); var r = doc.RootElement;
            string Tx(string k) => r.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
            var tipoMm = Tx("destinoTipo") == "hotel" ? "ALOJAMIENTO" : "DESTINO"; var clave = Tx("clave"); var uso = Tx("uso") is "PORTADA" ? "PORTADA" : "GALERIA";
            if (!await _mm.ExisteProductoAsync(tipoMm, clave)) return "El hotel o destino de estas fotos ya no existe.";
            var primera = true;
            foreach (var a in r.GetProperty("archivos").EnumerateArray())
            {
                var ruta = Path.Combine(DirImagenes(id), $"{a.GetProperty("n").GetInt32()}.{a.GetProperty("ext").GetString()}");
                if (!File.Exists(ruta)) continue;
                await using var st = File.OpenRead(ruta);
                await _mm.SubirAsync(tipoMm, clave, uso == "PORTADA" && primera ? "PORTADA" : "GALERIA", st, a.TryGetProperty("original", out var o) ? o.GetString() : null);
                primera = false;
            }
            return null;
        }

        /// <summary>Lo que se puede pedir crear: tipo → catálogo de Herramientas, título e icono.</summary>
        public static readonly IReadOnlyDictionary<string, (string Catalogo, string Titulo, string Icono, string Ayuda)> Tipos = new Dictionary<string, (string, string, string, string)>
        {
            ["hotel"] = ("alojamientos", "Pedir un hotel nuevo", "bi-building", "Completa los datos del hotel. Eurovip lo revisa, pero ya lo puedes usar en tu carga."),
            ["destino"] = ("destinos", "Pedir un destino nuevo", "bi-geo-alt", "Ciudad o zona que no está en la lista. Ya lo puedes usar en tu carga."),
            ["aeropuerto"] = ("estaciones", "Pedir un aeropuerto nuevo", "bi-airplane-engines", "Aeropuerto o estación que no está en la lista (código IATA)."),
            ["habitacion"] = ("habitaciones", "Pedir una habitación nueva", "bi-door-open", "Tipo de habitación de un hotel que no está en la lista."),
        };

        /// <summary>Campos que el proveedor NO llena (los pone Eurovip): imágenes, números automáticos y si está activo.</summary>
        private static bool Visible(HrCampo f) => !f.Auto && f.Tipo != "imagen" && f.Col != "activo" && f.Col != "informacion";

        private static bool? _hayTabla; private static DateTime _mirado = DateTime.MinValue;
        private async Task<bool> HayTablaAsync(System.Data.Common.DbConnection cn)
        {
            if (_hayTabla == true) return true;
            if (DateTime.UtcNow - _mirado < TimeSpan.FromMinutes(2)) return false;
            _mirado = DateTime.UtcNow;
            _hayTabla = await SqlMapper.ExecuteScalarAsync<int?>(cn, "SELECT OBJECT_ID('Eurovip.proveedor_solicitud', 'U')") is not null;
            return _hayTabla == true;
        }

        public sealed record CampoForm(string Col, string Etiqueta, string Tipo, int Max, bool Req, string? Ayuda, string? Defecto, string? Lista, bool Mayus);

        /// <summary>El formulario de ese tipo (los mismos campos que ve Eurovip en Herramientas) y sus listas.</summary>
        public async Task<object?> FormularioAsync(string tipo)
        {
            if (!Tipos.TryGetValue(tipo ?? "", out var t) || HrCatalogos.Buscar(t.Catalogo) is not { } c) return null;
            var campos = c.Campos.Where(Visible).Select(f => new CampoForm(f.Col, f.Etiqueta, f.Tipo, f.Max, f.Req, f.Ayuda, f.Defecto, f.Lista, f.Mayus)).ToList();
            var listas = new Dictionary<string, IEnumerable<Opcion>>();
            foreach (var l in campos.Select(x => x.Lista).Where(x => x is not null && x != "continentes").Distinct())
                listas[l!] = await _hr.ListaAsync(l!, null);
            return new { tipo, titulo = t.Titulo, icono = t.Icono, ayuda = t.Ayuda, singular = c.Singular, campos, listas };
        }

        /// <summary>Tipos de habitación (Doble, Suite…) que contienen el texto.</summary>
        public Task<IEnumerable<Opcion>> TiposHabitacionAsync(string? q) => _hr.ListaAsync("tiposHabitacion", q);

        /// <summary>Aviso en vivo: lo que ya existe o se parece (para no pedir dos veces lo mismo).</summary>
        public async Task<List<HrParecido>?> ParecidosAsync(string tipo, string col, string texto, Dictionary<string, string?> contexto)
        {
            if (!Tipos.TryGetValue(tipo ?? "", out var t) || HrCatalogos.Buscar(t.Catalogo) is not { } c || c.Campo(col) is null) return null;
            return await _hr.ParecidosAsync(c, col, texto ?? "", contexto ?? new(), new());
        }

        /// <summary>Crea el dato con las reglas de Herramientas y anota la solicitud. Devuelve la opción lista para usar en el formulario.</summary>
        public async Task<(object? Resultado, string? Error)> CrearAsync(ProveedorCuenta cuenta, string tipo, Dictionary<string, JsonElement> datos)
        {
            if (!Tipos.TryGetValue(tipo ?? "", out var t) || HrCatalogos.Buscar(t.Catalogo) is not { } c) return (null, "No sé crear eso.");
            using (var cn0 = _db.Conexion())
                if (!await HayTablaAsync(cn0)) return (null, "Las solicitudes todavía no están disponibles en este servidor. Escribe a Eurovip y lo creamos por ti.");

            // Solo los campos del formulario (nada de «activo» ni imágenes que el proveedor no controla)
            var permitidos = c.Campos.Where(Visible).Select(f => f.Col).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var limpio = datos.Where(x => permitidos.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            if (c.Especial == "alojamiento") limpio["activo"] = JsonDocument.Parse("false").RootElement.Clone();   // inactivo hasta que Eurovip lo apruebe

            var (clave, error) = await _hr.GuardarAsync(c, limpio, true);
            if (error is not null || clave is null) return (null, error ?? "No se pudo crear.");

            string Txt(string col) => limpio.TryGetValue(col, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()) ?? "" : "";
            var op = await OpcionAsync(tipo!, clave, limpio);
            using var cn = _db.Conexion();
            var id = await cn.ExecuteScalarAsync<int>(
                "INSERT INTO Eurovip.proveedor_solicitud (id_cuenta, tipo, nombre, datos, clave) OUTPUT INSERTED.id_solicitud VALUES (@c, @t, @n, @d, @k)",
                new { c = cuenta.Id, t = tipo, n = op.Texto.Length > 0 ? op.Texto : Txt("nombre"), d = JsonSerializer.Serialize(limpio), k = JsonSerializer.Serialize(clave) });
            return (new { ok = true, id, opcion = op, mensaje = $"Listo: {c.El} {c.Singular} «{op.Texto}» ya está en tu lista. Eurovip lo va a revisar." }, null);
        }

        /// <summary>La opción para el buscador (valor, texto y detalle) del dato recién creado.</summary>
        private static Task<Opcion> OpcionAsync(string tipo, Dictionary<string, object?> clave, Dictionary<string, JsonElement> d)
        {
            string T(string col) => d.TryGetValue(col, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())?.Trim() ?? "" : "";
            string K(string col) => clave.TryGetValue(col, out var v) ? v?.ToString() ?? "" : "";
            return Task.FromResult(tipo switch
            {
                "hotel" => new Opcion { Valor = K("id_alojamiento"), Texto = T("nombre"), Extra = T("id_destino"), Extra2 = T("categoria") },
                "destino" => new Opcion { Valor = T("id_destino"), Texto = T("id_destino"), Extra = T("id_pais") },
                "aeropuerto" => new Opcion { Valor = K("id_estacion").ToUpperInvariant(), Texto = $"{K("id_estacion").ToUpperInvariant()} · {T("nombre")}", Extra = T("id_destino"), Extra2 = T("id_pais") },
                _ => new Opcion { Valor = K("id_habitacion"), Texto = T("nombre"), Extra = K("id_alojamiento") },
            });
        }

        public sealed class SolicitudFila
        {
            public int Id_Solicitud { get; set; }
            public int Id_Cuenta { get; set; }
            public string? Cuenta { get; set; }
            public string? Email { get; set; }
            public string Tipo { get; set; } = "";
            public string Nombre { get; set; } = "";
            public string? Datos { get; set; }
            public string? Clave { get; set; }
            public string Estado { get; set; } = "";
            public string? Motivo { get; set; }
            public DateTime Creado_En { get; set; }
            public DateTime? Revisado_En { get; set; }
            public string? Revisado_Por { get; set; }
        }

        /// <summary>Las solicitudes de un proveedor, o de todos (cuenta = null) para Eurovip.</summary>
        public async Task<(List<SolicitudFila> Items, Dictionary<string, int> Conteo)> ListarAsync(int? cuenta, string? estado)
        {
            using var cn = _db.Conexion();
            if (!await HayTablaAsync(cn)) return (new(), new());
            var items = (await cn.QueryAsync<SolicitudFila>(@"
                SELECT TOP 200 s.id_solicitud, s.id_cuenta, c.nombre AS Cuenta, c.email AS Email, s.tipo, s.nombre, s.datos, s.clave, s.estado, s.motivo, s.creado_en, s.revisado_en, s.revisado_por
                FROM Eurovip.proveedor_solicitud s LEFT JOIN Eurovip.proveedor_cuenta c ON c.id = s.id_cuenta
                WHERE (@cuenta IS NULL OR s.id_cuenta = @cuenta) AND (@estado IS NULL OR s.estado = @estado)
                ORDER BY CASE s.estado WHEN 'PENDIENTE' THEN 0 ELSE 1 END, s.creado_en DESC", new { cuenta, estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant() })).ToList();
            var conteo = (await cn.QueryAsync<(string E, int N)>("SELECT estado, COUNT(*) FROM Eurovip.proveedor_solicitud WHERE (@cuenta IS NULL OR id_cuenta = @cuenta) GROUP BY estado", new { cuenta }))
                .ToDictionary(x => x.E, x => x.N);
            return (items, conteo);
        }

        /// <summary>Hoteles que este proveedor pidió y todavía no están activos (para que los vea en su buscador).</summary>
        public async Task<List<Opcion>> HotelesPendientesAsync(int cuenta, string? q)
        {
            using var cn = _db.Conexion();
            if (!await HayTablaAsync(cn)) return new();
            return (await cn.QueryAsync<Opcion>(@"
                SELECT CAST(a.id_alojamiento AS varchar(12)) AS Valor, a.nombre AS Texto, a.id_destino AS Extra, a.categoria AS Extra2
                FROM Eurovip.proveedor_solicitud s
                JOIN Eurovip.alojamiento a ON a.id_idioma = 'ES' AND a.id_alojamiento = TRY_CAST(JSON_VALUE(s.clave, '$.id_alojamiento') AS int)
                WHERE s.id_cuenta = @cuenta AND s.tipo = 'hotel' AND s.estado <> 'RECHAZADA' AND a.activo = 0
                  AND (@q IS NULL OR a.nombre LIKE '%' + @q + '%')", new { cuenta, q = string.IsNullOrWhiteSpace(q) ? null : q.Trim() })).ToList();
        }

        /// <summary>Eurovip aprueba: el hotel pasa a activo. Devuelve el error o null.</summary>
        public async Task<string?> AprobarAsync(int id, string usuario)
        {
            using var cn = _db.Conexion();
            var s = await cn.QueryFirstOrDefaultAsync<SolicitudFila>("SELECT id_solicitud, tipo, clave, estado FROM Eurovip.proveedor_solicitud WHERE id_solicitud = @id", new { id });
            if (s is null) return "Esa solicitud ya no existe.";
            if (s.Estado == "APROBADA") return "Ya estaba aprobada.";
            if (s.Tipo == "imagen")
            {
                var datosImg = await cn.ExecuteScalarAsync<string?>("SELECT datos FROM Eurovip.proveedor_solicitud WHERE id_solicitud = @id", new { id });
                if (await AprobarImagenesAsync(id, datosImg) is string errImg) return errImg;
            }
            if (s.Tipo == "hotel")
                await cn.ExecuteAsync("UPDATE Eurovip.alojamiento SET activo = 1 WHERE id_idioma = 'ES' AND id_alojamiento = TRY_CAST(JSON_VALUE(@k, '$.id_alojamiento') AS int)", new { k = s.Clave });
            await cn.ExecuteAsync("UPDATE Eurovip.proveedor_solicitud SET estado = 'APROBADA', motivo = NULL, revisado_en = SYSDATETIME(), revisado_por = @u WHERE id_solicitud = @id", new { id, u = usuario });
            return null;
        }

        /// <summary>Eurovip rechaza (con motivo): el dato sigue creado pero el hotel queda inactivo y se le avisa al proveedor.</summary>
        public async Task<string?> RechazarAsync(int id, string? motivo, string usuario)
        {
            if (string.IsNullOrWhiteSpace(motivo)) return "Escribe el motivo para que el proveedor sepa qué hacer.";
            using var cn = _db.Conexion();
            var n = await cn.ExecuteAsync("UPDATE Eurovip.proveedor_solicitud SET estado = 'RECHAZADA', motivo = @m, revisado_en = SYSDATETIME(), revisado_por = @u WHERE id_solicitud = @id",
                new { id, m = motivo.Trim()[..Math.Min(500, motivo.Trim().Length)], u = usuario });
            return n == 0 ? "Esa solicitud ya no existe." : null;
        }
    }
}
