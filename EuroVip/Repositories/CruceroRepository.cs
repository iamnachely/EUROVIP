using System.Data;
using System.Globalization;
using Dapper;
using EuroVip.Models;

namespace EuroVip.Repositories
{
    public interface ICruceroRepository
    {
        Task<List<CruceroLista>> ListarAsync();
        Task<CruceroDto?> ObtenerAsync(int id);
        /// <summary>Crea (Id_Crucero null) o reemplaza el crucero con todo lo suyo; devuelve el número y las categorías que hay que refrescar en la Home.</summary>
        Task<(int Id, List<int> Categorias)> GuardarAsync(CruceroDto d, string usuario);
        Task<List<int>?> EliminarAsync(int id);
        Task<List<int>?> ActivarAsync(int id, bool activo);
        Task<int?> RepetidoAsync(string titulo, int? idBarco, int? excepto);
        Task ActualizarInicioAsync(IEnumerable<int> categorias);

        Task<List<CruceroNaviera>> NavierasAsync();
        Task<List<CruceroBarco>> BarcosAsync();
        Task<List<CruceroCategoria>> CategoriasAsync();
        Task<int> GuardarNavieraAsync(CruceroNaviera n);
        Task<int> GuardarBarcoAsync(CruceroBarco b);
        Task<int> GuardarCategoriaAsync(CruceroCategoria c);
        Task<string?> EliminarNavieraAsync(int id);
        Task<string?> EliminarBarcoAsync(int id);
        Task<string?> EliminarCategoriaAsync(int id);

        // PDF de cada crucero (FASE 30)
        Task<List<CruceroPdf>> PdfsAsync(int idCrucero);
        Task<int> AgregarPdfAsync(int idCrucero, string titulo, string ruta, int bytes, string usuario);
        /// <summary>Quita la fila y la devuelve (con la ruta del archivo); null si no existía.</summary>
        Task<CruceroPdf?> QuitarPdfAsync(int idPdf);
        Task<bool> TituloPdfAsync(int idPdf, string titulo);
    }

    /// <summary>
    /// Cruceros (Eurovip.crucero y sus tablas). El editor guarda el crucero entero: se reemplazan las filas hijas
    /// (itinerario, a bordo, cubiertas, cabinas, salidas, tarifas y precios) dentro de una transacción.
    /// La Home tiene una tarjeta 'CRUCERO' por categoría (spActualizaInicioCrucero).
    /// </summary>
    public class CruceroRepository : ICruceroRepository
    {
        private readonly ICircuitoRepository _db;
        public CruceroRepository(ICircuitoRepository db) => _db = db;

        public async Task<List<CruceroLista>> ListarAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<CruceroLista>(@"
                SELECT c.id_crucero AS Id_Crucero, c.titulo AS Titulo, c.codigo AS Codigo, c.id_barco AS Id_Barco, b.nombre AS Barco,
                       n.id_naviera AS Id_Naviera, n.nombre AS Naviera, n.logo AS Naviera_Logo, c.noches AS Noches,
                       c.puerto_salida AS Puerto_Salida, c.puerto_llegada AS Puerto_Llegada, c.activo AS Activo, c.peso AS Peso, c.precio_proveedor AS Precio_Proveedor,
                       (SELECT COUNT(*) FROM Eurovip.crucero_pdf p WHERE p.id_crucero = c.id_crucero) AS Pdfs,
                       (SELECT STRING_AGG(CAST(i.puerto AS nvarchar(max)), N' / ') WITHIN GROUP (ORDER BY i.dia)
                          FROM Eurovip.crucero_itinerario i WHERE i.id_crucero = c.id_crucero AND i.en_navegacion = 0) AS Itinerario,
                       (SELECT COUNT(*) FROM Eurovip.crucero_salida s WHERE s.id_crucero = c.id_crucero) AS Salidas,
                       ISNULL(d.salidas, 0) AS Salidas_Futuras, d.proxima_salida AS Proxima_Salida,
                       d.desde_pp AS Desde_Pp, d.interior_pp AS Interior_Pp, d.exterior_pp AS Exterior_Pp, d.balcon_pp AS Balcon_Pp, d.suite_pp AS Suite_Pp,
                       (SELECT STRING_AGG(CAST(g.nombre AS nvarchar(max)), N', ') FROM Eurovip.crucero_categoria_crucero cc
                          JOIN Eurovip.crucero_categoria g ON g.id_categoria = cc.id_categoria WHERE cc.id_crucero = c.id_crucero) AS Categorias,
                       (SELECT STRING_AGG(CAST(cc.id_categoria AS varchar(20)), ',') FROM Eurovip.crucero_categoria_crucero cc WHERE cc.id_crucero = c.id_crucero) AS Ids_Categoria,
                       (SELECT COUNT(*) FROM Eurovip.crucero_cabina_tipo t WHERE t.id_crucero = c.id_crucero) AS Cabinas,
                       (SELECT COUNT(*) FROM Eurovip.crucero_actividad a JOIN Eurovip.crucero_seccion s ON s.id_seccion = a.id_seccion WHERE s.id_crucero = c.id_crucero) AS Actividades,
                       c.usuario AS Usuario, c.fecha_alta AS Fecha_Alta, c.fecha_modificacion AS Fecha_Modificacion
                FROM Eurovip.crucero c
                LEFT JOIN Eurovip.crucero_barco b ON b.id_barco = c.id_barco
                LEFT JOIN Eurovip.crucero_naviera n ON n.id_naviera = b.id_naviera
                LEFT JOIN Eurovip.vCruceroDesde d ON d.id_crucero = c.id_crucero
                ORDER BY ISNULL(d.proxima_salida, '99991231'), c.titulo")).ToList();
        }

