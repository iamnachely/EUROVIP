using System.Collections.Concurrent;

namespace EuroVip.Services
{
    /// <summary>Cómo va una carga: porcentaje (0-100) y el paso que está haciendo.</summary>
    /// <summary>«Terminado» = la carga ya acabó (bien o mal) y su resultado está en «Cuerpo» con su código HTTP en «Estado»: así la pantalla se entera aunque se haya cortado su conexión.</summary>
    public record ProgresoCarga(int Porcentaje, string Paso, DateTime Actualizado, bool Terminado = false, int Estado = 0, object? Cuerpo = null);

    /// <summary>
    /// Progreso de las cargas largas (Paquetes y Circuitos) para la barra de la pantalla.
    /// El navegador manda un id en la cabecera X-Progreso y consulta /{Carga}/Progreso?id=… mientras espera.
    /// Vive en memoria (singleton): si se reinicia la aplicación, solo se pierde la barra, no la carga.
    /// </summary>
    public interface IProgresoCargas
    {
        void Reportar(string? id, double porcentaje, string paso);
        ProgresoCarga? Leer(string? id);
        /// <summary>La carga acabó: deja el resultado (y su código HTTP) para que la pantalla lo recoja con /Progreso?id=…</summary>
        void Terminar(string? id, int estado, object cuerpo);
    }

    public class ProgresoCargas : IProgresoCargas
    {
        public const string Cabecera = "X-Progreso";
        private readonly ConcurrentDictionary<string, ProgresoCarga> _cargas = new();

        public void Reportar(string? id, double porcentaje, string paso)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return;
            if (_cargas.TryGetValue(id, out var antes) && antes.Terminado) return;   // ya acabó: no se pisa
            _cargas[id] = new ProgresoCarga((int)Math.Clamp(Math.Round(porcentaje), 0, 100), paso, DateTime.UtcNow);
            // limpieza: lo de hace más de una hora ya no lo mira nadie
            if (_cargas.Count > 50)
                foreach (var k in _cargas.Where(x => x.Value.Actualizado < DateTime.UtcNow.AddHours(-1)).Select(x => x.Key).ToList())
                    _cargas.TryRemove(k, out _);
        }

        public void Terminar(string? id, int estado, object cuerpo)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return;
            _cargas[id] = new ProgresoCarga(estado is >= 200 and < 300 ? 100 : (_cargas.TryGetValue(id, out var p) ? p.Porcentaje : 0), estado is >= 200 and < 300 ? "¡Listo!" : "Terminó con un problema", DateTime.UtcNow, true, estado, cuerpo);
        }

        public ProgresoCarga? Leer(string? id) => id is not null && _cargas.TryGetValue(id, out var p) ? p : null;
    }
}
