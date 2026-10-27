using EuroVip.Services;
using Dapper;
using EuroVip.Models;
using System.Data;
using System.Data.SqlClient;
using System.Text.Json;

namespace EuroVip.Repositories
{
    public interface ICargaOfertaService
    {
        Task<List<string>> ValidarAsync(CargaOfertaDto dto);
        Task<(CargaResultado? Resultado, List<string> Errores)> CargarAsync(CargaOfertaDto dto, string usuario, string? ip, string? navegador, string? progresoId = null);
    }

    /// <summary>
    /// Carga manual de ofertas de hotel (equivale a /CargarOferta/CargarServices rama "Oferta" de BackTiTan) para Eurovip:
    ///   • por cada HOTEL × CAMPAÑA × FECHA se crea un servicio SOLO_HOTEL (Solo Hotel) o PAQUETES (Paquetes, dto.Tipo) con su hotel (servicio_detalle),
    ///     sus habitaciones (servicio_habitacion), suplementos, notas, origen de venta y, solo si lleva, vuelos;
    ///   • fecha de salida del hotel = desde + noches (una sola regla);
    ///   • todo en UNA transacción; sin vuelos de relleno; se guarda el número de vuelo;
    ///   • al final se refresca la vitrina web de hoteles (alojamiento_destino).
    /// </summary>
    public class CargaOfertaService : ICargaOfertaService
    {
        private const string Idioma = "ES";
        private readonly ICircuitoRepository _db;
        private readonly IOfertaRepository _ofertas;
        private readonly ILogger<CargaOfertaService> _logger;
        private readonly IProgresoCargas _progreso;

        public CargaOfertaService(ICircuitoRepository db, IOfertaRepository ofertas, ILogger<CargaOfertaService> logger, IProgresoCargas progreso)
        {
            _progreso = progreso;
            _db = db;
            _ofertas = ofertas;
            _logger = logger;
        }

