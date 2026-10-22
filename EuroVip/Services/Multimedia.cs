using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using EuroVip.Repositories;

namespace EuroVip.Services
{
    /// <summary>Un tipo de producto que guarda fotos en Eurovip.multimedia.</summary>
    /// <param name="Columna">Columna de multimedia con la referencia al producto.</param>
    /// <param name="Entero">La referencia es un número (id) o un texto (nombre del circuito, destino, país).</param>
    /// <param name="Carpeta">Carpeta dentro de la base de imágenes (en plural, como el documento esquema-multimedia.md).</param>
    /// <param name="Modulo">Módulo de Usuarios y accesos que permite cambiar sus fotos.</param>
    public sealed record TipoMultimedia(string Tipo, string Columna, bool Entero, string Carpeta, string Modulo, string Nombre);

    /// <summary>Una foto de un producto (fila de Eurovip.multimedia con su archivo de Eurovip.imagen).</summary>
    public class MultimediaItem
    {
        public int Id { get; set; }
        public int Id_Imagen { get; set; }
        public string Tipo { get; set; } = "";
        public string Uso { get; set; } = "";
        public short Orden { get; set; }
        public string Clave { get; set; } = "";
        /// <summary>Ruta dentro de la base de imágenes: "excursiones/37/3f2a….webp".</summary>
        public string Ruta { get; set; } = "";
        public string Url { get; set; } = "";
        public string? Texto_Alternativo { get; set; }
        public string? Nombre_Original { get; set; }
        public long Tamano_Bytes { get; set; }
        public int? Ancho { get; set; }
        public int? Alto { get; set; }
        public DateTime Creado_Utc { get; set; }
        /// <summary>false si la fila existe pero el archivo no está en este servidor (base compartida entre equipos).</summary>
        public bool Existe { get; set; } = true;
    }

    public interface IMultimedia
    {
        string UrlBase { get; }
        string CarpetaBase { get; }
        string Url(string ruta);
        TipoMultimedia? Tipo(string? tipo);
        Task<bool> ExisteProductoAsync(string tipo, string clave);
        Task<List<MultimediaItem>> ListarAsync(string tipo, string clave);
        Task<MultimediaItem?> ObtenerAsync(int id);
        /// <summary>Portadas de todos los productos de un tipo: clave (en minúsculas) → URL.</summary>
        Task<Dictionary<string, string>> PortadasAsync(string tipo);
        Task<string?> PortadaAsync(string tipo, string clave);
        Task<MultimediaItem> SubirAsync(string tipo, string clave, string uso, Stream origen, string? nombreOriginal, string? texto = null);
        Task<MultimediaItem?> QuitarAsync(int id);
        Task<int> QuitarUsoAsync(string tipo, string clave, string uso);
        Task OrdenarAsync(string tipo, string clave, IList<int> ids);
        Task<bool> TextoAsync(int id, string? texto);
        Task<MultimediaItem?> HacerPortadaAsync(int id);
        Task<int> EliminarProductoAsync(string tipo, string clave);
        Task<int> CambiarClaveAsync(string tipo, string vieja, string nueva, IDbConnection cn, IDbTransaction tx);
        Task<ResumenMigracion> MigrarImagenesViejasAsync();
        /// <summary>Foto de un día del itinerario de circuito: NO va en Eurovip.multimedia (se guarda en circuito_itinerario.caratula),
        /// pero el archivo va en la misma base: {base}/circuito_itinerario/{circuito}-dia-{n}.webp. Devuelve su URL.</summary>
        Task<string> GuardarItinerarioAsync(string circuito, int dia, Stream origen);
        /// <summary>Guarda una foto suelta (no va en Eurovip.multimedia) en {base}/{carpeta}/{nombre}.webp y devuelve su URL.
        /// carpeta: solo minúsculas, números, "-", "_" y "/" (ej. "cruceros/halloween-en-alta-mar/actividades").</summary>
        Task<string> GuardarArchivoAsync(string carpeta, string? nombre, Stream origen);
        /// <summary>Pone nombre legible a las fotos de multimedia que aún tienen código (destinos/punta-cana/punta-cana.webp).</summary>
        Task<ResumenRenombrar> RenombrarArchivosAsync();
        /// <summary>Dónde guarda las fotos este servidor y si puede escribir ahí (panel Fotos de Herramientas).</summary>
        EstadoCarpeta Estado();
        /// <summary>Días del itinerario de un circuito con su foto (circuito_itinerario.caratula).</summary>
        Task<List<DiaFoto>> DiasCircuitoAsync(string circuito);
        /// <summary>Cambia (origen con la imagen) o quita (origen null) la foto de un día y la guarda al momento. Devuelve la URL nueva.</summary>
        Task<string?> FotoDiaAsync(string circuito, short dia, Stream? origen);
    }

    public sealed class ResumenRenombrar
    {
        public int Renombradas { get; set; }
        public int YaBien { get; set; }
        public List<string> SinArchivo { get; } = new();
        public List<string> Errores { get; } = new();
    }

    public sealed record EstadoCarpeta(string Carpeta, string Url, bool Existe, bool SePuedeEscribir, int Archivos, string? Error);

    public sealed class DiaFoto
    {
        public short Dia { get; set; }
        public string? Titulo { get; set; }
        public string? Caratula { get; set; }
    }

    public class ResumenMigracion
    {
        public Dictionary<string, int> Pasadas { get; } = new();
        public Dictionary<string, int> YaEstaban { get; } = new();
        /// <summary>Fotos del itinerario de circuito copiadas a la carpeta nueva (circuito_itinerario.caratula actualizado).</summary>
        public int Itinerario { get; set; }
        /// <summary>Archivos del slider copiados de la carpeta anterior a la nueva (mismo nombre: carrusel no cambia).</summary>
        public int Slider { get; set; }
        public List<string> SinArchivo { get; } = new();
        public List<string> Errores { get; } = new();
    }

