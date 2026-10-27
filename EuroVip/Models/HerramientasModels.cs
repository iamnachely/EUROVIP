namespace EuroVip.Models
{
    // ======================= HERRAMIENTAS (mantenimientos básicos) =======================
    // Cada catálogo se define UNA vez aquí y lo usan el mismo repositorio y la misma vista.
    // Los nombres de tabla/columna salen SOLO de estas definiciones (nunca del navegador).

    /// <param name="Tipo">text · textarea · number · decimal · bool · select · destino (depende de id_pais) · hotel · tipohab · url · imagen</param>
    /// <param name="Carpeta">Solo tipo "imagen": carpeta dentro de wwwroot/imagenes donde se guarda lo que se sube (destinos, alojamientos…).</param>
    /// <param name="NombreDe">Solo tipo "imagen": columna cuyo valor da nombre al archivo (como BackTiTan: minúsculas, sin acentos ni espacios + .webp).</param>
    public record HrCampo(string Col, string Etiqueta, string Tipo = "text", int Max = 0, bool Req = false,
                          bool Clave = false, bool Auto = false, string? Lista = null, bool EnTabla = true,
                          bool Mayus = false, string? Ayuda = null, string? Defecto = null,
                          string? Carpeta = null, string? NombreDe = null);

    /// <summary>Dónde se usa un registro: si hay filas, no se deja borrar. Where usa @columna de la clave.</summary>
    public record HrUso(string Tabla, string Where, string Etiqueta);

    /// <summary>Registro que ya existe o se parece a lo que se escribe (aviso en vivo). Nivel: "existe" (rojo) o "parecido" (amarillo).</summary>
    public record HrParecido(string Nivel, string Texto, string? Detalle, Dictionary<string, object?> Clave, double Puntos);

    /// <summary>Columna calculada solo para mostrar (p. ej. el nombre del país).</summary>
    public record HrExtra(string Alias, string Sql, string Etiqueta);

    public record HrCatalogo(string Clave, string Titulo, string Singular, string Icono, string Grupo, string Descripcion,
                             string Tabla, bool Idioma, HrCampo[] Campos, string Orden, string[] Buscar,
                             HrUso[] Usos, string[]? Filtros = null, HrExtra[]? Extras = null, string? Especial = null, bool F = false)
    {
        public IEnumerable<HrCampo> Claves => Campos.Where(c => c.Clave);
        /// <summary>Concordancia: "el destino" / "la habitación".</summary>
        public string El => F ? "la" : "el";
        public string Ese => F ? "esa" : "ese";
        public string Nuevo => F ? "Nueva" : "Nuevo";
        public HrCampo? Campo(string col) => Campos.FirstOrDefault(c => c.Col.Equals(col, StringComparison.OrdinalIgnoreCase));
    }

    public static class HrCatalogos
    {
        private const string ES = "id_idioma = 'ES'";

        public static readonly HrCatalogo[] Todos =
        {
            // ---------------- GEOGRAFÍA ----------------
            new("paises", "Países", "país", "bi-globe-americas", "Geografía", "Países de origen y destino de las ofertas.",
                "pais", true,
                new HrCampo[] {
                    new("id_pais", "Código", Max: 2, Req: true, Clave: true, Mayus: true, Ayuda: "2 letras, ej. DO"),
                    new("nombre", "Nombre", Max: 30, Req: true),
                    new("nombre_continente", "Continente", "select", 15, Lista: "continentes")
                }, "nombre", new[] { "id_pais", "nombre", "nombre_continente" },
                new[] { new HrUso("destino", $"id_pais = @id_pais AND {ES}", "destinos"), new HrUso("alojamiento", $"id_pais = @id_pais AND {ES}", "alojamientos") },
                Filtros: new[] { "nombre_continente" }),

            new("destinos", "Destinos", "destino", "bi-geo-alt", "Geografía", "Ciudades y zonas: Punta Cana, Santo Domingo, Madrid…",
                "destino", true,
                new HrCampo[] {
                    new("id_pais", "País", "select", 2, Req: true, Clave: true, Lista: "paises", EnTabla: false),
                    new("id_destino", "Destino", Max: 30, Req: true, Clave: true, Ayuda: "Nombre del destino, ej. Punta Cana"),
                    new("caratula", "Imagen", "imagen", 300, Carpeta: "destinos", NombreDe: "id_destino",
                        Ayuda: "Se guarda en imagenes/destinos con el nombre del destino (ej. puntacana.webp)"),
                    new("latitud", "Latitud", Max: 50, EnTabla: false),
                    new("longitud", "Longitud", Max: 50, EnTabla: false),
                    new("informacion", "Información", "textarea", EnTabla: false),
                    new("id", "Código interno", "number", EnTabla: false)
                }, "id_destino", new[] { "id_destino", "id_pais" },
                new[] {
                    new HrUso("alojamiento", $"id_destino = @id_destino AND id_pais = @id_pais AND {ES}", "alojamientos"),
                    new HrUso("estacion", $"id_destino = @id_destino AND id_pais = @id_pais AND {ES}", "estaciones"),
                    new HrUso("circuito_destino", $"id_destino = @id_destino AND id_pais = @id_pais AND {ES}", "circuitos"),
                    new HrUso("inicio", $"id_destino = @id_destino AND {ES}", "tarjetas de la Home")
                },
                Filtros: new[] { "id_pais" },
                Extras: new[] { new HrExtra("pais_nombre", "(SELECT TOP 1 p.nombre FROM Eurovip.pais p WHERE p.id_pais = t.id_pais AND p.id_idioma = 'ES')", "País") }),

            new("estaciones", "Estaciones", "estación", "bi-airplane-engines", "Geografía", "Aeropuertos para los vuelos (IATA).",
                "estacion", true,
                new HrCampo[] {
                    new("id_estacion", "Código IATA", Max: 10, Req: true, Clave: true, Mayus: true, Ayuda: "ej. PUJ"),
                    new("nombre", "Nombre", Max: 80, Req: true),
                    new("id_pais", "País", "select", 2, Req: true, Clave: true, Lista: "paises"),
                    new("id_destino", "Destino", "destino", 30, Req: true, Clave: true),
                    new("id_tipo_estacion", "Tipo", "select", 15, Req: true, Lista: "tiposEstacion", Defecto: "AEROPUERTO")
                }, "id_estacion", new[] { "id_estacion", "nombre", "id_destino" },
                new[] { new HrUso("servicio_itinerario", "(id_estacion_origen = @id_estacion OR id_estacion_destino = @id_estacion)", "vuelos cargados") },
                Filtros: new[] { "id_pais" }, F: true),

            // ---------------- COMERCIAL ----------------
            new("campanyas", "Campañas", "campaña", "bi-megaphone", "Comercial", "Agrupan las ofertas en la web (DTGeneral, Verano…).",
                "campanya", true,
                new HrCampo[] {
                    new("id_campanya", "Número", "number", Clave: true, Auto: true, Ayuda: "Se asigna solo"),
                    new("nombre", "Nombre", Max: 30, Req: true),
                    new("id_tipo_servicio", "Tipo de servicio", "select", 20, Req: true, Lista: "tiposServicio", Defecto: "SOLO_HOTEL"),
                    new("descripcion", "Descripción", "textarea")
                }, "nombre", new[] { "nombre", "id_tipo_servicio", "CAST(t.id_campanya AS varchar(10))" },
                new[] {
                    new HrUso("servicio", "id_campanya = @id_campanya AND id_idioma = 'ES'", "ofertas/salidas"),
                    new HrUso("inicio", "id_campanya = @id_campanya AND id_idioma = 'ES'", "tarjetas de la Home"),
                    new HrUso("carrucelDestino", "id_campanya = @id_campanya", "precios del carrusel")
                }, Filtros: new[] { "id_tipo_servicio" }, F: true),

            new("monedas", "Monedas", "moneda", "bi-currency-euro", "Comercial", "Divisas en las que se vende.",
                "moneda", false,
                new HrCampo[] {
                    new("id_moneda", "Código", Max: 3, Req: true, Clave: true, Mayus: true, Ayuda: "3 letras, ej. EUR"),
                    new("nombre", "Nombre", Max: 20, Req: true)
                }, "id_moneda", new[] { "id_moneda", "nombre" },
                new[] { new HrUso("servicio", "id_moneda = @id_moneda", "ofertas/salidas"), new HrUso("inicio", "id_moneda = @id_moneda", "tarjetas de la Home") }, F: true),

            new("tasas", "Tasas y suplementos", "tasa", "bi-receipt", "Comercial", "Suplementos que se suman al precio (tasas de origen, seguros…).",
                "tasa", true,
                new HrCampo[] {
                    new("id_tasa", "Nombre", Max: 50, Req: true, Clave: true),
                    new("id_tipo_tasa", "Tipo (número)", "number", Ayuda: "Código de tipo de tasa de ViajeVDT")
                }, "id_tasa", new[] { "id_tasa" },
                new[] { new HrUso("servicio_tasa", "id_tasa = @id_tasa AND id_idioma = 'ES'", "ofertas/salidas") }, F: true),

            new("regimenes", "Regímenes", "régimen", "bi-cup-hot", "Comercial", "Todo incluido, desayuno, media pensión…",
                "regimen", true,
                new HrCampo[] {
                    new("id_regimen", "Código", Max: 2, Req: true, Clave: true, Mayus: true, Ayuda: "2 letras, ej. TI"),
                    new("descripcion", "Descripción", Max: 200, Req: true)
                }, "id_regimen", new[] { "id_regimen", "descripcion" },
                new[] { new HrUso("servicio_detalle", "id_regimen = @id_regimen AND id_idioma = 'ES'", "ofertas") }),

            new("idiomas", "Idiomas", "idioma", "bi-translate", "Comercial", "Idiomas / mercados. Eurovip carga solo en ES.",
                "idioma", false,
                new HrCampo[] {
                    new("id_idioma", "Código", Max: 2, Req: true, Clave: true, Mayus: true),
                    new("nombre", "Nombre", Max: 20, Req: true, Mayus: true)
                }, "id_idioma", new[] { "id_idioma", "nombre" },
                new[] { new HrUso("destino", "id_idioma = @id_idioma", "destinos"), new HrUso("campanya", "id_idioma = @id_idioma", "campañas") }),

            // ---------------- PROVEEDORES ----------------
            new("proveedores", "Proveedores", "proveedor", "bi-briefcase", "Proveedores", "Tour operadores y aerolíneas.",
                "proveedor", false,
                new HrCampo[] {
                    new("id_proveedor", "Código", Max: 10, Req: true, Clave: true, Mayus: true),
                    new("nombre", "Nombre", Max: 50, Req: true),
                    new("id_tipo_proveedor", "Tipo", "select", 20, Req: true, Lista: "tiposProveedor", Defecto: "TOUR OPERADOR")
                }, "nombre", new[] { "id_proveedor", "nombre" },
                new[] {
                    new HrUso("servicio", "id_Proveedor = @id_proveedor", "ofertas/salidas"),
                    new HrUso("circuito", "id_proveedor = @id_proveedor", "circuitos"),
                    new HrUso("servicio_itinerario", "id_proveedor = @id_proveedor", "vuelos cargados")
                }, Filtros: new[] { "id_tipo_proveedor" }),

            // ---------------- HOTELES ----------------
            new("alojamientos", "Alojamientos", "alojamiento", "bi-building", "Hoteles", "Hoteles con su destino, categoría, imagen y habitaciones.",
                "alojamiento", true,
                new HrCampo[] {
                    new("id_alojamiento", "Número", "number", Clave: true, Auto: true, Ayuda: "Se asigna solo"),
                    new("nombre", "Nombre", Max: 100, Req: true),
                    new("id_pais", "País", "select", 2, Req: true, Lista: "paises", Defecto: "DO"),
                    new("id_destino", "Destino", "destino", 30, Req: true),
                    new("id_tipo_alojamiento", "Tipo", "select", 20, Req: true, Lista: "tiposAlojamiento", Defecto: "HOTEL"),
                    new("categoria", "Categoría (estrellas)", "estrellas", 15, Req: true, Ayuda: "pulsa de 1 a 6 estrellas"),
                    new("caratula", "Imagen", "imagen", 300, Carpeta: "alojamientos", NombreDe: "id_alojamiento",
                        Ayuda: "Se guarda en imagenes/alojamientos con el número del hotel (ej. 6031.webp). En un hotel nuevo, guárdalo primero."),
                    new("descripcion", "Descripción", "textarea", EnTabla: false),
                    new("latitude", "Latitud", Max: 50, EnTabla: false),
                    new("longitude", "Longitud", Max: 50, EnTabla: false),
                    // Grupo (= número del hotel), Categoría en circuitos y Se usa en circuitos: ocultos (pedido de la usuaria);
                    // al editar se conservan y en un hotel nuevo Grupo = su número (HerramientasRepository)
                    new("activo", "Activo", "bool", Defecto: "1")
                }, "nombre", new[] { "nombre", "id_destino", "CAST(t.id_alojamiento AS varchar(12))" },
                new[] {
                    new HrUso("servicio_detalle", "id_alojamiento = @id_alojamiento AND id_idioma = 'ES'", "ofertas"),
                    new HrUso("servicio_circuito_alojamiento", "id_alojamiento = @id_alojamiento AND id_idioma = 'ES'", "circuitos")
                }, Filtros: new[] { "id_pais", "activo" },
                Extras: new[] { new HrExtra("habitaciones", "(SELECT COUNT(*) FROM Eurovip.alojamiento_habitacion h WHERE h.id_alojamiento = t.id_alojamiento AND h.id_idioma = 'ES')", "Habitaciones") },
                Especial: "alojamiento"),

            new("habitaciones", "Habitaciones", "habitación", "bi-door-open", "Hoteles", "Habitaciones de cada hotel (doble, suite, vista mar…).",
                "alojamiento_habitacion", true,
                new HrCampo[] {
                    new("id_alojamiento", "Hotel", "hotel", Req: true, Clave: true),
                    new("id_habitacion", "Número", "number", Clave: true, Auto: true, Ayuda: "Se asigna solo"),
                    new("nombre", "Nombre", Max: 255, Req: true),
                    new("id_tipo_habitacion", "Tipo", "tipohab", 100, Req: true),
                    new("codigo", "Código", Max: 50, Req: true, Defecto: ""),
                    new("precio", "Suplemento", "decimal", Req: true, Defecto: "0"),
                    new("grupo", "Grupo", "number", Req: true, EnTabla: false, Defecto: "0"),
                    new("nota", "Nota", "textarea", EnTabla: false)
                    // Código / Nombre Newblue: quitados de la pantalla (pedido de la usuaria); al editar se conservan los que haya
                }, "nombre", new[] { "nombre", "id_tipo_habitacion", "codigo", "CAST(t.id_habitacion AS varchar(12))" },
                new[] { new HrUso("servicio_habitacion", "id_habitacion = @id_habitacion AND id_alojamiento = @id_alojamiento AND id_idioma = 'ES'", "ofertas") },
                Filtros: new[] { "id_alojamiento" },
                Extras: new[] { new HrExtra("hotel_nombre", "(SELECT TOP 1 a.nombre FROM Eurovip.alojamiento a WHERE a.id_alojamiento = t.id_alojamiento AND a.id_idioma = 'ES')", "Hotel") },
                Especial: "habitacion", F: true)
        };

        /// <summary>Campos que se revisan al escribir: los de texto que identifican el registro (clave, nombre o descripción).</summary>
        public static bool Vigilado(HrCampo f) => f.Tipo == "text" && !f.Auto && (f.Clave || f.Col is "nombre" or "descripcion");

        public static HrCatalogo? Buscar(string? clave) => Todos.FirstOrDefault(c => c.Clave.Equals(clave, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Catálogos con fotos en Eurovip.multimedia (documento esquema-multimedia.md): tipo, columna clave y si la portada se
        /// carga en el componente de fotos (Países, que no tiene columna de imagen) o en su campo "imagen" (Destinos, Alojamientos).
        /// </summary>
        public static (string Tipo, string Col, bool Portada)? Fotos(HrCatalogo c) => c.Clave switch
        {
            "destinos" => ("DESTINO", "id_destino", false),
            "alojamientos" => ("ALOJAMIENTO", "id_alojamiento", false),
            "paises" => ("PAIS", "id_pais", true),
            _ => null
        };
    }

    // ---------- respuestas ----------
    public class HrResumen
    {
        public string Clave { get; set; } = "";
        public int Total { get; set; }
    }

    public class HrActividad
    {
        public string Clave { get; set; } = "";
        public string Catalogo { get; set; } = "";
        public string Icono { get; set; } = "";
        public string Accion { get; set; } = "";
        public string Usuario { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string? Detalle { get; set; }
    }

    public class HrResultadoBusqueda
    {
        public string Clave { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Icono { get; set; } = "";
        public List<Dictionary<string, object?>> Filas { get; set; } = new();
    }
}