        private static string? Hora(TimeSpan? t) => t?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        private sealed class TipoFila : CruceroCabinaTipoDto { public int Id { get; set; } }

        public async Task<CruceroDto?> ObtenerAsync(int id)
        {
            using var cn = _db.Conexion();
            var c = await cn.QueryFirstOrDefaultAsync<CruceroDto>(@"
                SELECT c.id_crucero AS Id_Crucero, c.titulo AS Titulo, c.subtitulo AS Subtitulo, c.id_barco AS Id_Barco, b.nombre AS Barco_Nombre, n.nombre AS Naviera_Nombre,
                       c.puerto_salida AS Puerto_Salida, c.puerto_llegada AS Puerto_Llegada, c.noches AS Noches, c.codigo AS Codigo, c.descripcion AS Descripcion,
                       c.incluye AS Incluye, c.no_incluye AS No_Incluye, c.titulo_seccion AS Titulo_Seccion, c.activo AS Activo, c.peso AS Peso, c.precio_proveedor AS Precio_Proveedor, c.usuario AS Usuario,
                       c.fecha_alta AS Fecha_Alta, c.fecha_modificacion AS Fecha_Modificacion
                FROM Eurovip.crucero c
                LEFT JOIN Eurovip.crucero_barco b ON b.id_barco = c.id_barco
                LEFT JOIN Eurovip.crucero_naviera n ON n.id_naviera = b.id_naviera
                WHERE c.id_crucero = @id", new { id });
            if (c is null) return null;
            c.Categorias = (await cn.QueryAsync<int>("SELECT id_categoria FROM Eurovip.crucero_categoria_crucero WHERE id_crucero = @id", new { id })).ToList();
            c.Itinerario = (await cn.QueryAsync<(short Dia, string Puerto, bool Nav, TimeSpan? Lleg, TimeSpan? Sal, string? Desc, string? Img)>(
                "SELECT dia, puerto, en_navegacion, hora_llegada, hora_salida, descripcion, imagen FROM Eurovip.crucero_itinerario WHERE id_crucero = @id ORDER BY dia", new { id }))
                .Select(x => new CruceroDiaDto { Dia = x.Dia, Puerto = x.Puerto, En_Navegacion = x.Nav, Hora_Llegada = Hora(x.Lleg), Hora_Salida = Hora(x.Sal), Descripcion = x.Desc, Imagen = x.Img }).ToList();
            var secciones = (await cn.QueryAsync<(int Id, string Titulo)>("SELECT id_seccion, titulo FROM Eurovip.crucero_seccion WHERE id_crucero = @id ORDER BY orden, id_seccion", new { id })).ToList();
            var actividades = (await cn.QueryAsync<(int Sec, string Titulo, string? Desc, string? Img)>(@"
                SELECT a.id_seccion, a.titulo, a.descripcion, a.imagen FROM Eurovip.crucero_actividad a
                JOIN Eurovip.crucero_seccion s ON s.id_seccion = a.id_seccion WHERE s.id_crucero = @id ORDER BY a.orden, a.id_actividad", new { id })).ToLookup(a => a.Sec);
            c.Secciones = secciones.Select(s => new CruceroSeccionDto
            {
                Titulo = s.Titulo,
                Actividades = actividades[s.Id].Select(a => new CruceroActividadDto { Titulo = a.Titulo, Descripcion = a.Desc, Imagen = a.Img }).ToList()
            }).ToList();
            c.Paradas = (await cn.QueryAsync<CruceroParadaDto>(
                "SELECT nombre AS Nombre, tipo AS Tipo, dia AS Dia, latitud AS Latitud, longitud AS Longitud, descripcion AS Descripcion FROM Eurovip.crucero_parada WHERE id_crucero = @id ORDER BY orden", new { id })).ToList();
            c.Cubiertas = (await cn.QueryAsync<CruceroCubiertaDto>("SELECT numero AS Numero, nombre AS Nombre, plano AS Plano FROM Eurovip.crucero_cubierta WHERE id_crucero = @id ORDER BY numero", new { id })).ToList();
            var tipos = (await cn.QueryAsync<TipoFila>(@"
                SELECT id_cabina_tipo AS Id, categoria AS Categoria, codigo AS Codigo, nombre AS Nombre, descripcion AS Descripcion,
                       capacidad_min AS Capacidad_Min, capacidad_max AS Capacidad_Max, metros AS Metros, imagen AS Imagen, permite_garantizada AS Permite_Garantizada
                FROM Eurovip.crucero_cabina_tipo WHERE id_crucero = @id
                ORDER BY CASE categoria WHEN 'INTERIOR' THEN 1 WHEN 'EXTERIOR' THEN 2 WHEN 'BALCON' THEN 3 ELSE 4 END, orden, id_cabina_tipo", new { id }))
                .Select(f => (f.Id, T: (CruceroCabinaTipoDto)f)).ToList();
            var cabinas = (await cn.QueryAsync<(int Tipo, string Numero, short? Cubierta, bool Disp)>(@"
                SELECT k.id_cabina_tipo, k.numero, u.numero, k.disponible FROM Eurovip.crucero_cabina k
                JOIN Eurovip.crucero_cabina_tipo t ON t.id_cabina_tipo = k.id_cabina_tipo
                LEFT JOIN Eurovip.crucero_cubierta u ON u.id_cubierta = k.id_cubierta
                WHERE t.id_crucero = @id ORDER BY k.numero", new { id })).ToLookup(k => k.Tipo);
            foreach (var (tid, t) in tipos)
            {
                t.Clave = "t" + tid;
                t.Cabinas = cabinas[tid].Select(k => new CruceroCabinaDto { Numero = k.Numero, Cubierta = k.Cubierta, Disponible = k.Disp }).ToList();
            }
            c.Cabinas = tipos.Select(x => x.T).ToList();
            c.Salidas = (await cn.QueryAsync<(int Id, DateTime F, bool A)>("SELECT id_salida, fecha_salida, activo FROM Eurovip.crucero_salida WHERE id_crucero = @id ORDER BY fecha_salida", new { id }))
                .Select(s => new CruceroSalidaDto { Clave = "s" + s.Id, Fecha = s.F, Activo = s.A }).ToList();
            c.Tarifas = (await cn.QueryAsync<(int Id, string Cod, string Nom, string? Desc)>("SELECT id_tarifa, codigo, nombre, descripcion FROM Eurovip.crucero_tarifa WHERE id_crucero = @id ORDER BY orden, id_tarifa", new { id }))
                .Select(t => new CruceroTarifaDto { Clave = "f" + t.Id, Codigo = t.Cod, Nombre = t.Nom, Descripcion = t.Desc }).ToList();
            c.Precios = (await cn.QueryAsync<(int S, int F, int T, decimal P)>(@"
                SELECT p.id_salida, p.id_tarifa, p.id_cabina_tipo, p.precio_pp FROM Eurovip.crucero_precio p
                JOIN Eurovip.crucero_salida s ON s.id_salida = p.id_salida WHERE s.id_crucero = @id", new { id }))
                .Select(p => new CruceroPrecioDto { Salida = "s" + p.S, Tarifa = "f" + p.F, Cabina = "t" + p.T, Precio_Pp = p.P }).ToList();
            return c;
        }

        private static TimeSpan? HoraDe(string? h) =>
            TimeSpan.TryParseExact((h ?? "").Trim(), [@"hh\:mm", @"h\:mm"], CultureInfo.InvariantCulture, out var t) ? t : null;
        private static string? Nulo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        public async Task<(int Id, List<int> Categorias)> GuardarAsync(CruceroDto d, string usuario)
        {
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            // Naviera, barco y categorías escritos a mano: se buscan por nombre y, si no están, se crean
            var idBarco = d.Id_Barco;
            if (d.Barco_Nombre is string barco)
            {
                int? idNav = d.Naviera_Nombre is string nav ? await BuscarOCrearAsync(cn, tx, "crucero_naviera", "id_naviera", nav, null) : null;
                idBarco = await cn.ExecuteScalarAsync<int?>(
                    "SELECT TOP 1 id_barco FROM Eurovip.crucero_barco WHERE nombre = @barco AND (@idNav IS NULL OR id_naviera = @idNav) ORDER BY id_barco", new { barco, idNav }, tx);
                if (idBarco is null)
                {
                    idNav ??= await BuscarOCrearAsync(cn, tx, "crucero_naviera", "id_naviera", "Otra naviera", null);
                    idBarco = await BuscarOCrearAsync(cn, tx, "crucero_barco", "id_barco", barco, idNav);
                }
            }
            foreach (var nueva in d.Categorias_Nuevas)
                d.Categorias.Add(await BuscarOCrearAsync(cn, tx, "crucero_categoria", "id_categoria", nueva, null));
            // Sin categoría no hay tarjeta en la Home: va a «Cruceros» (se crea si no existe)
            if (d.Categorias.Count == 0)
                d.Categorias.Add(await BuscarOCrearAsync(cn, tx, "crucero_categoria", "id_categoria", "Cruceros", null));

            var p = new
            {
                id = d.Id_Crucero, Titulo = d.Titulo.Trim(), Subtitulo = Nulo(d.Subtitulo), Id_Barco = idBarco, Puerto_Salida = Nulo(d.Puerto_Salida), Puerto_Llegada = Nulo(d.Puerto_Llegada),
                d.Noches, Codigo = Nulo(d.Codigo), Descripcion = Nulo(d.Descripcion), Incluye = Nulo(d.Incluye), No_Incluye = Nulo(d.No_Incluye),
                Titulo_Seccion = Nulo(d.Titulo_Seccion), d.Activo, usuario, d.Peso, d.Precio_Proveedor
            };
            int id;
            var antes = new List<int>();
            if (d.Id_Crucero is int existente)
            {
                antes = (await cn.QueryAsync<int>("SELECT id_categoria FROM Eurovip.crucero_categoria_crucero WHERE id_crucero = @existente", new { existente }, tx)).ToList();
                var n = await cn.ExecuteAsync(@"
                    UPDATE Eurovip.crucero SET titulo = @Titulo, subtitulo = @Subtitulo, id_barco = @Id_Barco, puerto_salida = @Puerto_Salida,
                           puerto_llegada = @Puerto_Llegada, noches = @Noches, codigo = @Codigo, descripcion = @Descripcion, incluye = @Incluye,
                           no_incluye = @No_Incluye, titulo_seccion = @Titulo_Seccion, activo = @Activo, peso = @Peso, precio_proveedor = @Precio_Proveedor,
                           usuario = @usuario, fecha_modificacion = SYSDATETIME()
                    WHERE id_crucero = @id", p, tx);
                if (n == 0) throw new InvalidOperationException("Ese crucero ya no existe. Recarga la página.");
                id = existente;
                // Se reemplazan las partes (en orden por las claves foráneas)
                await cn.ExecuteAsync(@"
                    DELETE p FROM Eurovip.crucero_precio p JOIN Eurovip.crucero_salida s ON s.id_salida = p.id_salida WHERE s.id_crucero = @id;
                    DELETE FROM Eurovip.crucero_salida WHERE id_crucero = @id;
                    DELETE FROM Eurovip.crucero_tarifa WHERE id_crucero = @id;
                    DELETE k FROM Eurovip.crucero_cabina k JOIN Eurovip.crucero_cabina_tipo t ON t.id_cabina_tipo = k.id_cabina_tipo WHERE t.id_crucero = @id;
                    DELETE FROM Eurovip.crucero_cabina_tipo WHERE id_crucero = @id;
                    DELETE FROM Eurovip.crucero_cubierta WHERE id_crucero = @id;
                    DELETE a FROM Eurovip.crucero_actividad a JOIN Eurovip.crucero_seccion s ON s.id_seccion = a.id_seccion WHERE s.id_crucero = @id;
                    DELETE FROM Eurovip.crucero_seccion WHERE id_crucero = @id;
                    DELETE FROM Eurovip.crucero_itinerario WHERE id_crucero = @id;
                    DELETE FROM Eurovip.crucero_parada WHERE id_crucero = @id;
                    DELETE FROM Eurovip.crucero_categoria_crucero WHERE id_crucero = @id;", new { id }, tx);
            }
            else
            {
                id = await cn.ExecuteScalarAsync<int>(@"
                    INSERT INTO Eurovip.crucero (titulo, subtitulo, id_barco, puerto_salida, puerto_llegada, noches, codigo, descripcion, incluye, no_incluye,
                                                 titulo_seccion, activo, usuario, peso, precio_proveedor)
                    VALUES (@Titulo, @Subtitulo, @Id_Barco, @Puerto_Salida, @Puerto_Llegada, @Noches, @Codigo, @Descripcion, @Incluye, @No_Incluye,
                            @Titulo_Seccion, @Activo, @usuario, @Peso, @Precio_Proveedor);
                    SELECT CAST(SCOPE_IDENTITY() AS int);", p, tx);
            }

            foreach (var g in d.Categorias.Distinct())
                await cn.ExecuteAsync("INSERT INTO Eurovip.crucero_categoria_crucero (id_categoria, id_crucero) VALUES (@g, @id)", new { g, id }, tx);

            foreach (var x in d.Itinerario)
                await cn.ExecuteAsync(@"INSERT INTO Eurovip.crucero_itinerario (id_crucero, dia, puerto, en_navegacion, hora_llegada, hora_salida, descripcion, imagen)
                                        VALUES (@id, @Dia, @Puerto, @Nav, @Lleg, @Sal, @Desc, @Img)",
                    new { id, x.Dia, Puerto = (x.Puerto ?? "").Trim(), Nav = x.En_Navegacion, Lleg = x.En_Navegacion ? null : HoraDe(x.Hora_Llegada),
                          Sal = x.En_Navegacion ? null : HoraDe(x.Hora_Salida), Desc = Nulo(x.Descripcion), Img = Nulo(x.Imagen) }, tx);

            short ordenParada = 0;
            foreach (var x in d.Paradas)
                await cn.ExecuteAsync(@"INSERT INTO Eurovip.crucero_parada (id_crucero, orden, nombre, tipo, dia, latitud, longitud, descripcion)
                                        VALUES (@id, @o, @Nombre, @Tipo, @Dia, @Latitud, @Longitud, @Descripcion)",
                    new { id, o = ++ordenParada, Nombre = (x.Nombre ?? "").Trim(), x.Tipo, x.Dia, x.Latitud, x.Longitud, Descripcion = Nulo(x.Descripcion) }, tx);

            short ordenSec = 0;
            foreach (var s in d.Secciones)
            {
                var sid = await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.crucero_seccion (id_crucero, titulo, orden) VALUES (@id, @t, @o); SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new { id, t = (s.Titulo ?? "A bordo").Trim(), o = ++ordenSec }, tx);
                short ordenAct = 0;
                foreach (var a in s.Actividades)
                    await cn.ExecuteAsync("INSERT INTO Eurovip.crucero_actividad (id_seccion, titulo, descripcion, imagen, orden) VALUES (@sid, @t, @dsc, @img, @o)",
                        new { sid, t = (a.Titulo ?? "Actividad").Trim(), dsc = Nulo(a.Descripcion), img = Nulo(a.Imagen), o = ++ordenAct }, tx);
            }

            var cubiertas = new Dictionary<short, int>();
            foreach (var u in d.Cubiertas)
                cubiertas[u.Numero] = await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.crucero_cubierta (id_crucero, numero, nombre, plano) VALUES (@id, @n, @nom, @pl); SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new { id, n = u.Numero, nom = Nulo(u.Nombre), pl = Nulo(u.Plano) }, tx);

            var tipos = new Dictionary<string, int>();
            short ordenTipo = 0;
            foreach (var t in d.Cabinas)
            {
                var tid = await cn.ExecuteScalarAsync<int>(@"
                    INSERT INTO Eurovip.crucero_cabina_tipo (id_crucero, categoria, codigo, nombre, descripcion, capacidad_min, capacidad_max, metros, imagen, permite_garantizada, orden)
                    VALUES (@id, @Categoria, @Codigo, @Nombre, @Descripcion, @Capacidad_Min, @Capacidad_Max, @Metros, @Imagen, @Permite_Garantizada, @o);
                    SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new { id, t.Categoria, Codigo = Nulo(t.Codigo), Nombre = (t.Nombre ?? "Cabina").Trim(), Descripcion = Nulo(t.Descripcion), t.Capacidad_Min, t.Capacidad_Max, t.Metros,
                          Imagen = Nulo(t.Imagen), t.Permite_Garantizada, o = ++ordenTipo }, tx);
                tipos[t.Clave] = tid;
                foreach (var k in t.Cabinas)
                    await cn.ExecuteAsync("INSERT INTO Eurovip.crucero_cabina (id_cabina_tipo, id_cubierta, numero, disponible) VALUES (@tid, @cub, @num, @disp)",
                        new { tid, cub = k.Cubierta is short cn2 && cubiertas.TryGetValue(cn2, out var cu) ? cu : (int?)null, num = (k.Numero ?? "").Trim(), disp = k.Disponible }, tx);
            }

            var salidas = new Dictionary<string, int>();
            foreach (var s in d.Salidas)
                salidas[s.Clave] = await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.crucero_salida (id_crucero, fecha_salida, activo) VALUES (@id, @f, @a); SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new { id, f = s.Fecha.Date, a = s.Activo }, tx);

            var tarifas = new Dictionary<string, int>();
            short ordenTar = 0;
            foreach (var f in d.Tarifas)
                tarifas[f.Clave] = await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.crucero_tarifa (id_crucero, codigo, nombre, descripcion, orden) VALUES (@id, @c, @n, @dsc, @o); SELECT CAST(SCOPE_IDENTITY() AS int);",
                    new { id, c = (f.Codigo ?? "BEST_PRICE").Trim(), n = (f.Nombre ?? f.Codigo ?? "BEST_PRICE").Trim(), dsc = Nulo(f.Descripcion), o = ++ordenTar }, tx);

            foreach (var pr in d.Precios.Where(x => x.Precio_Pp > 0))
                if (salidas.TryGetValue(pr.Salida, out var sa) && tarifas.TryGetValue(pr.Tarifa, out var ta) && tipos.TryGetValue(pr.Cabina, out var ti))
                    await cn.ExecuteAsync("INSERT INTO Eurovip.crucero_precio (id_salida, id_tarifa, id_cabina_tipo, precio_pp) VALUES (@sa, @ta, @ti, @p)",
                        new { sa, ta, ti, p = pr.Precio_Pp }, tx);

            tx.Commit();
            return (id, antes.Union(d.Categorias).Distinct().ToList());
        }

        /// <summary>Busca por nombre en un catálogo (naviera, barco o categoría) y lo crea si no está. Devuelve su número.</summary>
        private static async Task<int> BuscarOCrearAsync(IDbConnection cn, IDbTransaction tx, string tabla, string columnaId, string nombre, int? idNaviera)
        {
            var filtroNav = tabla == "crucero_barco" ? " AND id_naviera = @idNaviera" : "";
            var id = await cn.ExecuteScalarAsync<int?>($"SELECT TOP 1 {columnaId} FROM Eurovip.{tabla} WHERE nombre = @nombre{filtroNav} ORDER BY {columnaId}", new { nombre, idNaviera }, tx);
            if (id is int ya) return ya;
            var insert = tabla switch
            {
                "crucero_barco" => "INSERT INTO Eurovip.crucero_barco (id_naviera, nombre, activo) VALUES (@idNaviera, @nombre, 1)",
                "crucero_categoria" => "INSERT INTO Eurovip.crucero_categoria (nombre, orden, activo) VALUES (@nombre, ISNULL((SELECT MAX(orden) FROM Eurovip.crucero_categoria), 0) + 1, 1)",
                _ => "INSERT INTO Eurovip.crucero_naviera (nombre, activo) VALUES (@nombre, 1)"
            };
            return await cn.ExecuteScalarAsync<int>(insert + "; SELECT CAST(SCOPE_IDENTITY() AS int);", new { nombre, idNaviera }, tx);
        }

        public async Task<List<int>?> EliminarAsync(int id)
        {
            using var cn = _db.Conexion();
            var cats = (await cn.QueryAsync<int>("SELECT id_categoria FROM Eurovip.crucero_categoria_crucero WHERE id_crucero = @id", new { id })).ToList();
            // ON DELETE CASCADE borra itinerario, a bordo, cubiertas, cabinas, salidas, tarifas y precios
            await cn.ExecuteAsync(@"DELETE p FROM Eurovip.crucero_precio p JOIN Eurovip.crucero_salida s ON s.id_salida = p.id_salida WHERE s.id_crucero = @id;
                                    DELETE k FROM Eurovip.crucero_cabina k JOIN Eurovip.crucero_cabina_tipo t ON t.id_cabina_tipo = k.id_cabina_tipo WHERE t.id_crucero = @id;", new { id });
            return await cn.ExecuteAsync("DELETE FROM Eurovip.crucero WHERE id_crucero = @id", new { id }) > 0 ? cats : null;
        }

        // ---------------- PDF de cada crucero (FASE 30) ----------------
        public async Task<List<CruceroPdf>> PdfsAsync(int idCrucero)
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<CruceroPdf>(@"SELECT id_pdf AS Id_Pdf, id_crucero AS Id_Crucero, titulo AS Titulo, ruta AS Ruta, bytes AS Bytes, orden AS Orden,
                       usuario AS Usuario, fecha_alta AS Fecha_Alta FROM Eurovip.crucero_pdf WHERE id_crucero = @idCrucero ORDER BY orden, id_pdf", new { idCrucero })).ToList();
        }

        public async Task<int> AgregarPdfAsync(int idCrucero, string titulo, string ruta, int bytes, string usuario)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO Eurovip.crucero_pdf (id_crucero, titulo, ruta, bytes, orden, usuario)
                VALUES (@idCrucero, @titulo, @ruta, @bytes, (SELECT ISNULL(MAX(orden), 0) + 1 FROM Eurovip.crucero_pdf WHERE id_crucero = @idCrucero), @usuario);
                SELECT CAST(SCOPE_IDENTITY() AS int);", new { idCrucero, titulo, ruta, bytes, usuario });
        }

        public async Task<CruceroPdf?> QuitarPdfAsync(int idPdf)
        {
            using var cn = _db.Conexion();
            return await cn.QueryFirstOrDefaultAsync<CruceroPdf>(@"DELETE FROM Eurovip.crucero_pdf
                OUTPUT deleted.id_pdf AS Id_Pdf, deleted.id_crucero AS Id_Crucero, deleted.titulo AS Titulo, deleted.ruta AS Ruta, deleted.bytes AS Bytes
                WHERE id_pdf = @idPdf", new { idPdf });
        }

        public async Task<bool> TituloPdfAsync(int idPdf, string titulo)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteAsync("UPDATE Eurovip.crucero_pdf SET titulo = @titulo WHERE id_pdf = @idPdf", new { idPdf, titulo }) > 0;
        }

        public async Task<List<int>?> ActivarAsync(int id, bool activo)
        {
            using var cn = _db.Conexion();
            var n = await cn.ExecuteAsync("UPDATE Eurovip.crucero SET activo = @activo, fecha_modificacion = SYSDATETIME() WHERE id_crucero = @id", new { id, activo });
            return n == 0 ? null : (await cn.QueryAsync<int>("SELECT id_categoria FROM Eurovip.crucero_categoria_crucero WHERE id_crucero = @id", new { id })).ToList();
        }

        public async Task<int?> RepetidoAsync(string titulo, int? idBarco, int? excepto)
        {
            using var cn = _db.Conexion();
            return await cn.ExecuteScalarAsync<int?>(@"SELECT TOP 1 id_crucero FROM Eurovip.crucero
                WHERE titulo = @titulo AND (id_barco = @idBarco OR (@idBarco IS NULL AND id_barco IS NULL)) AND (@excepto IS NULL OR id_crucero <> @excepto)",
                new { titulo = titulo.Trim(), idBarco, excepto });
        }

        /// <summary>Tarjetas CRUCERO de la Home (una por categoría): se crean, actualizan o borran según los cruceros activos con precio.</summary>
        public async Task ActualizarInicioAsync(IEnumerable<int> categorias)
        {
            using var cn = _db.Conexion();
            foreach (var g in categorias.Distinct())
                await cn.ExecuteAsync("Eurovip.spActualizaInicioCrucero", new { idCategoria = g }, commandType: CommandType.StoredProcedure);
        }

        // ---------------- catálogos ----------------
        public async Task<List<CruceroNaviera>> NavierasAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<CruceroNaviera>(@"SELECT n.id_naviera AS Id_Naviera, n.nombre AS Nombre, n.logo AS Logo, n.activo AS Activo,
                       (SELECT COUNT(*) FROM Eurovip.crucero_barco b WHERE b.id_naviera = n.id_naviera) AS Barcos
                FROM Eurovip.crucero_naviera n ORDER BY n.nombre")).ToList();
        }

        public async Task<List<CruceroBarco>> BarcosAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<CruceroBarco>(@"SELECT b.id_barco AS Id_Barco, b.id_naviera AS Id_Naviera, n.nombre AS Naviera, b.nombre AS Nombre,
                       b.descripcion AS Descripcion, b.imagen AS Imagen, b.activo AS Activo,
                       (SELECT COUNT(*) FROM Eurovip.crucero c WHERE c.id_barco = b.id_barco) AS Cruceros
                FROM Eurovip.crucero_barco b JOIN Eurovip.crucero_naviera n ON n.id_naviera = b.id_naviera ORDER BY n.nombre, b.nombre")).ToList();
        }

        public async Task<List<CruceroCategoria>> CategoriasAsync()
        {
            using var cn = _db.Conexion();
            return (await cn.QueryAsync<CruceroCategoria>(@"SELECT g.id_categoria AS Id_Categoria, g.nombre AS Nombre, g.descripcion AS Descripcion, g.imagen AS Imagen,
                       g.orden AS Orden, g.activo AS Activo,
                       (SELECT COUNT(*) FROM Eurovip.crucero_categoria_crucero cc WHERE cc.id_categoria = g.id_categoria) AS Cruceros
                FROM Eurovip.crucero_categoria g ORDER BY g.orden, g.nombre")).ToList();
        }

        public async Task<int> GuardarNavieraAsync(CruceroNaviera n)
        {
            using var cn = _db.Conexion();
            var p = new { id = n.Id_Naviera, nombre = n.Nombre.Trim(), logo = Nulo(n.Logo), n.Activo };
            if (n.Id_Naviera > 0)
            {
                if (await cn.ExecuteAsync("UPDATE Eurovip.crucero_naviera SET nombre = @nombre, logo = @logo, activo = @Activo WHERE id_naviera = @id", p) == 0)
                    throw new InvalidOperationException("Esa naviera ya no existe.");
                return n.Id_Naviera;
            }
            return await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.crucero_naviera (nombre, logo, activo) VALUES (@nombre, @logo, @Activo); SELECT CAST(SCOPE_IDENTITY() AS int);", p);
        }

        public async Task<int> GuardarBarcoAsync(CruceroBarco b)
        {
            using var cn = _db.Conexion();
            var p = new { id = b.Id_Barco, b.Id_Naviera, nombre = b.Nombre.Trim(), descripcion = Nulo(b.Descripcion), imagen = Nulo(b.Imagen), b.Activo };
            if (b.Id_Barco > 0)
            {
                if (await cn.ExecuteAsync("UPDATE Eurovip.crucero_barco SET id_naviera = @Id_Naviera, nombre = @nombre, descripcion = @descripcion, imagen = @imagen, activo = @Activo WHERE id_barco = @id", p) == 0)
                    throw new InvalidOperationException("Ese barco ya no existe.");
                return b.Id_Barco;
            }
            return await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.crucero_barco (id_naviera, nombre, descripcion, imagen, activo) VALUES (@Id_Naviera, @nombre, @descripcion, @imagen, @Activo); SELECT CAST(SCOPE_IDENTITY() AS int);", p);
        }

        public async Task<int> GuardarCategoriaAsync(CruceroCategoria c)
        {
            using var cn = _db.Conexion();
            var p = new { id = c.Id_Categoria, nombre = c.Nombre.Trim(), descripcion = Nulo(c.Descripcion), imagen = Nulo(c.Imagen), c.Orden, c.Activo };
            if (c.Id_Categoria > 0)
            {
                if (await cn.ExecuteAsync("UPDATE Eurovip.crucero_categoria SET nombre = @nombre, descripcion = @descripcion, imagen = @imagen, orden = @Orden, activo = @Activo WHERE id_categoria = @id", p) == 0)
                    throw new InvalidOperationException("Esa categoría ya no existe.");
                return c.Id_Categoria;
            }
            return await cn.ExecuteScalarAsync<int>("INSERT INTO Eurovip.crucero_categoria (nombre, descripcion, imagen, orden, activo) VALUES (@nombre, @descripcion, @imagen, @Orden, @Activo); SELECT CAST(SCOPE_IDENTITY() AS int);", p);
        }

        public async Task<string?> EliminarNavieraAsync(int id)
        {
            using var cn = _db.Conexion();
            var n = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.crucero_barco WHERE id_naviera = @id", new { id });
            if (n > 0) return $"Tiene {n} barco(s): elimínalos o cámbialos de naviera primero.";
            return await cn.ExecuteAsync("DELETE FROM Eurovip.crucero_naviera WHERE id_naviera = @id", new { id }) > 0 ? null : "Esa naviera ya no existe.";
        }

        public async Task<string?> EliminarBarcoAsync(int id)
        {
            using var cn = _db.Conexion();
            var n = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.crucero WHERE id_barco = @id", new { id });
            if (n > 0) return $"Lo usan {n} crucero(s): cámbialos de barco o elimínalos primero.";
            return await cn.ExecuteAsync("DELETE FROM Eurovip.crucero_barco WHERE id_barco = @id", new { id }) > 0 ? null : "Ese barco ya no existe.";
        }

        public async Task<string?> EliminarCategoriaAsync(int id)
        {
            using var cn = _db.Conexion();
            // Los cruceros NO se borran: solo dejan de estar en esa categoría (ON DELETE CASCADE de la tabla intermedia)
            return await cn.ExecuteAsync("DELETE FROM Eurovip.crucero_categoria WHERE id_categoria = @id", new { id }) > 0 ? null : "Esa categoría ya no existe.";
        }
    }
}
