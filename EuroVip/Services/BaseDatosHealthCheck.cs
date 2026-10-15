using EuroVip.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EuroVip.Services
{
    /// <summary>
    /// /health (lo usa DeploymentScripts/Deploy.ps1 al publicar): responde bien solo si la aplicación arrancó
    /// Y llega a la base de datos. Si falla, el despliegue vuelve solo a la versión anterior.
    /// </summary>
    public class BaseDatosHealthCheck : IHealthCheck
    {
        private readonly ICircuitoRepository _db;
        public BaseDatosHealthCheck(ICircuitoRepository db) => _db = db;

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                using var cn = _db.Conexion();
                await cn.OpenAsync(ct);
                using var cmd = cn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync(ct);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                // Sin detalles de la conexión en la respuesta (es pública): solo el motivo general.
                return HealthCheckResult.Unhealthy("Sin conexión a la base de datos", ex);
            }
        }
    }
}
