using System.Data;
using System.Globalization;
using Dapper;

namespace EuroVip.Services
{
    /// <summary>
    /// CAMBIO 139 · Precio mínimo de carga POR VISTA (Hoteles, Solo Hotel, Paquetes, Circuitos, Excursiones, Traslados, Cruceros).
    /// Pedido de la usuaria: «un botón en cada vista de carga para establecer el precio mínimo con que se podrá cargar; si no se
    /// pone nada no hay mínimo; si se pone, avisa y no deja cargar por debajo, pero deja editar y ajustar el precio. Quita el
    /// mínimo que hay ahora» (el antiguo config_sistema.precio_base_minimo = 300, el mismo para todo, ya no se usa).
    /// Se compara con el precio tal como se escribe (el total), en la moneda de la carga. Mismo valor para formularios y Excel.
    /// </summary>
    public static class PreciosMinimos
    {
        public const string Hoteles = "HOTELES", Ofertas = "OFERTAS", Paquetes = "PAQUETES", Circuitos = "CIRCUITOS",
                            Excursiones = "EXCURSIONES", Traslados = "TRASLADOS", Cruceros = "CRUCEROS";

        /// <summary>Vistas con su nombre para la pantalla y lo que se carga («un traslado», «una salida»…).</summary>
        public static readonly IReadOnlyDictionary<string, (string Nombre, string Cosa)> Modulos = new Dictionary<string, (string, string)>
        {
            [Hoteles] = ("Hoteles", "hotel"), [Ofertas] = ("Solo Hotel", "oferta"), [Paquetes] = ("Paquetes", "paquete"),
            [Circuitos] = ("Circuitos", "salida de circuito"), [Excursiones] = ("Excursiones", "excursión"),
            [Traslados] = ("Traslados", "traslado"), [Cruceros] = ("Cruceros", "crucero"),
        };

        public static string? Normalizar(string? modulo) =>
            modulo?.Trim().ToUpperInvariant() is { } m && Modulos.ContainsKey(m) ? m : null;

        /// <summary>El mínimo de la vista, o null si no tiene (no hay mínimo).</summary>
        public static Task<decimal?> ObtenerAsync(IDbConnection cn, string modulo) =>
            cn.ExecuteScalarAsync<decimal?>("SELECT precio FROM Eurovip.precio_minimo_carga WHERE modulo = @modulo", new { modulo });

        public static async Task<Dictionary<string, decimal?>> TodosAsync(IDbConnection cn)
        {
            var filas = (await cn.QueryAsync<(string Modulo, decimal? Precio)>("SELECT modulo, precio FROM Eurovip.precio_minimo_carga")).ToDictionary(x => x.Modulo, x => x.Precio);
            return Modulos.Keys.ToDictionary(k => k, k => filas.GetValueOrDefault(k));
        }

        /// <summary>Guarda (o quita, con null) el mínimo de la vista. Devuelve el valor anterior.</summary>
        public static async Task<decimal?> GuardarAsync(IDbConnection cn, string modulo, decimal? precio, string usuario)
        {
            var antes = await ObtenerAsync(cn, modulo);
            await cn.ExecuteAsync(@"
MERGE Eurovip.precio_minimo_carga AS t
USING (SELECT @modulo AS modulo) AS s ON t.modulo = s.modulo
WHEN MATCHED THEN UPDATE SET precio = @precio, usuario = @usuario, fecha = SYSDATETIME()
WHEN NOT MATCHED THEN INSERT (modulo, precio, usuario) VALUES (@modulo, @precio, @usuario);", new { modulo, precio, usuario });
            return antes;
        }

        private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-DO");
        public static string Cifra(decimal v) => v.ToString(v % 1 == 0 ? "#,0" : "#,0.00", Es);

        /// <summary>True si el precio queda por debajo del mínimo (sin mínimo o sin precio → false).</summary>
        public static bool Bajo(decimal? minimo, decimal? precio) => minimo is > 0 && precio is > 0 && precio < minimo;

        /// <summary>«Traslado Aeropuerto → Bávaro: el precio 25 es menor al mínimo de Traslados (30). Súbelo a 30 o más.»</summary>
        public static string Mensaje(string modulo, decimal minimo, decimal precio, string? que = null) =>
            $"{(string.IsNullOrWhiteSpace(que) ? "" : que + ": ")}el precio {Cifra(precio)} es menor al mínimo de {Modulos[modulo].Nombre} ({Cifra(minimo)}). Súbelo a {Cifra(minimo)} o más.";

        /// <summary>
        /// Respuesta de error de un formulario con precios por debajo del mínimo: el primer mensaje, el campo a marcar y la
        /// lista completa (la pantalla muestra el aviso con todo lo que hay que ajustar).
        /// </summary>
        public static object Rechazo(string modulo, decimal minimo, IReadOnlyList<(string? Que, decimal Precio)> bajos, string? campo = null)
        {
            var lista = bajos.Select(b => Mensaje(modulo, minimo, b.Precio, b.Que)).ToList();
            var mensaje = bajos.Count == 1 ? lista[0]
                : $"{bajos.Count} precios son menores al mínimo de {Modulos[modulo].Nombre} ({Cifra(minimo)}). Súbelos a {Cifra(minimo)} o más.";
            return new
            {
                mensaje, precioMinimo = Info(modulo, minimo), precioMinimoLista = lista,
                errores = campo is null ? null : new Dictionary<string, string> { [campo] = lista[0] }
            };
        }

        /// <summary>Precios por debajo del mínimo en una carga que devuelve una lista de errores (Solo Hotel, Paquetes, Circuitos).</summary>
        public sealed record Fallo(string Modulo, decimal Minimo, List<string> Lista)
        {
            public object Info() => PreciosMinimos.Info(Modulo, Minimo);
        }

        /// <summary>Lo que se agrega a la respuesta de error para que la pantalla muestre el aviso del precio mínimo.</summary>
        public static object Info(string modulo, decimal minimo) => new { modulo, nombre = Modulos[modulo].Nombre, minimo };
    }
}