        public async Task<List<string>> ValidarAsync(CargaOfertaDto dto)
        {
            Normalizar(dto);
            var e = new List<string>();
            using var cn = _db.Conexion();
            await cn.OpenAsync();
            async Task<bool> Existe(string sql, object p) => await cn.ExecuteScalarAsync<int>(sql, p) > 0;

            if (!await Existe("SELECT COUNT(1) FROM Eurovip.proveedor WHERE id_proveedor = @p", new { p = dto.Id_Proveedor })) e.Add($"El proveedor \"{dto.Id_Proveedor}\" no existe.");
            if (!await Existe("SELECT COUNT(1) FROM Eurovip.moneda WHERE id_moneda = @m", new { m = dto.Divisa })) e.Add($"La moneda \"{dto.Divisa}\" no existe.");
            if (!await Existe("SELECT COUNT(1) FROM Eurovip.destino WHERE id_destino = @o AND id_pais = @p AND id_idioma = 'ES'", new { o = dto.Origen, p = dto.Id_Pais_Origen }))
                e.Add($"El origen \"{dto.Origen} ({dto.Id_Pais_Origen})\" no existe en destinos.");

            if (dto.Campanyas.Count == 0) e.Add("Elige al menos una campaña.");
            foreach (var c in dto.Campanyas.DistinctBy(x => x.Id_Campanya))
                if (!await Existe("SELECT COUNT(1) FROM Eurovip.campanya WHERE id_campanya = @c AND id_idioma = 'ES'", new { c = c.Id_Campanya }))
                    e.Add($"La campaña #{c.Id_Campanya} no existe en español.");
            if (dto.Suplementos.GroupBy(x => x.Id_Tasa).Any(g => g.Count() > 1)) e.Add("Hay suplementos repetidos.");
            foreach (var s in dto.Suplementos.DistinctBy(x => x.Id_Tasa))
                if (!await Existe("SELECT COUNT(1) FROM Eurovip.tasa WHERE id_tasa = @t AND id_idioma = 'ES'", new { t = s.Id_Tasa })) e.Add($"El suplemento \"{s.Id_Tasa}\" no existe.");

            if (dto.Hoteles.Count == 0) e.Add("Agrega al menos un hotel.");
            if (dto.Hoteles.GroupBy(h => (h.Id_Alojamiento, h.Id_Regimen)).Any(g => g.Count() > 1)) e.Add("Hay un hotel repetido con el mismo régimen.");

            // El destino de la oferta es el de sus hoteles: en una carga todos tienen que ser del mismo destino.
            if (dto.Hoteles.Count > 0)
            {
                var dh = await DestinosHotelesAsync(cn, null, dto.Hoteles.Select(h => h.Id_Alojamiento));
                foreach (var h in dh.Where(x => string.IsNullOrWhiteSpace(x.Destino)))
                    e.Add($"{h.Nombre}: el hotel no tiene destino. Ponle el destino en Herramientas › Alojamientos.");
                var grupos = dh.Where(x => !string.IsNullOrWhiteSpace(x.Destino)).GroupBy(x => (x.Destino.Trim().ToUpperInvariant(), x.Pais)).ToList();
                if (grupos.Count > 1)
                    e.Add("Todos los hoteles de una carga tienen que ser del mismo destino. Hay: " +
                          string.Join(" · ", grupos.Select(g => $"{g.First().Destino.Trim()} ({string.Join(", ", g.Select(x => x.Nombre))})")) +
                          ". Haz una carga por destino.");
            }

            var minimo = await _db.PrecioBaseMinimoAsync();
            var tasas = dto.Suplementos.Sum(x => x.Valor);
            foreach (var h in dto.Hoteles)
            {
                var nombre = await cn.ExecuteScalarAsync<string?>("SELECT nombre FROM Eurovip.alojamiento WHERE id_alojamiento = @a AND id_idioma = 'ES'", new { a = h.Id_Alojamiento });
                var et = nombre ?? $"Hotel #{h.Id_Alojamiento}";
                if (nombre is null) { e.Add($"El hotel #{h.Id_Alojamiento} no existe."); continue; }
                if (!await Existe("SELECT COUNT(1) FROM Eurovip.regimen WHERE id_regimen = @r AND id_idioma = 'ES'", new { r = h.Id_Regimen })) e.Add($"{et}: el régimen \"{h.Id_Regimen}\" no existe.");
                if (h.Habitaciones.Count == 0) e.Add($"{et}: elige al menos una habitación.");
                var ids = h.Habitaciones.Select(x => x.Id_Habitacion).Distinct().ToList();
                if (ids.Count > 0)
                {
                    var validas = await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.alojamiento_habitacion WHERE id_alojamiento = @a AND id_idioma = 'ES' AND id_habitacion IN @ids", new { a = h.Id_Alojamiento, ids });
                    if (validas != ids.Count) e.Add($"{et}: alguna habitación no pertenece a ese hotel.");
                }
                if (h.Salidas.Count == 0) e.Add($"{et}: no tiene fechas con precio.");
                if (h.Salidas.GroupBy(s => s.Desde.Date).Any(g => g.Count() > 1)) e.Add($"{et}: hay fechas repetidas.");
                foreach (var s in h.Salidas)
                {
                    var f = s.Desde.ToString("dd/MM/yyyy");
                    // Las fechas de hoy o pasadas no se venden ni salen en la web: solo desde mañana.
                    if (s.Desde.Date <= DateTime.Today) e.Add($"{et}: la fecha {f} es de hoy o ya pasó. Solo se cargan fechas desde mañana ({DateTime.Today.AddDays(1):dd/MM/yyyy}), si no la oferta no sale en la web.");
                    if (s.PrecioTotal <= 0) e.Add($"{et}: la fecha {f} no tiene precio.");
                    else if (s.PrecioTotal - (s.Tasas ?? tasas) < minimo) e.Add($"{et}, {f}: el precio base ({s.PrecioTotal - (s.Tasas ?? tasas):N0}) queda por debajo del mínimo ({minimo:N0}).");
                    if (s.Tasas is not null && Math.Abs(s.Tasas.Value - tasas) > 0.009m)
                        e.Add($"{et}, {f}: las tasas de la fila ({s.Tasas:0.##}) no coinciden con la suma de suplementos ({tasas:0.##}).");
                }

                // Duplicados con ofertas activas ya cargadas
                if (dto.Campanyas.Count > 0 && h.Salidas.Count > 0)
                {
                    var rep = (await cn.QueryAsync<(DateTime Desde, short Camp)>(@"
                        SELECT sd.desde, s.id_campanya FROM Eurovip.servicio s JOIN Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio
                        WHERE s.id_tipo_servicio = @tipo AND s.activo = 1 AND sd.id_alojamiento = @a AND sd.id_regimen = @r AND s.id_moneda = @m
                          AND sd.desde IN @fechas AND s.id_campanya IN @camps",
                        new { tipo = OfertaRepository.TipoDe(dto.Tipo), a = h.Id_Alojamiento, r = h.Id_Regimen, m = dto.Divisa, fechas = h.Salidas.Select(x => x.Desde.Date).ToList(), camps = dto.Campanyas.Select(x => x.Id_Campanya).ToList() })).ToList();
                    if (rep.Count > 0)
                        e.Add($"{et}: ya hay {(OfertaRepository.TipoDe(dto.Tipo) == OfertaRepository.TipoPaquete ? "paquetes" : "ofertas")} activos para " + string.Join(", ", rep.Select(x => $"{x.Desde:dd/MM/yyyy} (campaña #{x.Camp})").Distinct().Take(6)) +
                              (rep.Count > 6 ? "…" : "") + ". Desactívalas en la lista o quita esas fechas.");
                }
            }

            if (dto.ConVuelo)
            {
                if (dto.VuelosIda.Count == 0) e.Add("Marcaste que lleva vuelo: agrega al menos un vuelo de IDA.");
                if (dto.VuelosVuelta.Count == 0) e.Add("Marcaste que lleva vuelo: agrega al menos un vuelo de VUELTA.");
                var n = 0;
                foreach (var v in dto.VuelosIda.Concat(dto.VuelosVuelta))
                {
                    n++;
                    if (await CargaCircuitoService.LeerEstacionAsync(cn, null, v.AeropuertoOrigen) is null) e.Add($"Vuelo {n}: el aeropuerto \"{v.AeropuertoOrigen}\" no existe.");
                    if (await CargaCircuitoService.LeerEstacionAsync(cn, null, v.AeropuertoDestino) is null) e.Add($"Vuelo {n}: el aeropuerto \"{v.AeropuertoDestino}\" no existe.");
                    if (!await Existe("SELECT COUNT(1) FROM Eurovip.proveedor WHERE id_proveedor = @a", new { a = v.Aerolinea })) e.Add($"Vuelo {n}: la aerolínea \"{v.Aerolinea}\" no existe.");
                    if (!TimeSpan.TryParse(v.HoraSalida, out _) || !TimeSpan.TryParse(v.HoraLlegada, out _)) e.Add($"Vuelo {n}: revisa las horas (HH:MM).");
                }
            }
            return e;
        }