    /// <summary>
    /// Fotos de los productos según el documento esquema-multimedia.md (SPEC-013):
    ///  • Eurovip.imagen = el archivo; Eurovip.multimedia = qué producto lo usa, para qué (PORTADA, INTERMEDIA, GALERIA) y en qué orden.
    ///  • Archivo: {Multimedia:CarpetaFisica}/{tipo}/{nombre}.webp, sin subcarpeta por producto (pedido de la usuaria):
    ///    hotel/punta-cana.webp, traslado/traslado-punta-cana.webp, destino/punta-cana-banner.webp, galería -1, -2… — nunca se sobrescribe.
    ///    Carpetas (en plural, regla de la usuaria): destinos (destinos y paquetes), paises, traslados, excursiones, circuitos, circuito_itinerario, slider, cruceros,
    ///    hotel (pantalla Hoteles / HOTELES BOOKINGMOTOR) y hoteles/{id_alojamiento} (alojamientos).
    ///    URL: {Multimedia:Url}/ + la misma ruta. Por defecto C:\inetpub\wwwroot\photosVacacionalparce\eurovip\imagenes y https://fotos.eurovip.com.do/eurovip/imagenes.
    ///  • Tipos del documento (ALOJAMIENTO, CIRCUITO, EXCURSION, TRASLADO) + DESTINO, PAIS y HOTEL (tarjetas de la pantalla Hoteles).
    ///  • NO van aquí las fotos del itinerario de circuito ni las del slider: siguen como estaban.
    ///  • Para que lo viejo siga funcionando, al cambiar la PORTADA se copia la URL en la columna de antes
    ///    (destino.caratula + tarjetas de la Home, alojamiento.caratula, circuito.caratula; traslado.imagen se vacía).
    /// </summary>
    public class Multimedia : IMultimedia
    {
        public static readonly IReadOnlyDictionary<string, TipoMultimedia> Tipos = new Dictionary<string, TipoMultimedia>(StringComparer.OrdinalIgnoreCase)
        {
            ["ALOJAMIENTO"] = new("ALOJAMIENTO", "id_alojamiento", true, "hoteles", "herramientas", "alojamiento"),
            ["CIRCUITO"] = new("CIRCUITO", "nombre_circuito", false, "circuitos", "circuitos", "circuito"),
            ["EXCURSION"] = new("EXCURSION", "id_excursion", true, "excursiones", "excursiones", "excursión"),
            ["TRASLADO"] = new("TRASLADO", "id_traslado", true, "traslados", "traslados", "traslado"),
            ["DESTINO"] = new("DESTINO", "id_destino", false, "destinos", "herramientas", "destino"),
            ["PAIS"] = new("PAIS", "id_pais", false, "paises", "herramientas", "país"),
            ["HOTEL"] = new("HOTEL", "id_hotel", true, "hotel", "hoteles", "hotel"),   // pantalla Hoteles (sección HOTELES BOOKINGMOTOR) → hotel/
            ["CRUCERO"] = new("CRUCERO", "id_crucero", true, "cruceros", "cruceros", "crucero"),
            // Foto propia de una tarjeta de la Home (Eurovip.inicio.id): no se comparte con el destino ni con otras tarjetas
            ["INICIO"] = new("INICIO", "id_inicio", true, "inicio", "home_web", "tarjeta"),
        };
        public static readonly string[] Usos = { "PORTADA", "INTERMEDIA", "GALERIA" };

        private readonly ICircuitoRepository _db;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _cfg;
        private readonly ILogger<Multimedia> _log;

        public string CarpetaBase { get; }
        public string UrlBase { get; }

        public Multimedia(ICircuitoRepository db, IWebHostEnvironment env, IConfiguration cfg, ILogger<Multimedia> log)
        {
            _db = db; _env = env; _log = log; _cfg = cfg;
            CarpetaBase = CarpetaDe(cfg);
            UrlBase = UrlDe(cfg);
        }

        // Carpeta y URL de las fotos (pedido de la usuaria). Si appsettings.json no trae la sección Multimedia, se usan ESTAS
        // (antes caía en wwwroot\multimedia del proyecto y la URL quedaba "/multimedia/…", que la web no puede ver).
        // Todas las imágenes bajo eurovip\imagenes (eurovip\PDF u otras carpetas quedan para otros tipos de archivo).
        public const string CarpetaPorDefecto = @"C:\inetpub\wwwroot\photosVacacionalparce\eurovip\imagenes";
        public const string UrlPorDefecto = "https://fotos.eurovip.com.do/eurovip/imagenes";
        public static string CarpetaDe(IConfiguration cfg) => string.IsNullOrWhiteSpace(cfg["Multimedia:CarpetaFisica"]) ? CarpetaPorDefecto : cfg["Multimedia:CarpetaFisica"]!.Trim();
        public static string UrlDe(IConfiguration cfg) => (string.IsNullOrWhiteSpace(cfg["Multimedia:Url"]) ? UrlPorDefecto : cfg["Multimedia:Url"]!.Trim()).TrimEnd('/');

        public string Url(string ruta) => $"{UrlBase}/{ruta}";
        public TipoMultimedia? Tipo(string? tipo) => tipo is not null && Tipos.TryGetValue(tipo.Trim(), out var t) ? t : null;

        /// <summary>«Ruta Colonial» → "ruta-colonial": minúsculas, sin acentos, guiones (lo que admite CK_Eurovip_imagen_ruta).</summary>
        public static string Slug(string? texto)
        {
            var sb = new StringBuilder();
            foreach (var ch in (texto ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant())
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(ch is >= 'a' and <= 'z' or >= '0' and <= '9' ? ch : '-');
            }
            return Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        }

        /// <summary>Texto → nombre de archivo: «Punta Cana» → "punta-cana" (máx. 60 letras; si queda vacío, "otro").</summary>
        public static string NombreArchivo(string? texto, string otro = "foto")
        {
            var s = Slug(texto);
            if (s.Length > 60) s = s[..60].Trim('-');
            return s == "" ? otro : s;
        }

        /// <summary>
        /// Carpeta y nombre de la foto (pedido de la usuaria): {tipo}/{nombre}. Las tarjetas de la Home van en la carpeta de lo que
        /// muestran: paquetes (ofertas) → destinos/punta-cana; hoteles (bookingmotor) → hotel/hotel-kristal; excursiones → excursiones/excursiones-punta-cana; traslados → traslados/traslados-punta-cana…
        /// </summary>
        private async Task<(string Carpeta, string Nombre)> UbicacionAsync(TipoMultimedia t, object v)
        {
            if (t.Tipo == "INICIO")
            {
                using var cn = _db.Conexion();
                var (tipo, destino) = await cn.QueryFirstOrDefaultAsync<(string?, string?)>("SELECT tipo_servicio, id_destino FROM Eurovip.inicio WHERE id = @v", new { v });
                // Paquetes (ofertas) → destinos/{destino}; Hoteles (bookingmotor) → hotel/{hotel}; los demás → {tipos}/{tipos}-{destino}
                var carpeta = (tipo ?? "").ToUpperInvariant() switch { "OFERTA" or "COMBINADO" or "" => "destinos", "HOTEL" => "hotel", "EXCURSION" => "excursiones", "TRASLADO" => "traslados",
                                                                    "CIRCUITO" => "circuitos", "CRUCERO" => "cruceros", var x => NombreArchivo(x, "inicio") };
                var nom = NombreArchivo(destino, "tarjeta-" + v);
                return (carpeta + "/", carpeta is "destinos" or "hotel" ? nom : NombreArchivo($"{carpeta} {nom}"));
            }
            // Alojamientos: una carpeta por hotel con su número → hoteles/{id_alojamiento}/{nombre}.webp
            if (t.Tipo == "ALOJAMIENTO") return ($"hoteles/{v}/", await NombreProductoAsync(t, v));
            return (t.Carpeta + "/", await NombreProductoAsync(t, v));
        }

