using EuroVip.Services;
﻿using Dapper;
using EuroVip.Models;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.Json;

namespace EuroVip.Repositories
{
    public interface ICargaCircuitoService
    {
        Task<(CargaResultado? Resultado, List<string> Errores)> CargarAsync(CargaCircuitoDto dto, string usuario, string? ip, string? navegador, string? progresoId = null);
        Task<List<string>> ValidarAsync(CargaCircuitoDto dto);
    }

    /// <summary>
    /// Carga de circuitos (equivale a /CargarOferta/CargarServices rama "Circuito" de BackTiTan) para Eurovip:
    ///   • por cada SALIDA × CAMPAÑA se crea un servicio (spInsertaServicio) + servicio_circuito
    ///     + vuelos (solo si ConVuelo) + suplementos + notas;
    ///   • todo en UNA transacción: si algo falla no queda nada a medias;
    ///   • vuelos opcionales de verdad (no se graban vuelos de relleno);
    ///   • se usa la campaña elegida (BackTiTan fijaba la 17).
    /// </summary>
    public class CargaCircuitoService : ICargaCircuitoService
    {
        private const string Idioma = CircuitoRepository.Idioma;
        private const string Tipo = "CIRCUITO";
        private readonly ICircuitoRepository _circuitos;
        private readonly ILogger<CargaCircuitoService> _logger;
        private readonly IProgresoCargas _progreso;

        public CargaCircuitoService(ICircuitoRepository circuitos, ILogger<CargaCircuitoService> logger, IProgresoCargas progreso)
        {
            _progreso = progreso;
            _circuitos = circuitos;
            _logger = logger;
        }

        private record CircuitoBase(string Nombre, string Id_Proveedor, short Noches);
        internal record Estacion(string Id_Estacion, string Id_Destino, string Id_Pais);

        public async Task<List<string>> ValidarAsync(CargaCircuitoDto dto)
        {
            var errores = new List<string>();
            using var cn = _circuitos.Conexion();
            await cn.OpenAsync();

            // --- circuito ---
            if (dto.NuevoCircuito is not null)
            {
                var n = dto.NuevoCircuito;
                if (await _circuitos.CodigoExisteAsync(n.Codigo)) errores.Add($"Ya existe un circuito con el código \"{n.Codigo}\".");
                if (await _circuitos.NombreExisteAsync(n.Nombre)) errores.Add($"Ya existe un circuito llamado \"{n.Nombre}\".");
                if (n.Destinos.Count == 0) errores.Add("Agrega al menos un destino al circuito.");
            }
            else if (string.IsNullOrWhiteSpace(dto.CodigoCircuito))
                errores.Add("Elige el circuito que vas a cargar.");
            else if (await LeerCircuitoAsync(cn, null, dto.CodigoCircuito) is null)
                errores.Add($"No existe el circuito \"{dto.CodigoCircuito}\".");

            // --- catálogos ---
            if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.categoria WHERE categoria = @c", new { c = dto.Categoria }) == 0)
                errores.Add($"La categoría \"{dto.Categoria}\" no existe.");
            if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.moneda WHERE id_moneda = @m", new { m = dto.Divisa }) == 0)
                errores.Add($"La moneda \"{dto.Divisa}\" no existe.");
            if (!string.IsNullOrWhiteSpace(dto.Id_Proveedor) &&
                await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.proveedor WHERE id_proveedor = @p", new { p = dto.Id_Proveedor }) == 0)
                errores.Add($"El proveedor \"{dto.Id_Proveedor}\" no existe.");
            if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.destino WHERE id_destino = @o AND id_pais = @p AND id_idioma = @Idioma",
                    new { o = dto.Origen, p = dto.Id_Pais_Origen, Idioma }) == 0)
                errores.Add($"El origen \"{dto.Origen} ({dto.Id_Pais_Origen})\" no existe en destinos.");