        public record DestinoHotel(int Id_Alojamiento, string Nombre, string Destino, string Pais);

        /// <summary>Destino (alojamiento.id_destino) y país de cada hotel de la carga.</summary>
        public static async Task<List<DestinoHotel>> DestinosHotelesAsync(IDbConnection cn, IDbTransaction? tx, IEnumerable<int> ids) =>
            (await cn.QueryAsync<DestinoHotel>(@"SELECT id_alojamiento AS Id_Alojamiento, nombre AS Nombre, ISNULL(id_destino, '') AS Destino, ISNULL(id_pais, '') AS Pais
                                                FROM Eurovip.alojamiento WHERE id_idioma = 'ES' AND id_alojamiento IN @ids",
                new { ids = ids.Distinct().ToList() }, tx)).ToList();

        public async Task<(CargaResultado? Resultado, List<string> Errores)> CargarAsync(CargaOfertaDto dto, string usuario, string? ip, string? navegador, string? progresoId = null)
        {
            void Avance(double pct, string paso) => _progreso.Reportar(progresoId, pct, paso);   // barra de progreso de la pantalla
            Avance(3, "Revisando los datos…");
            var tipo = OfertaRepository.TipoDe(dto.Tipo);
            var esPaquete = tipo == OfertaRepository.TipoPaquete;
            var palabra = esPaquete ? "paquete" : "oferta";
            var errores = await ValidarAsync(dto);
            if (errores.Count > 0) return (null, errores);

            var codigo = string.IsNullOrWhiteSpace(dto.CodigoServicio) ? CargaCircuitoService.Corta($"{usuario}{DateTime.Now:yyyyMMddHHmmss}", 30)! : CargaCircuitoService.Corta(dto.CodigoServicio.Trim(), 30)!;
            var nombre = string.IsNullOrWhiteSpace(dto.Nombre) ? (esPaquete ? "Paquete" : "Oferta") : CargaCircuitoService.Corta(dto.Nombre.Trim(), 50)!;
            var total = dto.Hoteles.Sum(h => h.Salidas.Count) * Math.Max(1, dto.Campanyas.Count);
            var todas = dto.Hoteles.SelectMany(h => h.Salidas).ToList();

            using var cn = _db.Conexion();
            await cn.OpenAsync();
            // La validación ya aseguró que todos los hoteles comparten destino: ese es el destino de la oferta.
            var destino = (await DestinosHotelesAsync(cn, null, dto.Hoteles.Select(h => h.Id_Alojamiento))).First(x => !string.IsNullOrWhiteSpace(x.Destino));
            var nombresHotel = (await cn.QueryAsync<string>("SELECT nombre FROM Eurovip.alojamiento WHERE id_idioma = 'ES' AND id_alojamiento IN @ids",
                new { ids = dto.Hoteles.Select(h => h.Id_Alojamiento).ToList() })).ToList();

            var logId = await cn.ExecuteScalarAsync<int>(@"
                INSERT INTO Eurovip.Log_Cargas (tipo_carga, usuario, ip, navegador, fecha_hora, origen, destino, campanyas, regimenes, noches,
                                                fecha_inicio, fecha_fin, codigo_oferta, estatus, datos)
                VALUES (@tipo, @usuario, @ip, @navegador, GETDATE(), @origen, @destino, @campanyas, @regimenes, @noches, @ini, @fin, @codigo, N'En proceso', @datos);
                SELECT CAST(SCOPE_IDENTITY() AS int);",
                new
                {
                    tipo = (dto.Modo == "express" ? "Carga Express " : "Carga Excel ") + (esPaquete ? "Paquetes" : "Ofertas"),
                    usuario, ip, navegador = CargaCircuitoService.Corta(navegador, 1000),
                    origen = $"{dto.Origen} ({dto.Id_Pais_Origen})",
                    destino = CargaCircuitoService.Corta($"{destino.Destino.Trim()} — {string.Join(" · ", nombresHotel)}", 1000),
                    campanyas = string.Join(",", dto.Campanyas.Select(c => c.Id_Campanya)),
                    regimenes = string.Join(",", dto.Hoteles.Select(h => h.Id_Regimen).Distinct()),
                    noches = (int)dto.Noches,
                    ini = todas.Min(s => s.Desde.Date), fin = todas.Max(s => s.Desde.Date),
                    codigo, datos = JsonSerializer.Serialize(dto)
                });

            var r = new CargaResultado { LogId = logId, Codigo = codigo, Circuito = nombresHotel.Count == 1 ? nombresHotel[0] : $"{nombresHotel.Count} hoteles" };
            var tasasSuplementos = dto.Suplementos.Sum(x => x.Valor);
            Avance(8, $"Datos correctos. Creando {total} {palabra}{(total == 1 ? "" : "s")}…");

            using (var tx = cn.BeginTransaction())
            {
                try
                {
                    var estaciones = new Dictionary<string, CargaCircuitoService.Estacion>(StringComparer.OrdinalIgnoreCase);
                    if (dto.ConVuelo)
                        foreach (var cod in dto.VuelosIda.Concat(dto.VuelosVuelta).SelectMany(v => new[] { v.AeropuertoOrigen, v.AeropuertoDestino }).Distinct(StringComparer.OrdinalIgnoreCase))
                            estaciones[cod] = (await CargaCircuitoService.LeerEstacionAsync(cn, tx, cod))!;

                    foreach (var hotel in dto.Hoteles)
                    foreach (var camp in dto.Campanyas.DistinctBy(c => c.Id_Campanya))
                    foreach (var salida in hotel.Salidas.OrderBy(s => s.Desde))
                    {
                        var desde = salida.Desde.Date;
                        var p = new DynamicParameters();
                        p.Add("@tipoServicio", tipo); p.Add("@idioma", Idioma); p.Add("@moneda", dto.Divisa); p.Add("@campanya", camp.Id_Campanya);
                        p.Add("@proveedor", dto.Id_Proveedor); p.Add("@apertura", DateTime.Today); p.Add("@usuario", CargaCircuitoService.Corta(usuario, 15));
                        p.Add("@precioBase", salida.PrecioTotal - (salida.Tasas ?? tasasSuplementos)); p.Add("@descuento", camp.Descuento); p.Add("@nota", "");
                        p.Add("@activo", true); p.Add("@noches", dto.Noches); p.Add("@precioMasMenos", 0m); p.Add("@codigoServicio", codigo);
                        p.Add("@nombre", nombre); p.Add("@categoriaCircuito", null); p.Add("@baja", new DateTime(2060, 12, 12));
                        p.Add("@ret", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);
                        await cn.ExecuteAsync("Eurovip.spInsertaServicio", p, tx, commandType: CommandType.StoredProcedure);
                        var id = p.Get<int>("@ret");
                        if (id <= 0) throw new InvalidOperationException($"No se pudo crear {(esPaquete ? "el paquete" : "la oferta")} del {desde:dd/MM/yyyy}.");
                        r.Ids.Add(id);
                        if (hotel.Peso is not null || hotel.Precio_Proveedor is not null)
                            await cn.ExecuteAsync("UPDATE Eurovip.servicio SET peso = @Peso, precio_proveedor = @Precio_Proveedor WHERE id_servicio = @id",
                                new { id, hotel.Peso, hotel.Precio_Proveedor }, tx);
                        Avance(8 + 80.0 * r.Ids.Count / Math.Max(1, total), $"Creando {palabra} {r.Ids.Count} de {total} · {desde:dd/MM/yyyy}");

                        await cn.ExecuteAsync("Eurovip.spInsertaServicioDetalle", new
                        {
                            numero = id, alojamiento = hotel.Id_Alojamiento, idioma = Idioma, regimen = hotel.Id_Regimen,
                            desde, horaDesde = "", hasta = desde.AddDays(dto.Noches), horaHasta = "", noches = dto.Noches
                        }, tx, commandType: CommandType.StoredProcedure);

                        foreach (var h in hotel.Habitaciones.DistinctBy(x => x.Id_Habitacion))
                            await cn.ExecuteAsync("Eurovip.InsertarServicioHabitacion",
                                new { Id_Servicio = id, Secuencia = 1, Id_Alojamiento = hotel.Id_Alojamiento, Id_Idioma = Idioma, h.Id_Habitacion, h.Precio },
                                tx, commandType: CommandType.StoredProcedure);

                        await cn.ExecuteAsync("INSERT INTO Eurovip.servicio_origen (id_servicio, id_pais_origen, origen) VALUES (@id, @p, @o)",
                            new { id, p = dto.Id_Pais_Origen, o = dto.Origen }, tx);
                        await cn.ExecuteAsync("INSERT INTO Eurovip.servicio_destino (id_servicio, id_pais_destino, destino) VALUES (@id, @p, @d)",
                            new { id, p = destino.Pais, d = destino.Destino.Trim() }, tx);

                        if (dto.ConVuelo)
                        {
                            r.Vuelos += await CargaCircuitoService.InsertarVuelosAsync(cn, tx, id, dto.VuelosIda, desde, estaciones);
                            r.Vuelos += await CargaCircuitoService.InsertarVuelosAsync(cn, tx, id, dto.VuelosVuelta, desde.AddDays(dto.Noches), estaciones);
                        }
                        foreach (var s in dto.Suplementos.Where(x => !string.IsNullOrWhiteSpace(x.Id_Tasa)))
                            await cn.ExecuteAsync("Eurovip.spInsertaServicioSuplemento", new { numeroServicio = id, nombreSuplemento = s.Id_Tasa, codigoIdioma = Idioma, valor = s.Valor }, tx, commandType: CommandType.StoredProcedure);
                    }
                    // Incluye / No incluye / notas: una vez por carga (código), no por oferta/fecha
                    Avance(89, "Guardando incluye / no incluye y notas…");
                    await CargaCircuitoService.GuardarDescripcionAsync(cn, tx, codigo, dto.Notas);
                    Avance(91, "Confirmando la carga…");
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    _logger.LogError(ex, "Error en carga de {Tipo} (log {LogId})", tipo, logId);
                    await cn.ExecuteAsync(@"UPDATE Eurovip.Log_Cargas SET estatus = N'Revisar', mensaje = @m, fecha_hora_fin = GETDATE(), registros_insertados = 0, registros_no_insertados = @n WHERE id = @logId",
                        new { m = ex.Message, n = total, logId });
                    return (null, new List<string> { "No se cargó nada (se deshizo todo). Motivo: " + CargaCircuitoService.MensajeAmigable(ex) });
                }
            }

            if (dto.PublicarEnWeb)
            {
                Avance(94, "Publicando en la web…");
                await _ofertas.RefrescarVitrinaAsync(await _ofertas.ClavesVitrinaAsync(cn, null, r.Ids));
            }
            Avance(99, "Terminando…");

            r.Ok = true;
            r.Servicios = r.Ids.Count;
            r.Mensaje = $"¡Listo! Se cargaron {r.Servicios} {palabra}{(r.Servicios == 1 ? "" : "s")} de {r.Circuito}" + (dto.ConVuelo ? $" con {r.Vuelos} vuelos." : " (sin vuelo).");
            await cn.ExecuteAsync(@"UPDATE Eurovip.Log_Cargas SET estatus = N'Correcto', mensaje = @m, fecha_hora_fin = GETDATE(), registros_insertados = @n, registros_no_insertados = 0 WHERE id = @logId",
                new { m = r.Mensaje, n = r.Servicios, logId });
            return (r, new List<string>());
        }

        private static void Normalizar(CargaOfertaDto d)
        {
            d.Divisa = d.Divisa.Trim().ToUpperInvariant();
            d.Id_Pais_Origen = d.Id_Pais_Origen.Trim().ToUpperInvariant();
            d.Origen = d.Origen.Trim();
            d.Id_Proveedor = d.Id_Proveedor.Trim();
            foreach (var h in d.Hoteles) h.Id_Regimen = h.Id_Regimen.Trim().ToUpperInvariant();
            if (!d.ConVuelo) { d.VuelosIda.Clear(); d.VuelosVuelta.Clear(); }
            foreach (var v in d.VuelosIda.Concat(d.VuelosVuelta)) { v.AeropuertoOrigen = v.AeropuertoOrigen.Trim().ToUpperInvariant(); v.AeropuertoDestino = v.AeropuertoDestino.Trim().ToUpperInvariant(); }
        }
    }
}
