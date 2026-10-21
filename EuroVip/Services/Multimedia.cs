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
        /// pero el archivo va en la misma base: {base}/circuitos/itinerario/{slug del circuito}/{GUID}.webp. Devuelve su URL.</summary>
        Task<string> GuardarItinerarioAsync(string circuito, Stream origen);
        /// <summary>Guarda una foto suelta (no va en Eurovip.multimedia) en {base}/{carpeta}/{GUID}.webp y devuelve su URL.
        /// carpeta: solo minúsculas, números, "-", "_" y "/" (ej. "cruceros/12/actividades").</summary>
        Task<string> GuardarArchivoAsync(string carpeta, Stream origen);
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
    ///  • Archivo: {Multimedia:CarpetaFisica}/{carpeta del tipo}/{id o slug}/{GUID sin guiones}.webp — nunca se sobrescribe.
    ///    URL: {Multimedia:Url}/ + la misma ruta. Por defecto wwwroot/multimedia y "/multimedia".
    ///  • Tipos del documento (ALOJAMIENTO, CIRCUITO, EXCURSION, TRASLADO) + DESTINO, PAIS y HOTEL (tarjetas de la pantalla Hoteles).
    ///  • NO van aquí las fotos del itinerario de circuito ni las del slider: siguen como estaban.
    ///  • Para que lo viejo siga funcionando, al cambiar la PORTADA se copia la URL en la columna de antes
    ///    (destino.caratula + tarjetas de la Home, alojamiento.caratula, circuito.caratula; traslado.imagen se vacía).
    /// </summary>
    public class Multimedia : IMultimedia
    {
        public static readonly IReadOnlyDictionary<string, TipoMultimedia> Tipos = new Dictionary<string, TipoMultimedia>(StringComparer.OrdinalIgnoreCase)
        {
            ["ALOJAMIENTO"] = new("ALOJAMIENTO", "id_alojamiento", true, "alojamientos", "herramientas", "alojamiento"),
            ["CIRCUITO"] = new("CIRCUITO", "nombre_circuito", false, "circuitos", "circuitos", "circuito"),
            ["EXCURSION"] = new("EXCURSION", "id_excursion", true, "excursiones", "excursiones", "excursión"),
            ["TRASLADO"] = new("TRASLADO", "id_traslado", true, "traslados", "traslados", "traslado"),
            ["DESTINO"] = new("DESTINO", "id_destino", false, "destinos", "herramientas", "destino"),
            ["PAIS"] = new("PAIS", "id_pais", false, "paises", "herramientas", "país"),
            ["HOTEL"] = new("HOTEL", "id_hotel", true, "hoteles", "hoteles", "hotel"),
            ["CRUCERO"] = new("CRUCERO", "id_crucero", true, "cruceros", "cruceros", "crucero"),
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
            var c = cfg["Multimedia:CarpetaFisica"];
            CarpetaBase = string.IsNullOrWhiteSpace(c) ? Path.Combine(env.WebRootPath, "multimedia") : c.Trim();
            UrlBase = (string.IsNullOrWhiteSpace(cfg["Multimedia:Url"]) ? "/multimedia" : cfg["Multimedia:Url"]!.Trim()).TrimEnd('/');
        }

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

        private static string Carpeta(TipoMultimedia t, string clave)
        {
            var parte = t.Entero ? int.Parse(clave, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : Slug(clave);
            if (parte == "") throw new InvalidOperationException($"La referencia del {t.Nombre} no sirve para la carpeta: escribe letras o números.");
            return $"{t.Carpeta}/{parte}/";
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
            var carpeta = Carpeta(t, clave);

            var (datos, ancho, alto) = await ImagenesLocales.ConvertirWebpAsync(origen);
            var nombre = Guid.NewGuid().ToString("N");                       // 32 hexadecimales: sin choques ni caché vieja
            var fisica = Fisica(carpeta + nombre + ".webp");
            Directory.CreateDirectory(Path.GetDirectoryName(fisica)!);
            await File.WriteAllBytesAsync(fisica, datos);                    // archivo nuevo: nunca se sobrescribe

            int idMultimedia;
            int? imagenVieja = null;
            try
            {
                using var cn = _db.Conexion();
                await cn.OpenAsync();
                using var tx = cn.BeginTransaction();
                var idImagen = await cn.ExecuteScalarAsync<int>(@"
                    INSERT INTO Eurovip.imagen (ruta_relativa, nombre_archivo, extension, nombre_original, tamano_bytes, ancho, alto)
                    VALUES (@carpeta, @nombre, 'webp', @original, @bytes, @ancho, @alto);
                    SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new { carpeta, nombre, original = Recortar(nombreOriginal, 255), bytes = (long)datos.Length, ancho, alto }, tx);

                short orden = 1;
                if (uso == "GALERIA")
                    orden = await cn.ExecuteScalarAsync<short>($"SELECT CAST(ISNULL(MAX(orden), 0) + 1 AS smallint) FROM Eurovip.multimedia WITH (UPDLOCK, HOLDLOCK) WHERE tipo = @tipo AND {t.Columna} = @v AND uso = 'GALERIA'",
                        new { tipo = t.Tipo, v }, tx);
                else
                {
                    // Portada e intermedia son una sola: la anterior se quita (su archivo se borra si nadie más lo usa)
                    var anterior = (await cn.QueryAsync<(int Id, int IdImagen)>($"SELECT id, id_imagen FROM Eurovip.multimedia WITH (UPDLOCK, HOLDLOCK) WHERE tipo = @tipo AND {t.Columna} = @v AND uso = @uso",
                        new { tipo = t.Tipo, v, uso }, tx)).ToList();
                    foreach (var a in anterior)
                    {
                        await cn.ExecuteAsync("DELETE FROM Eurovip.multimedia WHERE id = @Id", new { a.Id }, tx);
                        imagenVieja = a.IdImagen;
                    }
                }
                idMultimedia = await cn.ExecuteScalarAsync<int>($@"
                    INSERT INTO Eurovip.multimedia (id_imagen, tipo, uso, {t.Columna}, orden, texto_alternativo)
                    VALUES (@idImagen, @tipo, @uso, @v, @orden, @texto);
                    SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new { idImagen, tipo = t.Tipo, uso, v, orden, texto = Recortar(texto, 250) }, tx);
                tx.Commit();
            }
            catch
            {
                try { File.Delete(fisica); } catch { /* el archivo huérfano no rompe nada */ }
                throw;
            }

            if (imagenVieja is int iv) await LimpiarHuerfanasAsync(new[] { iv });
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
                            UPDATE Eurovip.inicio SET caratulaDestino = @url WHERE id_destino = @v AND tipo_servicio IN ('EXCURSION', 'TRASLADO');"
                        : @"UPDATE Eurovip.destino SET caratula = NULL WHERE id_destino = @v AND caratula = @quitada;
                            UPDATE Eurovip.inicio SET caratulaDestino = NULL WHERE id_destino = @v AND tipo_servicio IN ('EXCURSION', 'TRASLADO') AND caratulaDestino = @quitada;", p);
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
        public async Task<string> GuardarItinerarioAsync(string circuito, Stream origen)
        {
            var slug = Slug(circuito);
            if (slug == "") throw new InvalidOperationException("Primero escribe el nombre del circuito.");
            var (datos, _, _) = await ImagenesLocales.ConvertirWebpAsync(origen);
            var ruta = $"circuitos/itinerario/{slug}/{Guid.NewGuid():N}.webp";
            var fisica = Fisica(ruta);
            Directory.CreateDirectory(Path.GetDirectoryName(fisica)!);
            await File.WriteAllBytesAsync(fisica, datos);
            return Url(ruta);
        }

        public async Task<string> GuardarArchivoAsync(string carpeta, Stream origen)
        {
            carpeta = (carpeta ?? "").Trim().Trim('/');
            if (carpeta == "" || !Regex.IsMatch(carpeta, "^[a-z0-9_-]+(/[a-z0-9_-]+)*$")) throw new InvalidOperationException("Carpeta no válida.");
            var (datos, _, _) = await ImagenesLocales.ConvertirWebpAsync(origen);
            var ruta = $"{carpeta}/{Guid.NewGuid():N}.webp";
            var fisica = Fisica(ruta);
            Directory.CreateDirectory(Path.GetDirectoryName(fisica)!);
            await File.WriteAllBytesAsync(fisica, datos);
            return Url(ruta);
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
                    candidatos.Add(("HOTEL", id.ToString(), Path.Combine(viejas, "hoteles", NombreArchivo(nombre)), $"Hotel {id} {nombre}"));
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
                    var url = await GuardarItinerarioAsync(nombre, st);
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
        private static string NombreArchivo(string nombre)
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