            if (dto.Campanyas.Count == 0) errores.Add("Elige al menos una campaña.");
            foreach (var c in dto.Campanyas.DistinctBy(x => x.Id_Campanya))
                if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.campanya WHERE id_campanya = @c AND id_idioma = @Idioma", new { c = c.Id_Campanya, Idioma }) == 0)
                    errores.Add($"La campaña #{c.Id_Campanya} no existe en español.");
            foreach (var s in dto.Suplementos.Where(x => !string.IsNullOrWhiteSpace(x.Id_Tasa)).DistinctBy(x => x.Id_Tasa))
                if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.tasa WHERE id_tasa = @t AND id_idioma = @Idioma", new { t = s.Id_Tasa, Idioma }) == 0)
                    errores.Add($"El suplemento \"{s.Id_Tasa}\" no existe.");
            if (dto.Suplementos.GroupBy(x => x.Id_Tasa).Any(g => g.Count() > 1))
                errores.Add("Hay suplementos repetidos: cada suplemento solo puede ir una vez.");

            // --- salidas ---
            var minimo = await _circuitos.PrecioBaseMinimoAsync();
            var hoy = DateTime.Today;
            if (dto.Salidas.Count == 0) errores.Add("Agrega al menos una fecha de salida con su precio.");
            if (dto.Salidas.GroupBy(x => x.Desde.Date).Any(g => g.Count() > 1)) errores.Add("Hay fechas de salida repetidas.");
            var tasasSuplementos = dto.Suplementos.Sum(x => x.Valor);
            // Las tasas incluidas en el precio de cada fila deben ser las que realmente se guardan (suplementos);
            // si no, el precio web (base + suplementos) no daría el precio total que se escribió.
            var descuadradas = dto.Salidas.Where(s => s.Tasas is not null && Math.Abs(s.Tasas.Value - tasasSuplementos) > 0.009m).ToList();
            if (descuadradas.Count > 0)
                errores.Add($"Las tasas de {descuadradas.Count} fila{(descuadradas.Count > 1 ? "s" : "")} del calendario " +
                            $"({string.Join(", ", descuadradas.Select(s => s.Tasas!.Value.ToString("0.##")).Distinct())}) no coinciden con la suma de suplementos " +
                            $"({tasasSuplementos:0.##}). Iguálalas para que el precio web sea el precio total que escribiste.");
            foreach (var s in dto.Salidas)
            {
                var etiqueta = s.Desde.ToString("dd/MM/yyyy");
                // Las salidas de hoy o pasadas no se venden ni salen en la web: solo desde mañana.
                if (s.Desde.Date <= hoy) errores.Add($"La salida del {etiqueta} es de hoy o ya pasó: solo se cargan salidas desde mañana ({hoy.AddDays(1):dd/MM/yyyy}), si no el circuito no sale en la web.");
                if (s.PrecioTotal <= 0) errores.Add($"La salida del {etiqueta} no tiene precio.");
                var base_ = s.PrecioTotal - (s.Tasas ?? tasasSuplementos);
                if (s.PrecioTotal > 0 && base_ < minimo)
                    errores.Add($"La salida del {etiqueta}: el precio base ({base_:N0}) queda por debajo del mínimo ({minimo:N0}).");
            }

