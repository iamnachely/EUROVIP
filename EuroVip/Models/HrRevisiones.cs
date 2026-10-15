namespace EuroVip.Models
{
    /// <summary>
    /// Un control de "Datos a revisar" de un mantenimiento. <paramref name="Where"/> filtra filas de la tabla del
    /// catálogo (alias t) que están mal; <paramref name="Valor"/> y <paramref name="Detalle"/> son lo que se muestra.
    /// </summary>
    public record HrRevision(string Motivo, string Ayuda, string Where, string Valor, string? Detalle = null);

    /// <summary>Un grupo de datos inconsistentes encontrado (lo que ve el usuario en el mantenimiento).</summary>
    public class HrRevisionResultado
    {
        public string Motivo { get; set; } = "";
        public string Ayuda { get; set; } = "";
        public int Total { get; set; }
        public List<Dictionary<string, object?>> Filas { get; set; } = new();
    }

    /// <summary>
    /// Controles de calidad de cada mantenimiento de Herramientas: lo mismo que ya no deja cargar
    /// <see cref="EuroVip.Services.ReglasDatos"/>, pero buscando lo que ya está guardado (copiado de ViajeVDT o de antes).
    /// </summary>
    public static class HrRevisiones
    {
        private const string ES = "t.id_idioma = 'ES'";
        // Letras con las que puede empezar un nombre (incluye acentos y ñ).
        private const string Letra = "[A-Za-zÁÉÍÓÚÜÑáéíóúüñ]";

        private static HrRevision Espacios(string col, string etiqueta) =>
            new("Espacios de más", $"{etiqueta} con espacios al principio, al final o dobles: se ve igual pero no coincide al buscar.",
                $"(t.[{col}] LIKE ' %' OR t.[{col}] LIKE '% ' OR t.[{col}] LIKE '%  %')", $"'[' + t.[{col}] + ']'");

        private static HrRevision SinLetras(string col, string etiqueta) =>
            new("Nombre sin letras o con símbolos", $"{etiqueta} solo con números/símbolos o que empieza por un símbolo (ej. «222», «-pego»).",
                $"(t.[{col}] NOT LIKE '%{Letra}%' OR (LTRIM(t.[{col}]) NOT LIKE '{Letra}%' AND LTRIM(t.[{col}]) NOT LIKE '[0-9(]%'))", $"t.[{col}]");

        private static HrRevision Repetido(string tabla, string col, string etiqueta, string grupoExtra = "", bool idioma = true) =>
            new($"{etiqueta} repetido", $"Hay más de un registro con el mismo {etiqueta.ToLower()}: deja uno solo.",
                $"EXISTS (SELECT 1 FROM Eurovip.[{tabla}] x WHERE x.[{col}] = t.[{col}]{(idioma ? " AND x.id_idioma = t.id_idioma" : "")}{grupoExtra} " +
                $"GROUP BY x.[{col}] HAVING COUNT(*) > 1)", $"t.[{col}]");

        private static readonly Dictionary<string, HrRevision[]> Por = new(StringComparer.OrdinalIgnoreCase)
        {
            ["paises"] = new[]
            {
                new HrRevision("Código que no son 2 letras", "El código del país tiene que ser de 2 letras (ej. DO, ES).",
                    "t.id_pais NOT LIKE '[A-Z][A-Z]'", "t.id_pais", "t.nombre"),
                new HrRevision("Nombre que no es un país", "Lleva números o símbolos, o tiene menos de 3 letras.",
                    $"(t.nombre LIKE '%[0-9]%' OR LEN(t.nombre) < 3 OR LTRIM(t.nombre) NOT LIKE '{Letra}%')", "t.nombre", "t.id_pais"),
                Repetido("pais", "nombre", "Nombre de país") with { Detalle = "t.id_pais" },
                Espacios("nombre", "Nombres de país")
            },
            ["destinos"] = new[]
            {
                new HrRevision("No parece un destino", "Lleva números o símbolos, es muy corto o no es un lugar (ej. «65401», «-pego», «falta»).",
                    $"(t.id_destino LIKE '%[0-9]%' OR LEN(LTRIM(t.id_destino)) < 3 OR LTRIM(t.id_destino) NOT LIKE '{Letra}%' " +
                    "OR LTRIM(RTRIM(t.id_destino)) IN ('falta','por cuenta del cliente','sin destino','ninguno','n/a','prueba','test'))",
                    "t.id_destino", "(SELECT TOP 1 p.nombre FROM Eurovip.pais p WHERE p.id_pais = t.id_pais AND p.id_idioma = 'ES')"),
                new HrRevision("Mismo destino en varios países", "Un destino va en un solo país. Si son ciudades distintas, cambia el nombre de una (ej. «Toledo (EE.UU.)»).",
                    "EXISTS (SELECT 1 FROM Eurovip.destino x WHERE x.id_destino = t.id_destino AND x.id_idioma = t.id_idioma AND x.id_pais <> t.id_pais)",
                    "t.id_destino", "(SELECT TOP 1 p.nombre FROM Eurovip.pais p WHERE p.id_pais = t.id_pais AND p.id_idioma = 'ES')"),
                new HrRevision("País que no existe", "El país del destino no está en Países.",
                    "NOT EXISTS (SELECT 1 FROM Eurovip.pais p WHERE p.id_pais = t.id_pais AND p.id_idioma = 'ES')", "t.id_destino", "t.id_pais"),
                Espacios("id_destino", "Destinos")
            },
            ["estaciones"] = new[]
            {
                new HrRevision("Destino que no existe en ese país", "La estación apunta a un destino que no está en Destinos con ese país.",
                    "NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = t.id_destino AND d.id_pais = t.id_pais AND d.id_idioma = 'ES')",
                    "t.id_estacion + ' · ' + t.nombre", "t.id_destino + ' (' + t.id_pais + ')'"),
                new HrRevision("Código IATA raro", "El código del aeropuerto tiene que ser de 3 letras (ej. PUJ).",
                    "t.id_estacion NOT LIKE '[A-Z][A-Z][A-Z]'", "t.id_estacion", "t.nombre"),
                new HrRevision("No es un aeropuerto", "Sin nombre real («Sin Especificar», «Nuevo», el propio código) o es una estación de tren/autobús.",
                    "(t.nombre IN ('Sin Especificar','Desconocido','Nuevo','Prueba','Test') OR t.nombre = t.id_estacion OR t.id_destino = t.id_estacion " +
                    "OR t.nombre LIKE '%estaci_n de tren%' OR t.nombre LIKE '%ferrocarril%' OR t.nombre LIKE '%rail stn%' OR t.nombre LIKE '%railway%' OR t.nombre LIKE '%autobuses%')",
                    "t.id_estacion + ' · ' + t.nombre", "t.id_destino"),
                SinLetras("nombre", "Nombre de la estación"),
                Espacios("id_destino", "Destinos de la estación")
            },
            ["alojamientos"] = new[]
            {
                SinLetras("nombre", "Nombre del hotel") with { Detalle = "t.id_destino" },
                new HrRevision("Hotel repetido", "Mismo nombre y mismo destino: deja uno solo (los demás desactívalos o elimínalos).",
                    "EXISTS (SELECT 1 FROM Eurovip.alojamiento x WHERE x.nombre = t.nombre AND x.id_destino = t.id_destino AND x.id_idioma = t.id_idioma AND x.id_alojamiento <> t.id_alojamiento)",
                    "t.nombre", "t.id_destino + ' · n.º ' + CAST(t.id_alojamiento AS varchar(12))"),
                new HrRevision("Destino que no existe", "El destino del hotel no está en Destinos.",
                    "NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = t.id_destino AND d.id_idioma = 'ES')",
                    "t.nombre", "ISNULL(NULLIF(t.id_destino, ''), '(vacío)')"),
                new HrRevision("País distinto al de su destino", "El destino existe, pero en otro país: corrige el país del hotel.",
                    "EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = t.id_destino AND d.id_idioma = 'ES') " +
                    "AND NOT EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = t.id_destino AND d.id_pais = t.id_pais AND d.id_idioma = 'ES')",
                    "t.nombre", "t.id_destino + ' · hotel en ' + ISNULL(t.id_pais, '?') + ', destino en ' + (SELECT TOP 1 d.id_pais FROM Eurovip.destino d WHERE d.id_destino = t.id_destino AND d.id_idioma = 'ES')"),
                new HrRevision("Destino escrito distinto", "El destino del hotel está escrito con otras mayúsculas que en Destinos (ej. «RIVIERA MAYA»).",
                    "EXISTS (SELECT 1 FROM Eurovip.destino d WHERE d.id_destino = t.id_destino AND d.id_pais = t.id_pais AND d.id_idioma = 'ES' " +
                    "AND d.id_destino COLLATE Latin1_General_CS_AS <> t.id_destino COLLATE Latin1_General_CS_AS)", "t.nombre", "t.id_destino"),
                Espacios("nombre", "Nombres de hotel") with { Where = "(t.nombre LIKE ' %' OR t.nombre LIKE '% ' OR t.nombre LIKE '%  %' OR t.id_destino LIKE ' %' OR t.id_destino LIKE '% ')" }
            },
            ["habitaciones"] = new[]
            {
                new HrRevision("Habitación repetida en el mismo hotel", "El hotel tiene dos habitaciones con el mismo nombre: deja una sola.",
                    "EXISTS (SELECT 1 FROM Eurovip.alojamiento_habitacion x WHERE x.id_alojamiento = t.id_alojamiento AND x.id_idioma = t.id_idioma AND x.nombre = t.nombre AND x.id_habitacion <> t.id_habitacion)",
                    "t.nombre", "(SELECT TOP 1 a.nombre FROM Eurovip.alojamiento a WHERE a.id_alojamiento = t.id_alojamiento AND a.id_idioma = 'ES')"),
                new HrRevision("Hotel que no existe", "La habitación es de un hotel que ya no está en Alojamientos.",
                    "NOT EXISTS (SELECT 1 FROM Eurovip.alojamiento a WHERE a.id_alojamiento = t.id_alojamiento AND a.id_idioma = 'ES')",
                    "t.nombre", "'hotel n.º ' + CAST(t.id_alojamiento AS varchar(12))"),
                SinLetras("nombre", "Nombre de la habitación"),
                Espacios("nombre", "Nombres de habitación")
            },
            ["campanyas"] = new[] { Repetido("campanya", "nombre", "Nombre de campaña"), SinLetras("nombre", "Nombre de la campaña"), Espacios("nombre", "Nombres de campaña") },
            ["monedas"] = new[] { Repetido("moneda", "nombre", "Nombre de moneda", idioma: false), SinLetras("nombre", "Nombre de la moneda"),
                new HrRevision("Código que no son 3 letras", "El código de moneda tiene que ser de 3 letras (ej. EUR).", "t.id_moneda NOT LIKE '[A-Z][A-Z][A-Z]'", "t.id_moneda", "t.nombre") },
            ["tasas"] = new[] { SinLetras("id_tasa", "Nombre de la tasa"), Espacios("id_tasa", "Nombres de tasa") },
            ["regimenes"] = new[] { Repetido("regimen", "descripcion", "Descripción de régimen"), SinLetras("descripcion", "Descripción del régimen"),
                new HrRevision("Código que no son 2 letras", "El código de régimen tiene que ser de 2 letras (ej. TI).", "t.id_regimen NOT LIKE '[A-Z][A-Z]'", "t.id_regimen", "t.descripcion") },
            ["idiomas"] = new[] { SinLetras("nombre", "Nombre del idioma") },
            ["proveedores"] = new[] { Repetido("proveedor", "nombre", "Nombre de proveedor", idioma: false), SinLetras("nombre", "Nombre del proveedor"), Espacios("nombre", "Nombres de proveedor") }
        };

        public static HrRevision[] De(HrCatalogo c) => Por.TryGetValue(c.Clave, out var r) ? r : Array.Empty<HrRevision>();

        /// <summary>Filtro de idioma para la tabla del catálogo (las que lo tienen).</summary>
        public static string Idioma(HrCatalogo c) => c.Idioma ? ES : "1 = 1";
    }
}
