using Dapper;
using EuroVip.Models;
using EuroVip.Repositories;

namespace EuroVip.Services
{
    /// <summary>
    /// CAMBIO 140 (cargas de proveedores): las reglas y los pasos de guardado de Excursiones, Traslados y Cruceros que antes
    /// estaban dentro de cada controlador, sacados aquí SIN cambiar lo que hacen, para que la pantalla de cada módulo y la
    /// publicación de las cargas de proveedores (ProveedoresCarga) usen exactamente el mismo camino.
    ///   • Revisar*Async: las mismas reglas del formulario y del Excel (devuelven el campo y el error, o null).
    ///   • Minimo*Async: precio mínimo de la vista (CAMBIO 139).
    ///   • Guardar*Async: guardar + información para el cliente + imagen por defecto (+ tarjeta de la Home en cruceros).
    /// </summary>
    public class GuardadoProductos
    {
        private const string TipoImagenExcursion = "excursiones";
        private readonly IExcursionRepository _excursiones;
        private readonly ITrasladoRepository _traslados;
        private readonly ICruceroRepository _cruceros;
        private readonly ICircuitoRepository _aud;
        private readonly IImagenesLocales _img;
        private readonly IImagenesPorDefecto _porDefecto;

        public GuardadoProductos(IExcursionRepository excursiones, ITrasladoRepository traslados, ICruceroRepository cruceros, ICircuitoRepository aud,
                                 IImagenesLocales img, IImagenesPorDefecto porDefecto)
        {
            _excursiones = excursiones; _traslados = traslados; _cruceros = cruceros; _aud = aud; _img = img; _porDefecto = porDefecto;
        }

        /// <summary>Precios por debajo del mínimo de la vista: para la respuesta del formulario o para la lista de errores.</summary>
        public sealed record MinimoFallo(string Modulo, decimal Minimo, List<(string? Que, decimal Precio)> Bajos, string Campo)
        {
            public object Respuesta() => PreciosMinimos.Rechazo(Modulo, Minimo, Bajos, Campo);
            public List<string> Mensajes() => Bajos.Select(b => PreciosMinimos.Mensaje(Modulo, Minimo, b.Precio, b.Que)).ToList();
        }

        // =============================== EXCURSIONES ===============================

        /// <summary>Archivo de la foto a partir del nombre ("Excursión Catamarán (Party Boat)" → "ExcursionCatamaranPartyBoat.webp").</summary>
        public string ArchivoExcursion(string? nombre)
        {
            var b = _img.NombreCircuito(nombre ?? "");
            if (b.Length > 120) b = b[..120];
            return b == "" ? "" : b + ".webp";
        }

        /// <summary>
        /// Reglas para guardar una excursión (las mismas en el formulario y en Cargar Excel): nombre con letras, destino que exista
        /// (lo deja escrito como en Eurovip.destino) y que no se repita el nombre en ese destino. Devuelve el campo y el error, o null.
        /// </summary>
        public async Task<(string Campo, string Mensaje)?> RevisarExcursionAsync(ExcursionGuardarDto dto, ISet<string>? monedas = null)
        {
            dto.Nombre = ReglasDatos.Limpiar(dto.Nombre);
            using var cn = _aud.Conexion();
            // FASE 38: moneda que exista, proveedor que exista y código interno único entre todos los productos
            var (moneda, malMoneda) = CatalogoCargas.Moneda(dto.Id_Moneda, monedas ?? await CatalogoCargas.MonedasAsync(cn));
            if (malMoneda is not null) return ("Id_Moneda", malMoneda);
            dto.Id_Moneda = moneda;
            if (await CatalogoCargas.ErrorProveedorAsync(cn, dto.Id_Proveedor) is string malProv) return ("Id_Proveedor", malProv);
            dto.Codigo = CodigoInterno.Limpiar(dto.Codigo);
            var anterior = dto.Id_Excursion is int ida ? await cn.ExecuteScalarAsync<string?>("SELECT codigo FROM Eurovip.servicio_excursion WHERE id_Excursion = @ida", new { ida }) : null;
            if (await CodigoInterno.ErrorAsync(cn, dto.Codigo, "EXCURSION", dto.Id_Excursion?.ToString(), anterior) is string malCodigo) return ("Codigo", malCodigo);
            if (ReglasDatos.Nombre("nombre de excursión", dto.Nombre, 3) is string malNombre) return ("Nombre", malNombre);
            var (malDestino, destino) = await _excursiones.ValidarDestinoAsync(dto.Id_Destino);
            if (malDestino is not null) return ("Id_Destino", malDestino);
            dto.Id_Destino = destino!;
            if (ArchivoExcursion(dto.Nombre) == "") return ("Nombre", "El nombre tiene que llevar letras o números.");
            if (await _excursiones.RepetidaAsync(dto.Nombre, dto.Id_Destino, dto.Id_Excursion) is int otra)
                return ("Nombre", $"Ya existe la excursión «{dto.Nombre}» en {dto.Id_Destino} (n.º {otra}). No se puede repetir.");
            return null;
        }