            // --- vuelos (opcionales) ---
            if (dto.ConVuelo)
            {
                if (dto.VuelosIda.Count == 0) errores.Add("Marcaste que lleva vuelo: agrega al menos un vuelo de IDA.");
                if (dto.VuelosVuelta.Count == 0) errores.Add("Marcaste que lleva vuelo: agrega al menos un vuelo de VUELTA.");
                var n = 0;
                foreach (var v in dto.VuelosIda.Concat(dto.VuelosVuelta))
                {
                    n++;
                    if (await LeerEstacionAsync(cn, null, v.AeropuertoOrigen) is null) errores.Add($"Vuelo {n}: el aeropuerto de salida \"{v.AeropuertoOrigen}\" no existe.");
                    if (await LeerEstacionAsync(cn, null, v.AeropuertoDestino) is null) errores.Add($"Vuelo {n}: el aeropuerto de llegada \"{v.AeropuertoDestino}\" no existe.");
                    if (await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.proveedor WHERE id_proveedor = @a", new { a = v.Aerolinea }) == 0)
                        errores.Add($"Vuelo {n}: la aerolínea \"{v.Aerolinea}\" no existe.");
                    if (!TimeSpan.TryParse(v.HoraSalida, out _) || !TimeSpan.TryParse(v.HoraLlegada, out _))
                        errores.Add($"Vuelo {n}: revisa las horas (formato HH:MM).");
                }
            }

            // --- duplicados con salidas ya cargadas ---
            if (dto.NuevoCircuito is null && !string.IsNullOrWhiteSpace(dto.CodigoCircuito) && errores.Count == 0)
            {
                var circuito = await LeerCircuitoAsync(cn, null, dto.CodigoCircuito!);
                var repetidas = (await cn.QueryAsync<(DateTime Desde, short Camp)>(@"
                    SELECT sc.desde, s.id_campanya FROM Eurovip.servicio_circuito sc JOIN Eurovip.servicio s ON s.id_servicio = sc.id_servicio
                    WHERE sc.nombre_circuito = @nombre AND sc.id_idoma = @Idioma AND s.activo = 1 AND s.categoria_circuito = @cat
                      AND sc.desde IN @fechas AND s.id_campanya IN @camps AND s.id_moneda = @mon",
                    new { nombre = circuito!.Nombre, Idioma, cat = dto.Categoria, fechas = dto.Salidas.Select(x => x.Desde.Date).ToList(),
                          camps = dto.Campanyas.Select(x => x.Id_Campanya).ToList(), mon = dto.Divisa })).ToList();
                if (repetidas.Count > 0)
                    errores.Add("Ya hay salidas activas cargadas para: " +
                        string.Join(", ", repetidas.Select(r => $"{r.Desde:dd/MM/yyyy} (campaña #{r.Camp})").Distinct()) +
                        ". Desactívalas en el catálogo o quita esas fechas.");
            }

            return errores;
        }

        public async Task<(CargaResultado? Resultado, List<string> Errores)> CargarAsync(CargaCircuitoDto dto, string usuario, string? ip, string? navegador, string? progresoId = null)
        {
            void Avance(double pct, string paso) => _progreso.Reportar(progresoId, pct, paso);   // barra de progreso de la pantalla
            Avance(3, "Revisando los datos…");
            Normalizar(dto);
            var errores = await ValidarAsync(dto);
            if (errores.Count > 0) return (null, errores);

            var codigoServicio = string.IsNullOrWhiteSpace(dto.CodigoServicio)
                ? Corta($"{usuario}{DateTime.Now:yyyyMMddHHmmss}", 30)!
                : Corta(dto.CodigoServicio.Trim(), 30)!;
            var usuarioCorto = Corta(usuario, 15)!;

            using var cn = _circuitos.Conexion();
            await cn.OpenAsync();

            // Registro de la carga (fuera de la transacción para que quede aunque falle)
            var logId = await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO Eurovip.Log_Cargas (tipo_carga, usuario, ip, navegador, fecha_hora, origen, destino, campanyas, noches,
                                                fecha_inicio, fecha_fin, codigo_oferta, estatus, datos)
                VALUES (@tipo, @usuario, @ip, @navegador, GETDATE(), @origen, @destino, @campanyas, @noches, @ini, @fin, @codigo, N'En proceso', @datos);
                SELECT CAST(SCOPE_IDENTITY() AS int);",
                new
                {
                    tipo = dto.Modo == "express" ? "Carga Express Circuitos" : dto.Modo == "excel" ? "Carga Excel Circuitos" : "Carga Clásica Circuitos",
                    usuario, ip, navegador = Corta(navegador, 1000),
                    origen = $"{dto.Origen} ({dto.Id_Pais_Origen})",
                    destino = dto.NuevoCircuito?.Nombre ?? dto.CodigoCircuito,
                    campanyas = string.Join(",", dto.Campanyas.Select(c => c.Id_Campanya)),
                    noches = (int?)dto.Noches,
                    ini = dto.Salidas.Min(s => s.Desde.Date),
                    fin = dto.Salidas.Max(s => s.Desde.Date),
                    codigo = codigoServicio,
                    datos = JsonSerializer.Serialize(dto)
                });

