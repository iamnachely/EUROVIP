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

        /// <summary>Revisa que el crucero esté completo y bien antes de guardar (null = todo bien).</summary>
        private static string? Revisar(CruceroDto d, out string? campo)
        {
            campo = null;
            if (ReglasDatos.Nombre("título del crucero", ReglasDatos.Limpiar(d.Titulo), 3) is string malTitulo) { campo = "Titulo"; return malTitulo; }
            foreach (var x in d.Itinerario)
            {
                if (string.IsNullOrWhiteSpace(x.Puerto)) return $"Itinerario: el día {x.Dia} no tiene puerto (o márcalo «En navegación»).";
                if (!x.En_Navegacion && ((!string.IsNullOrWhiteSpace(x.Hora_Llegada) && !EsHora(x.Hora_Llegada)) || (!string.IsNullOrWhiteSpace(x.Hora_Salida) && !EsHora(x.Hora_Salida))))
                    return $"Itinerario: la hora del día {x.Dia} tiene que ser HH:mm (ej. 07:30).";
            }
            if (d.Itinerario.GroupBy(x => x.Dia).FirstOrDefault(g => g.Count() > 1) is { } rep) return $"Itinerario: el día {rep.Key} está repetido.";
            if (d.Itinerario.Any(x => x.Dia < 1 || x.Dia > d.Noches + 1)) return $"Itinerario: los días van del 1 al {d.Noches + 1} (noches + 1).";
            foreach (var s in d.Secciones)
            {
                if (string.IsNullOrWhiteSpace(s.Titulo)) return "A bordo: hay una sección sin título.";
                if (s.Actividades.Any(a => string.IsNullOrWhiteSpace(a.Titulo))) return $"A bordo: en «{s.Titulo}» hay una actividad sin título.";
            }
            if (d.Cubiertas.GroupBy(x => x.Numero).FirstOrDefault(g => g.Count() > 1) is { } cu) return $"Cubiertas: la cubierta {cu.Key} está repetida.";
            foreach (var t in d.Cabinas)
            {
                if (!CruceroValores.CategoriasCabina.ContainsKey(t.Categoria)) return "Cabinas: categoría desconocida.";
                if (string.IsNullOrWhiteSpace(t.Nombre)) return "Cabinas: hay un tipo de cabina sin nombre.";
                if (t.Capacidad_Min > t.Capacidad_Max) return $"Cabinas: en «{t.Nombre}» la capacidad mínima es mayor que la máxima.";
                if (t.Cabinas.Any(k => string.IsNullOrWhiteSpace(k.Numero))) return $"Cabinas: en «{t.Nombre}» hay una cabina sin número.";
                if (t.Cabinas.GroupBy(k => k.Numero.Trim()).FirstOrDefault(g => g.Count() > 1) is { } kr) return $"Cabinas: la cabina {kr.Key} de «{t.Nombre}» está repetida.";
                if (t.Cabinas.Any(k => k.Cubierta is short n && d.Cubiertas.All(c => c.Numero != n))) return $"Cabinas: en «{t.Nombre}» hay una cabina en una cubierta que no está en la lista de cubiertas.";
            }
            if (d.Cabinas.GroupBy(x => x.Clave).Any(g => g.Count() > 1)) return "Cabinas: claves repetidas (recarga la página).";
            if (d.Salidas.GroupBy(x => x.Fecha.Date).FirstOrDefault(g => g.Count() > 1) is { } sr) return $"Fechas: la salida del {sr.Key:dd/MM/yyyy} está repetida.";
            foreach (var f in d.Tarifas)
                if (string.IsNullOrWhiteSpace(f.Codigo) || string.IsNullOrWhiteSpace(f.Nombre)) return "Tarifas: cada tarifa necesita código y nombre.";
            if (d.Tarifas.GroupBy(x => x.Codigo.Trim().ToUpperInvariant()).FirstOrDefault(g => g.Count() > 1) is { } tr) return $"Tarifas: el código «{tr.Key}» está repetido.";
            if (d.Precios.Any(p => p.Precio_Pp < 0)) return "Precios: no puede haber precios negativos.";
            return null;
        }

        private static bool EsHora(string? h) => TimeSpan.TryParseExact((h ?? "").Trim(), [@"hh\:mm", @"h\:mm"], System.Globalization.CultureInfo.InvariantCulture, out var t) && t < TimeSpan.FromDays(1);

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] CruceroDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key, x => x.Value!.Errors.First().ErrorMessage);
                return BadRequest(new { mensaje = errores.Values.First(), errores });
            }
            dto.Titulo = ReglasDatos.Limpiar(dto.Titulo);
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

        /// <summary>Foto suelta (actividad, cabina, cubierta, día del itinerario, logo de naviera, barco o categoría) → URL.</summary>
        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12_000_000)]
        public async Task<IActionResult> SubirImagen([FromForm] string tipo, [FromForm] int? id, IFormFile? archivo)
        {
            var carpeta = tipo switch
            {
                "actividad" or "cabina" or "cubierta" or "itinerario" => $"cruceros/{(id is > 0 ? id.ToString() : "nuevos")}/{tipo}",
                "naviera" => "cruceros/navieras",
                "barco" => "cruceros/barcos",
                "categoria" => "cruceros/categorias",
                _ => null
            };
            if (carpeta is null) return BadRequest(new { mensaje = "Tipo de foto desconocido." });
            if (archivo is null || archivo.Length == 0) return BadRequest(new { mensaje = "Elige una imagen." });
            if (archivo.Length > 10_000_000) return BadRequest(new { mensaje = "La imagen pesa más de 10 MB." });
            if (await ImagenSegura.ExtensionAsync(archivo) is null) return BadRequest(new { mensaje = "El archivo no es una imagen JPG, PNG o WebP." });
            try
            {
                await using var st = archivo.OpenReadStream();
                var url = await _mm.GuardarArchivoAsync(carpeta, st);
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