        /// <summary>Nombre del producto para sus archivos (pedido de la usuaria: «si es Punta Cana, que sea punta-cana»).</summary>
        private async Task<string> NombreProductoAsync(TipoMultimedia t, object v)
        {
            var sql = t.Tipo switch
            {
                "ALOJAMIENTO" => "SELECT TOP 1 nombre FROM Eurovip.alojamiento WHERE id_alojamiento = @v",
                "EXCURSION" => "SELECT TOP 1 nombre FROM Eurovip.servicio_excursion WHERE id_Excursion = @v",
                "TRASLADO" => "SELECT TOP 1 nombre FROM Eurovip.traslado WHERE id_traslado = @v",
                "PAIS" => "SELECT TOP 1 nombre FROM Eurovip.pais WHERE id_pais = @v",
                "HOTEL" => "SELECT TOP 1 id_destino FROM Eurovip.inicio WHERE id_servicio = @v AND tipo_servicio = 'HOTEL'",
                "CRUCERO" => "SELECT TOP 1 titulo FROM Eurovip.crucero WHERE id_crucero = @v",
                "INICIO" => "SELECT TOP 1 LOWER(tipo_servicio) + ' ' + id_destino FROM Eurovip.inicio WHERE id = @v",
                _ => null   // CIRCUITO y DESTINO: la clave ya es el nombre
            };
            string? n = null;
            if (sql is not null) { using var cn = _db.Conexion(); n = await cn.ExecuteScalarAsync<string?>(sql, new { v }); }
            return NombreArchivo(string.IsNullOrWhiteSpace(n) ? Convert.ToString(v, CultureInfo.InvariantCulture) : n, t.Carpeta.TrimEnd('s'));
        }

        /// <summary>
        /// Escribe el archivo con el primer nombre libre: "punta-cana", "punta-cana-1", "punta-cana-2"… (con numerar, desde "-1").
        /// Nunca pisa un archivo que ya existe (CreateNew) ni repite uno que esté en Eurovip.imagen.
        /// </summary>
        private async Task<string> EscribirNuevoAsync(string carpeta, string nombreBase, bool numerar, byte[] datos)
        {
            nombreBase = NombreArchivo(Regex.Replace((nombreBase ?? "").ToLowerInvariant(), "[^a-z0-9_-]", "-"));
            for (var n = numerar ? 1 : 0; n < 5000; n++)
            {
                var nombre = n == 0 ? nombreBase : $"{nombreBase}-{n}";
                var fisica = Fisica(carpeta + nombre + ".webp");
                if (File.Exists(fisica)) continue;
                using (var cn = _db.Conexion())
                    if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.imagen WHERE ruta_relativa = @carpeta AND nombre_archivo = @nombre AND extension = 'webp'", new { carpeta, nombre }) > 0) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(fisica)!);
                try { await using var fs = new FileStream(fisica, FileMode.CreateNew, FileAccess.Write); await fs.WriteAsync(datos); return nombre; }
                catch (IOException) when (File.Exists(fisica)) { /* otro lo escribió a la vez: siguiente número */ }
            }
            throw new InvalidOperationException("No se encontró un nombre libre para la foto.");
        }