            var resultado = new CargaResultado { LogId = logId, Codigo = codigoServicio };
            var totalSalidas = dto.Salidas.Count * Math.Max(1, dto.Campanyas.DistinctBy(c => c.Id_Campanya).Count());
            Avance(8, $"Datos correctos. Creando {totalSalidas} salida{(totalSalidas == 1 ? "" : "s")}…");
            short nochesUsadas = 0;
            var publicar = new HashSet<(short Camp, string PaisOrigen, string Origen, string PaisDestino)>();

            using (var tx = cn.BeginTransaction())
            {
                try
                {
                    // 1) Circuito nuevo (versión Express)
                    if (dto.NuevoCircuito is not null)
                    {
                        dto.NuevoCircuito.CodigoOriginal = null;
                        await _circuitos.GuardarAsync(dto.NuevoCircuito, cn, tx);
                        dto.CodigoCircuito = dto.NuevoCircuito.Codigo.Trim();
                    }

                    var circuito = await LeerCircuitoAsync(cn, tx, dto.CodigoCircuito!)
                                   ?? throw new InvalidOperationException("El circuito no existe.");
                    resultado.Circuito = circuito.Nombre;
                    var noches = dto.Noches ?? circuito.Noches;
                    nochesUsadas = noches;
                    var proveedor = string.IsNullOrWhiteSpace(dto.Id_Proveedor) ? circuito.Id_Proveedor : dto.Id_Proveedor!;
                    var paisesDestino = (await cn.QueryAsync<string>(
                        "SELECT DISTINCT id_pais FROM Eurovip.circuito_destino WHERE nombre_circuito = @n AND id_idioma = @Idioma",
                        new { n = circuito.Nombre, Idioma }, tx)).ToList();

                    // Aeropuertos de los vuelos (si lleva)
                    var estaciones = new Dictionary<string, Estacion>(StringComparer.OrdinalIgnoreCase);
                    if (dto.ConVuelo)
                        foreach (var cod in dto.VuelosIda.Concat(dto.VuelosVuelta).SelectMany(v => new[] { v.AeropuertoOrigen, v.AeropuertoDestino }).Distinct(StringComparer.OrdinalIgnoreCase))
                            estaciones[cod] = (await LeerEstacionAsync(cn, tx, cod))!;

                    var tasasSuplementos = dto.Suplementos.Sum(x => x.Valor);

                    // 2) Una salida (servicio) por fecha × campaña
                    foreach (var camp in dto.Campanyas.DistinctBy(c => c.Id_Campanya))
                    {
                        foreach (var salida in dto.Salidas.OrderBy(s => s.Desde))
                        {
                            var desde = salida.Desde.Date;
                            var precioBase = salida.PrecioTotal - (salida.Tasas ?? tasasSuplementos);

                            var p = new DynamicParameters();
                            p.Add("@tipoServicio", Tipo);
                            p.Add("@idioma", Idioma);
                            p.Add("@moneda", dto.Divisa);
                            p.Add("@campanya", camp.Id_Campanya);
                            p.Add("@proveedor", proveedor);
                            p.Add("@apertura", DateTime.Today);
                            p.Add("@usuario", usuarioCorto);
                            p.Add("@precioBase", precioBase);
                            p.Add("@descuento", camp.Descuento);
                            p.Add("@nota", "");
                            p.Add("@activo", true);
                            p.Add("@noches", noches);
                            p.Add("@precioMasMenos", 0m);
                            p.Add("@codigoServicio", codigoServicio);
                            p.Add("@nombre", circuito.Nombre);
                            p.Add("@categoriaCircuito", dto.Categoria);
                            p.Add("@baja", new DateTime(2060, 12, 12));
                            p.Add("@ret", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);
                            await cn.ExecuteAsync("Eurovip.spInsertaServicio", p, tx, commandType: CommandType.StoredProcedure);
                            var id = p.Get<int>("@ret");
                            if (id <= 0) throw new InvalidOperationException($"No se pudo crear la salida del {desde:dd/MM/yyyy}.");
                            resultado.Ids.Add(id);
                            Avance(8 + 80.0 * resultado.Ids.Count / Math.Max(1, totalSalidas), $"Creando salida {resultado.Ids.Count} de {totalSalidas} · {desde:dd/MM/yyyy}");

                            // Origen de venta de esta salida (del vuelo si lleva, si no el elegido)
                            var paisOrigen = dto.Id_Pais_Origen;
                            var origen = dto.Origen;
                            if (dto.ConVuelo)
                            {
                                var primero = estaciones[dto.VuelosIda[0].AeropuertoOrigen];
                                paisOrigen = primero.Id_Pais;
                                origen = primero.Id_Destino;
                            }

                            await cn.ExecuteAsync(@"
                                INSERT INTO Eurovip.servicio_circuito (id_servicio, nombre_circuito, id_idoma, desde, hasta, id_pais_origen, origen)
                                VALUES (@id, @nombre, @Idioma, @desde, @hasta, @paisOrigen, @origen)",
                                new { id, nombre = circuito.Nombre, Idioma, desde, hasta = desde.AddDays(noches), paisOrigen = dto.Id_Pais_Origen, origen = dto.Origen }, tx);

                            if (dto.ConVuelo)
                            {
                                resultado.Vuelos += await InsertarVuelosAsync(cn, tx, id, dto.VuelosIda, desde, estaciones);
                                resultado.Vuelos += await InsertarVuelosAsync(cn, tx, id, dto.VuelosVuelta, desde.AddDays(noches), estaciones);
                            }

                            foreach (var s in dto.Suplementos.Where(x => !string.IsNullOrWhiteSpace(x.Id_Tasa)))
                                await cn.ExecuteAsync("Eurovip.spInsertaServicioSuplemento",
                                    new { numeroServicio = id, nombreSuplemento = s.Id_Tasa, codigoIdioma = Idioma, valor = s.Valor }, tx, commandType: CommandType.StoredProcedure);

                            foreach (var pd in paisesDestino) publicar.Add((camp.Id_Campanya, paisOrigen, origen, pd));
                        }
                    }

                    // 3) Hoteles del circuito (ej. los que vienen del Excel)
                    foreach (var h in dto.Hoteles.Where(x => x.Id_Alojamiento > 0).DistinctBy(x => x.Id_Alojamiento))
                        await cn.ExecuteAsync("Eurovip.spInsertarServicioCircuitoAlojamiento",
                            new { nombre_circuito = circuito.Nombre, id_idioma = Idioma, id_alojamiento = h.Id_Alojamiento,
                                  categoria = string.IsNullOrWhiteSpace(h.Categoria) ? dto.Categoria : h.Categoria, caratula = (string?)null },
                            tx, commandType: CommandType.StoredProcedure);

                    // 4) Incluye / No incluye / notas: una vez por carga (código), no por salida
                    Avance(89, "Guardando incluye / no incluye y notas…");
                    await GuardarDescripcionAsync(cn, tx, codigoServicio, dto.Notas);

                    Avance(91, "Confirmando la carga…");
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    _logger.LogError(ex, "Error en carga de circuito (log {LogId})", logId);
                    await cn.ExecuteAsync(@"UPDATE Eurovip.Log_Cargas SET estatus = N'Revisar', mensaje = @m, fecha_hora_fin = GETDATE(),
                                                   registros_insertados = 0, registros_no_insertados = @n WHERE id = @logId",
                        new { m = ex.Message, n = dto.Salidas.Count * Math.Max(1, dto.Campanyas.Count), logId });
                    return (null, new List<string> { "No se cargó nada (se deshizo todo). Motivo: " + MensajeAmigable(ex) });
                }
            }