        /// <summary>CAMBIO 139: precio mínimo de la vista Excursiones.</summary>
        public async Task<MinimoFallo?> MinimoExcursionAsync(ExcursionGuardarDto dto)
        {
            using var cn = _aud.Conexion();
            return await PreciosMinimos.ObtenerAsync(cn, PreciosMinimos.Excursiones) is decimal min && PreciosMinimos.Bajo(min, dto.Precio)
                ? new MinimoFallo(PreciosMinimos.Excursiones, min, new() { (dto.Nombre, dto.Precio!.Value) }, nameof(dto.Precio)) : null;
        }

        /// <summary>Guarda la excursión, su información para el cliente (CAMBIO 130) y, si no tiene foto, la imagen por defecto.</summary>
        public async Task<int> GuardarExcursionAsync(ExcursionGuardarDto dto)
        {
            var id = await _excursiones.GuardarAsync(dto);
            using (var cnInfo = _aud.Conexion()) await InfoCliente.GuardarAsync(cnInfo, "Eurovip.servicio_excursion", id, dto.Info);   // CAMBIO 130
            await _porDefecto.AsegurarAsync("EXCURSION", id.ToString(), dto.Id_Destino, _img.BuscarArchivo(TipoImagenExcursion, ArchivoExcursion(dto.Nombre)) is not null);
            return id;
        }

        // =============================== TRASLADOS ===============================

