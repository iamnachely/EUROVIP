using Dapper;
using EuroVip.Repositories;

namespace EuroVip.Services
{
    /// <summary>
    /// Orden de las pestañas del buscador de la web (CAMBIO 134, tabla Eurovip.buscador_pestana). Pedido de la usuaria: «poder
    /// cambiar las secciones del buscador y ponerles orden». La web lo lee con Eurovip.spPortalListarBuscadorPestanas
    /// (guía: Docs/buscador-pestanas.md).
    /// </summary>
    public static class BuscadorPestanas
    {
        public sealed record Pestana(string Clave, string Nombre, string? Icono, short Orden, bool Activo, bool Predeterminada);
        public sealed record Cambio(string Clave, bool Activo, bool Predeterminada);

        public static async Task<List<Pestana>> ListarAsync(ICircuitoRepository db)
        {
            using var cn = db.Conexion();
            return (await cn.QueryAsync<Pestana>("SELECT clave, nombre, icono, orden, activo, predeterminada FROM Eurovip.buscador_pestana ORDER BY orden, clave")).ToList();
        }

        /// <summary>Guarda el orden (el de la lista: la primera es la 1), cuáles se ven y cuál se abre primero.</summary>
        public static async Task<string?> GuardarAsync(ICircuitoRepository db, IReadOnlyList<Cambio> lista, string usuario)
        {
            var actuales = (await ListarAsync(db)).Select(p => p.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (lista.Count == 0 || lista.Any(x => !actuales.Contains(x.Clave)) || lista.Select(x => x.Clave.ToLowerInvariant()).Distinct().Count() != lista.Count)
                return "La lista de pestañas no coincide con las de la web. Recarga la página.";
            if (!lista.Any(x => x.Activo)) return "Tiene que quedar al menos una pestaña visible.";
            // La predeterminada: la marcada (si está visible); si no, la primera visible
            var pred = lista.FirstOrDefault(x => x.Predeterminada && x.Activo)?.Clave ?? lista.First(x => x.Activo).Clave;
            using var cn = db.Conexion();
            cn.Open();
            using var tx = cn.BeginTransaction();
            for (var i = 0; i < lista.Count; i++)
                await cn.ExecuteAsync(@"UPDATE Eurovip.buscador_pestana SET orden = @orden, activo = @activo, predeterminada = @pred,
                    usuario = @usuario, fecha_modificacion = SYSDATETIME() WHERE clave = @clave",
                    new { orden = (short)(i + 1), activo = lista[i].Activo, pred = string.Equals(lista[i].Clave, pred, StringComparison.OrdinalIgnoreCase), usuario = usuario.Length > 50 ? usuario[..50] : usuario, clave = lista[i].Clave }, tx);
            tx.Commit();
            return null;
        }
    }
}
