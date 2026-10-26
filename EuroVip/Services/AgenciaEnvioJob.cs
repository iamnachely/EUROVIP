using EuroVip.Models;
using EuroVip.Repositories;

namespace EuroVip.Services
{
    /// <summary>Una imagen del envío a agencias: archivo, destino (para el link del editor) y la página del destino en la web.</summary>
    public record AgenciaImagenEnvio(string Ruta, string Archivo, string Id_Destino, string Nombre_Destino, string UrlDestino);

    /// <summary>
    /// Envío masivo de flyers a las AGENCIAS por WhatsApp (como AgenciaMasivoService de BackTiTan), uno a la vez en todo el servidor.
    /// Plantilla aprobada «oferta_agencia»: cabecera = imagen; cuerpo = {{1}} agencia, {{2}} destino, {{3}} link de SU editor,
    /// {{4}} página del destino en la web. Cada mensaje lleva un link ÚNICO nuevo (Eurovip.flyer_agencia_link).
    /// Orden «por rondas»: imagen 1 a todas las agencias, luego imagen 2…, con pausas para que Meta no corte el envío;
    /// los que fallan por error de Meta se reintentan al final. Todo queda en flyer_envio_masivo (+ detalle) para el monitor.
    /// </summary>
    public class AgenciaEnvioJob
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _cfg;
        private readonly ILogger<AgenciaEnvioJob> _logger;
        private readonly object _lock = new();
        private CancellationTokenSource? _cts;
        public int? EnCurso { get; private set; }

        public AgenciaEnvioJob(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<AgenciaEnvioJob> logger)
        {
            _scopes = scopes;
            _cfg = cfg;
            _logger = logger;
        }

        public string Plantilla => _cfg["Eurovip:Flyer:PlantillaAgencia"] is { Length: > 0 } p ? p : "oferta_agencia";
        private int PausaContacto => int.TryParse(_cfg["Eurovip:Flyer:PausaContactoSeg"], out var s) ? Math.Clamp(s, 0, 120) : 10;
        private int PausaImagen => int.TryParse(_cfg["Eurovip:Flyer:PausaImagenSeg"], out var s) ? Math.Clamp(s, 0, 600) : 60;

        /// <summary>Segundos aproximados que tarda (para avisar antes de empezar).</summary>
        public int Duracion(int imagenes, int agencias) => imagenes * Math.Max(0, agencias - 1) * PausaContacto + Math.Max(0, imagenes - 1) * PausaImagen + imagenes * agencias;

        public void Cancelar() => _cts?.Cancel();