            // 4) Vitrina web de circuitos (fuera de la transacción: si falla, la carga igual quedó bien)
            if (dto.PublicarEnWeb)
            {
                var nPub = 0;
                foreach (var x in publicar)
                {
                    Avance(92 + 7.0 * nPub++ / Math.Max(1, publicar.Count), $"Publicando en la web… {nPub} de {publicar.Count}");
                    try
                    {
                        await cn.ExecuteAsync("Eurovip.spActualizaAlojamientoCircuito",
                            new { idIdioma = Idioma, idTipoServicio = Tipo, idCampanya = x.Camp, idMoneda = dto.Divisa,
                                  idPaisOrigen = x.PaisOrigen, idOrigen = x.Origen, idPaisDestino = x.PaisDestino },
                            commandType: CommandType.StoredProcedure);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo actualizar la vitrina de circuitos {@Clave}", x);
                    }
                }
            }

            resultado.Ok = true;
            resultado.Servicios = resultado.Ids.Count;
            resultado.Mensaje = $"¡Listo! Se cargaron {resultado.Servicios} salida{(resultado.Servicios == 1 ? "" : "s")} de \"{resultado.Circuito}\"" +
                                (dto.ConVuelo ? $" con {resultado.Vuelos} vuelos." : " (sin vuelo).");

