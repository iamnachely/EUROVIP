using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using EuroVip.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace EuroVip.Services
{
    /// <summary>
    /// «Circuitos por el mundo» (CAMBIO 133): los programas de la integración Yourttoo (API v2, ver
    /// EuroVip/docs/08-integracion-yourttoo.md) que el BackOffice elige, por destino, para la Home. Se guardan en
    /// Eurovip.inicio como tipo 'CIRCUITO_MUNDO' (tabla intermedia que lee la web con Eurovip.spPortalListarCircuitosMundo).
    /// Credenciales SOLO en la configuración (Yourttoo:Url, :Usuario, :Clave; en local, appsettings.Local.json).
    /// </summary>
    public interface ICircuitosMundo
    {
        Task<List<CircuitosMundo.Destino>> DestinosAsync(CancellationToken ct = default);
        Task<CircuitosMundo.Pagina> ProgramasAsync(string pais, int pagina, CancellationToken ct = default);
        Task<List<CircuitosMundo.Elegido>> ElegidosAsync();
        Task<(int? Id, string? Error)> AgregarAsync(string codigo, string pais, string usuario, CancellationToken ct = default);
        Task<bool> QuitarAsync(int id);
        Task OrdenarAsync(string pais, IReadOnlyList<int> ids);
    }

    public sealed class CircuitosMundo : ICircuitosMundo
    {
        public const string Tipo = "CIRCUITO_MUNDO";
        public const string Fuente = "YOURTTOO";

        // ---- contrato de Yourttoo (solo lo que se usa) ----
        public sealed record Login(string? Userid, string? AccessToken);
        public sealed record Pais(string? Slug, [property: JsonPropertyName("label_es")] string? LabelEs);
        public sealed record Resumen(int? Duration, IReadOnlyList<string>? Countries, IReadOnlyList<string>? Cities);
        public sealed record Programa(string Code, string? Title, string? CategoryName, string? ImageUrl, Resumen? Brief, decimal? MinPrice, string? Currency);
        public sealed record Busqueda(int TotalItems, int Pages, int Currentpage, IReadOnlyList<Programa>? Items);

        // ---- lo que ve la pantalla ----
        public sealed record Destino(string Pais, string Nombre, int Elegidos);
        public sealed record Tarjeta(string Codigo, string Titulo, string? Imagen, int? Dias, string? Ciudades, string? Categoria, decimal? Desde, string? Moneda, int? IdElegido);
        public sealed record Pagina(string Pais, string Nombre, int Total, int Paginas, int Actual, List<Tarjeta> Programas);
        public sealed record Elegido(int Id, string Codigo, string Pais, string Destino, string Titulo, string? Imagen, int? Dias, string? Ciudades, string? Categoria, decimal? Desde, string? Moneda, short? Peso);

        private readonly HttpClient _http;
        private readonly IConfiguration _cfg;
        private readonly IMemoryCache _cache;
        private readonly ICircuitoRepository _db;
        private static (string Usuario, string Token)? _sesion;
        private static DateTime _caduca;
        private static readonly SemaphoreSlim _cerrojo = new(1, 1);
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public CircuitosMundo(HttpClient http, IConfiguration cfg, IMemoryCache cache, ICircuitoRepository db)
        {
            _http = http; _cfg = cfg; _cache = cache; _db = db;
            if (_http.BaseAddress is null && cfg["Yourttoo:Url"] is { Length: > 0 } url) _http.BaseAddress = new Uri(url);
        }

        // ======================================================== Yourttoo
        public async Task<List<Destino>> DestinosAsync(CancellationToken ct = default)
        {
            var paises = await _cache.GetOrCreateAsync("yt:paises", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                var l = await EnviarAsync<List<Pais>>("find", new { type = "countries" }, ct);
                // Solo países (ISO de 2 letras); hay regiones como «ccaa_andalusia»
                return l.Where(p => p.Slug is { Length: 2 } && !string.IsNullOrWhiteSpace(p.LabelEs))
                        .GroupBy(p => p.Slug!.ToLowerInvariant()).Select(g => g.First()).OrderBy(p => p.LabelEs).ToList();
            }) ?? new();
            var elegidos = (await ElegidosAsync()).GroupBy(x => x.Pais.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Count());
            return paises.Select(p => new Destino(p.Slug!.ToLowerInvariant(), p.LabelEs!.Trim(), elegidos.GetValueOrDefault(p.Slug!.ToLowerInvariant())))
                         .OrderByDescending(d => d.Elegidos > 0).ThenBy(d => d.Nombre).ToList();
        }

        /// <summary>Programas de un destino, del más económico al más caro (20 por página; la primera es la 0).</summary>
        public async Task<Pagina> ProgramasAsync(string pais, int pagina, CancellationToken ct = default)
        {
            pais = (pais ?? "").Trim().ToLowerInvariant();
            if (pais.Length != 2) throw new InvalidOperationException("Elige un destino.");
            pagina = Math.Max(0, pagina);
            var b = await _cache.GetOrCreateAsync($"yt:buscar:{pais}:{pagina}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                var r = await EnviarAsync<Busqueda>("search", new { filter = new { countries = new[] { pais }, maxresults = 20, sort = "asc", page = pagina } }, ct);
                // Cada programa se guarda aparte (para «Agregar» sin volver a llamar a Yourttoo)
                foreach (var p in r.Items ?? Array.Empty<Programa>()) _cache.Set($"yt:prog:{p.Code}", p, TimeSpan.FromHours(2));
                return r;
            })!;
            var nombre = (await DestinosAsync(ct)).FirstOrDefault(d => d.Pais == pais)?.Nombre ?? pais.ToUpperInvariant();
            var elegidos = (await ElegidosAsync()).ToDictionary(x => x.Codigo, x => x.Id, StringComparer.Ordinal);
            var tarjetas = (b.Items ?? Array.Empty<Programa>()).Select(p => new Tarjeta(p.Code, Limpio(p.Title) ?? p.Code, p.ImageUrl, p.Brief?.Duration,
                Ciudades(p), Limpio(p.CategoryName), p.MinPrice, p.Currency ?? "USD", elegidos.TryGetValue(p.Code, out var id) ? id : null)).ToList();
            return new Pagina(pais, nombre, b.TotalItems, b.Pages, b.Currentpage, tarjetas);
        }

        private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) || s.Trim() == "null" ? null : s.Trim();
        private static string? Ciudades(Programa p) => p.Brief?.Cities is { Count: > 0 } c ? string.Join(" · ", c.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(6)) : null;

        private async Task<T> EnviarAsync<T>(string comando, object cuerpo, CancellationToken ct)
        {
            if (_http.BaseAddress is null) throw new InvalidOperationException("Falta configurar Yourttoo:Url (la dirección de la API de Yourttoo).");
            for (var intento = 0; ; intento++)
            {
                var (usuario, token) = await SesionAsync(ct);
                using var req = new HttpRequestMessage(HttpMethod.Post, $"/apiv2/{comando}") { Content = JsonContent.Create(cuerpo, options: Json) };
                req.Headers.Add("userid", usuario);
                req.Headers.Add("accesstoken", token);
                using var r = await _http.SendAsync(req, ct);
                var texto = await r.Content.ReadAsStringAsync(ct);
                if (!r.IsSuccessStatusCode || texto.StartsWith("{\"message\":\"An error occurred\"", StringComparison.Ordinal))
                {
                    var error = Mensaje(texto) ?? $"HTTP {(int)r.StatusCode}";
                    if (intento == 0 && (r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || error.Contains("Auth recovery failed", StringComparison.OrdinalIgnoreCase)))
                    { _sesion = null; continue; }
                    throw new InvalidOperationException($"Yourttoo no respondió bien ({comando}): {error}");
                }
                return JsonSerializer.Deserialize<T>(texto, Json) ?? throw new InvalidOperationException($"Yourttoo devolvió una respuesta vacía ({comando}).");
            }
        }

        private static string? Mensaje(string texto)
        {
            try
            {
                using var d = JsonDocument.Parse(texto);
                if (!d.RootElement.TryGetProperty("error", out var e)) return null;
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("error", out var dentro)) e = dentro;
                return e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString();
            }
            catch (JsonException) { return null; }
        }

        private async Task<(string, string)> SesionAsync(CancellationToken ct)
        {
            if (_sesion is { } s && DateTime.UtcNow < _caduca) return s;
            await _cerrojo.WaitAsync(ct);
            try
            {
                if (_sesion is { } s2 && DateTime.UtcNow < _caduca) return s2;
                string Clave(string k) => _cfg[k] is { Length: > 0 } v ? v : throw new InvalidOperationException($"Falta configurar {k} (credenciales de Yourttoo).");
                using var r = await _http.PostAsJsonAsync("/apiv2/auth", new { email = Clave("Yourttoo:Usuario"), password = Clave("Yourttoo:Clave") }, ct);
                var texto = await r.Content.ReadAsStringAsync(ct);
                if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"Yourttoo no aceptó las credenciales: {Mensaje(texto) ?? $"HTTP {(int)r.StatusCode}"}");
                var login = JsonSerializer.Deserialize<Login>(texto, Json);
                if (login is not { Userid.Length: > 0, AccessToken.Length: > 0 }) throw new InvalidOperationException("Yourttoo no devolvió la sesión.");
                _sesion = (login.Userid, login.AccessToken);
                _caduca = DateTime.UtcNow.AddMinutes(50);
                return _sesion.Value;
            }
            finally { _cerrojo.Release(); }
        }

        // ======================================================== Eurovip.inicio (tabla intermedia)
        public async Task<List<Elegido>> ElegidosAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<Elegido>(@"
                SELECT id AS Id, programa_codigo AS Codigo, LOWER(id_paisDestino) AS Pais, id_destino AS Destino, descripcion AS Titulo,
                       caratulaDestino AS Imagen, CAST(programa_dias AS int) AS Dias, programa_ciudades AS Ciudades, programa_categoria AS Categoria,
                       CAST(precioWeb AS decimal(18,2)) AS Desde, LTRIM(RTRIM(id_moneda)) AS Moneda, peso AS Peso
                FROM Eurovip.inicio WHERE tipo_servicio = @Tipo AND programa_codigo IS NOT NULL AND id_idioma = 'ES'
                ORDER BY id_destino, CASE WHEN peso IS NULL THEN 1 ELSE 0 END, peso, precioWeb, id", new { Tipo })).ToList();
        }

        /// <summary>Guarda el programa en la Home (en su destino, al final). Si ya estaba, devuelve el que había.</summary>
        public async Task<(int? Id, string? Error)> AgregarAsync(string codigo, string pais, string usuario, CancellationToken ct = default)
        {
            codigo = (codigo ?? "").Trim();
            if (codigo.Length == 0) return (null, "Falta el programa.");
            var p = _cache.Get<Programa>($"yt:prog:{codigo}");
            if (p is null)
            {
                // No está en la caché (pasó el tiempo): se busca de nuevo en su destino
                await ProgramasAsync(pais, 0, ct);
                p = _cache.Get<Programa>($"yt:prog:{codigo}");
            }
            if (p is null) return (null, "No encontré ese circuito en Yourttoo. Vuelve a abrir el destino y elígelo otra vez.");
            var destino = (await DestinosAsync(ct)).FirstOrDefault(d => d.Pais == pais.ToLowerInvariant())?.Nombre ?? pais.ToUpperInvariant();
            using var cn = _db.Conexion();
            var ya = await cn.ExecuteScalarAsync<int?>("SELECT id FROM Eurovip.inicio WHERE programa_fuente = @Fuente AND programa_codigo = @codigo AND id_idioma = 'ES'", new { Fuente, codigo });
            if (ya is int idYa) return (idYa, null);
            var id = await cn.ExecuteScalarAsync<int>(@"
                DECLARE @sid int = (SELECT ISNULL(MIN(CASE WHEN id_servicio < 0 THEN id_servicio END), 0) - 1 FROM Eurovip.inicio WITH (UPDLOCK, HOLDLOCK));
                DECLARE @camp smallint = ISNULL((SELECT TOP 1 id_campanya FROM Eurovip.inicio WHERE tipo_servicio = 'CIRCUITO' ORDER BY id),
                                               (SELECT MIN(id_campanya) FROM Eurovip.campanya WHERE id_idioma = 'ES'));
                DECLARE @peso smallint = (SELECT ISNULL(MAX(peso), 0) + 1 FROM Eurovip.inicio WHERE tipo_servicio = @Tipo AND id_paisDestino = @pais);
                INSERT INTO Eurovip.inicio (id_servicio, id_idioma, tipo_servicio, id_campanya, id_moneda, id_pais_origen, origen, id_paisDestino, id_destino,
                       caratulaDestino, descripcion, precioWeb, noches, peso, estado, notas, usuario_alta, fecha_alta,
                       programa_fuente, programa_codigo, programa_dias, programa_ciudades, programa_categoria)
                VALUES (@sid, 'ES', @Tipo, @camp, @moneda, 'DO', 'Santo Domingo', @pais, @destino,
                       @imagen, @titulo, @desde, @noches, @peso, 0, N'Circuito por el mundo (Yourttoo) elegido en Circuitos', @usuario, SYSDATETIME(),
                       @Fuente, @codigo, @dias, @ciudades, @categoria);
                SELECT CAST(SCOPE_IDENTITY() AS int);",
                new
                {
                    Tipo, Fuente, codigo, pais = pais.ToUpperInvariant(), destino = destino.Length > 1500 ? destino[..1500] : destino,
                    imagen = p.ImageUrl is { Length: > 300 } ? null : p.ImageUrl, titulo = Limpio(p.Title) ?? codigo, desde = p.MinPrice,
                    moneda = (p.Currency ?? "USD").Trim().ToUpperInvariant() is { Length: 3 } m ? m : "USD",
                    noches = p.Brief?.Duration is int d && d > 1 ? d - 1 : (int?)null, dias = p.Brief?.Duration,
                    ciudades = Ciudades(p) is { Length: > 400 } c ? c[..400] : Ciudades(p), categoria = Limpio(p.CategoryName) is { Length: > 100 } k ? k[..100] : Limpio(p.CategoryName),
                    usuario = usuario.Length > 50 ? usuario[..50] : usuario
                });
            return (id, null);
        }

        public async Task<bool> QuitarAsync(int id)
        {
            using var cn = _db.Conexion();
            // OUTPUT … INTO no hace falta: solo se mira cuántas filas se borraron (la tabla tiene triggers)
            return await cn.ExecuteAsync("DELETE FROM Eurovip.inicio WHERE id = @id AND tipo_servicio = @Tipo", new { id, Tipo }) > 0;
        }

        /// <summary>Orden dentro del destino: el primero de la lista es peso 1.</summary>
        public async Task OrdenarAsync(string pais, IReadOnlyList<int> ids)
        {
            using var cn = _db.Conexion();
            for (var i = 0; i < ids.Count; i++)
                await cn.ExecuteAsync("UPDATE Eurovip.inicio SET peso = @peso WHERE id = @id AND tipo_servicio = @Tipo AND id_paisDestino = @pais",
                    new { peso = (short)(i + 1), id = ids[i], Tipo, pais = (pais ?? "").ToUpperInvariant() });
        }
    }
}