        public bool Iniciar(int idEnvio, List<AgenciaImagenEnvio> imagenes, List<FlyerAgencia> agencias, string baseEditor, string usuario)
        {
            lock (_lock)
            {
                if (EnCurso is not null) return false;
                EnCurso = idEnvio;
                _cts = new CancellationTokenSource();
            }
            var ct = _cts.Token;
            _ = Task.Run(async () =>
            {
                using var scope = _scopes.CreateScope();
                var wa = scope.ServiceProvider.GetRequiredService<WhatsAppService>();
                var repo = scope.ServiceProvider.GetRequiredService<IAgenciaRepository>();
                var fallidos = new List<(FlyerAgencia Agencia, AgenciaImagenEnvio Imagen, string Media)>();
                try
                {
                    await repo.ActualizarEnvioAsync(idEnvio, estado: "EnProceso", inicio: true);

                    // 1) Subir cada imagen UNA vez a Meta
                    var medias = new List<(AgenciaImagenEnvio Imagen, string Media)>();
                    foreach (var img in imagenes)
                    {
                        try { medias.Add((img, await wa.SubirImagenAsync(img.Ruta))); }
                        catch (Exception ex)
                        {
                            await repo.ActualizarEnvioAsync(idEnvio, error: $"No se pudo subir {img.Archivo} a WhatsApp: {ex.Message}");
                            await repo.RegistrarDetalleAsync(idEnvio, "(subida a WhatsApp)", "-", img.Archivo, false, ex.Message, null);
                        }
                    }
                    if (medias.Count == 0) throw new InvalidOperationException("WhatsApp no aceptó ninguna imagen.");
                    await repo.ActualizarEnvioAsync(idEnvio, totalMensajes: medias.Count * agencias.Count);

                    // 2) Por rondas: imagen 1 a todas, imagen 2 a todas…
                    for (var i = 0; i < medias.Count; i++)
                    {
                        var (img, media) = medias[i];
                        for (var a = 0; a < agencias.Count; a++)
                        {
                            ct.ThrowIfCancellationRequested();
                            var ag = agencias[a];
                            await repo.ActualizarEnvioAsync(idEnvio, contacto: ag.Nombre, imagen: img.Archivo);
                            var (ok, error, cap) = await EnviarUnoAsync(wa, repo, idEnvio, ag, img, media, baseEditor, usuario, ct);
                            if (ok) await repo.ActualizarEnvioAsync(idEnvio, sumarOk: 1);
                            else if (cap)
                            {
                                await repo.RegistrarDetalleAsync(idEnvio, ag.Nombre, ag.Telefono, img.Archivo, false, error, null);
                                await repo.ActualizarEnvioAsync(idEnvio, sumarError: 1);
                            }
                            else fallidos.Add((ag, img, media));
                            if (a < agencias.Count - 1) await Task.Delay(TimeSpan.FromSeconds(PausaContacto), ct);
                        }
                        if (i < medias.Count - 1) await Task.Delay(TimeSpan.FromSeconds(PausaImagen), ct);
                    }

                    // 3) Reintento de los que fallaron por error de Meta
                    if (fallidos.Count > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(15), ct);
                        foreach (var (ag, img, media) in fallidos)
                        {
                            ct.ThrowIfCancellationRequested();
                            await repo.ActualizarEnvioAsync(idEnvio, contacto: ag.Nombre + " (reintento)", imagen: img.Archivo);
                            var (ok, error, _) = await EnviarUnoAsync(wa, repo, idEnvio, ag, img, media, baseEditor, usuario, ct);
                            if (ok) await repo.ActualizarEnvioAsync(idEnvio, sumarOk: 1);
                            else
                            {
                                await repo.RegistrarDetalleAsync(idEnvio, ag.Nombre, ag.Telefono, img.Archivo, false, error, null);
                                await repo.ActualizarEnvioAsync(idEnvio, sumarError: 1);
                            }
                            await Task.Delay(TimeSpan.FromSeconds(Math.Max(3, PausaContacto / 2)), ct);
                        }
                    }
                    var fin = await repo.EnvioAsync(idEnvio);
                    await repo.ActualizarEnvioAsync(idEnvio, estado: fin?.Enviados_Error > 0 ? "ConErrores" : "Completado", fin: true);
                }
                catch (OperationCanceledException)
                {
                    await repo.ActualizarEnvioAsync(idEnvio, estado: "Cancelado", fin: true, error: $"Cancelado por {usuario}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el envío a agencias #{Id}", idEnvio);
                    await repo.ActualizarEnvioAsync(idEnvio, estado: "ConErrores", fin: true, error: ex.Message);
                }
                finally
                {
                    lock (_lock) { EnCurso = null; _cts = null; }
                }
            });
            return true;
        }

        /// <summary>
        /// Un mensaje a una agencia, con su link único nuevo. Reintenta si Meta pide esperar (429 / 130429 / 130472) y espera 90 s una vez
        /// si la agencia llegó a su límite de mensajes de marketing (131049). cap = true → no vale la pena reintentar más tarde.
        /// </summary>
        private async Task<(bool Ok, string? Error, bool Cap)> EnviarUnoAsync(WhatsAppService wa, IAgenciaRepository repo, int idEnvio, FlyerAgencia ag,
                                                                             AgenciaImagenEnvio img, string media, string baseEditor, string usuario, CancellationToken ct)
        {
            var token = await repo.CrearLinkAsync(img.Id_Destino, img.Nombre_Destino, ag.Id > 0 ? ag.Id : null, ag.Nombre, ag.Email, null, usuario, img.Archivo, idEnvio);
            var textos = new[] { ag.Nombre, img.Nombre_Destino, $"{baseEditor.TrimEnd('/')}/agencia/editor/{token}", img.UrlDestino };
            string? error = null;
            for (var intento = 1; intento <= 3; intento++)
            {
                var (ok, status, resp) = await wa.EnviarPlantillaAsync(ag.Telefono, Plantilla, media, textos);
                if (ok)
                {
                    await repo.RegistrarDetalleAsync(idEnvio, ag.Nombre, ag.Telefono, img.Archivo, true, null, token);
                    return (true, null, false);
                }
                error = $"Meta {status}: {WhatsAppService.ErrorDe(resp)}";
                if (resp.Contains("131049"))
                {
                    if (intento == 1) { await Task.Delay(TimeSpan.FromSeconds(90), ct); continue; }
                    await repo.RevocarLinkAsync(await IdLinkAsync(repo, token));
                    return (false, "La agencia llegó a su límite de mensajes de marketing de WhatsApp (131049).", true);
                }
                if (status == 429 || resp.Contains("130429") || resp.Contains("130472"))
                {
                    await Task.Delay(TimeSpan.FromSeconds(15 * intento), ct);
                    continue;
                }
                break;   // otro error: se reintenta en la pasada final
            }
            await repo.RevocarLinkAsync(await IdLinkAsync(repo, token));   // el mensaje no llegó: ese link no se usa
            return (false, error, false);
        }

        private static async Task<int> IdLinkAsync(IAgenciaRepository repo, string token) => (await repo.LinkVigenteAsync(token, false))?.Id ?? 0;
    }
}
