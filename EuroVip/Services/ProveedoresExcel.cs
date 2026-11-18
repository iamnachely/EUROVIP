using Dapper;
using EuroVip.Models;
using EuroVip.Repositories;

namespace EuroVip.Services
{
    /// <summary>
    /// CAMBIO 140 · Fase 2 (pantallas del proveedor en Gestión): el camino «Subir Excel» de cada módulo, SIN guardar nada.
    /// Pedido de la usuaria: «MUCHO más fácil e intuitivo de cargar […] dos caminos: Llenar formulario y Subir Excel
    /// (descargar plantilla → subir → ver errores por fila y corregir en pantalla → guardar)».
    ///   • Excursiones y Traslados: una plantilla SENCILLA (solo lo que sabe el proveedor: sin proveedor, prioridad, precio del
    ///     proveedor ni «activo», que pone Eurovip) leída con lo común de las cargas masivas (CargaExcel: coincidencias por
    ///     parecido, columnas con otro nombre, errores por fila). Cada fila se convierte en el MISMO DTO del formulario y se
    ///     revisa con la validación del módulo (ProveedoresCarga.ValidarAsync). La pantalla carga las filas en el formulario
    ///     para corregirlas ahí.
    ///   • Solo Hotel / Paquetes: la plantilla de la Carga manual (matriz hotel × fecha) leída con ExcelOfertaParser.
    ///   • Circuitos: la plantilla de Carga de circuitos leída con ExcelCircuitoParser (se toman las salidas con su precio).
    ///   • Cruceros: sin Excel (en el BackEurovip tampoco existe).
    /// </summary>
    public class ProveedoresExcel
    {
        private readonly ICircuitoRepository _db;
        private readonly ProveedoresCarga _srv;
        private readonly IExcelOfertaParser _ofertas;
        private readonly IExcelCircuitoParser _circuitos;

        public ProveedoresExcel(ICircuitoRepository db, ProveedoresCarga srv, IExcelOfertaParser ofertas, IExcelCircuitoParser circuitos)
        {
            _db = db; _srv = srv; _ofertas = ofertas; _circuitos = circuitos;
        }

        public static bool TieneExcel(string modulo) => modulo != ProveedorModulos.Cruceros;

        // ============================================ COLUMNAS (Excursiones y Traslados) ============================================

        private static CargaExcelColumna Num(string n, string campo, params string[] alias) => new() { Nombre = n, Alias = alias, Campo = campo, Tipo = "numero" };