        /// <summary>
        /// Reglas para guardar un traslado (las mismas en el formulario y en Cargar Excel). Deja los códigos en mayúsculas y el destino
        /// como está en Eurovip.destino. Devuelve el campo y el error, o null si está bien.
        /// </summary>
        public async Task<(string Campo, string Mensaje)?> RevisarTrasladoAsync(TrasladoGuardarDto dto, ISet<string>? monedas = null)
        {
            dto.Nombre = ReglasDatos.Limpiar(dto.Nombre);
            dto.Tipo = (dto.Tipo ?? "").Trim().ToUpperInvariant();
            dto.Categoria = (dto.Categoria ?? "").Trim().ToUpperInvariant();
            dto.Vehiculo = (dto.Vehiculo ?? "").Trim().ToUpperInvariant();
            dto.Cobro = (dto.Cobro ?? "").Trim().ToUpperInvariant();
            dto.Id_Estacion_Origen = ReglasDatos.Limpiar(dto.Id_Estacion_Origen).ToUpperInvariant();

            if (ReglasDatos.Nombre("nombre del traslado", dto.Nombre, 3) is string malNombre) return ("Nombre", malNombre);
            if (!TrasladoValores.Tipos.ContainsKey(dto.Tipo)) return ("Tipo", "Elige Compartido o Privado.");
            if (!TrasladoValores.Categorias.ContainsKey(dto.Categoria)) return ("Categoria", "Elige la categoría.");
            if (!TrasladoValores.Vehiculos.ContainsKey(dto.Vehiculo)) return ("Vehiculo", "Elige el vehículo.");
            if (!TrasladoValores.Cobros.ContainsKey(dto.Cobro)) return ("Cobro", "Elige cómo se cobra.");
            if (dto.Pasajeros_Max < dto.Pasajeros_Min) return ("Pasajeros_Max", "El máximo de pasajeros no puede ser menor que el mínimo.");
            if (dto.Precio_Ida is null && dto.Precio_Ida_Vuelta is null) return ("Precio_Ida_Vuelta", "Escribe al menos un precio: ida y vuelta o solo ida.");
            if (await _traslados.ValidarEstacionAsync(dto.Id_Estacion_Origen) is string malEst) return ("Id_Estacion_Origen", malEst);
            // 20/11/2026: «Llega a» = UN destino (sirve para todos sus hoteles) O uno o varios hoteles: siempre una de las dos y nunca las dos.
            // Con hoteles elegidos el destino sale de ellos (no se pide).
            if (dto.Todos_Hoteles && dto.Hoteles.Count > 0) return ("Hoteles", "Elige una sola opción: un destino (todos sus hoteles) o uno o varios hoteles, no las dos.");
            if (dto.Todos_Hoteles)
            {
                if (string.IsNullOrWhiteSpace(dto.Id_Destino)) return ("Id_Destino", "Elige el destino o, si prefieres, uno o varios hoteles.");
            }
            else
            {
                if (dto.Hoteles.Count == 0) return ("Hoteles", "Elige el hotel o los hoteles a los que llega, o elige un destino.");
                var (destinosH, hallados) = await _traslados.DestinosDeHotelesAsync(dto.Hoteles);
                if (hallados < dto.Hoteles.Distinct().Count()) return ("Hoteles", "Alguno de los hoteles elegidos ya no existe. Quítalo de la lista.");
                if (destinosH.Count > 1) { var lista = string.Join(" y ", destinosH); return ("Hoteles", $"Los hoteles tienen que ser del mismo destino (hay de {lista}). Haz un traslado por cada destino."); }
                if (destinosH.Count == 1) dto.Id_Destino = destinosH[0];
            }
            var (malDestino, destino, _) = await _traslados.ValidarDestinoAsync(dto.Id_Destino ?? "");
            if (malDestino is not null) return ("Id_Destino", malDestino);
            dto.Id_Destino = destino!;
            if (!dto.Todos_Hoteles)
            {
                var fuera = await _traslados.HotelesQueNoSonDelDestinoAsync(dto.Hoteles, dto.Id_Destino);
                if (fuera.Count > 0) return ("Hoteles", $"Estos hoteles no son de {dto.Id_Destino}: {string.Join(", ", fuera)}. Quítalos de la lista.");
            }
            if (await _traslados.RepetidoAsync(dto) is int otro)
                return ("Nombre", $"Ya existe ese traslado (n.º {otro}) con la misma ruta, tipo, categoría y vehículo.");
            // FASE 38: moneda que exista, proveedor que exista y código interno único entre todos los productos
            using var cn = _aud.Conexion();
            var (moneda, malMoneda) = CatalogoCargas.Moneda(dto.Id_Moneda, monedas ?? await CatalogoCargas.MonedasAsync(cn));
            if (malMoneda is not null) return ("Id_Moneda", malMoneda);
            dto.Id_Moneda = moneda;
            if (await CatalogoCargas.ErrorProveedorAsync(cn, dto.Id_Proveedor) is string malProv) return ("Id_Proveedor", malProv);
            dto.Codigo = CodigoInterno.Limpiar(dto.Codigo);
            var anterior = dto.Id_Traslado is int idt ? await cn.ExecuteScalarAsync<string?>("SELECT codigo FROM Eurovip.traslado WHERE id_traslado = @idt", new { idt }) : null;
            if (await CodigoInterno.ErrorAsync(cn, dto.Codigo, "TRASLADO", dto.Id_Traslado?.ToString(), anterior) is string malCodigo) return ("Codigo", malCodigo);
            return null;
        }