        private static object Valor(TipoMultimedia t, string clave) =>
            t.Entero ? int.TryParse(clave, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n
                       : throw new InvalidOperationException($"El {t.Nombre} tiene que ser un número.")
                     : clave.Trim();

        private TipoMultimedia Validar(string tipo, string clave)
        {
            var t = Tipo(tipo) ?? throw new InvalidOperationException("Tipo de producto desconocido.");
            if (string.IsNullOrWhiteSpace(clave)) throw new InvalidOperationException($"Falta el {t.Nombre}.");
            return t;
        }

        private string Fisica(string ruta)
        {
            // La ruta ya cumple CK_Eurovip_imagen_ruta (sin "..", "\" ni ":"), pero se comprueba igual que no salga de la base.
            var completa = Path.GetFullPath(Path.Combine(CarpetaBase, ruta.Replace('/', Path.DirectorySeparatorChar)));
            if (!completa.StartsWith(Path.GetFullPath(CarpetaBase), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ruta no válida.");
            return completa;
        }

        private const string Sel = @"SELECT v.id AS Id, v.id_imagen AS Id_Imagen, v.tipo AS Tipo, v.uso AS Uso, v.orden AS Orden, v.clave AS Clave, v.ruta AS Ruta,
                                            v.texto_alternativo AS Texto_Alternativo, v.nombre_original AS Nombre_Original, v.tamano_bytes AS Tamano_Bytes,
                                            v.ancho AS Ancho, v.alto AS Alto, v.creado_utc AS Creado_Utc
                                     FROM Eurovip.vMultimedia v";

        private MultimediaItem Completar(MultimediaItem m)
        {
            m.Url = Url(m.Ruta);
            m.Existe = File.Exists(Fisica(m.Ruta));
            return m;
        }

        public async Task<bool> ExisteProductoAsync(string tipo, string clave)
        {
            var t = Validar(tipo, clave);
            using var cn = _db.Conexion();
            var sql = t.Tipo switch
            {
                "ALOJAMIENTO" => "SELECT COUNT(*) FROM Eurovip.alojamiento WHERE id_alojamiento = @v",
                "CIRCUITO" => "SELECT COUNT(*) FROM Eurovip.circuito WHERE nombre_circuito = @v",
                "EXCURSION" => "SELECT COUNT(*) FROM Eurovip.servicio_excursion WHERE id_Excursion = @v",
                "TRASLADO" => "SELECT COUNT(*) FROM Eurovip.traslado WHERE id_traslado = @v",
                "DESTINO" => "SELECT COUNT(*) FROM Eurovip.destino WHERE id_destino = @v",
                "PAIS" => "SELECT COUNT(*) FROM Eurovip.pais WHERE id_pais = @v",
                "HOTEL" => "SELECT COUNT(*) FROM Eurovip.inicio WHERE id_servicio = @v AND tipo_servicio = 'HOTEL'",
                "CRUCERO" => "SELECT COUNT(*) FROM Eurovip.crucero WHERE id_crucero = @v",
                "INICIO" => "SELECT COUNT(*) FROM Eurovip.inicio WHERE id = @v",
                _ => throw new InvalidOperationException("Tipo de producto desconocido.")
            };
            return await cn.ExecuteScalarAsync<int>(sql, new { v = Valor(t, clave) }) > 0;
        }

        public async Task<List<MultimediaItem>> ListarAsync(string tipo, string clave)
        {
            var t = Validar(tipo, clave);
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<MultimediaItem>($"{Sel} WHERE v.tipo = @tipo AND v.{t.Columna} = @v ORDER BY CASE v.uso WHEN 'PORTADA' THEN 0 WHEN 'INTERMEDIA' THEN 1 ELSE 2 END, v.orden",
                new { tipo = t.Tipo, v = Valor(t, clave) })).Select(Completar).ToList();
        }

        public async Task<MultimediaItem?> ObtenerAsync(int id)
        {
            using var cn = _db.Conexion();
            var m = await cn.QueryFirstOrDefaultAsync<MultimediaItem>($"{Sel} WHERE v.id = @id", new { id });
            return m is null ? null : Completar(m);
        }

        public async Task<Dictionary<string, string>> PortadasAsync(string tipo)
        {
            var t = Tipo(tipo) ?? throw new InvalidOperationException("Tipo de producto desconocido.");
            using var cn = _db.Conexion();
            var filas = await cn.QueryAsync<(string Clave, string Ruta)>("SELECT clave, ruta FROM Eurovip.vMultimedia WHERE tipo = @tipo AND uso = 'PORTADA'", new { tipo = t.Tipo });
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (c, r) in filas) d[c.Trim()] = Url(r);
            return d;
        }

        public async Task<string?> PortadaAsync(string tipo, string clave)
        {
            var t = Validar(tipo, clave);
            using var cn = _db.Conexion();
            var ruta = await cn.ExecuteScalarAsync<string?>($"SELECT TOP 1 ruta FROM Eurovip.vMultimedia WHERE tipo = @tipo AND {t.Columna} = @v AND uso = 'PORTADA'",
                new { tipo = t.Tipo, v = Valor(t, clave) });
            return ruta is null ? null : Url(ruta);
        }

        public async Task<MultimediaItem> SubirAsync(string tipo, string clave, string uso, Stream origen, string? nombreOriginal, string? texto = null)
        {
            var t = Validar(tipo, clave);
            uso = (uso ?? "").Trim().ToUpperInvariant();
            if (!Usos.Contains(uso)) throw new InvalidOperationException("Uso de la foto desconocido.");
            if (uso == "INTERMEDIA" && t.Tipo is not ("CIRCUITO" or "CRUCERO")) throw new InvalidOperationException("La foto intermedia es solo para circuitos y cruceros.");
            var v = Valor(t, clave);
            // destinos/punta-cana/punta-cana.webp · …/punta-cana-banner.webp · galería …/punta-cana-1.webp, -2…
            var (carpeta, producto) = await UbicacionAsync(t, v);
            // Defensa: solo minúsculas, números, «-» y «_» (lo que exigen CK_Eurovip_imagen_ruta / _nombre); si algo raro se colara, se limpia
            carpeta = Regex.Replace(carpeta.ToLowerInvariant(), "[^a-z0-9/_-]", "-");
            producto = NombreArchivo(Regex.Replace(producto.ToLowerInvariant(), "[^a-z0-9_-]", "-"), t.Carpeta.TrimEnd('s'));

            var (datos, ancho, alto) = await ImagenesLocales.ConvertirWebpAsync(origen);
            var nombreBase = uso == "INTERMEDIA" ? producto + "-banner" : producto;

            // Portada y banner: SIEMPRE con su nombre (pedido de la usuaria: «miches, no miches-2; si ya existe, bórrala y ponla»).
            // Si ese archivo es de OTRO producto con el mismo nombre (dos excursiones «Isla Saona»), no se pisa: se numera.
            int? reutilizar = null;          // id de Eurovip.imagen que ya tiene esa ruta y se reemplaza
            var fijo = uso != "GALERIA";
            if (fijo)
                using (var cn = _db.Conexion())
                {
                    var ya = await cn.QueryFirstOrDefaultAsync<int?>("SELECT id FROM Eurovip.imagen WHERE ruta_relativa = @carpeta AND nombre_archivo = @nombreBase AND extension = 'webp'", new { carpeta, nombreBase });
                    var deOtro = ya is int yid && await cn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Eurovip.multimedia WHERE id_imagen = @yid AND NOT (tipo = @tipo AND {t.Columna} = @v AND uso = @uso)", new { yid, tipo = t.Tipo, v, uso }) > 0;
                    if (deOtro) fijo = false; else reutilizar = ya;
                }

            string nombre, fisica, temporal;
            if (fijo)
            {
                // Se escribe aparte y se pone en su sitio al confirmar la base (si algo falla, la foto de antes sigue intacta)
                nombre = nombreBase;
                fisica = Fisica(carpeta + nombre + ".webp");
                temporal = fisica + "." + Guid.NewGuid().ToString("N") + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(fisica)!);
                await File.WriteAllBytesAsync(temporal, datos);
            }
            else
            {
                nombre = await EscribirNuevoAsync(carpeta, nombreBase, uso == "GALERIA", datos);
                fisica = Fisica(carpeta + nombre + ".webp");                 // galería: archivo nuevo, nunca se sobrescribe
                temporal = fisica;
            }

            int idMultimedia;
            var imagenesViejas = new List<int>();
            try
            {
                using var cn = _db.Conexion();
                await cn.OpenAsync();
                using var tx = cn.BeginTransaction();
                short orden = 1;
                if (uso == "GALERIA")
                    orden = await cn.ExecuteScalarAsync<short>($"SELECT CAST(ISNULL(MAX(orden), 0) + 1 AS smallint) FROM Eurovip.multimedia WITH (UPDLOCK, HOLDLOCK) WHERE tipo = @tipo AND {t.Columna} = @v AND uso = 'GALERIA'",
                        new { tipo = t.Tipo, v }, tx);
                else
                {
                    // Portada e intermedia son una sola: la anterior se quita (su archivo se borra si nadie más lo usa)
                    var anterior = (await cn.QueryAsync<(int Id, int IdImagen)>($"SELECT id, id_imagen FROM Eurovip.multimedia WITH (UPDLOCK, HOLDLOCK) WHERE tipo = @tipo AND {t.Columna} = @v AND uso = @uso",
                        new { tipo = t.Tipo, v, uso }, tx)).ToList();
                    foreach (var x in anterior)
                    {
                        await cn.ExecuteAsync("DELETE FROM Eurovip.multimedia WHERE id = @Id", new { x.Id }, tx);
                        imagenesViejas.Add(x.IdImagen);
                    }
                }
                int idImagen;
                if (reutilizar is int rid)
                {
                    // Mismo nombre que antes: se actualiza esa fila en vez de crear otra
                    await cn.ExecuteAsync(@"UPDATE Eurovip.imagen SET nombre_original = @original, tamano_bytes = @bytes, ancho = @ancho, alto = @alto, creado_utc = SYSUTCDATETIME() WHERE id = @rid",
                        new { rid, original = Recortar(nombreOriginal, 255), bytes = (long)datos.Length, ancho, alto }, tx);
                    idImagen = rid;
                }
                else
                    idImagen = await cn.ExecuteScalarAsync<int>(@"
                        INSERT INTO Eurovip.imagen (ruta_relativa, nombre_archivo, extension, nombre_original, tamano_bytes, ancho, alto)
                        VALUES (@carpeta, @nombre, 'webp', @original, @bytes, @ancho, @alto);
                        SELECT CAST(SCOPE_IDENTITY() AS int);",
                        new { carpeta, nombre, original = Recortar(nombreOriginal, 255), bytes = (long)datos.Length, ancho, alto }, tx);
                imagenesViejas.Remove(idImagen);
                idMultimedia = await cn.ExecuteScalarAsync<int>($@"
                    INSERT INTO Eurovip.multimedia (id_imagen, tipo, uso, {t.Columna}, orden, texto_alternativo)
                    VALUES (@idImagen, @tipo, @uso, @v, @orden, @texto);
                    SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new { idImagen, tipo = t.Tipo, uso, v, orden, texto = Recortar(texto, 250) }, tx);
                tx.Commit();
            }
            catch (Exception ex)
            {
                try { File.Delete(temporal); } catch { /* el archivo huérfano no rompe nada */ }
                // Si la base rechaza la ruta o el nombre, el mensaje dice cuál fue (para encontrarlo enseguida)
                if (ex.Message.Contains("CK_Eurovip_imagen")) throw new InvalidOperationException($"La base no aceptó la foto «{carpeta}{nombre}.webp» ({t.Tipo} {clave}): {ex.Message}", ex);
                throw;
            }
            if (temporal != fisica) File.Move(temporal, fisica, overwrite: true);   // la foto de antes con ese nombre se reemplaza

            // Las de antes de este producto (miches-1, miches-2… de versiones anteriores) se borran si nadie más las usa
            if (imagenesViejas.Count > 0) await LimpiarHuerfanasAsync(imagenesViejas);

            if (uso == "PORTADA") await SincronizarProductoAsync(t, clave);
            using (var cn = _db.Conexion())
                return Completar((await cn.QueryFirstAsync<MultimediaItem>($"{Sel} WHERE v.id = @idMultimedia", new { idMultimedia })));
        }