            await cn.ExecuteAsync(@"UPDATE Eurovip.Log_Cargas SET estatus = N'Correcto', mensaje = @m, fecha_hora_fin = GETDATE(),
                                           registros_insertados = @n, registros_no_insertados = 0, noches = @noches,
                                           destino = @circuito WHERE id = @logId",
                new { m = resultado.Mensaje, n = resultado.Servicios, noches = (int)nochesUsadas, circuito = resultado.Circuito, logId });
            return (resultado, new List<string>());
        }

        /// <summary>
        /// Inserta los vuelos de un trayecto calculando las fechas: cada vuelo sale el día en que llegó el anterior
        /// (o el día siguiente si su hora de salida es anterior a la llegada previa) y llega al día siguiente si la
        /// hora de llegada es menor que la de salida (vuelo nocturno).
        /// </summary>
        internal static async Task<int> InsertarVuelosAsync(SqlConnection cn, SqlTransaction tx, int idServicio,
            List<CargaSegmentoDto> vuelos, DateTime fechaInicial, Dictionary<string, Estacion> estaciones)
        {
            var fecha = fechaInicial;
            TimeSpan? llegadaAnterior = null;
            foreach (var v in vuelos)
            {
                var hs = TimeSpan.Parse(v.HoraSalida, CultureInfo.InvariantCulture);
                var hl = TimeSpan.Parse(v.HoraLlegada, CultureInfo.InvariantCulture);
                if (llegadaAnterior is not null && hs < llegadaAnterior) fecha = fecha.AddDays(1);
                var fechaLlegada = hl < hs ? fecha.AddDays(1) : fecha;
                var o = estaciones[v.AeropuertoOrigen];
                var d = estaciones[v.AeropuertoDestino];

                await cn.ExecuteAsync("Eurovip.spInsertaServicioItinerario", new
                {
                    numero = idServicio,
                    proveedor = v.Aerolinea,
                    estacionOrigen = o.Id_Estacion,
                    origen = o.Id_Destino,
                    idioma = Idioma,
                    PaisOrigen = o.Id_Pais,
                    estacionDestino = d.Id_Estacion,
                    destino = d.Id_Destino,
                    paisDestino = d.Id_Pais,
                    vuelo = Corta(v.NumeroVuelo?.Trim(), 15),
                    fechaSalida = fecha,
                    horaSalida = hs.ToString(@"hh\:mm"),
                    fechaRegreso = fechaLlegada,
                    horaRegreso = hl.ToString(@"hh\:mm"),
                    clase = string.IsNullOrWhiteSpace(v.Clase) ? "T" : v.Clase.Trim()[..1].ToUpperInvariant(),
                    conexion = v.Conexion
                }, tx, commandType: CommandType.StoredProcedure);

                fecha = fechaLlegada;
                llegadaAnterior = hl;
            }
            return vuelos.Count;
        }

        private static async Task<CircuitoBase?> LeerCircuitoAsync(SqlConnection cn, SqlTransaction? tx, string codigo) =>
            await cn.QueryFirstOrDefaultAsync<CircuitoBase>(
                "SELECT nombre_circuito AS Nombre, id_proveedor AS Id_Proveedor, noches AS Noches FROM Eurovip.circuito WHERE LTRIM(RTRIM(id)) = @codigo AND id_idioma = @Idioma",
                new { codigo = codigo.Trim(), Idioma }, tx);

        internal static async Task<Estacion?> LeerEstacionAsync(SqlConnection cn, SqlTransaction? tx, string codigo) =>
            await cn.QueryFirstOrDefaultAsync<Estacion>(
                "SELECT TOP 1 id_estacion AS Id_Estacion, id_destino AS Id_Destino, id_pais AS Id_Pais FROM Eurovip.estacion WHERE id_estacion = @codigo AND id_idioma = @Idioma",
                new { codigo = codigo.Trim(), Idioma }, tx);

