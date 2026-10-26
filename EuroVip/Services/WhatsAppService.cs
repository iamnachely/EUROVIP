using EuroVip.Models;
using EuroVip.Repositories;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EuroVip.Services
{
    /// <summary>WhatsApp Cloud API de Meta (misma cuenta que BackTiTan). Credenciales en la sección "WhatsApp" de appsettings.Local.json.</summary>
    public class WhatsAppService
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(IHttpClientFactory http, IConfiguration cfg, ILogger<WhatsAppService> logger)
        {
            _http = http;
            _cfg = cfg;
            _logger = logger;
        }

        /// <summary>
        /// Plantilla aprobada por Meta con cabecera de imagen y {{1}} = nombre (la misma "cliente" que usa BackTiTan).
        /// Sin plantilla, WhatsApp solo entrega a quien escribió en las últimas 24 h (Meta responde OK pero no llega).
        /// </summary>
        private string PlantillaFlyer => _cfg["WhatsApp:PlantillaFlyer"] is { Length: > 0 } p ? p : "cliente";
        private string IdiomaPlantilla => _cfg["WhatsApp:IdiomaPlantilla"] is { Length: > 0 } i ? i : "en";

        private string Token => _cfg["WhatsApp:AccessToken"] ?? "";
        private string NumeroId => _cfg["WhatsApp:PhoneNumberId"] ?? "";
        private string Version => string.IsNullOrWhiteSpace(_cfg["WhatsApp:ApiVersion"]) ? "v21.0" : _cfg["WhatsApp:ApiVersion"]!;
        public bool Configurado => !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(NumeroId);
        private string Base => $"https://graph.facebook.com/{Version}/{NumeroId}";

        private HttpClient Cliente()
        {
            var c = _http.CreateClient("whatsapp");
            c.Timeout = TimeSpan.FromSeconds(60);
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            return c;
        }

        /// <summary>Solo consulta el número de la cuenta (no envía nada).</summary>
        public async Task<(bool Ok, string Mensaje)> ProbarAsync()
        {
            if (!Configurado) return (false, "Falta configurar WhatsApp (AccessToken y PhoneNumberId en appsettings.Local.json).");
            try
            {
                var r = await Cliente().GetAsync($"{Base}?fields=display_phone_number,verified_name,quality_rating");
                var cuerpo = await r.Content.ReadAsStringAsync();
                if (!r.IsSuccessStatusCode) return (false, $"Meta respondió {(int)r.StatusCode}: {Error(cuerpo)}");
                using var d = JsonDocument.Parse(cuerpo);
                var num = d.RootElement.TryGetProperty("display_phone_number", out var n) ? n.GetString() : "?";
                var nom = d.RootElement.TryGetProperty("verified_name", out var v) ? v.GetString() : "?";
                var cal = d.RootElement.TryGetProperty("quality_rating", out var q) ? q.GetString() : "?";
                return (true, $"Conectado: {nom} ({num}) · calidad {cal}");
            }
            catch (Exception ex) { return (false, "No se pudo conectar con Meta: " + ex.Message); }
        }

        public async Task<string> SubirImagenAsync(string ruta)
        {
            using var form = new MultipartFormDataContent();
            var archivo = new ByteArrayContent(await File.ReadAllBytesAsync(ruta));
            archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(archivo, "file", Path.GetFileName(ruta));
            form.Add(new StringContent("image/png"), "type");
            form.Add(new StringContent("whatsapp"), "messaging_product");
            var r = await Cliente().PostAsync($"{Base}/media", form);
            var cuerpo = await r.Content.ReadAsStringAsync();
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"Error subiendo imagen ({(int)r.StatusCode}): {Error(cuerpo)}");
            using var d = JsonDocument.Parse(cuerpo);
            return d.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Meta no devolvió el id de la imagen.");
        }

        public async Task EnviarTextoAsync(string numero, string texto) =>
            await Mandar(new { messaging_product = "whatsapp", to = numero, type = "text", text = new { body = texto } });

        public async Task EnviarImagenAsync(string numero, string mediaId) =>
            await Mandar(new { messaging_product = "whatsapp", to = numero, type = "image", image = new { id = mediaId } });

        /// <summary>Flyer con la plantilla aprobada: llega aunque el contacto no haya escrito antes.</summary>
        public async Task EnviarFlyerAsync(string numero, string mediaId, string? nombre) =>
            await Mandar(new
            {
                messaging_product = "whatsapp",
                to = numero,
                type = "template",
                template = new
                {
                    name = PlantillaFlyer,
                    language = new { code = IdiomaPlantilla },
                    components = new object[]
                    {
                        new { type = "header", parameters = new object[] { new { type = "image", image = new { id = mediaId } } } },
                        new { type = "body", parameters = new object[] { new { type = "text", text = string.IsNullOrWhiteSpace(nombre) ? "viajero" : nombre.Trim().Split(' ')[0] } } }
                    }
                }
            });

        /// <summary>
        /// Cualquier plantilla aprobada con cabecera de imagen y textos en el cuerpo ({{1}}, {{2}}…); si lleva botón con URL dinámica,
        /// <paramref name="botonUrl"/> es la parte variable. NO lanza error: devuelve el código y la respuesta de Meta para decidir reintentos.
        /// </summary>
        public async Task<(bool Ok, int Status, string Respuesta)> EnviarPlantillaAsync(string numero, string plantilla, string? mediaId, IEnumerable<string> textos,
                                                                                         string? botonUrl = null, string? idioma = null)
        {
            var componentes = new List<object>();
            if (!string.IsNullOrEmpty(mediaId))
                componentes.Add(new { type = "header", parameters = new object[] { new { type = "image", image = new { id = mediaId } } } });
            var cuerpo = textos.Select(t => (object)new { type = "text", text = string.IsNullOrWhiteSpace(t) ? "-" : t.Trim() }).ToArray();
            if (cuerpo.Length > 0) componentes.Add(new { type = "body", parameters = cuerpo });
            if (!string.IsNullOrWhiteSpace(botonUrl))
                componentes.Add(new { type = "button", sub_type = "url", index = "0", parameters = new object[] { new { type = "text", text = botonUrl } } });
            var json = JsonSerializer.Serialize(new
            {
                messaging_product = "whatsapp", to = numero, type = "template",
                template = new { name = plantilla, language = new { code = string.IsNullOrWhiteSpace(idioma) ? IdiomaPlantilla : idioma }, components = componentes }
            });
            try
            {
                var r = await Cliente().PostAsync($"{Base}/messages", new StringContent(json, Encoding.UTF8, "application/json"));
                var resp = await r.Content.ReadAsStringAsync();
                _logger.LogInformation("WhatsApp {Plantilla} → {Numero}: {Status} {Respuesta}", plantilla, numero, (int)r.StatusCode, resp.Length > 400 ? resp[..400] : resp);
                return (r.IsSuccessStatusCode, (int)r.StatusCode, resp);
            }
            catch (Exception ex) { return (false, 0, ex.Message); }
        }

        /// <summary>El mensaje de error de una respuesta de Meta (o el texto tal cual).</summary>
        public static string ErrorDe(string json) => Error(json);

        private async Task Mandar(object cuerpo)
        {
            var r = await Cliente().PostAsync($"{Base}/messages", new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json"));
            var resp = await r.Content.ReadAsStringAsync();
            _logger.LogInformation("WhatsApp {Status}: {Respuesta}", (int)r.StatusCode, resp.Length > 400 ? resp[..400] : resp);
            if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"Meta {(int)r.StatusCode}: {Error(resp)}");
        }

        private static string Error(string json)
        {
            try { using var d = JsonDocument.Parse(json); return d.RootElement.GetProperty("error").GetProperty("message").GetString() ?? json; }
            catch { return json.Length > 200 ? json[..200] : json; }
        }
    }

    /// <summary>
    /// Envío de flyers en segundo plano (uno a la vez en todo el servidor). La pantalla consulta el progreso.
    /// Sube cada imagen UNA vez a Meta y la reutiliza para todos los contactos; pausa entre envíos para no ser bloqueados.
    /// </summary>
    public class FlyerEnvioJob
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<FlyerEnvioJob> _logger;
        private readonly object _lock = new();
        private FlyerEnvioEstado _estado = new();
        private CancellationTokenSource? _cts;

        public FlyerEnvioJob(IServiceScopeFactory scopes, ILogger<FlyerEnvioJob> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        public FlyerEnvioEstado Estado { get { lock (_lock) return new FlyerEnvioEstado { EnCurso = _estado.EnCurso, Cancelado = _estado.Cancelado, Total = _estado.Total, Enviados = _estado.Enviados, Errores = _estado.Errores, Actual = _estado.Actual, Inicio = _estado.Inicio, Fin = _estado.Fin, Registro = _estado.Registro.TakeLast(60).ToList() }; } }

        public void Cancelar() => _cts?.Cancel();

        /// <param name="botones">Con link: la parte variable del botón de cada imagen (mismo orden que imagenes); null = sin link.</param>
        public bool Iniciar(List<(string Numero, string? Nombre)> destinatarios, List<string> imagenes, string? texto, string carpeta, string usuario,
                            List<string>? botones = null, string? plantillaLink = null)
        {
            lock (_lock)
            {
                if (_estado.EnCurso) return false;
                _estado = new FlyerEnvioEstado { EnCurso = true, Total = destinatarios.Count, Inicio = DateTime.Now };
                _cts = new CancellationTokenSource();
            }
            var ct = _cts.Token;
            _ = Task.Run(async () =>
            {
                using var scope = _scopes.CreateScope();
                var wa = scope.ServiceProvider.GetRequiredService<WhatsAppService>();
                var repo = scope.ServiceProvider.GetRequiredService<IFlyerRepository>();
                void Log(string m) { lock (_lock) _estado.Registro.Add($"{DateTime.Now:HH:mm:ss} {m}"); }
                try
                {
                    var medias = new List<string>();
                    foreach (var img in imagenes) { medias.Add(await wa.SubirImagenAsync(img)); Log($"Imagen subida: {Path.GetFileName(img)}"); }
                    foreach (var (numero, nombre) in destinatarios)
                    {
                        if (ct.IsCancellationRequested) { lock (_lock) _estado.Cancelado = true; Log("Envío cancelado por el usuario."); break; }
                        lock (_lock) _estado.Actual = nombre ?? numero;
                        try
                        {
                            // Primero los flyers con la plantilla aprobada (siempre llegan); el texto libre después,
                            // porque WhatsApp solo lo entrega si el contacto escribió en las últimas 24 h.
                            for (var k = 0; k < medias.Count; k++)
                            {
                                if (botones is null) await wa.EnviarFlyerAsync(numero, medias[k], nombre);
                                else
                                {
                                    // Con link: plantilla con botón a la página del destino de esa imagen
                                    var (ok, status, resp) = await wa.EnviarPlantillaAsync(numero, plantillaLink ?? "cliente_info", medias[k],
                                        new[] { string.IsNullOrWhiteSpace(nombre) ? "viajero" : nombre.Trim().Split(' ')[0] }, botones[k]);
                                    if (!ok) throw new InvalidOperationException($"Meta {status}: {WhatsAppService.ErrorDe(resp)}");
                                }
                                await Task.Delay(300, ct);
                            }
                            if (!string.IsNullOrWhiteSpace(texto)) { await wa.EnviarTextoAsync(numero, texto.Replace("{nombre}", (nombre ?? "").Split(' ')[0])); await Task.Delay(300, ct); }
                            lock (_lock) _estado.Enviados++;
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            lock (_lock) _estado.Errores++;
                            Log($"Error con {nombre ?? numero}: {ex.Message}");
                        }
                        await Task.Delay(500, ct);
                    }
                }
                catch (OperationCanceledException) { lock (_lock) _estado.Cancelado = true; Log("Envío cancelado."); }
                catch (Exception ex) { _logger.LogError(ex, "Error en envío de flyers"); Log("Se detuvo el envío: " + ex.Message); }
                finally
                {
                    FlyerEnvioEstado fin;
                    lock (_lock) { _estado.EnCurso = false; _estado.Fin = DateTime.Now; _estado.Actual = null; fin = _estado; }
                    try
                    {
                        await repo.RegistrarEnvioAsync(new FlyerEnvio
                        {
                            Fecha_Envio = fin.Inicio ?? DateTime.Now, Carpeta_Fecha = carpeta, Total_Contactos = fin.Total, Enviados_Ok = fin.Enviados, Errores = fin.Errores,
                            Imagenes_Count = imagenes.Count, Texto_Mensaje = texto, Estado = fin.Cancelado ? "Cancelado" : fin.Errores > 0 ? "Con errores" : "Completado"
                        });
                    }
                    catch (Exception ex) { _logger.LogError(ex, "No se pudo registrar el envío de flyers ({Usuario})", usuario); }
                }
            });
            return true;
        }
    }
}
