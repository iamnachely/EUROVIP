using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace EuroVip.Services
{
    /// <summary>
    /// API de Booking Motor (maestros): países, destinos por país y hoteles por destino, para completar el formulario de
    /// Hoteles eligiendo en vez de escribir. Solo lectura. La clave va SOLO en la configuración (BookingMotor:Token; en local,
    /// appsettings.Local.json, que no se sube a git; en el servidor, la configuración del IIS/pipeline). NUNCA en el código.
    /// Los listados se guardan en memoria (los maestros cambian poco) para no llamar a la API en cada tecla.
    /// </summary>
    public interface IBookingMotor
    {
        bool Configurado { get; }
        Task<List<BookingMotor.Pais>> PaisesAsync(CancellationToken ct = default);
        Task<List<BookingMotor.Destino>> DestinosAsync(string pais, CancellationToken ct = default);
        Task<BookingMotor.PaginaHoteles> HotelesAsync(string destino, string? q, int pagina, CancellationToken ct = default);
        Task<BookingMotor.Hotel?> HotelAsync(string codigo, CancellationToken ct = default);
        /// <summary>Busca por nombre en TODOS los hoteles del destino (se indexan en segundo plano la primera vez; mientras tanto, los que ya están).</summary>
        BookingMotor.Busqueda Buscar(string destino, string? q, int pagina);
        /// <summary>Descarga la foto de portada del hotel (solo de la ficha de Booking Motor, nunca de una dirección que mande el navegador).</summary>
        Task<(byte[] Bytes, string Nombre)?> FotoAsync(string codigo, CancellationToken ct = default);
    }

    public sealed class BookingMotor : IBookingMotor
    {
        public const int PorPagina = 24;

        public sealed record Pais(string Codigo, string Nombre);
        /// <summary>Tipo: City, Multi City Vicinity, Point Of Interest… Nombre completo («Santo Domingo, Distrito Nacional, República Dominicana»); Corto = lo de antes de la primera coma.</summary>
        public sealed record Destino(string Id, string Nombre, string Corto, string? Tipo);
        public sealed record Hotel(string Codigo, string Nombre, string? Imagen, int? Estrellas, string? Direccion, string? Pais);
        public sealed record PaginaHoteles(int Total, int Pagina, int Paginas, List<Hotel> Hoteles);
        /// <summary>Resultado de buscar: Listo = ya se leyeron todos los hoteles del destino; Cargados/TotalDestino = avance de esa lectura.</summary>
        public sealed record Busqueda(bool Listo, int Cargados, int TotalDestino, int Total, int Pagina, int Paginas, List<Hotel> Hoteles, string? Error);

        private sealed class Indice
        {
            public readonly object Cerrojo = new();
            public List<Hotel> Hoteles = new();
            public int Total;
            public bool Listo;
            public bool Iniciado;
            public string? Error;
            public DateTime Inicio = DateTime.UtcNow;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Indice> Indices = new();
        public const int PorPaginaBusqueda = 30;

        private static string SoloDigitos(string? s) => new string((s ?? "").Where(char.IsAsciiDigit).Take(20).ToArray());

        private static string Norm(string? s)
        {
            var d = (s ?? "").Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in d) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BookingMotor> _log;
        private readonly string? _token;

        public BookingMotor(HttpClient http, IMemoryCache cache, IConfiguration cfg, ILogger<BookingMotor> log)
        {
            _http = http; _cache = cache; _log = log;
            _http.BaseAddress = new Uri((cfg["BookingMotor:Url"] ?? "https://api.bookingmotor.com").TrimEnd('/') + "/");
            _token = cfg["BookingMotor:Token"];
        }

        public bool Configurado => !string.IsNullOrWhiteSpace(_token);

        private async Task<JsonElement> GetAsync(string ruta, CancellationToken ct)
        {
            if (!Configurado) throw new InvalidOperationException("Falta la clave de Booking Motor (BookingMotor:Token) en la configuración.");
            using var req = new HttpRequestMessage(HttpMethod.Get, ruta);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            req.Headers.AcceptLanguage.ParseAdd("es");
            using var r = await _http.SendAsync(req, ct);
            if (!r.IsSuccessStatusCode)
            {
                _log.LogWarning("Booking Motor {Ruta} → {Codigo}", ruta, (int)r.StatusCode);
                throw new InvalidOperationException($"Booking Motor respondió {(int)r.StatusCode}.");
            }
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("response").Clone();
        }

        private static string Es(JsonElement n) =>
            n.ValueKind == JsonValueKind.Object
                ? (n.TryGetProperty("es", out var es) && es.GetString() is { Length: > 0 } s ? s : n.TryGetProperty("en", out var en) ? en.GetString() ?? "" : "")
                : n.GetString() ?? "";

        private Task<T> Guardado<T>(string clave, TimeSpan vida, Func<Task<T>> crear) =>
            _cache.GetOrCreateAsync(clave, e => { e.AbsoluteExpirationRelativeToNow = vida; return crear(); })!;

        public Task<List<Pais>> PaisesAsync(CancellationToken ct = default) => Guardado("bm:paises", TimeSpan.FromHours(12), async () =>
        {
            var r = await GetAsync("v1/master/country", ct);
            return r.EnumerateArray().Select(p => new Pais(p.GetProperty("code").GetString()!, Es(p.GetProperty("name"))))
                .OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();
        });

        public Task<List<Destino>> DestinosAsync(string pais, CancellationToken ct = default)
        {
            pais = (pais ?? "").Trim().ToUpperInvariant();
            if (pais.Length != 2 || !pais.All(char.IsAsciiLetter)) return Task.FromResult(new List<Destino>());
            return Guardado("bm:dest:" + pais, TimeSpan.FromHours(12), async () =>
            {
                var r = await GetAsync($"v1/master/country/{pais}/destination", ct);
                return r.GetProperty("destinations").EnumerateArray().Select(d =>
                {
                    var nombre = Es(d.GetProperty("name"));
                    return new Destino(d.GetProperty("id").GetInt64().ToString(), nombre, nombre.Split(',')[0].Trim(),
                        d.TryGetProperty("type", out var t) ? t.GetString() : null);
                })
                // primero las ciudades y zonas; los puntos de interés al final
                .OrderBy(d => d.Tipo == "Point Of Interest" ? 1 : 0).ThenBy(d => d.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();
            });
        }

        private Task<List<string>> CodigosAsync(string destino, CancellationToken ct) => Guardado("bm:cod:" + destino, TimeSpan.FromHours(6), async () =>
        {
            var r = await GetAsync($"v1/master/destination/{destino}/hotel", ct);
            return r.EnumerateArray().Select(c => c.GetString() ?? "").Where(c => c.Length > 0).ToList();
        });

        public async Task<Hotel?> HotelAsync(string codigo, CancellationToken ct = default)
        {
            codigo = (codigo ?? "").Trim().ToLowerInvariant();
            if (codigo.Length is < 2 or > 12 || !codigo.All(char.IsAsciiLetterOrDigit)) return null;
            return await Guardado("bm:h:" + codigo, TimeSpan.FromHours(12), async () =>
            {
                try
                {
                    var h = await GetAsync("v1/master/hotel/information/" + codigo, ct);
                    return (Hotel?)new Hotel(codigo, h.TryGetProperty("name", out var n) ? n.GetString() ?? codigo : codigo,
                        h.TryGetProperty("image", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null,
                        h.TryGetProperty("stars", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : null,
                        h.TryGetProperty("address", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null,
                        h.TryGetProperty("country", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null);
                }
                catch (InvalidOperationException) { return null; }
            });
        }

        /// <summary>
        /// La API de un destino solo da los códigos; el nombre sale de la ficha de cada hotel. Se trae una página de 24 (con
        /// hasta 8 llamadas a la vez) y se guarda. Con «q» se busca por nombre dentro de los hoteles que ya se conocen
        /// (los de las páginas vistas) y, si no alcanzan, se sigue trayendo páginas hasta completar la de resultados.
        /// </summary>
        public async Task<PaginaHoteles> HotelesAsync(string destino, string? q, int pagina, CancellationToken ct = default)
        {
            destino = SoloDigitos(destino);
            var codigos = await CodigosAsync(destino, ct);
            q = q?.Trim();
            if (string.IsNullOrEmpty(q))
            {
                pagina = Math.Max(1, pagina);
                var trozo = codigos.Skip((pagina - 1) * PorPagina).Take(PorPagina).ToList();
                var lista = await FichasAsync(trozo, ct);
                return new PaginaHoteles(codigos.Count, pagina, Math.Max(1, (int)Math.Ceiling(codigos.Count / (double)PorPagina)), lista);
            }
            // Búsqueda: recorre los códigos en lotes (máx. 120 fichas por búsqueda) y se queda con los que coinciden.
            var halladas = new List<Hotel>();
            foreach (var lote in codigos.Chunk(PorPagina).Take(5))
            {
                var fichas = await FichasAsync(lote, ct);
                halladas.AddRange(fichas.Where(h => h.Nombre.Contains(q, StringComparison.CurrentCultureIgnoreCase)));
                if (halladas.Count >= PorPagina) break;
            }
            return new PaginaHoteles(halladas.Count, 1, 1, halladas.Take(PorPagina).ToList());
        }

        public Busqueda Buscar(string destino, string? q, int pagina)
        {
            destino = SoloDigitos(destino);
            var ind = Indices.GetOrAdd(destino, _ => new Indice());
            bool empezar = false;
            lock (ind.Cerrojo)
            {
                // se lee la primera vez; otra vez si falló hace más de 2 minutos o si tiene más de 12 horas
                var viejo = ind.Iniciado && (ind.Error is not null ? DateTime.UtcNow - ind.Inicio > TimeSpan.FromMinutes(2) : ind.Listo && DateTime.UtcNow - ind.Inicio > TimeSpan.FromHours(12));
                if (!ind.Iniciado || viejo)
                {
                    ind.Hoteles = new(); ind.Total = 0; ind.Listo = false; ind.Error = null; ind.Iniciado = true; ind.Inicio = DateTime.UtcNow; empezar = true;
                }
            }
            if (empezar) _ = Task.Run(() => LeerDestinoAsync(destino, ind));

            List<Hotel> copia; int total; bool listo; string? error;
            lock (ind.Cerrojo) { copia = ind.Hoteles.ToList(); total = ind.Total; listo = ind.Listo; error = ind.Error; }
            var n = Norm(q?.Trim());
            var lista = (string.IsNullOrEmpty(n) ? copia : copia.Where(h => Norm(h.Nombre).Contains(n)).ToList())
                .OrderByDescending(h => h.Estrellas ?? 0).ThenBy(h => h.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();
            pagina = Math.Max(1, pagina);
            var paginas = Math.Max(1, (int)Math.Ceiling(lista.Count / (double)PorPaginaBusqueda));
            return new Busqueda(listo, copia.Count, total, lista.Count, pagina, paginas, lista.Skip((pagina - 1) * PorPaginaBusqueda).Take(PorPaginaBusqueda).ToList(), error);
        }

        /// <summary>Lee en segundo plano las fichas de todos los hoteles del destino (8 llamadas a la vez) y las va dejando disponibles.</summary>
        private async Task LeerDestinoAsync(string destino, Indice ind)
        {
            try
            {
                var codigos = await CodigosAsync(destino, CancellationToken.None);
                lock (ind.Cerrojo) ind.Total = codigos.Count;
                foreach (var lote in codigos.Chunk(32))
                {
                    var fichas = await FichasAsync(lote, CancellationToken.None);
                    lock (ind.Cerrojo) ind.Hoteles.AddRange(fichas);
                }
                lock (ind.Cerrojo) ind.Listo = true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Booking Motor: no se pudo leer el destino {Destino}", destino);
                lock (ind.Cerrojo) { ind.Error = "No se pudieron leer todos los hoteles de este destino. Prueba de nuevo en un momento."; ind.Inicio = DateTime.UtcNow; }
            }
        }

        public async Task<(byte[] Bytes, string Nombre)?> FotoAsync(string codigo, CancellationToken ct = default)
        {
            var h = await HotelAsync(codigo, ct);
            if (h?.Imagen is not { Length: > 0 } url || !Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return null;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(TimeSpan.FromSeconds(12));
                using var r = await new HttpClient().GetAsync(u, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (!r.IsSuccessStatusCode || (r.Content.Headers.ContentLength ?? 0) > 10_000_000) return null;
                var bytes = await r.Content.ReadAsByteArrayAsync(cts.Token);
                return bytes.Length is > 0 and <= 10_000_000 ? (bytes, Path.GetFileName(u.AbsolutePath)) : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException) { return null; }
        }

        private async Task<List<Hotel>> FichasAsync(IEnumerable<string> codigos, CancellationToken ct)
        {
            using var turno = new SemaphoreSlim(8);
            var tareas = codigos.Select(async c =>
            {
                await turno.WaitAsync(ct);
                try { return await HotelAsync(c, ct); } finally { turno.Release(); }
            }).ToList();
            return (await Task.WhenAll(tareas)).OfType<Hotel>().ToList();
        }
    }
}