        private static void Normalizar(CargaCircuitoDto dto)
        {
            dto.Divisa = dto.Divisa.Trim().ToUpperInvariant();
            dto.Id_Pais_Origen = dto.Id_Pais_Origen.Trim().ToUpperInvariant();
            dto.Origen = dto.Origen.Trim();
            dto.Categoria = dto.Categoria.Trim();
            if (!dto.ConVuelo) { dto.VuelosIda.Clear(); dto.VuelosVuelta.Clear(); }
            foreach (var v in dto.VuelosIda.Concat(dto.VuelosVuelta))
            {
                v.AeropuertoOrigen = v.AeropuertoOrigen.Trim().ToUpperInvariant();
                v.AeropuertoDestino = v.AeropuertoDestino.Trim().ToUpperInvariant();
            }
        }

        internal static string MensajeAmigable(Exception ex) => ex switch
        {
            SqlException { Number: 547 } => "un dato no coincide con los catálogos (revisa campaña, proveedor, aeropuertos o categoría).",
            SqlException { Number: 2627 or 2601 } => "hay datos repetidos.",
            _ => ex.Message
        };

        /// <summary>
        /// Incluye / No incluye / notas de una carga: UNA fila en Eurovip.servicio_descripcion por código de carga
        /// (como BackTiTan: todas las salidas/fechas de la carga comparten el código y los textos).
        /// Incluye → incluye · No incluye → no_incluye · Oferta → oferta_especial · el resto (Condiciones, Importante,
        /// Documentación…) → nota, con su título delante. Si la carga ya tenía descripción, se actualiza.
        /// </summary>
        internal static async Task GuardarDescripcionAsync(SqlConnection cn, SqlTransaction tx, string codigo, IEnumerable<CargaNotaDto> notas)
        {
            var l = notas.Where(n => !string.IsNullOrWhiteSpace(n.Texto)).Select(n => (Tipo: (n.Tipo ?? "").Trim(), Texto: n.Texto.Trim())).ToList();
            if (l.Count == 0) return;
            static bool Es(string tipo, string valor) => tipo.Equals(valor, StringComparison.OrdinalIgnoreCase);
            string? Juntar(Func<string, bool> cual, bool conTitulo = false)
            {
                var x = l.Where(n => cual(n.Tipo)).Select(n => conTitulo && !Es(n.Tipo, "Condiciones") && n.Tipo != "" ? $"{n.Tipo}: {n.Texto}" : n.Texto).ToList();
                return x.Count == 0 ? null : string.Join("\n\n", x);
            }
            var d = new
            {
                codigo, id_idioma = Idioma,
                incluye = Juntar(t => Es(t, "Incluye")),
                no_incluye = Juntar(t => Es(t, "No incluye")),
                oferta_especial = Juntar(t => Es(t, "Oferta")),
                nota = Juntar(t => !Es(t, "Incluye") && !Es(t, "No incluye") && !Es(t, "Oferta"), conTitulo: true)
            };
            var existe = await cn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Eurovip.servicio_descripcion WHERE codigo = @codigo AND id_idioma = @id_idioma", d, tx);
            if (existe > 0)
                await cn.ExecuteAsync(@"UPDATE Eurovip.servicio_descripcion SET nota = @nota, incluye = @incluye, no_incluye = @no_incluye, oferta_especial = @oferta_especial
                                        WHERE codigo = @codigo AND id_idioma = @id_idioma", d, tx);
            else
                await cn.ExecuteAsync("Eurovip.spInsertaServicioDescripcion",
                    new { d.codigo, d.id_idioma, nota = d.nota ?? "", incluye = d.incluye ?? "", no_incluye = d.no_incluye ?? "", itinerario = "", imagen = "", oferta_especial = d.oferta_especial ?? "" },
                    tx, commandType: CommandType.StoredProcedure);
        }

        internal static string? Corta(string? s, int max) => s is null ? null : (s.Length > max ? s[..max] : s);
    }
}


