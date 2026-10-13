using Dapper;
using EuroVip.Models;
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
                if (f.Tipo == "url" && !s.StartsWith("http://") && !s.StartsWith("https://") && !s.StartsWith("/")) return (null, $"«{f.Etiqueta}» debe ser una URL (https://…) o una ruta que empiece por /");
                try { v[f.Col] = Convertir(f, s); }
                catch { return (null, $"«{f.Etiqueta}» no es un número válido."); }
            }

            using var cn = _db.Conexion();
            await cn.OpenAsync();

            // 2) referencias que deben existir
            foreach (var f in c.Campos.Where(f => v.GetValueOrDefault(f.Col) is not null))
            {
                var val = v[f.Col];
                var existe = f.Tipo switch
                {
                    "destino" => await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.destino WHERE id_destino = @val AND id_pais = @pais AND id_idioma = 'ES'", new { val, pais = v.GetValueOrDefault("id_pais") }),
                    "hotel" => await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.alojamiento WHERE id_alojamiento = @val AND id_idioma = 'ES'", new { val }),
                    "select" when f.Lista is not null && f.Lista != "continentes" => (await ListaAsync(f.Lista, null)).Any(o => o.Valor.Equals(val?.ToString(), StringComparison.OrdinalIgnoreCase)) ? 1 : 0,
                    _ => 1
                };
                if (existe == 0) return (null, f.Tipo == "destino" ? $"El destino «{val}» no existe en ese país." : $"«{val}» no es un valor válido para {f.Etiqueta}.");
            }

            // 3) especiales: alojamiento y habitación con los SP copiados de ViajeVDT
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

            // 4) genérico
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