        private async Task<List<CargaExcelColumna>> ColumnasAsync(string modulo)
        {
            using var cn = _db.Conexion();
            var destinos = await CatalogoCargas.DestinosAsync(cn);
            var monedas = await CatalogoCargas.MonedasAsync(cn);
            var codigo = new CargaExcelColumna { Nombre = "Tu código", Alias = new[] { "Código interno", "Codigo interno", "Código", "Codigo" }, Campo = "Codigo" };
            if (modulo == ProveedorModulos.Excursiones)
                return new List<CargaExcelColumna>
                {
                    new() { Nombre = "Nombre", Alias = new[] { "Excursión", "Excursion" }, Campo = "Nombre", Obligatoria = true },
                    new() { Nombre = "Destino", Campo = "Id_Destino", Opciones = destinos, Obligatoria = true },
                    CargaExcel.ColumnaMoneda(monedas),
                    new() { Nombre = "Precio por persona", Alias = new[] { "Precio" }, Campo = "Precio", Tipo = "numero", Obligatoria = true },
                    CargaExcel.ColumnaSiNo("Cancelable"),
                    new() { Nombre = "Descripción", Alias = new[] { "Descripcion" }, Campo = "Descripcion", Tipo = "largo" },
                    codigo,
                }.Concat(CargaExcel.ColumnasInfo()).ToList();

            var aeropuertos = (await cn.QueryAsync<(string Codigo, string? Nombre, string? Destino)>(@"
                SELECT LTRIM(RTRIM(e.id_estacion)), MIN(e.nombre), MIN(LTRIM(RTRIM(e.id_destino))) FROM Eurovip.estacion e
                WHERE e.id_pais = 'DO' OR EXISTS (SELECT 1 FROM Eurovip.traslado t WHERE t.id_estacion_origen = e.id_estacion)
                GROUP BY e.id_estacion ORDER BY 1")).Select(a => new CargaExcelOpcion(a.Codigo, a.Nombre ?? a.Codigo, a.Destino)).ToList();
            return new List<CargaExcelColumna>
            {
                new() { Nombre = "Nombre", Alias = new[] { "Traslado" }, Campo = "Nombre", Obligatoria = true },
                new() { Nombre = "Aeropuerto", Alias = new[] { "Aeropuerto de salida", "Desde" }, Campo = "Id_Estacion_Origen", Opciones = aeropuertos, Obligatoria = true },
                new() { Nombre = "Destino", Alias = new[] { "Hasta" }, Campo = "Id_Destino", Opciones = destinos, Obligatoria = true },
                new() { Nombre = "Tipo", Campo = "Tipo", Obligatoria = true, Opciones = CargaExcel.DeDiccionario(TrasladoValores.Tipos) },
                new() { Nombre = "Vehículo", Alias = new[] { "Vehiculo" }, Campo = "Vehiculo", Opciones = CargaExcel.DeDiccionario(TrasladoValores.Vehiculos) },
                new() { Nombre = "Categoría", Alias = new[] { "Categoria" }, Campo = "Categoria", Opciones = CargaExcel.DeDiccionario(TrasladoValores.Categorias) },
                Num("Pasajeros mín", "Pasajeros_Min", "Pasajeros minimo"), Num("Pasajeros máx", "Pasajeros_Max", "Pasajeros maximo"),
                Num("Maletas", "Maletas"), Num("Duración (min)", "Duracion_Min", "Duración", "Minutos"),
                new() { Nombre = "Cobro", Campo = "Cobro", Opciones = CargaExcel.DeDiccionario(TrasladoValores.Cobros) },
                CargaExcel.ColumnaMoneda(monedas),
                Num("Precio ida y vuelta", "Precio_Ida_Vuelta"), Num("Precio solo ida", "Precio_Ida", "Precio ida"),
                CargaExcel.ColumnaSiNo("Cancelable"), Num("Horas para cancelar", "Horas_Cancelacion", "Horas cancelación", "Horas cancelacion"),
                new() { Nombre = "Descripción", Alias = new[] { "Descripcion" }, Campo = "Descripcion", Tipo = "largo" },
                codigo,
            }.Concat(CargaExcel.ColumnasInfo()).ToList();
        }

        // ============================================ PLANTILLAS ============================================

        /// <summary>El archivo de la plantilla del módulo y su nombre, o null si el módulo no tiene Excel.</summary>
        public async Task<(byte[] Archivo, string Nombre)?> PlantillaAsync(string modulo)
        {
            using var cn = _db.Conexion();
            switch (modulo)
            {
                case ProveedorModulos.Ofertas:
                case ProveedorModulos.Paquetes:
                    return (await Controllers.CargaManualController.PlantillaXlsxAsync(cn), $"Eurovip-plantilla-{(modulo == ProveedorModulos.Paquetes ? "paquetes" : "solo-hotel")}.xlsx");
                case ProveedorModulos.Circuitos:
                    return (await Controllers.CargaCircuitosController.PlantillaXlsxAsync(cn), "Eurovip-plantilla-circuitos.xlsx");
                case ProveedorModulos.Excursiones:
                case ProveedorModulos.Traslados:
                    break;
                default: return null;
            }
            var columnas = await ColumnasAsync(modulo);
            var nombres = columnas.Select(c => c.Nombre).ToArray();
            var destinos = (await cn.QueryAsync<(string Destino, string? Pais)>(@"SELECT d.id_destino, ISNULL(p.nombre, d.id_pais) FROM Eurovip.destino d
                LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES' WHERE d.id_idioma = 'ES' ORDER BY d.id_destino")).ToList();
            var ej = destinos.Select(d => d.Destino).Where(d => d is "Punta Cana" or "Samaná" or "La Romana").DefaultIfEmpty(destinos.FirstOrDefault().Destino ?? "Punta Cana").ToList();
            object?[] Fila(params object?[] v) => v.Concat(Enumerable.Repeat<object?>(null, Math.Max(0, nombres.Length - v.Length))).ToArray();
            var pc = ej.FirstOrDefault(d => d == "Punta Cana") ?? ej[0];   // los ejemplos de traslados salen de PUJ
            ExcelTabla.Hoja datos;
            List<object?[]> ayuda;
            if (modulo == ProveedorModulos.Excursiones)
            {
                datos = new ExcelTabla.Hoja("Excursiones", nombres, new List<object?[]>
                {
                    Fila("Isla Saona en catamarán", ej[0], "USD", 85m, "Sí", "Día completo: catamarán, almuerzo buffet, bebidas y piscina natural.", "SAONA1", "Transporte\nAlmuerzo buffet\nBebidas", "Propinas"),
                    Fila("Cascada El Limón a caballo", ej[^1], "DOP", 3500m, "No", "Paseo a caballo hasta la cascada El Limón con almuerzo típico."),
                }, new double[] { 40, 22, 10, 18, 12, 70, 14, 40, 30, 30, 30, 30, 30 }) { Listas = CargaExcel.Listas(columnas) };
                ayuda = new()
                {
                    new object?[] { "Nombre", "OBLIGATORIO. El nombre de la excursión, como quieres que se vea en la web." },
                    new object?[] { "Destino", "OBLIGATORIO. Dónde es (elige de la lista; abajo están todos)." },
                    new object?[] { "Moneda", "DOP, USD o EUR. Vacío = DOP." },
                    new object?[] { "Precio por persona", "OBLIGATORIO. Solo el número, sin símbolo (ej.: 85 o 3500)." },
                    new object?[] { "Cancelable", "Sí o No. Vacío = No." },
                    new object?[] { "Descripción", "Opcional. Qué incluye el día, horarios…" },
                    new object?[] { "Tu código", "Opcional. Tu código interno: solo letras y números." },
                };
            }
            else
            {
                datos = new ExcelTabla.Hoja("Traslados", nombres, new List<object?[]>
                {
                    Fila("Aeropuerto Punta Cana → hoteles de Bávaro", "PUJ", pc, "Compartido", "Autobús", "Estándar", 1, 50, 2, 40, "Por persona", "USD", 30m, 18m, "Sí", 24, "Traslado en autobús con aire acondicionado."),
                    Fila("Privado Punta Cana (hasta 4 personas)", "PUJ", pc, "Privado", "Van", "Premium", 1, 4, 4, 35, "Para todos los viajeros", "USD", 90m, 50m, "Sí", 48),
                }, new double[] { 40, 14, 22, 14, 12, 12, 13, 13, 10, 14, 22, 10, 18, 16, 12, 18, 60, 14 }) { Listas = CargaExcel.Listas(columnas) };
                ayuda = new()
                {
                    new object?[] { "Nombre", "OBLIGATORIO. Cómo se verá el traslado en la web." },
                    new object?[] { "Aeropuerto", "OBLIGATORIO. Código del aeropuerto de salida (PUJ, SDQ, LRM, POP…)." },
                    new object?[] { "Destino", "OBLIGATORIO. A dónde llega (elige de la lista; abajo están todos). Llega a todos los hoteles del destino." },
                    new object?[] { "Tipo", "OBLIGATORIO. Compartido o Privado." },
                    new object?[] { "Vehículo / Categoría", "Autobús, Minibús, Van, Coche o Limusina · Estándar, Premium o Lujo." },
                    new object?[] { "Pasajeros mín / máx", "Cuántas personas caben. Vacío = 1." },
                    new object?[] { "Cobro", "«Por persona» o «Para todos los viajeros» (precio del vehículo)." },
                    new object?[] { "Precios", "Al menos uno: ida y vuelta o solo ida. Solo el número." },
                    new object?[] { "Cancelable / Horas para cancelar", "Sí o No (vacío = Sí) y hasta cuántas horas antes se puede cancelar." },
                };
            }
            ayuda.Add(CargaExcel.AyudaInfo(2));
            ayuda.Add(new object?[] { "", "" });
            ayuda.Add(new object?[] { "Importante", "No cambies los títulos de la primera fila. Una fila = un producto. Al subirlo verás fila por fila si falta algo y lo podrás corregir en la pantalla." });
            ayuda.Add(new object?[] { "", "" });
            ayuda.Add(new object?[] { "Destinos válidos", "País" });
            ayuda.AddRange(destinos.Select(d => new object?[] { d.Destino, d.Pais }));
            return (ExcelTabla.Crear(datos, new ExcelTabla.Hoja("Instrucciones", new[] { "Columna", "Qué poner" }, ayuda, new double[] { 34, 100 })),
                    modulo == ProveedorModulos.Excursiones ? "Eurovip-plantilla-excursiones.xlsx" : "Eurovip-plantilla-traslados.xlsx");
        }

        // ============================================ LEER (sin guardar) ============================================

        /// <summary>
        /// Lee el Excel del proveedor y devuelve lo que la pantalla necesita para pasarlo al formulario. Nunca guarda nada.
        /// Excursiones/Traslados: { tipo: "filas", filas: [ fila, indice, nombre, detalle, ok, errores, avisos, dudas ], datos: [DTO…] }.
        /// Solo Hotel/Paquetes: { tipo: "matriz", fechas, hoteles: [ texto, estado, id_Alojamiento, nombre, precios… ], avisos }.
        /// Circuitos: { tipo: "salidas", salidas: [ fecha, precio ], avisos }.
        /// </summary>
        public async Task<(object? Resultado, string? Error)> LeerAsync(ProveedorCuenta cuenta, string modulo, Microsoft.AspNetCore.Http.IFormFile? archivo, string? mapa)
        {
            if (modulo is ProveedorModulos.Ofertas or ProveedorModulos.Paquetes or ProveedorModulos.Circuitos)
            {
                if (archivo is null || archivo.Length == 0) return (null, "Elige el archivo Excel.");
                if (archivo.Length > CargaExcel.MaxBytes) return (null, "El Excel pesa más de 10 MB.");
                if (!archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return (null, "El archivo debe ser .xlsx (Excel moderno). Ábrelo y guárdalo como .xlsx.");
                try
                {
                    using var ms = new MemoryStream();
                    await archivo.CopyToAsync(ms);
                    ms.Position = 0;
                    if (modulo == ProveedorModulos.Circuitos)
                    {
                        var c = await _circuitos.LeerAsync(ms);
                        var avisos = c.Avisos.ToList();
                        if (c.Aerolinea is not null || c.Tasa is not null) avisos.Add("El vuelo y las tasas del Excel no se cargan aquí: si el circuito lleva vuelo, escríbelo en las notas y Eurovip lo completa.");
                        return (new { tipo = "salidas", salidas = c.Fechas.OrderBy(f => f.Fecha).Select(f => new { fecha = f.Fecha.ToString("yyyy-MM-dd"), precio = f.Precio }), avisos }, null);
                    }
                    var o = await _ofertas.LeerAsync(ms);
                    return (new
                    {
                        tipo = "matriz",
                        fechas = o.Fechas.Select(f => f.ToString("yyyy-MM-dd")),
                        hoteles = o.Hoteles.Select(h => new
                        {
                            h.Texto, h.Estado, h.Id_Alojamiento, h.Nombre, h.Destino, h.Aproximado,
                            candidatos = h.Candidatos.Take(6).Select(x => new { valor = x.Valor, texto = x.Texto, extra = x.Extra }),
                            precios = h.Precios.Select(p => new { fecha = p.Fecha.ToString("yyyy-MM-dd"), precio = p.Precio })
                        }),
                        avisos = o.Avisos
                    }, null);
                }
                catch (Exception ex) { return (null, "No pude leer el Excel: " + ex.Message + ". Usa la plantilla."); }
            }
            if (modulo is not (ProveedorModulos.Excursiones or ProveedorModulos.Traslados)) return (null, "Este tipo de producto no tiene carga por Excel: usa el formulario.");

            var (filas, error) = await CargaExcel.LeerAsync(archivo);
            if (error is not null) return (null, error);
            var columnas = await ColumnasAsync(modulo);
            var plantilla = CargaExcel.Encabezados(filas!, columnas, mapa);
            var ayudaFilas = CargaExcel.Ayudar(filas!, columnas);
            var r = new CargaExcelResultado();
            var datos = new List<object>();
            short Corto(ExcelTabla.Fila f, short defecto, params string[] c) => f.Cifra(c) is decimal d ? (short)Math.Clamp(Math.Round(d), short.MinValue, short.MaxValue) : defecto;
            string? Codigo(ExcelTabla.Fila f) => f.Texto("Tu código", "Código interno", "Codigo interno", "Código", "Codigo");
            foreach (var f in filas!)
            {
                object dto;
                CargaExcelFila fila;
                if (modulo == ProveedorModulos.Excursiones)
                {
                    var e = new ExcursionGuardarDto
                    {
                        Nombre = f.Texto("Nombre", "Excursión", "Excursion") ?? "", Id_Destino = f.Texto("Destino") ?? "", Id_Moneda = f.Texto("Moneda", "Divisa") ?? "DOP",
                        Precio = f.Cifra("Precio por persona", "Precio"), Cancelable = f.SiNo(false, "Cancelable"), Descripcion = f.Texto("Descripción", "Descripcion"),
                        Codigo = Codigo(f), Info = CargaExcel.InfoDeFila(f)
                    };
                    dto = e;
                    fila = new CargaExcelFila { Fila = f.Numero, Nombre = e.Nombre, Detalle = $"{e.Id_Destino} · {(e.Precio is decimal p ? $"{e.Id_Moneda} {p:N2}" : "sin precio")}" };
                }
                else
                {
                    string Valor(Dictionary<string, string> d, string col, string defecto) => CargaExcel.Opcion(d, f.Texto(col)) ?? f.Texto(col) ?? defecto;
                    var t = new TrasladoGuardarDto
                    {
                        Nombre = f.Texto("Nombre", "Traslado") ?? "", Id_Estacion_Origen = f.Texto("Aeropuerto", "Aeropuerto de salida", "Desde") ?? "", Id_Destino = f.Texto("Destino", "Hasta") ?? "",
                        Tipo = Valor(TrasladoValores.Tipos, "Tipo", ""), Vehiculo = Valor(TrasladoValores.Vehiculos, "Vehículo", "BUS"),
                        Categoria = Valor(TrasladoValores.Categorias, "Categoría", "ESTANDAR"), Cobro = Valor(TrasladoValores.Cobros, "Cobro", "VEHICULO"),
                        Pasajeros_Min = Corto(f, 1, "Pasajeros mín", "Pasajeros minimo"), Pasajeros_Max = Corto(f, 1, "Pasajeros máx", "Pasajeros maximo"),
                        Maletas = Corto(f, 2, "Maletas"), Duracion_Min = f.Cifra("Duración (min)", "Duración", "Minutos") is decimal du ? (short)Math.Round(du) : null,
                        Id_Moneda = f.Texto("Moneda", "Divisa") ?? "DOP", Precio_Ida_Vuelta = f.Cifra("Precio ida y vuelta"), Precio_Ida = f.Cifra("Precio solo ida", "Precio ida"),
                        Cancelable = f.SiNo(true, "Cancelable"), Horas_Cancelacion = f.Cifra("Horas para cancelar", "Horas cancelación", "Horas cancelacion") is decimal h ? (short)Math.Round(h) : null,
                        Descripcion = f.Texto("Descripción", "Descripcion"), Codigo = Codigo(f), Todos_Hoteles = true, Info = CargaExcel.InfoDeFila(f)
                    };
                    dto = t;
                    fila = new CargaExcelFila { Fila = f.Numero, Nombre = t.Nombre, Detalle = $"{t.Id_Estacion_Origen} → {t.Id_Destino} · {f.Texto("Tipo")}" };
                }
                ProveedoresCarga.Forzar(modulo, dto, cuenta.Id_Proveedor, "EXCEL");
                datos.Add(dto);
                r.Filas.Add(fila);
            }

            // Las MISMAS reglas que al enviar a revisión (se revisa una copia: los datos que vuelven son los del Excel)
            var errores = await _srv.ValidarAsync(modulo, datos, "EXCEL");
            for (var i = 0; i < r.Filas.Count; i++)
            {
                var fila = r.Filas[i];
                var mios = errores.Where(e => e.Indice == i + 1).ToList();
                foreach (var e in mios)
                {
                    var col = columnas.FirstOrDefault(c => string.Equals(c.Campo, e.Campo, StringComparison.OrdinalIgnoreCase))?.Nombre;
                    if (!fila.Errores.Any(x => x.Mensaje == e.Mensaje)) fila.Errores.Add(new CargaExcelError(col, e.Mensaje));
                }
                fila.Mensaje = fila.Errores.FirstOrDefault()?.Mensaje;
                fila.Columna = fila.Errores.FirstOrDefault()?.Columna;
                fila.Ok = fila.Errores.Count == 0;
            }
            CargaExcel.Completar(r, filas!, columnas, ayudaFilas, sinListas: true, plantilla);
            // Completar marca como error las dudas y las obligatorias vacías: la fila con dudas no está bien todavía
            foreach (var fila in r.Filas) fila.Ok = fila.Ok && fila.Errores.Count == 0 && fila.Dudas.Count == 0;
            return (new
            {
                tipo = "filas",
                filas = r.Filas.Select((x, i) => new { x.Fila, indice = i + 1, x.Nombre, x.Detalle, x.Ok, x.Errores, x.Avisos, x.Dudas }),
                datos,
                plantilla = r.Plantilla,
                correctas = r.Filas.Count(x => x.Ok),
                conError = r.Filas.Count(x => !x.Ok),
            }, null);
        }
    }
}
