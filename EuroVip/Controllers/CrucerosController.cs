using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using EuroVip.Services;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Mantenimiento de cruceros (Eurovip.crucero y sus tablas, documento Database/esquema-cruceros.md):
    /// buscar, ver (como la web), crear, editar, duplicar, activar/desactivar y eliminar; con itinerario, "a bordo",
    /// cabinas y cubiertas, fechas, tarifas y precios. Catálogos: navieras, barcos y categorías.
    /// Fotos: portada, banner y galería en Eurovip.multimedia (tipo CRUCERO); las demás (actividades, cabinas, cubiertas,
    /// logos…) en la carpeta de fotos {Multimedia:CarpetaFisica}/cruceros/… La Home tiene una tarjeta por categoría.
    /// </summary>
    public class CrucerosController : Controller
    {
        private readonly ICruceroRepository _repo;
        private readonly ICircuitoRepository _aud;
        private readonly IMultimedia _mm;
        private readonly ILogger<CrucerosController> _logger;

        public CrucerosController(ICruceroRepository repo, ICircuitoRepository aud, IMultimedia mm, ILogger<CrucerosController> logger)
        {
            _repo = repo; _aud = aud; _mm = mm; _logger = logger;
        }

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var cruceros = await _repo.ListarAsync();
            var portadas = await _mm.PortadasAsync("CRUCERO");
            foreach (var c in cruceros) c.Imagen = portadas.TryGetValue(c.Id_Crucero.ToString(), out var u) ? u : null;
            return Json(new
            {
                cruceros,
                navieras = await _repo.NavierasAsync(),
                barcos = await _repo.BarcosAsync(),
                categorias = await _repo.CategoriasAsync(),
                categoriasCabina = CruceroValores.CategoriasCabina
            });
        }

        [HttpGet]
        public async Task<IActionResult> Obtener(int id)
        {
            var c = await _repo.ObtenerAsync(id);
            return c is null ? NotFound(new { mensaje = "Ese crucero ya no existe." }) : Json(c);
        }

        private BadRequestObjectResult Mal(string mensaje, string? campo = null) =>
            BadRequest(new { mensaje, errores = campo is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [campo] = mensaje } });

        private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : ReglasDatos.Limpiar(s);

        /// <summary>
        /// Casi todo es opcional: lo que viene vacío se quita o se completa solo (un día sin puerto, una actividad sin
        /// título, una tarifa sin código…) en vez de dar error. Las horas valen "7:30", "07:30" o "07:30:00".
        /// </summary>
        private static void Normalizar(CruceroDto d)
        {
            d.Titulo = ReglasDatos.Limpiar(d.Titulo);
            d.Naviera_Nombre = Texto(d.Naviera_Nombre); d.Barco_Nombre = Texto(d.Barco_Nombre);
            d.Puerto_Salida = Texto(d.Puerto_Salida); d.Puerto_Llegada = Texto(d.Puerto_Llegada) ?? d.Puerto_Salida;
            d.Categorias_Nuevas = d.Categorias_Nuevas.Select(Texto).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            d.Itinerario = d.Itinerario
                .Where(x => x.En_Navegacion || Texto(x.Puerto) is not null || Texto(x.Hora_Llegada) is not null || Texto(x.Hora_Salida) is not null || Texto(x.Descripcion) is not null || Texto(x.Imagen) is not null)
                .ToList();
            foreach (var x in d.Itinerario)
            {
                x.Puerto = x.En_Navegacion ? Texto(x.Puerto) ?? "En navegación" : Texto(x.Puerto) ?? "Por definir";
                x.Hora_Llegada = x.En_Navegacion ? null : HoraTexto(x.Hora_Llegada);
                x.Hora_Salida = x.En_Navegacion ? null : HoraTexto(x.Hora_Salida);
            }

            foreach (var s in d.Secciones)
                s.Actividades = s.Actividades.Where(a => Texto(a.Titulo) is not null || Texto(a.Descripcion) is not null || Texto(a.Imagen) is not null)
                                             .Select(a => { a.Titulo = Texto(a.Titulo) ?? "Actividad"; return a; }).ToList();
            d.Secciones = d.Secciones.Where(s => Texto(s.Titulo) is not null || s.Actividades.Count > 0)
                                     .Select(s => { s.Titulo = Texto(s.Titulo) ?? "A bordo"; return s; }).ToList();

            foreach (var c in d.Cabinas)
            {
                if (!CruceroValores.CategoriasCabina.ContainsKey(c.Categoria ?? "")) c.Categoria = "INTERIOR";
                c.Nombre = Texto(c.Nombre) ?? CruceroValores.CategoriasCabina[c.Categoria];
                c.Cabinas = c.Cabinas.Where(k => Texto(k.Numero) is not null).ToList();
            }
            foreach (var f in d.Tarifas)
            {
                f.Codigo = Texto(f.Codigo) ?? Texto(f.Nombre) ?? "BEST_PRICE";
                f.Nombre = Texto(f.Nombre) ?? f.Codigo;
            }
            if (d.Noches is null && d.Itinerario.Count > 1) d.Noches = (short)Math.Min(180, d.Itinerario.Max(x => x.Dia) - 1);
        }

        /// <summary>Lo único que de verdad impide guardar (null = todo bien).</summary>
        private static string? Revisar(CruceroDto d, out string? campo)
        {
            campo = null;
            if (ReglasDatos.Nombre("título del crucero", d.Titulo, 3) is string malTitulo) { campo = "Titulo"; return malTitulo; }
            if (d.Id_Barco is null && d.Barco_Nombre is null && d.Naviera_Nombre is not null) return "Escribiste la naviera: escribe también el barco (o borra la naviera).";
            foreach (var x in d.Itinerario)
                if (x.Hora_Llegada == "?" || x.Hora_Salida == "?") return $"Itinerario: la hora del día {x.Dia} no se entiende. Escríbela así: 07:30.";
            if (d.Itinerario.GroupBy(x => x.Dia).FirstOrDefault(g => g.Count() > 1) is { } rep) return $"Itinerario: el día {rep.Key} está dos veces.";
            if (d.Itinerario.Any(x => x.Dia < 1 || x.Dia > 200)) return "Itinerario: el número de día tiene que ser de 1 a 200.";
            if (d.Cubiertas.GroupBy(x => x.Numero).FirstOrDefault(g => g.Count() > 1) is { } cu) return $"Cubiertas: la cubierta {cu.Key} está dos veces.";
            foreach (var t in d.Cabinas)
            {
                if (t.Capacidad_Min > t.Capacidad_Max) return $"Cabinas: en «{t.Nombre}» la capacidad mínima es mayor que la máxima.";
                if (t.Cabinas.GroupBy(k => k.Numero!.Trim()).FirstOrDefault(g => g.Count() > 1) is { } kr) return $"Cabinas: la cabina {kr.Key} de «{t.Nombre}» está dos veces.";
            }
            if (d.Cabinas.GroupBy(x => x.Clave).Any(g => g.Count() > 1)) return "Cabinas: claves repetidas (recarga la página).";
            if (d.Salidas.GroupBy(x => x.Fecha.Date).FirstOrDefault(g => g.Count() > 1) is { } sr) return $"Fechas: la salida del {sr.Key:dd/MM/yyyy} está dos veces.";
            if (d.Tarifas.GroupBy(x => x.Codigo!.Trim().ToUpperInvariant()).FirstOrDefault(g => g.Count() > 1) is { } tr) return $"Tarifas: el código «{tr.Key}» está dos veces.";
            if (d.Precios.Any(p => p.Precio_Pp < 0)) return "Precios: no puede haber precios negativos.";
            return null;
        }

        /// <summary>"7:30", "07:30" o "07:30:00" → "07:30"; vacío → null; si no se entiende → "?".</summary>
        private static string? HoraTexto(string? h)
        {
            h = (h ?? "").Trim();
            if (h == "") return null;
            return TimeSpan.TryParseExact(h, [@"hh\:mm", @"h\:mm", @"hh\:mm\:ss", @"h\:mm\:ss"], System.Globalization.CultureInfo.InvariantCulture, out var t) && t < TimeSpan.FromDays(1)
                ? t.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture) : "?";
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] CruceroDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key, x => x.Value!.Errors.First().ErrorMessage);
                return BadRequest(new { mensaje = errores.Values.First(), errores });
            }
            Normalizar(dto);
            if (Revisar(dto, out var campo) is string mal) return Mal(mal, campo);
            if (await _repo.RepetidoAsync(dto.Titulo, dto.Id_Barco, dto.Id_Crucero) is int otro)
                return Mal($"Ya existe el crucero «{dto.Titulo}» en ese barco (n.º {otro}).", "Titulo");
            try
            {
                var antes = dto.Id_Crucero is int idA ? await _repo.ObtenerAsync(idA) : null;
                if (dto.Id_Crucero is not null && antes is null) return NotFound(new { mensaje = "Ese crucero ya no existe. Recarga la página." });
                var (id, cats) = await _repo.GuardarAsync(dto, User.Identity?.Name ?? "desconocido");
                await _repo.ActualizarInicioAsync(cats);
                await Auditar(antes is null ? "Crucero_Crear" : "Crucero_Editar", id, new { dto.Titulo, dto.Id_Barco, dto.Noches, salidas = dto.Salidas.Count, cabinas = dto.Cabinas.Count, precios = dto.Precios.Count });
                return Json(new { ok = true, id, mensaje = antes is null ? $"¡Listo! Se creó «{dto.Titulo}»." : "Cambios guardados." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando el crucero {Titulo}", dto.Titulo);
                return StatusCode(500, new { mensaje = "No se pudo guardar: " + ex.Message });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicar(int id)
        {
            var c = await _repo.ObtenerAsync(id);
            if (c is null) return NotFound(new { mensaje = "Ese crucero ya no existe." });
            var titulo = c.Titulo + " (copia)";
            for (var i = 2; await _repo.RepetidoAsync(titulo, c.Id_Barco, null) is not null; i++) titulo = $"{c.Titulo} (copia {i})";
            c.Id_Crucero = null; c.Titulo = titulo; c.Activo = false;   // la copia empieza desactivada (no sale dos veces en la web)
            var (nuevo, cats) = await _repo.GuardarAsync(c, User.Identity?.Name ?? "desconocido");
            await _repo.ActualizarInicioAsync(cats);
            await Auditar("Crucero_Duplicar", nuevo, new { de = id, titulo });
            return Json(new { ok = true, id = nuevo, mensaje = $"Se duplicó como «{titulo}» (desactivado, sin fotos de portada/galería). Cámbiale lo que haga falta y actívalo." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Activar(int id, bool activo)
        {
            var cats = await _repo.ActivarAsync(id, activo);
            if (cats is null) return NotFound(new { mensaje = "Ese crucero ya no existe." });
            await _repo.ActualizarInicioAsync(cats);
            await Auditar(activo ? "Crucero_Activar" : "Crucero_Desactivar", id, new { activo });
            return Json(new { ok = true, mensaje = activo ? "Crucero activo: sale en la web." : "Crucero desactivado: ya no sale en la web." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var c = await _repo.ObtenerAsync(id);
            if (c is null) return NotFound(new { mensaje = "Ese crucero ya no existe." });
            var cats = await _repo.EliminarAsync(id) ?? new List<int>();
            var fotos = await _mm.EliminarProductoAsync("CRUCERO", id.ToString());
            await _repo.ActualizarInicioAsync(cats);
            await Auditar("Crucero_Eliminar", id, new { c.Titulo, fotos });
            return Json(new { ok = true, mensaje = $"Se eliminó «{c.Titulo}»." });
        }

        /// <summary>
        /// Foto suelta (actividad, cabina, cubierta, día del itinerario, logo de naviera, barco o categoría) → URL.
        /// Nombre legible: crucero/{crucero}/actividades/{actividad}.webp, crucero/naviera/{naviera}.webp…
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> SubirImagen([FromForm] string tipo, [FromForm] int? id, [FromForm] string? crucero, [FromForm] string? nombre, IFormFile? archivo)
        {
            var delCrucero = Multimedia.NombreArchivo(crucero, id is > 0 ? "crucero-" + id : "crucero-nuevo");
            var carpeta = tipo switch
            {
                "actividad" => $"crucero/{delCrucero}/actividades",
                "cabina" => $"crucero/{delCrucero}/cabinas",
                "cubierta" => $"crucero/{delCrucero}/cubiertas",
                "itinerario" => $"crucero/{delCrucero}/itinerario",
                "naviera" => "crucero/naviera",
                "barco" => "crucero/barco",
                "categoria" => "crucero/categoria",
                _ => null
            };
            if (carpeta is null) return BadRequest(new { mensaje = "Tipo de foto desconocido." });
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > 10_000_000) return BadRequest(new { mensaje = "La imagen pesa más de 10 MB." });
            if (await ImagenSegura.ExtensionAsync(archivo) is null) return BadRequest(new { mensaje = "El archivo no es una imagen JPG, PNG o WebP." });
            try
            {
                await using var st = archivo.OpenReadStream();
                var url = await _mm.GuardarArchivoAsync(carpeta, nombre ?? Path.GetFileNameWithoutExtension(archivo.FileName), st);
                await Auditar("Crucero_Imagen", id ?? 0, new { tipo, url });
                return Json(new { ok = true, url, mensaje = "Foto subida. Se guarda en el crucero al pulsar Guardar." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { mensaje = ex.Message }); }
        }

        // ---------------- catálogos ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarNaviera([FromBody] CruceroNaviera n)
        {
            if (!ModelState.IsValid) return Mal(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
            n.Nombre = ReglasDatos.Limpiar(n.Nombre);
            if (ReglasDatos.Nombre("nombre de la naviera", n.Nombre, 2) is string mal) return Mal(mal);
            if ((await _repo.NavierasAsync()).Any(x => x.Id_Naviera != n.Id_Naviera && string.Equals(x.Nombre, n.Nombre, StringComparison.OrdinalIgnoreCase)))
                return Mal($"Ya existe la naviera «{n.Nombre}».");
            try { var id = await _repo.GuardarNavieraAsync(n); await Auditar("Crucero_Naviera", id, n); return Json(new { ok = true, id, mensaje = "Naviera guardada." }); }
            catch (InvalidOperationException ex) { return Mal(ex.Message); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarBarco([FromBody] CruceroBarco b)
        {
            if (!ModelState.IsValid) return Mal(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
            b.Nombre = ReglasDatos.Limpiar(b.Nombre);
            if (ReglasDatos.Nombre("nombre del barco", b.Nombre, 2) is string mal) return Mal(mal);
            if ((await _repo.BarcosAsync()).Any(x => x.Id_Barco != b.Id_Barco && x.Id_Naviera == b.Id_Naviera && string.Equals(x.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase)))
                return Mal($"Esa naviera ya tiene el barco «{b.Nombre}».");
            try { var id = await _repo.GuardarBarcoAsync(b); await Auditar("Crucero_Barco", id, new { b.Id_Naviera, b.Nombre }); return Json(new { ok = true, id, mensaje = "Barco guardado." }); }
            catch (InvalidOperationException ex) { return Mal(ex.Message); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarCategoria([FromBody] CruceroCategoria c)
        {
            if (!ModelState.IsValid) return Mal(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
            c.Nombre = ReglasDatos.Limpiar(c.Nombre);
            if (ReglasDatos.Nombre("nombre de la categoría", c.Nombre, 2) is string mal) return Mal(mal);
            if ((await _repo.CategoriasAsync()).Any(x => x.Id_Categoria != c.Id_Categoria && string.Equals(x.Nombre, c.Nombre, StringComparison.OrdinalIgnoreCase)))
                return Mal($"Ya existe la categoría «{c.Nombre}».");
            try
            {
                var id = await _repo.GuardarCategoriaAsync(c);
                await _repo.ActualizarInicioAsync(new[] { id });   // nombre, foto o visible de su tarjeta en la Home
                await Auditar("Crucero_Categoria", id, c);
                return Json(new { ok = true, id, mensaje = "Categoría guardada." });
            }
            catch (InvalidOperationException ex) { return Mal(ex.Message); }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCatalogo(string que, int id)
        {
            var error = que switch
            {
                "naviera" => await _repo.EliminarNavieraAsync(id),
                "barco" => await _repo.EliminarBarcoAsync(id),
                "categoria" => await _repo.EliminarCategoriaAsync(id),
                _ => "Catálogo desconocido."
            };
            if (error is not null) return Mal(error);
            if (que == "categoria") await _repo.ActualizarInicioAsync(new[] { id });   // quita su tarjeta de la Home
            await Auditar("Crucero_Eliminar_" + que, id, new { id });
            return Json(new { ok = true, mensaje = "Eliminado." });
        }

        private Task Auditar(string tipo, int id, object cambios) =>
            _aud.AuditarAsync(tipo, new[] { id }, cambios, User.Identity?.Name ?? "desconocido",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
