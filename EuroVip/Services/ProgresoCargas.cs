using System.Collections.Concurrent;

namespace EuroVip.Services
{
    /// <summary>Cómo va una carga: porcentaje (0-100) y el paso que está haciendo.</summary>
    public record ProgresoCarga(int Porcentaje, string Paso, DateTime Actualizado);

    /// <summary>
    /// Progreso de las cargas largas (Paquetes y Circuitos) para la barra de la pantalla.
    /// El navegador manda un id en la cabecera X-Progreso y consulta /{Carga}/Progreso?id=… mientras espera.
    /// Vive en memoria (singleton): si se reinicia la aplicación, solo se pierde la barra, no la carga.
    /// </summary>
    public interface IProgresoCargas
    {
        void Reportar(string? id, double porcentaje, string paso);
        ProgresoCarga? Leer(string? id);
    }

    public class ProgresoCargas : IProgresoCargas
    {
        public const string Cabecera = "X-Progreso";
        private readonly ConcurrentDictionary<string, ProgresoCarga> _cargas = new();

        public void Reportar(string? id, double porcentaje, string paso)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return;
            _cargas[id] = new ProgresoCarga((int)Math.Clamp(Math.Round(porcentaje), 0, 100), paso, DateTime.UtcNow);
            // limpieza: lo de hace más de una hora ya no lo mira nadie
            if (_cargas.Count > 50)
                foreach (var k in _cargas.Where(x => x.Value.Actualizado < DateTime.UtcNow.AddHours(-1)).Select(x => x.Key).ToList())
                    _cargas.TryRemove(k, out _);
        }

        public ProgresoCarga? Leer(string? id) => id is not null && _cargas.TryGetValue(id, out var p) ? p : null;
    }
}
