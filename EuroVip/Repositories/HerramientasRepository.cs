using Dapper;
using EuroVip.Models;
using EuroVip.Services;
using System.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace EuroVip.Repositories
{
    public interface IHerramientasRepository
    {
        Task<List<HrResumen>> ResumenAsync();
        Task<(List<Dictionary<string, object?>> Filas, int Total)> ListarAsync(HrCatalogo c, string? q, Dictionary<string, string> filtros, int pagina, int tam);
        Task<Dictionary<string, object?>?> ObtenerAsync(HrCatalogo c, Dictionary<string, JsonElement> clave);
        Task<(Dictionary<string, object?>? Clave, string? Error)> GuardarAsync(HrCatalogo c, Dictionary<string, JsonElement> datos, bool nuevo);
        Task<List<(string Etiqueta, int N)>> UsosAsync(HrCatalogo c, Dictionary<string, JsonElement> clave);
        Task<string?> EliminarAsync(HrCatalogo c, Dictionary<string, JsonElement> clave);
        Task<IEnumerable<Opcion>> ListaAsync(string nombre, string? q);
        Task<List<HrResultadoBusqueda>> BuscarGlobalAsync(string q);
        Task<List<HrActividad>> ActividadAsync(int n);
        Task<List<HrRevisionResultado>> RevisarAsync(HrCatalogo c);
        Task<Dictionary<string, int>> RevisarResumenAsync();
    }

    /// <summary>
    /// Motor ÚNICO de los mantenimientos básicos sobre [Eurovip] (nunca dbo). Arma el SQL solo con
    /// nombres de tabla/columna de <see cref="HrCatalogos"/>; todos los valores van como parámetros.
    /// Alojamientos y habitaciones se guardan con los SP copiados de ViajeVDT (CAMBIO 34 y 35).
    /// </summary>
    public class HerramientasRepository : IHerramientasRepository
    {
        private const string Idioma = "ES";
        private readonly ICircuitoRepository _db;
        public HerramientasRepository(ICircuitoRepository db) => _db = db;

        private static string Col(string c) => c.Contains('(') ? c : $"t.[{c}]";
        private static string Tabla(HrCatalogo c) => $"Eurovip.[{c.Tabla}]";

        public async Task<List<HrResumen>> ResumenAsync()
        {
            using var cn = _db.Conexion();
            var sql = string.Join(" UNION ALL ", HrCatalogos.Todos.Select(c =>
                $"SELECT '{c.Clave}' AS Clave, COUNT(*) AS Total FROM {Tabla(c)} t{(c.Idioma ? " WHERE t.id_idioma = 'ES'" : "")}"));
            return (await cn.QueryAsync<HrResumen>(sql)).ToList();
        }

        private static string SelectCols(HrCatalogo c) =>
            string.Join(", ", c.Campos.Select(f => $"t.[{f.Col}] AS [{f.Col}]")
                .Concat((c.Extras ?? Array.Empty<HrExtra>()).Select(x => $"{x.Sql} AS [{x.Alias}]")));

        private static (string Where, DynamicParameters P) Filtrar(HrCatalogo c, string? q, Dictionary<string, string> filtros)
        {
            var w = new List<string>();
            var p = new DynamicParameters();
            if (c.Idioma) w.Add("t.id_idioma = 'ES'");
            if (!string.IsNullOrWhiteSpace(q))
            {
                w.Add("(" + string.Join(" OR ", c.Buscar.Select(b => $"{Col(b)} LIKE @q")) + ")");
                p.Add("q", $"%{q.Trim()}%");
            }
            var i = 0;
            foreach (var (col, valor) in filtros)
            {
                if (string.IsNullOrWhiteSpace(valor) || c.Filtros?.Contains(col, StringComparer.OrdinalIgnoreCase) != true) continue;
                var f = c.Campo(col)!;
                w.Add($"t.[{f.Col}] = @f{i}");
                p.Add($"f{i++}", Convertir(f, valor));
            }
            return (w.Count == 0 ? "" : "WHERE " + string.Join(" AND ", w), p);
        }

        public async Task<(List<Dictionary<string, object?>> Filas, int Total)> ListarAsync(HrCatalogo c, string? q, Dictionary<string, string> filtros, int pagina, int tam)
        {
            tam = Math.Clamp(tam, 10, 100);
            pagina = Math.Max(1, pagina);
            var (where, p) = Filtrar(c, q, filtros);
            p.Add("salto", (pagina - 1) * tam);
            p.Add("tam", tam);
            using var cn = _db.Conexion();
            var total = await cn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {Tabla(c)} t {where}", p);
            var filas = (await cn.QueryAsync($@"SELECT {SelectCols(c)} FROM {Tabla(c)} t {where}
                ORDER BY t.[{c.Orden}] OFFSET @salto ROWS FETCH NEXT @tam ROWS ONLY", p))
                .Select(Fila).ToList();
            return (filas, total);
        }

        private static (string Where, DynamicParameters P) PorClave(HrCatalogo c, Dictionary<string, JsonElement> clave)
        {
            var p = new DynamicParameters();
            var w = new List<string>();
            foreach (var k in c.Claves)
            {
                if (!clave.TryGetValue(k.Col, out var v)) throw new ArgumentException($"Falta {k.Etiqueta}");
                w.Add($"t.[{k.Col}] = @{k.Col}");
                p.Add(k.Col, Convertir(k, Texto(v)));
            }
            if (c.Idioma) w.Add("t.id_idioma = 'ES'");
            return (string.Join(" AND ", w), p);
        }

        public async Task<Dictionary<string, object?>?> ObtenerAsync(HrCatalogo c, Dictionary<string, JsonElement> clave)
        {
            var (where, p) = PorClave(c, clave);
            using var cn = _db.Conexion();
            var r = await cn.QueryFirstOrDefaultAsync($"SELECT {SelectCols(c)} FROM {Tabla(c)} t WHERE {where}", p);
            return r is null ? null : Fila(r);
        }

        public async Task<List<(string Etiqueta, int N)>> UsosAsync(HrCatalogo c, Dictionary<string, JsonElement> clave)
        {
            var (_, p) = PorClave(c, clave);
            using var cn = _db.Conexion();
            var res = new List<(string, int)>();
            foreach (var u in c.Usos)
            {
                var n = await cn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Eurovip.[{u.Tabla}] WHERE {u.Where}", p);
                if (n > 0) res.Add((u.Etiqueta, n));
            }
            return res;
        }

        // ---------------------------------------------------------------- GUARDAR
        public async Task<(Dictionary<string, object?>? Clave, string? Error)> GuardarAsync(HrCatalogo c, Dictionary<string, JsonElement> datos, bool nuevo)
        {
            // 1) normalizar y validar cada campo
            var v = new Dictionary<string, object?>();
            foreach (var f in c.Campos)
            {
                if (f.Auto && nuevo) continue;
                datos.TryGetValue(f.Col, out var je);
                var s = Texto(je)?.Trim();
                if (f.Tipo is "text" or "destino" && s is not null) s = ReglasDatos.Limpiar(s);   // sin espacios dobles
                if (string.IsNullOrEmpty(s) && f.Defecto is not null && nuevo) s = f.Defecto;
                if (f.Mayus) s = s?.ToUpperInvariant();
                if (f.Tipo == "bool") { v[f.Col] = s is "1" or "true" or "True"; continue; }
                if (string.IsNullOrEmpty(s))
                {
                    if (f.Req && !(f.Tipo is "text" && f.Defecto == "")) return (null, $"Falta «{f.Etiqueta}».");
                    v[f.Col] = f.Tipo is "text" && f.Defecto == "" ? "" : null;
                    continue;
                }
                if (f.Max > 0 && s.Length > f.Max) return (null, $"«{f.Etiqueta}» admite como máximo {f.Max} caracteres.");
                if ((f.Tipo == "url" || f.Tipo == "imagen") && !s.StartsWith("http://") && !s.StartsWith("https://") && !s.StartsWith("/")) return (null, $"«{f.Etiqueta}» debe ser una URL (https://…) o una ruta que empiece por /");
                try { v[f.Col] = Convertir(f, s); }
                catch { return (null, $"«{f.Etiqueta}» no es un número válido."); }
            }

            using var cn = _db.Conexion();
            await cn.OpenAsync();

            // 2) reglas para no cargar datos mal creados (nombres basura, repetidos, país que no concuerda…)
            var regla = await ValidarReglasAsync(cn, c, v, nuevo);
            if (regla is not null) return (null, regla);

            // 3) referencias que deben existir
            foreach (var f in c.Campos.Where(f => v.GetValueOrDefault(f.Col) is not null))
            {
                var val = v[f.Col];
                if (f.Tipo == "destino")
                {
                    // Se guarda escrito igual que en Destinos ("Riviera Maya", no "RIVIERA MAYA"); si está en otro país, se avisa.
                    var pais = v.GetValueOrDefault("id_pais");
                    var exacto = await cn.ExecuteScalarAsync<string?>("SELECT TOP 1 id_destino FROM Eurovip.destino WHERE id_destino = @val AND id_pais = @pais AND id_idioma = 'ES'", new { val, pais });
                    if (exacto is not null) { v[f.Col] = exacto; continue; }
                    var otro = await cn.QueryFirstOrDefaultAsync<(string Destino, string Pais)>(@"SELECT TOP 1 d.id_destino, ISNULL(p.nombre, d.id_pais) FROM Eurovip.destino d
                        LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES' WHERE d.id_destino = @val AND d.id_idioma = 'ES'", new { val });
                    return (null, otro.Destino is not null
                        ? $"El destino «{otro.Destino}» es de {otro.Pais}, no del país elegido. El país y el destino tienen que concordar."
                        : $"El destino «{val}» no existe. Créalo primero en Herramientas › Destinos (o elige uno de la lista).");
                }
                var existe = f.Tipo switch
                {
                    "hotel" => await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.alojamiento WHERE id_alojamiento = @val AND id_idioma = 'ES'", new { val }),
                    "select" when f.Lista is not null && f.Lista != "continentes" => (await ListaAsync(f.Lista, null)).Any(o => o.Valor.Equals(val?.ToString(), StringComparison.OrdinalIgnoreCase)) ? 1 : 0,
                    _ => 1
                };
                if (existe == 0) return (null, f.Tipo == "destino" ? $"El destino «{val}» no existe en ese país." : $"«{val}» no es un valor válido para {f.Etiqueta}.");
            }

            // 4) especiales: alojamiento y habitación con los SP copiados de ViajeVDT
            if (c.Especial == "alojamiento")
            {
                var p = new DynamicParameters();
                p.Add("in_id_alojamiento", nuevo ? null : v.GetValueOrDefault("id_alojamiento"));
                p.Add("id_idioma", Idioma);
                foreach (var col in new[] { "id_tipo_alojamiento", "id_destino", "id_pais", "nombre", "caratula", "descripcion", "grupo", "categoria", "latitude", "longitude", "categoria_circuito", "circuito", "activo" })
                    p.Add(col, v.GetValueOrDefault(col));
                p.Add("id_alojamiento", dbType: DbType.Int32, direction: ParameterDirection.Output);
                await cn.ExecuteAsync("Eurovip.InsertarOActualizarAlojamiento", p, commandType: CommandType.StoredProcedure);
                return (new() { ["id_alojamiento"] = p.Get<int>("id_alojamiento") }, null);
            }
            if (c.Especial == "habitacion")
            {
                var id = await cn.ExecuteScalarAsync<int>("Eurovip.InsertarAlojamientoHabitacion", new
                {
                    id_alojamiento = v["id_alojamiento"], id_idioma = Idioma, id_tipo_habitacion = v["id_tipo_habitacion"], codigo = v["codigo"] ?? "",
                    nombre = v["nombre"], grupo = v.GetValueOrDefault("grupo") ?? 0, nota = v.GetValueOrDefault("nota"), precio = v.GetValueOrDefault("precio") ?? 0m,
                    codigo_newblue = v.GetValueOrDefault("codigo_newblue"), nombre_newblue = v.GetValueOrDefault("nombre_newblue"),
                    id_habitacion = nuevo ? null : v.GetValueOrDefault("id_habitacion")
                }, commandType: CommandType.StoredProcedure);
                return (new() { ["id_alojamiento"] = v["id_alojamiento"], ["id_habitacion"] = id }, null);
            }

            // 5) genérico
            using var tx = cn.BeginTransaction();
            var prm = new DynamicParameters();
            foreach (var (k, val) in v) prm.Add(k, val);
            var auto = c.Campos.FirstOrDefault(f => f.Auto);
            if (nuevo)
            {
                if (auto is not null)
                    prm.Add(auto.Col, await cn.ExecuteScalarAsync<int>($"SELECT ISNULL(MAX([{auto.Col}]), 0) + 1 FROM {Tabla(c)} WITH (UPDLOCK, HOLDLOCK)", transaction: tx));
                var whereClave = string.Join(" AND ", c.Claves.Select(k => $"[{k.Col}] = @{k.Col}")) + (c.Idioma ? " AND id_idioma = 'ES'" : "");
                if (await cn.ExecuteScalarAsync<int>($"SELECT COUNT(1) FROM {Tabla(c)} WITH (UPDLOCK, HOLDLOCK) WHERE {whereClave}", prm, tx) > 0)
                    return (null, $"Ya existe {c.Ese} {c.Singular} ({string.Join(" · ", c.Claves.Select(k => prm.Get<object>(k.Col)))}).");
                var cols = c.Campos.Select(f => f.Col).ToList();
                if (c.Idioma) { cols.Add("id_idioma"); prm.Add("id_idioma", Idioma); }
                await cn.ExecuteAsync($"INSERT INTO {Tabla(c)} ({string.Join(", ", cols.Select(x => $"[{x}]"))}) VALUES ({string.Join(", ", cols.Select(x => "@" + x))})", prm, tx);
            }
            else
            {
                var sets = c.Campos.Where(f => !f.Clave).Select(f => $"[{f.Col}] = @{f.Col}").ToList();
                if (sets.Count > 0)
                {
                    var n = await cn.ExecuteAsync($"UPDATE {Tabla(c)} SET {string.Join(", ", sets)} WHERE " +
                        string.Join(" AND ", c.Claves.Select(k => $"[{k.Col}] = @{k.Col}")) + (c.Idioma ? " AND id_idioma = 'ES'" : ""), prm, tx);
                    if (n == 0) return (null, $"No encontramos {c.Ese} {c.Singular}.");
                }
            }
            tx.Commit();
            return (c.Claves.ToDictionary(k => k.Col, k => (object?)prm.Get<object>(k.Col)), null);
        }

        // ---------------------------------------------------------------- REGLAS
        /// <summary>
        /// Lo que no se deja cargar en cada mantenimiento. Mismas reglas que muestra "Datos a revisar" (<see cref="HrRevisiones"/>).
        /// Devuelve el mensaje para el usuario o null si está bien.
        /// </summary>
        private static async Task<string?> ValidarReglasAsync(SqlConnection cn, HrCatalogo c, Dictionary<string, object?> v, bool nuevo)
        {
            string? S(string col) => v.GetValueOrDefault(col)?.ToString();
            // ¿Ya hay otro registro con ese valor? (al editar, se excluye el propio)
            async Task<bool> Repetido(string col, string valor)
            {
                var p = new DynamicParameters();
                p.Add("valor", valor);
                var w = $"[{col}] = @valor" + (c.Idioma ? " AND id_idioma = 'ES'" : "");
                if (!nuevo)
                {
                    w += " AND NOT (" + string.Join(" AND ", c.Claves.Select(k => $"[{k.Col}] = @k_{k.Col}")) + ")";
                    foreach (var k in c.Claves) p.Add("k_" + k.Col, v.GetValueOrDefault(k.Col));
                }
                return await cn.ExecuteScalarAsync<int>($"SELECT COUNT(1) FROM Eurovip.[{c.Tabla}] WHERE {w}", p) > 0;
            }

            switch (c.Clave)
            {
                case "paises":
                {
                    var e = ReglasDatos.Codigo("código de país", S("id_pais") ?? "", 2, 2) ?? ReglasDatos.Lugar("país", S("nombre") ?? "");
                    if (e is not null) return e;
                    if (await Repetido("nombre", S("nombre")!)) return $"Ya existe el país «{S("nombre")}» con otro código. No se puede repetir.";
                    break;
                }
                case "destinos":
                {
                    if (!nuevo) break;   // el nombre y el país son la clave: no se cambian al editar
                    var e = ReglasDatos.Lugar("destino", S("id_destino") ?? "");
                    if (e is not null) return e;
                    var otro = await cn.ExecuteScalarAsync<string?>(@"SELECT TOP 1 ISNULL(p.nombre, d.id_pais) FROM Eurovip.destino d
                        LEFT JOIN Eurovip.pais p ON p.id_pais = d.id_pais AND p.id_idioma = 'ES'
                        WHERE d.id_destino = @n AND d.id_idioma = 'ES' AND d.id_pais <> @p", new { n = S("id_destino"), p = S("id_pais") });
                    if (otro is not null) return $"Ya existe el destino «{S("id_destino")}» en {otro}. Un destino va en un solo país: si es otra ciudad, escríbelo distinto (ej. «{S("id_destino")} ({S("id_pais")})»).";
                    break;
                }
                case "estaciones":
                    // Solo aeropuertos: IATA de 3 letras y nombre real (no "Sin Especificar", ni el código, ni estaciones de tren).
                    return ReglasDatos.Estacion(S("id_estacion") ?? "", S("nombre") ?? "");
                case "alojamientos":
                {
                    var e = ReglasDatos.Nombre("nombre de hotel", S("nombre") ?? "", 3);
                    if (e is not null) return e;
                    var dup = await cn.ExecuteScalarAsync<int?>(@"SELECT TOP 1 id_alojamiento FROM Eurovip.alojamiento
                        WHERE id_idioma = 'ES' AND nombre = @n AND id_destino = @d AND (@id IS NULL OR id_alojamiento <> @id)",
                        new { n = S("nombre"), d = S("id_destino"), id = nuevo ? null : v.GetValueOrDefault("id_alojamiento") });
                    if (dup is not null) return $"Ya existe el hotel «{S("nombre")}» en {S("id_destino")} (n.º {dup}). No se puede repetir: edita ese.";
                    break;
                }
                case "habitaciones":
                {
                    var e = ReglasDatos.Nombre("nombre de habitación", S("nombre") ?? "");
                    if (e is not null) return e;
                    var dup = await cn.ExecuteScalarAsync<int?>(@"SELECT TOP 1 id_habitacion FROM Eurovip.alojamiento_habitacion
                        WHERE id_idioma = 'ES' AND id_alojamiento = @h AND nombre = @n AND (@id IS NULL OR id_habitacion <> @id)",
                        new { h = v.GetValueOrDefault("id_alojamiento"), n = S("nombre"), id = nuevo ? null : v.GetValueOrDefault("id_habitacion") });
                    if (dup is not null) return $"Este hotel ya tiene la habitación «{S("nombre")}» (n.º {dup}). No se puede repetir.";
                    break;
                }
                case "campanyas":
                {
                    var e = ReglasDatos.Nombre("nombre de campaña", S("nombre") ?? "", 3);
                    if (e is not null) return e;
                    if (await Repetido("nombre", S("nombre")!)) return $"Ya existe una campaña llamada «{S("nombre")}».";
                    break;
                }
                case "monedas":
                {
                    var e = ReglasDatos.Codigo("código de moneda", S("id_moneda") ?? "", 3, 3) ?? ReglasDatos.Nombre("nombre de moneda", S("nombre") ?? "", 3);
                    if (e is not null) return e;
                    if (await Repetido("nombre", S("nombre")!)) return $"Ya existe una moneda llamada «{S("nombre")}».";
                    break;
                }
                case "tasas":
                    return nuevo ? ReglasDatos.Nombre("nombre de tasa", S("id_tasa") ?? "", 3) : null;
                case "regimenes":
                {
                    var e = ReglasDatos.Codigo("código de régimen", S("id_regimen") ?? "", 2, 2) ?? ReglasDatos.Nombre("descripción de régimen", S("descripcion") ?? "", 3);
                    if (e is not null) return e;
                    if (await Repetido("descripcion", S("descripcion")!)) return $"Ya existe un régimen «{S("descripcion")}».";
                    break;
                }
                case "idiomas":
                    return ReglasDatos.Codigo("código de idioma", S("id_idioma") ?? "", 2, 2) ?? ReglasDatos.Nombre("nombre de idioma", S("nombre") ?? "", 3);
                case "proveedores":
                {
                    var e = ReglasDatos.Codigo("código de proveedor", S("id_proveedor") ?? "", 2, 10, conNumeros: true) ?? ReglasDatos.Nombre("nombre de proveedor", S("nombre") ?? "", 2);
                    if (e is not null) return e;
                    if (await Repetido("nombre", S("nombre")!)) return $"Ya existe un proveedor llamado «{S("nombre")}».";
                    break;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- DATOS A REVISAR
        /// <summary>Datos inconsistentes ya guardados en este catálogo, agrupados por motivo (hasta 200 filas por motivo).</summary>
        public async Task<List<HrRevisionResultado>> RevisarAsync(HrCatalogo c)
        {
            var claves = string.Join(", ", c.Claves.Select(k => $"t.[{k.Col}] AS [{k.Col}]"));
            using var cn = _db.Conexion();
            var res = new List<HrRevisionResultado>();
            foreach (var r in HrRevisiones.De(c))
            {
                var where = $"{HrRevisiones.Idioma(c)} AND ({r.Where})";
                var total = await cn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {Tabla(c)} t WHERE {where}");
                if (total == 0) continue;
                var filas = (await cn.QueryAsync($@"SELECT TOP 200 {claves}, {r.Valor} AS valor, {r.Detalle ?? "NULL"} AS detalle
                    FROM {Tabla(c)} t WHERE {where} ORDER BY 1, 2")).Select(Fila).ToList();
                res.Add(new HrRevisionResultado { Motivo = r.Motivo, Ayuda = r.Ayuda, Total = total, Filas = filas });
            }
            return res;
        }

        /// <summary>Cuántos datos a revisar tiene cada catálogo (para el tablero de Herramientas).</summary>
        public async Task<Dictionary<string, int>> RevisarResumenAsync()
        {
            using var cn = _db.Conexion();
            var res = new Dictionary<string, int>();
            foreach (var c in HrCatalogos.Todos)
            {
                var revs = HrRevisiones.De(c);
                if (revs.Length == 0) continue;
                var n = await cn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {Tabla(c)} t WHERE {HrRevisiones.Idioma(c)} AND (" +
                                                         string.Join(" OR ", revs.Select(r => $"({r.Where})")) + ")");
                if (n > 0) res[c.Clave] = n;
            }
            return res;
        }

        // ---------------------------------------------------------------- ELIMINAR
        public async Task<string?> EliminarAsync(HrCatalogo c, Dictionary<string, JsonElement> clave)
        {
            var usos = await UsosAsync(c, clave);
            if (usos.Count > 0)
                return $"No se puede eliminar: se usa en {string.Join(", ", usos.Select(u => $"{u.N:N0} {u.Etiqueta}"))}." +
                       (c.Especial == "alojamiento" ? " Puedes desactivarlo para que no se use más." : "");
            var (where, p) = PorClave(c, clave);
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            if (c.Especial == "alojamiento")   // sus habitaciones se van con él
                await cn.ExecuteAsync("DELETE FROM Eurovip.alojamiento_habitacion WHERE id_alojamiento = @id_alojamiento AND id_idioma = 'ES'", p, tx);
            var n = await cn.ExecuteAsync($"DELETE t FROM {Tabla(c)} t WHERE {where}", p, tx);
            tx.Commit();
            return n == 0 ? $"No encontramos {c.Ese} {c.Singular}." : null;
        }

        // ---------------------------------------------------------------- LISTAS
        public async Task<IEnumerable<Opcion>> ListaAsync(string nombre, string? q)
        {
            using var cn = _db.Conexion();
            var sql = nombre switch
            {
                "paises" => "SELECT id_pais AS Valor, nombre AS Texto, nombre_continente AS Extra FROM Eurovip.pais WHERE id_idioma = 'ES' ORDER BY nombre",
                "continentes" => "SELECT DISTINCT nombre_continente AS Valor, nombre_continente AS Texto FROM Eurovip.pais WHERE id_idioma = 'ES' AND nombre_continente IS NOT NULL ORDER BY 1",
                "tiposEstacion" => "SELECT id_tipo_estacion AS Valor, id_tipo_estacion AS Texto FROM Eurovip.tipo_estacion ORDER BY 1",
                "tiposProveedor" => "SELECT id_tipo_proveedor AS Valor, id_tipo_proveedor AS Texto FROM Eurovip.tipo_proveedor ORDER BY 1",
                "tiposAlojamiento" => "SELECT id_tipo_alojamiento AS Valor, descripcion AS Texto FROM Eurovip.tipo_alojamiento ORDER BY 2",
                "tiposServicio" => "SELECT id_tipo_servicio AS Valor, id_tipo_servicio + ' · ' + descripcion AS Texto FROM Eurovip.tipo_servicio ORDER BY 1",
                "categorias" => "SELECT categoria AS Valor, categoria AS Texto FROM Eurovip.categoria ORDER BY 1",
                "tiposHabitacion" => "SELECT TOP 40 id_tipo_habitacion AS Valor, id_tipo_habitacion AS Texto FROM Eurovip.tipo_habitacion WHERE id_idioma = 'ES' AND (@q IS NULL OR id_tipo_habitacion LIKE '%' + @q + '%') ORDER BY CASE WHEN id_tipo_habitacion LIKE @q + '%' THEN 0 ELSE 1 END, 1",
                _ => throw new ArgumentException("Lista desconocida")
            };
            return await cn.QueryAsync<Opcion>(sql, new { q = string.IsNullOrWhiteSpace(q) ? null : q.Trim() });
        }

        public async Task<List<HrResultadoBusqueda>> BuscarGlobalAsync(string q)
        {
            // Todos los catálogos a la vez (una conexión cada uno) y consulta ligera: 6 filas, sin columnas calculadas ni conteo.
            var tareas = HrCatalogos.Todos.Select(async c =>
            {
                var (where, p) = Filtrar(c, q, new());
                using var cn = _db.Conexion();
                var filas = (await cn.QueryAsync($@"SELECT TOP 6 {string.Join(", ", c.Campos.Select(f => $"t.[{f.Col}] AS [{f.Col}]"))}
                    FROM {Tabla(c)} t {where} ORDER BY t.[{c.Orden}]", p)).Select(Fila).ToList();
                return filas.Count == 0 ? null : new HrResultadoBusqueda
                {
                    Clave = c.Clave, Icono = c.Icono, Titulo = filas.Count > 5 ? $"{c.Titulo} (más de 5)" : $"{c.Titulo} ({filas.Count})",
                    Filas = filas.Take(5).ToList()
                };
            });
            return (await Task.WhenAll(tareas)).Where(r => r is not null).ToList()!;
        }

        /// <summary>Últimos cambios hechos desde Herramientas (auditoría Herramienta_*).</summary>
        public async Task<List<HrActividad>> ActividadAsync(int n)
        {
            using var cn = _db.Conexion();
            var filas = await cn.QueryAsync<(string Tipo, string Usuario, DateTime Fecha, string? Cambios)>(@"
                SELECT TOP (@n) tipo_mantenimiento, usuario, fecha_hora, cambios_realizados FROM Eurovip.auditoria_consulta_oferta
                WHERE tipo_mantenimiento LIKE 'Herramienta[_]%' ORDER BY id DESC", new { n });
            return filas.Select(f =>
            {
                var partes = f.Tipo.Split('_');
                var cat = HrCatalogos.Buscar(partes.ElementAtOrDefault(1));
                return new HrActividad
                {
                    Clave = cat?.Clave ?? "", Catalogo = cat?.Titulo ?? partes.ElementAtOrDefault(1) ?? "", Icono = cat?.Icono ?? "bi-tools",
                    Accion = partes.ElementAtOrDefault(2) ?? "", Usuario = f.Usuario, Fecha = f.Fecha, Detalle = Detalle(f.Cambios)
                };
            }).ToList();
        }

        /// <summary>Nombre legible del registro tocado (nombre / descripción / clave) a partir del JSON de auditoría.</summary>
        private static string? Detalle(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var d = JsonDocument.Parse(json);
                foreach (var raiz in new[] { "datos", "antes", "clave" })
                {
                    if (!d.RootElement.TryGetProperty(raiz, out var o) || o.ValueKind != JsonValueKind.Object) continue;
                    foreach (var k in new[] { "nombre", "descripcion", "id_destino", "id_tasa", "id_estacion", "id_moneda", "id_idioma", "id_regimen", "id_pais", "id_alojamiento" })
                        if (o.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number) return v.ToString();
                }
            }
            catch (JsonException) { }
            return null;
        }

        // ---------------------------------------------------------------- helpers
        private static Dictionary<string, object?> Fila(dynamic r) =>
            ((IDictionary<string, object>)r).ToDictionary(k => k.Key, k => (object?)k.Value);

        private static string? Texto(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            _ => null
        };

        private static object? Convertir(HrCampo f, string? s)
        {
            if (s is null) return null;
            return f.Tipo switch
            {
                "number" or "hotel" => int.Parse(s, CultureInfo.InvariantCulture),
                "decimal" => decimal.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture),
                "bool" => s is "1" or "true" or "True",
                _ => s
            };
        }
    }
}