        /// <summary>CAMBIO 139: precio mínimo de la vista Traslados (ida y vuelta y solo ida).</summary>
        public async Task<MinimoFallo?> MinimoTrasladoAsync(TrasladoGuardarDto dto)
        {
            using var cn = _aud.Conexion();
            if (await PreciosMinimos.ObtenerAsync(cn, PreciosMinimos.Traslados) is not decimal min) return null;
            var bajos = new List<(string?, decimal)>();
            if (PreciosMinimos.Bajo(min, dto.Precio_Ida_Vuelta)) bajos.Add(("Precio ida y vuelta", dto.Precio_Ida_Vuelta!.Value));
            if (PreciosMinimos.Bajo(min, dto.Precio_Ida)) bajos.Add(("Precio solo ida", dto.Precio_Ida!.Value));
            return bajos.Count == 0 ? null : new MinimoFallo(PreciosMinimos.Traslados, min, bajos,
                PreciosMinimos.Bajo(min, dto.Precio_Ida_Vuelta) ? nameof(dto.Precio_Ida_Vuelta) : nameof(dto.Precio_Ida));
        }

        /// <summary>Guarda el traslado, su información para el cliente (CAMBIO 130) y, si no tiene foto, la imagen por defecto.
        /// antes = el traslado como estaba (al editar), para saber si ya tenía la foto de antes.</summary>
        public async Task<int> GuardarTrasladoAsync(TrasladoGuardarDto dto, string usuario, Traslado? antes = null)
        {
            var id = await _traslados.GuardarAsync(dto, usuario);
            using (var cnInfo = _aud.Conexion()) await InfoCliente.GuardarAsync(cnInfo, "Eurovip.traslado", id, dto.Info);   // CAMBIO 130
            await _porDefecto.AsegurarAsync("TRASLADO", id.ToString(), dto.Id_Destino, !string.IsNullOrWhiteSpace(antes?.Archivo) && _img.BuscarArchivo("traslados", antes!.Archivo!) is not null);
            return id;
        }

        // =============================== CRUCEROS ===============================

        /// <summary>
        /// Las reglas del formulario de cruceros (CrucerosController.Normalizar y Revisar, código interno y moneda) y que no se
        /// repita el título en el barco. Sin el precio mínimo (MinimoCruceroAsync), igual que el formulario lo revisa aparte.
        /// </summary>
        public async Task<(string? Campo, string Mensaje)?> RevisarCruceroAsync(CruceroDto dto, bool conMinimo = false)
        {
            Controllers.CrucerosController.Normalizar(dto);
            if (Controllers.CrucerosController.Revisar(dto, out var campo) is string mal) return (campo, mal);
            if (await _cruceros.RevisarCodigoMonedaAsync(dto) is var (campoCod, malCod)) return (campoCod, malCod);   // FASE 38
            if (conMinimo && await MinimoCruceroAsync(dto) is { } m) return (m.Campo, m.Mensajes()[0]);
            if (await _cruceros.RepetidoAsync(dto.Titulo, dto.Id_Barco, dto.Id_Crucero) is int otro)
                return ("Titulo", $"Ya existe el crucero «{dto.Titulo}» en ese barco (n.º {otro}).");
            return null;
        }

        /// <summary>CAMBIO 139: precio mínimo de la vista Cruceros (cada precio por persona de la tabla).</summary>
        public async Task<MinimoFallo?> MinimoCruceroAsync(CruceroDto dto)
        {
            using var cn = _aud.Conexion();
            if (await PreciosMinimos.ObtenerAsync(cn, PreciosMinimos.Cruceros) is not decimal min) return null;
            var bajos = dto.Precios.Where(p => PreciosMinimos.Bajo(min, p.Precio_Pp))
                .Select(p => ((string?)string.Join(" · ", new[] { p.Salida, p.Cabina, p.Tarifa }.Where(x => !string.IsNullOrWhiteSpace(x))), p.Precio_Pp)).ToList();
            return bajos.Count == 0 ? null : new MinimoFallo(PreciosMinimos.Cruceros, min, bajos, "Precios");
        }

        /// <summary>Guarda el crucero, le pone la imagen por defecto si no tiene y actualiza las tarjetas de la Home de sus categorías.</summary>
        public async Task<int> GuardarCruceroAsync(CruceroDto dto, string usuario)
        {
            var (id, cats) = await _cruceros.GuardarAsync(dto, usuario);
            await _porDefecto.AsegurarAsync("CRUCERO", id.ToString(), null, false);
            await _cruceros.ActualizarInicioAsync(cats);
            return id;
        }
    }
}