        public async Task<MultimediaItem?> QuitarAsync(int id)
        {
            MultimediaItem? m;
            using (var cn = _db.Conexion())
            {
                m = await cn.QueryFirstOrDefaultAsync<MultimediaItem>($"{Sel} WHERE v.id = @id", new { id });
                if (m is null) return null;
                await cn.ExecuteAsync("DELETE FROM Eurovip.multimedia WHERE id = @id", new { id });
            }
            var t = Tipos[m.Tipo];
            if (m.Uso == "GALERIA") await RenumerarGaleriaAsync(t, m.Clave);
            await LimpiarHuerfanasAsync(new[] { m.Id_Imagen });
            if (m.Uso == "PORTADA") await SincronizarProductoAsync(t, m.Clave, Url(m.Ruta));
            return Completar(m);
        }

        public async Task<int> QuitarUsoAsync(string tipo, string clave, string uso)
        {
            var t = Validar(tipo, clave);
            List<int> ids;
            using (var cn = _db.Conexion())
                ids = (await cn.QueryAsync<int>($"SELECT id FROM Eurovip.multimedia WHERE tipo = @tipo AND {t.Columna} = @v AND uso = @uso",
                    new { tipo = t.Tipo, v = Valor(t, clave), uso })).ToList();
            foreach (var i in ids) await QuitarAsync(i);
            return ids.Count;
        }

        /// <summary>Nuevo orden de la galería (ids de multimedia en el orden deseado). Se hace en dos pasos para no chocar con UQ_..._orden.</summary>
        public async Task OrdenarAsync(string tipo, string clave, IList<int> ids)
        {
            var t = Validar(tipo, clave);
            var v = Valor(t, clave);
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            var actuales = (await cn.QueryAsync<int>($"SELECT id FROM Eurovip.multimedia WITH (UPDLOCK, HOLDLOCK) WHERE tipo = @tipo AND {t.Columna} = @v AND uso = 'GALERIA' ORDER BY orden",
                new { tipo = t.Tipo, v }, tx)).ToList();
            var nuevo = ids.Where(actuales.Contains).Distinct().Concat(actuales.Where(a => !ids.Contains(a))).ToList();
            await cn.ExecuteAsync($"UPDATE Eurovip.multimedia SET orden = orden + 10000 WHERE tipo = @tipo AND {t.Columna} = @v AND uso = 'GALERIA'", new { tipo = t.Tipo, v }, tx);
            for (var i = 0; i < nuevo.Count; i++)
                await cn.ExecuteAsync("UPDATE Eurovip.multimedia SET orden = @o WHERE id = @id", new { o = (short)(i + 1), id = nuevo[i] }, tx);
            tx.Commit();
        }

        public async Task<bool> TextoAsync(int id, string? texto)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("UPDATE Eurovip.multimedia SET texto_alternativo = @texto WHERE id = @id", new { id, texto = Recortar(texto, 250) }) > 0;
        }

        /// <summary>Una foto de la galería pasa a ser la portada; la portada anterior pasa al final de la galería.</summary>
        public async Task<MultimediaItem?> HacerPortadaAsync(int id)
        {
            MultimediaItem? m;
            using (var cn = _db.Conexion())
            {
                await cn.OpenAsync();
                using var tx = cn.BeginTransaction();
                m = await cn.QueryFirstOrDefaultAsync<MultimediaItem>($"{Sel} WHERE v.id = @id", new { id }, tx);
                if (m is null || m.Uso == "PORTADA") { tx.Commit(); return m is null ? null : Completar(m); }
                var t = Tipos[m.Tipo];
                var v = Valor(t, m.Clave);
                var maximo = await cn.ExecuteScalarAsync<short>($"SELECT CAST(ISNULL(MAX(orden), 0) AS smallint) FROM Eurovip.multimedia WHERE tipo = @tipo AND {t.Columna} = @v AND uso = 'GALERIA'", new { tipo = t.Tipo, v }, tx);
                await cn.ExecuteAsync($"UPDATE Eurovip.multimedia SET uso = 'GALERIA', orden = @o WHERE tipo = @tipo AND {t.Columna} = @v AND uso = 'PORTADA'",
                    new { tipo = t.Tipo, v, o = (short)(maximo + 1) }, tx);
                await cn.ExecuteAsync("UPDATE Eurovip.multimedia SET uso = 'PORTADA', orden = 1 WHERE id = @id", new { id }, tx);
                tx.Commit();
            }
            var tipo = Tipos[m.Tipo];
            await RenumerarGaleriaAsync(tipo, m.Clave);
            await SincronizarProductoAsync(tipo, m.Clave);
            using var c2 = _db.Conexion();
            return Completar(await c2.QueryFirstAsync<MultimediaItem>($"{Sel} WHERE v.id = @id", new { id }));
        }

        public async Task<int> EliminarProductoAsync(string tipo, string clave)
        {
            var t = Validar(tipo, clave);
            List<int> imagenes;
            int n;
            using (var cn = _db.Conexion())
            {
                imagenes = (await cn.QueryAsync<int>($"SELECT id_imagen FROM Eurovip.multimedia WHERE tipo = @tipo AND {t.Columna} = @v", new { tipo = t.Tipo, v = Valor(t, clave) })).ToList();
                n = await cn.ExecuteAsync($"DELETE FROM Eurovip.multimedia WHERE tipo = @tipo AND {t.Columna} = @v", new { tipo = t.Tipo, v = Valor(t, clave) });
            }
            await LimpiarHuerfanasAsync(imagenes);
            return n;
        }

        /// <summary>Si se renombra un circuito (su clave es el nombre), sus fotos lo siguen. Los archivos se quedan en la carpeta del nombre viejo (está guardada en cada fila).</summary>
        public async Task<int> CambiarClaveAsync(string tipo, string vieja, string nueva, IDbConnection cn, IDbTransaction tx)
        {
            var t = Validar(tipo, vieja);
            if (string.Equals(vieja.Trim(), nueva.Trim(), StringComparison.Ordinal)) return 0;
            return await cn.ExecuteAsync($"UPDATE Eurovip.multimedia SET {t.Columna} = @nueva WHERE tipo = @tipo AND {t.Columna} = @vieja",
                new { tipo = t.Tipo, vieja = Valor(t, vieja), nueva = Valor(t, nueva) }, tx);
        }

