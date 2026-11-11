using System.Data;
using Dapper;

namespace EuroVip.Services
{
    /// <summary>
    /// Información para el cliente (CAMBIO 130), igual en todos los productos y siempre OPCIONAL:
    /// Incluye · No incluye · Condiciones · Documentación · Importante · Oferta especial.
    /// Pedido de la usuaria: «agrégalo donde falte; siempre opcional, pero si un usuario carga información, que pueda editarla siempre».
    /// </summary>
    public class InfoClienteDto
    {
        public string? Incluye { get; set; }
        public string? No_Incluye { get; set; }
        public string? Condiciones { get; set; }
        public string? Documentacion { get; set; }
        public string? Importante { get; set; }
        public string? Oferta_Especial { get; set; }

        public bool Vacia => new[] { Incluye, No_Incluye, Condiciones, Documentacion, Importante, Oferta_Especial }.All(string.IsNullOrWhiteSpace);
    }

    public static class InfoCliente
    {
        private static string? N(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();

        /// <summary>Tablas con las 6 columnas (nombre → tabla y condición por la clave @id). Solo estas: van en el SQL.</summary>
        private static readonly IReadOnlyDictionary<string, (string Tabla, string Donde)> Tablas = new Dictionary<string, (string, string)>
        {
            ["Eurovip.servicio_excursion"] = ("Eurovip.servicio_excursion", "id_Excursion = @id"),
            ["Eurovip.traslado"] = ("Eurovip.traslado", "id_traslado = @id"),
            ["Eurovip.inicio"] = ("Eurovip.inicio", "id = @id"),
            // Hoteles de la vitrina: se identifican por id_servicio (como en HotelRepository)
            ["HOTEL"] = ("Eurovip.inicio", "id_servicio = @id AND tipo_servicio = 'HOTEL' AND id_idioma = 'ES'"),
            ["Eurovip.crucero"] = ("Eurovip.crucero", "id_crucero = @id"),
        };

        public static async Task<InfoClienteDto> LeerAsync(IDbConnection cn, string tabla, int id)
        {
            var (t, donde) = Tablas[tabla];
            return await cn.QueryFirstOrDefaultAsync<InfoClienteDto>($@"SELECT incluye AS Incluye, no_incluye AS No_Incluye, condiciones AS Condiciones,
                documentacion AS Documentacion, importante AS Importante, oferta_especial AS Oferta_Especial FROM {t} WHERE {donde}", new { id })
                ?? new InfoClienteDto();
        }

        /// <summary>Guarda los 6 textos (vacío = NULL). null = la pantalla no los mandó: no se toca nada.</summary>
        public static Task GuardarAsync(IDbConnection cn, string tabla, int id, InfoClienteDto? info, IDbTransaction? tx = null)
        {
            if (info is null) return Task.CompletedTask;
            var (t, donde) = Tablas[tabla];
            return cn.ExecuteAsync($@"UPDATE {t} SET incluye = @inc, no_incluye = @noinc, condiciones = @cond, documentacion = @docs,
                importante = @imp, oferta_especial = @ofe WHERE {donde}",
                new { id, inc = N(info.Incluye), noinc = N(info.No_Incluye), cond = N(info.Condiciones), docs = N(info.Documentacion), imp = N(info.Importante), ofe = N(info.Oferta_Especial) }, tx);
        }

        // ---------------- Solo Hotel, Paquetes y Circuitos: Eurovip.servicio_descripcion (lo que ya lee la web) ----------------
        // incluye / no_incluye / oferta_especial van en su columna; en «nota» las condiciones y luego «Documentación: …» e
        // «Importante: …», separados por una línea en blanco (igual que CargaCircuitoService.GuardarDescripcionAsync).

        /// <summary>Separa la «nota» de servicio_descripcion en condiciones, documentación e importante.</summary>
        public static (string? Condiciones, string? Documentacion, string? Importante) Desempacar(string? nota)
        {
            var cond = new List<string>(); var docs = new List<string>(); var imp = new List<string>();
            foreach (var b in (nota ?? "").Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                if (b.StartsWith("Documentación:", StringComparison.OrdinalIgnoreCase)) docs.Add(b["Documentación:".Length..].Trim());
                else if (b.StartsWith("Importante:", StringComparison.OrdinalIgnoreCase)) imp.Add(b["Importante:".Length..].Trim());
                else if (b.StartsWith("Condiciones:", StringComparison.OrdinalIgnoreCase)) cond.Add(b["Condiciones:".Length..].Trim());
                else if (b.Trim() != "") cond.Add(b.Trim());
            }
            static string? J(List<string> l) => l.Count == 0 ? null : string.Join("\n\n", l);
            return (J(cond), J(docs), J(imp));
        }

        /// <summary>Junta condiciones, documentación e importante en la «nota» de servicio_descripcion.</summary>
        public static string? Empacar(string? condiciones, string? documentacion, string? importante)
        {
            var partes = new[] { N(condiciones), N(documentacion) is string d ? "Documentación: " + d : null, N(importante) is string i ? "Importante: " + i : null }
                .Where(x => x is not null).ToList();
            return partes.Count == 0 ? null : string.Join("\n\n", partes);
        }
    }
}