        private async Task RenumerarGaleriaAsync(TipoMultimedia t, string clave)
        {
            List<int> ids;
            using (var cn = _db.Conexion())
                ids = (await cn.QueryAsync<int>($"SELECT id FROM Eurovip.multimedia WHERE tipo = @tipo AND {t.Columna} = @v AND uso = 'GALERIA' ORDER BY orden",
                    new { tipo = t.Tipo, v = Valor(t, clave) })).ToList();
            await OrdenarAsync(t.Tipo, clave, ids);
        }

        /// <summary>Borra de Eurovip.imagen (y del disco) los archivos que ya no usa ningún producto.</summary>
        private async Task LimpiarHuerfanasAsync(IEnumerable<int> idsImagen)
        {
            var ids = idsImagen.Distinct().ToList();
            if (ids.Count == 0) return;
            using var cn = _db.Conexion();
            var huerfanas = await cn.QueryAsync<(int Id, string Ruta)>(@"
                SELECT i.id, i.ruta_relativa + i.nombre_archivo + '.' + i.extension FROM Eurovip.imagen i
                WHERE i.id IN @ids AND NOT EXISTS (SELECT 1 FROM Eurovip.multimedia m WHERE m.id_imagen = i.id)", new { ids });
            foreach (var (id, ruta) in huerfanas)
            {
                await cn.ExecuteAsync("DELETE FROM Eurovip.imagen WHERE id = @id", new { id });
                try { var f = Fisica(ruta); if (File.Exists(f)) File.Delete(f); }
                catch (Exception ex) { _log.LogWarning(ex, "No se pudo borrar el archivo {Ruta}", ruta); }
            }
        }

        /// <summary>
        /// Copia la portada en la columna de antes para que todo lo que la lee siga igual (Home, portal, flyers…).
        /// Si ya no hay portada, se vacía la columna solo si tenía la URL de la que se quitó (no se pisa una URL externa).
        /// </summary>
        private async Task SincronizarProductoAsync(TipoMultimedia t, string clave, string? urlQuitada = null)
        {
            var url = await PortadaAsync(t.Tipo, clave);
            using var cn = _db.Conexion();
            var p = new { v = Valor(t, clave), url, quitada = urlQuitada ?? "\u0001" };
            switch (t.Tipo)
            {
                case "DESTINO":
                    await cn.ExecuteAsync(url is not null
                        ? @"UPDATE Eurovip.destino SET caratula = @url WHERE id_destino = @v;
                            UPDATE Eurovip.inicio SET caratulaDestino = @url WHERE id_destino = @v AND tipo_servicio IN ('OFERTA', 'COMBINADO');"
                        : @"UPDATE Eurovip.destino SET caratula = NULL WHERE id_destino = @v AND caratula = @quitada;
                            UPDATE Eurovip.inicio SET caratulaDestino = NULL WHERE id_destino = @v AND tipo_servicio IN ('OFERTA', 'COMBINADO') AND caratulaDestino = @quitada;", p);
                    // Las tarjetas de PAQUETES (ofertas) de ese destino usan la foto del destino (pedido de la usuaria: «esos son destinos»).
                    // Las de excursiones y traslados NO: tienen su propia foto (tipo INICIO).
                    break;
                case "INICIO":
                    // Cada tarjeta tiene su foto: la web la lee de inicio.caratulaDestino (la foto del destino ya no se copia en las tarjetas)
                    await cn.ExecuteAsync(url is not null
                        ? "UPDATE Eurovip.inicio SET caratulaDestino = @url WHERE id = @v"
                        : "UPDATE Eurovip.inicio SET caratulaDestino = NULL WHERE id = @v AND caratulaDestino = @quitada", p);
                    break;
                case "ALOJAMIENTO":
                    await cn.ExecuteAsync(url is not null
                        ? "UPDATE Eurovip.alojamiento SET caratula = @url WHERE id_alojamiento = @v"
                        : "UPDATE Eurovip.alojamiento SET caratula = NULL WHERE id_alojamiento = @v AND caratula = @quitada", p);
                    break;
                case "CIRCUITO":
                    await cn.ExecuteAsync(url is not null
                        ? "UPDATE Eurovip.circuito SET caratula = @url WHERE nombre_circuito = @v"
                        : "UPDATE Eurovip.circuito SET caratula = NULL WHERE nombre_circuito = @v AND caratula = @quitada", p);
                    break;
                case "TRASLADO":
                    // traslado.imagen era el archivo de wwwroot/imagenes/traslados: con portada nueva ya no se usa
                    if (url is not null) await cn.ExecuteAsync("UPDATE Eurovip.traslado SET imagen = NULL WHERE id_traslado = @v", p);
                    break;
            }
        }

        private static string? Recortar(string? s, int max)
        {
            s = s?.Trim();
            return string.IsNullOrEmpty(s) ? null : s.Length > max ? s[..max] : s;
        }

        // ============================================================ paso de las fotos viejas
        /// <summary>
        /// Pasa a Eurovip.multimedia (como PORTADA) las fotos que estaban en wwwroot/imagenes: excursiones, traslados, portadas
        /// de circuito, destinos (destino.caratula), alojamientos (alojamiento.caratula local) y hoteles de la pantalla Hoteles.
        /// No toca el itinerario de circuito ni el slider. No borra los archivos viejos. Se puede repetir: salta lo que ya tiene portada.
        /// </summary>
        public async Task<string> GuardarItinerarioAsync(string circuito, int dia, Stream origen)
        {
            var slug = Slug(circuito);
            if (slug == "") throw new InvalidOperationException("Primero escribe el nombre del circuito.");
            slug = NombreArchivo(slug);
            var (datos, _, _) = await ImagenesLocales.ConvertirWebpAsync(origen);
            return Url(await ReemplazarAsync($"circuito_itinerario/{slug}-dia-{dia}.webp", datos));
        }

        public async Task<string> GuardarArchivoAsync(string carpeta, string? nombre, Stream origen)
        {
            carpeta = (carpeta ?? "").Trim().Trim('/');
            if (carpeta == "" || !Regex.IsMatch(carpeta, "^[a-z0-9_-]+(/[a-z0-9_-]+)*$")) throw new InvalidOperationException("Carpeta no válida.");
            var (datos, _, _) = await ImagenesLocales.ConvertirWebpAsync(origen);
            return Url(await ReemplazarAsync($"{carpeta}/{NombreArchivo(nombre, carpeta.Split('/').Last())}.webp", datos));
        }

        /// <summary>
        /// Foto suelta con nombre fijo: si ya existe un archivo con ese nombre se REEMPLAZA (regla de la usuaria para todas las fotos:
        /// «si ya existe, la borras y la agregas con su nombre»). Se escribe aparte y luego se pone en su sitio.
        /// </summary>
        private async Task<string> ReemplazarAsync(string ruta, byte[] datos)
        {
            var fisica = Fisica(ruta);
            Directory.CreateDirectory(Path.GetDirectoryName(fisica)!);
            var temporal = fisica + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporal, datos);
            File.Move(temporal, fisica, overwrite: true);
            return ruta;
        }

        public async Task<ResumenRenombrar> RenombrarArchivosAsync()
        {
            var r = new ResumenRenombrar();
            List<(int IdImg, string Tipo, string Clave, string Uso, short Orden, string Carp, string Nom, string Ext)> filas;
            using (var cn = _db.Conexion())
                filas = (await cn.QueryAsync<(int, string, string, string, short, string, string, string)>(@"
                    SELECT v.id_imagen, v.tipo, v.clave, v.uso, v.orden, i.ruta_relativa, i.nombre_archivo, i.extension
                      FROM Eurovip.vMultimedia v JOIN Eurovip.imagen i ON i.id = v.id_imagen
                     ORDER BY v.tipo, v.clave, CASE v.uso WHEN 'PORTADA' THEN 0 WHEN 'INTERMEDIA' THEN 1 ELSE 2 END, v.orden")).ToList();
            var vacias = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in filas)
            {
                try
                {
                    var t = Tipo(f.Tipo)!;
                    var (carpeta, producto) = await UbicacionAsync(t, Valor(t, f.Clave));
                    var deseado = f.Uso == "PORTADA" ? producto : f.Uso == "INTERMEDIA" ? producto + "-banner" : $"{producto}-{f.Orden}";
                    if (f.Carp == carpeta && f.Nom == deseado) { r.YaBien++; continue; }
                    var vieja = Fisica(f.Carp + f.Nom + "." + f.Ext);
                    if (!File.Exists(vieja)) { r.SinArchivo.Add($"{t.Nombre} {f.Clave} ({f.Uso.ToLowerInvariant()}): {f.Carp}{f.Nom}.{f.Ext}"); continue; }
                    // Mismo nombre siempre (la-romana, no la-romana-2): si ya hay un archivo con ese nombre que nadie usa, se borra.
                    // Solo se numera si ese nombre lo usa OTRA foto registrada (otro producto que se llama igual).
                    var nombre = deseado;
                    using (var cn0 = _db.Conexion())
                        for (var n = 1; await cn0.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.imagen WHERE ruta_relativa = @carpeta AND nombre_archivo = @nombre AND extension = @Ext AND id <> @IdImg",
                                 new { carpeta, nombre, f.Ext, f.IdImg }) > 0; n++)
                            nombre = $"{deseado}-{n}";
                    var nueva = Fisica(carpeta + nombre + "." + f.Ext);
                    Directory.CreateDirectory(Path.GetDirectoryName(nueva)!);
                    File.Move(vieja, nueva, overwrite: true);
                    try
                    {
                        using var cn = _db.Conexion();
                        await cn.ExecuteAsync("UPDATE Eurovip.imagen SET ruta_relativa = @carpeta, nombre_archivo = @nombre WHERE id = @IdImg", new { carpeta, nombre, f.IdImg });
                    }
                    catch { File.Move(nueva, vieja, overwrite: true); throw; }
                    r.Renombradas++;
                    vacias.Add(Path.GetDirectoryName(vieja)!);
                    if (f.Uso == "PORTADA") await SincronizarProductoAsync(t, f.Clave);   // la URL nueva en las columnas de antes (Home, destino…)
                }
                catch (Exception ex) { r.Errores.Add($"{f.Tipo} {f.Clave}: {ex.Message}"); }
            }
            foreach (var d in vacias)
                try { if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { /* no pasa nada */ }
            return r;
        }

        public EstadoCarpeta Estado()
        {
            try
            {
                var existe = Directory.Exists(CarpetaBase);
                var escribe = false; string? error = null;
                try
                {
                    Directory.CreateDirectory(CarpetaBase);
                    var prueba = Path.Combine(CarpetaBase, ".prueba-escritura");
                    File.WriteAllText(prueba, DateTime.Now.ToString("O")); File.Delete(prueba);
                    escribe = true;
                }
                catch (Exception ex) { error = ex.Message; }
                var archivos = Directory.Exists(CarpetaBase) ? Directory.EnumerateFiles(CarpetaBase, "*", SearchOption.AllDirectories).Take(100000).Count() : 0;
                return new EstadoCarpeta(CarpetaBase, UrlBase, existe, escribe, archivos, error);
            }
            catch (Exception ex) { return new EstadoCarpeta(CarpetaBase, UrlBase, false, false, 0, ex.Message); }
        }

        public async Task<List<DiaFoto>> DiasCircuitoAsync(string circuito)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<DiaFoto>(
                "SELECT dia AS Dia, titulo AS Titulo, caratula AS Caratula FROM Eurovip.circuito_itinerario WHERE nombre_circuito = @circuito AND id_idioma = 'ES' ORDER BY dia",
                new { circuito = (circuito ?? "").Trim() })).ToList();
        }

        public async Task<string?> FotoDiaAsync(string circuito, short dia, Stream? origen)
        {
            circuito = (circuito ?? "").Trim();
            using var cn = _db.Conexion();
            var p = new { circuito, dia };
            if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.circuito_itinerario WHERE nombre_circuito = @circuito AND id_idioma = 'ES' AND dia = @dia", p) == 0)
                throw new InvalidOperationException($"El día {dia} no está en el itinerario de ese circuito.");
            var url = origen is null ? null : await GuardarItinerarioAsync(circuito, dia, origen);
            await cn.ExecuteAsync("UPDATE Eurovip.circuito_itinerario SET caratula = @url WHERE nombre_circuito = @circuito AND id_idioma = 'ES' AND dia = @dia",
                new { circuito, dia, url });
            return url;
        }

        public async Task<ResumenMigracion> MigrarImagenesViejasAsync()
        {
            var r = new ResumenMigracion();
            var viejas = Path.Combine(_env.WebRootPath, "imagenes");
            var candidatos = new List<(string Tipo, string Clave, string Fisica, string Etiqueta)>();
            using (var cn = _db.Conexion())
            {
                foreach (var (id, nombre) in await cn.QueryAsync<(int, string)>("SELECT id_Excursion, nombre FROM Eurovip.servicio_excursion WHERE nombre IS NOT NULL"))
                    candidatos.Add(("EXCURSION", id.ToString(), Path.Combine(viejas, "excursiones", NombreCircuito(nombre) + ".webp"), $"Excursión {id} {nombre}"));
                foreach (var (id, img) in await cn.QueryAsync<(int, string)>("SELECT id_traslado, imagen FROM Eurovip.traslado WHERE ISNULL(imagen, '') <> ''"))
                    candidatos.Add(("TRASLADO", id.ToString(), Path.Combine(viejas, "traslados", Path.GetFileName(img)), $"Traslado {id}"));
                foreach (var (nombre, car) in await cn.QueryAsync<(string, string)>("SELECT DISTINCT nombre_circuito, caratula FROM Eurovip.circuito WHERE ISNULL(caratula, '') <> ''"))
                {
                    var f = Local(viejas, car, "circuitos");
                    if (f is not null) candidatos.Add(("CIRCUITO", nombre, f, $"Circuito {nombre}"));
                }
                foreach (var (dest, car) in await cn.QueryAsync<(string, string)>("SELECT id_destino, MIN(caratula) FROM Eurovip.destino WHERE ISNULL(caratula, '') <> '' GROUP BY id_destino"))
                {
                    var f = Local(viejas, car, "destinos");
                    if (f is not null) candidatos.Add(("DESTINO", dest, f, $"Destino {dest}"));
                }
                foreach (var (id, car) in await cn.QueryAsync<(int, string)>("SELECT id_alojamiento, MIN(caratula) FROM Eurovip.alojamiento WHERE caratula LIKE '/imagenes/%' GROUP BY id_alojamiento"))
                {
                    var f = Local(viejas, car, "alojamientos");
                    if (f is not null) candidatos.Add(("ALOJAMIENTO", id.ToString(), f, $"Alojamiento {id}"));
                }
                foreach (var (id, nombre) in await cn.QueryAsync<(int, string)>("SELECT id_servicio, LTRIM(RTRIM(id_destino)) FROM Eurovip.inicio WHERE tipo_servicio = 'HOTEL'"))
                    candidatos.Add(("HOTEL", id.ToString(), Path.Combine(viejas, "hoteles", NombreViejo(nombre)), $"Hotel {id} {nombre}"));
            }

            foreach (var (tipo, clave, fisica, etiqueta) in candidatos)
            {
                try
                {
                    if (await PortadaAsync(tipo, clave) is not null) { r.YaEstaban[tipo] = r.YaEstaban.GetValueOrDefault(tipo) + 1; continue; }
                    if (!File.Exists(fisica)) { r.SinArchivo.Add($"{etiqueta}: no está el archivo {Path.GetFileName(fisica)}"); continue; }
                    await using var st = File.OpenRead(fisica);
                    await SubirAsync(tipo, clave, "PORTADA", st, Path.GetFileName(fisica));
                    r.Pasadas[tipo] = r.Pasadas.GetValueOrDefault(tipo) + 1;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "No se pudo pasar la foto de {Etiqueta}", etiqueta);
                    r.Errores.Add($"{etiqueta}: {ex.Message}");
                }
            }
            await MigrarItinerarioAsync(r, viejas);
            MigrarSlider(r);
            return r;
        }

        /// <summary>Itinerario de circuito: copia los archivos de /imagenes/circuitos/itinerario/… a la base nueva y cambia la caratula.</summary>
        private async Task MigrarItinerarioAsync(ResumenMigracion r, string viejas)
        {
            using var cn = _db.Conexion();
            var filas = (await cn.QueryAsync<(string Nombre, string Idioma, int Dia, string Caratula)>(
                "SELECT nombre_circuito, id_idioma, dia, caratula FROM Eurovip.circuito_itinerario WHERE caratula LIKE '/imagenes/circuitos/itinerario/%'")).ToList();
            foreach (var (nombre, idioma, dia, car) in filas)
            {
                try
                {
                    var rel = car["/imagenes/".Length..].Split('?')[0];
                    var vieja = rel.Contains("..") ? null : Path.Combine(viejas, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (vieja is null || !File.Exists(vieja)) { r.SinArchivo.Add($"Itinerario {nombre} día {dia}: no está el archivo {Path.GetFileName(car)}"); continue; }
                    await using var st = File.OpenRead(vieja);
                    var url = await GuardarItinerarioAsync(nombre, dia, st);
                    await cn.ExecuteAsync("UPDATE Eurovip.circuito_itinerario SET caratula = @url WHERE nombre_circuito = @nombre AND id_idioma = @idioma AND dia = @dia AND caratula = @car",
                        new { url, nombre, idioma, dia, car });
                    r.Itinerario++;
                }
                catch (Exception ex) { r.Errores.Add($"Itinerario {nombre} día {dia}: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Slider: copia los archivos de la carpeta anterior (Eurovip:Slider:Anterior:Fisico/FisicoMd/FisicoSm) a la nueva
        /// (Eurovip:Slider:Fisico/FisicoMd/FisicoSm) con el mismo nombre, sin pisar los que ya estén. La tabla carrusel no cambia.
        /// </summary>
        private void MigrarSlider(ResumenMigracion r)
        {
            foreach (var k in new[] { "Fisico", "FisicoMd", "FisicoSm" })
            {
                var de = _cfg[$"Eurovip:Slider:Anterior:{k}"];
                var a = _cfg[$"Eurovip:Slider:{k}"];
                if (string.IsNullOrWhiteSpace(de) || string.IsNullOrWhiteSpace(a) || !Directory.Exists(de)
                    || string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(de)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    Directory.CreateDirectory(a);
                    foreach (var f in Directory.GetFiles(de))   // solo esa carpeta: md y sm tienen su propia clave
                    {
                        var destino = Path.Combine(a, Path.GetFileName(f));
                        if (File.Exists(destino)) continue;
                        File.Copy(f, destino);
                        r.Slider++;
                    }
                }
                catch (Exception ex) { r.Errores.Add($"Slider ({k}): {ex.Message}"); }
            }
        }

        /// <summary>Archivo local de una carátula vieja ("/imagenes/destinos/x.webp" o solo "x.webp"); null si es una URL externa.</summary>
        private static string? Local(string viejas, string caratula, string carpeta)
        {
            var c = caratula.Trim();
            if (c.StartsWith("http", StringComparison.OrdinalIgnoreCase) || c.StartsWith("/multimedia/", StringComparison.OrdinalIgnoreCase)) return null;
            if (c.StartsWith("/imagenes/", StringComparison.OrdinalIgnoreCase))
            {
                var rel = c["/imagenes/".Length..].Split('?')[0];
                if (rel.Contains("..") || rel.Contains("itinerario/", StringComparison.OrdinalIgnoreCase)) return null;
                return Path.Combine(viejas, rel.Replace('/', Path.DirectorySeparatorChar));
            }
            return c.Contains('/') || c.Contains('\\') ? null : Path.Combine(viejas, carpeta, c);
        }

        // Mismas reglas de nombre que ImagenesLocales (las fotos viejas se guardaban así)
        private static string NombreViejo(string nombre)
        {
            var sb = new StringBuilder();
            foreach (var ch in (nombre ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant())
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && (ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9'))
                    sb.Append(ch);
            return sb.Length == 0 ? "" : sb + ".webp";
        }

        private static string NombreCircuito(string nombre)
        {
            var sb = new StringBuilder();
            foreach (var ch in (nombre ?? "").Normalize(NormalizationForm.FormD))
                if (ch is >= 'a' and <= 'z' || ch is >= 'A' and <= 'Z' || ch is >= '0' and <= '9')
                    sb.Append(ch);
            var b = sb.ToString();
            return b.Length > 120 ? b[..120] : b;
        }
    }
}
