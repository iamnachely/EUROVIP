using Common.Data.Entities;
using EuroVip.Helpers;
using EuroVip.Models;
using EuroVip.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace EuroVip.Controllers
{
    /// <summary>
    /// Usuarios y accesos (equivale a /Usuario2, /Usuario2/Accesos, /Usuario2/Modulos y /Rol de BackTiTan, todo en una pantalla).
    /// Reglas de seguridad: nadie se desactiva, se borra ni se quita el rol a sí mismo; siempre queda al menos un
    /// Superadministrador activo; solo un Superadministrador da o quita ese rol. Las contraseñas nunca se guardan en la auditoría.
    /// </summary>
    [AccesoEstricto]
    public class UsuariosController : Controller
    {
        private readonly IUsuariosRepository _repo;
        private readonly UserManager<Usuario> _users;
        private readonly ICircuitoRepository _db;

        public UsuariosController(IUsuariosRepository repo, UserManager<Usuario> users, ICircuitoRepository db)
        {
            _repo = repo;
            _users = users;
            _db = db;
        }

        private PermisosUsuario Yo => HttpContext.Items[AccesoModuloFilter.ClavePermisos] as PermisosUsuario ?? new PermisosUsuario();
        private string YoUser => User.Identity?.Name ?? "";

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> Datos()
        {
            var usuarios = await _repo.ListarAsync();
            return Json(new
            {
                usuarios, modulos = await _repo.ModulosAsync(), roles = await _repo.RolesAsync(), matriz = await _repo.MatrizAsync(),
                yo = YoUser, esSuper = Yo.EsSuper
            });
        }

        [HttpGet]
        public async Task<IActionResult> Sugerir(string nombre, string apellido)
        {
            string Limpio(string s) => new string(s.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => char.IsAsciiLetterOrDigit(c)).ToArray());
            var n = Limpio(nombre ?? ""); var a = Limpio(apellido ?? "");
            if (n.Length == 0 || a.Length == 0) return Json(Array.Empty<string>());
            var existentes = (await _repo.ListarAsync()).Select(u => u.UserName.ToLowerInvariant()).ToHashSet();
            var bases = new[] { n[..1] + a, n + a[..1], n + a, a + n[..1], n }.Select(x => x.Length > 15 ? x[..15] : x).Distinct();
            var res = new List<string>();
            foreach (var b in bases)
            {
                var c = b;
                for (var i = 1; existentes.Contains(c.ToLowerInvariant()) && i < 100; i++) c = (b.Length > 13 ? b[..13] : b) + i;
                if (!existentes.Contains(c.ToLowerInvariant())) res.Add(c);
                if (res.Count == 4) break;
            }
            return Json(res);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] UsuarioEditarDto u)
        {
            if (!ModelState.IsValid) return Error();
            u.Nombre = u.Nombre.Trim(); u.Apellido = u.Apellido.Trim(); u.UserName = u.UserName.Trim();
            u.Email = u.Email.Trim().ToLowerInvariant(); u.Telefono = string.IsNullOrWhiteSpace(u.Telefono) ? null : u.Telefono.Trim();
            if (!(await _repo.RolesAsync()).Contains(u.Rol, StringComparer.OrdinalIgnoreCase)) return Mal("Ese rol no existe.");

            var dup = await _repo.DuplicadoAsync(u.Email, u.UserName, u.CodigoUsuario);
            if (dup.Email) return Mal("Ese correo ya lo tiene otro usuario.");
            if (dup.Usuario) return Mal("Ese nombre de usuario ya existe.");

            if (u.CodigoUsuario == 0)
            {
                if (Roles.EsSuper(u.Rol) && !Yo.EsSuper) return Mal("Solo un Superadministrador puede crear otro Superadministrador.");
                var generada = string.IsNullOrWhiteSpace(u.Password);
                var clave = generada ? Generar() : u.Password!;
                var dummy = new Usuario { UserName = u.UserName, Email = u.Email, Nombre = u.Nombre, Apellido = u.Apellido, Clave = "" };
                var err = await ValidarClave(dummy, clave);
                if (err is not null) return Mal(err);
                var id = await _repo.CrearAsync(u, _users.PasswordHasher.HashPassword(dummy, clave));
                await Auditar("Usuario_Crear", new { id, u.UserName, u.Email, u.Rol, u.Activo, modulos = u.Modulos });
                return Json(new { ok = true, id, clave = generada ? clave : null, mensaje = $"Usuario {u.UserName} creado." });
            }

            var antes = await _repo.ObtenerAsync(u.CodigoUsuario);
            if (antes is null) return NotFound(new { mensaje = "No encontramos ese usuario." });
            var cambiaSuper = Roles.EsSuper(antes.Rol) != Roles.EsSuper(u.Rol);
            if ((Roles.EsSuper(antes.Rol) || cambiaSuper) && !Yo.EsSuper) return Mal("Solo un Superadministrador puede cambiar a un Superadministrador.");
            var soyYo = string.Equals(antes.UserName, YoUser, StringComparison.OrdinalIgnoreCase);
            if (soyYo && !u.Activo) return Mal("No puedes desactivarte a ti misma/o.");
            if (soyYo && cambiaSuper) return Mal("No puedes quitarte el rol de Superadministrador. Pídeselo a otro Superadministrador.");
            if (Roles.EsSuper(antes.Rol) && antes.Activo && (!u.Activo || cambiaSuper) && await _repo.SuperActivosAsync(antes.CodigoUsuario) == 0)
                return Mal("Tiene que quedar al menos un Superadministrador activo.");

            await _repo.ActualizarAsync(u, antes.UserName);
            if (soyYo)
            {
                HttpContext.Session.SetString("NombreUsuario", $"{u.Nombre} {u.Apellido}");
                HttpContext.Session.SetString("UserName", u.UserName);
            }
            await Auditar("Usuario_Editar", new { u.CodigoUsuario, antes = new { antes.UserName, antes.Email, antes.Rol, antes.Activo }, ahora = new { u.UserName, u.Email, u.Rol, u.Activo } });
            return Json(new { ok = true, id = u.CodigoUsuario, mensaje = soyYo && u.UserName != antes.UserName
                ? "Guardado. Cambiaste tu usuario: vuelve a iniciar sesión." : "Usuario guardado." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Clave([FromBody] ClaveDto d)
        {
            var u = await _repo.ObtenerAsync(d.CodigoUsuario);
            if (u is null) return NotFound(new { mensaje = "No encontramos ese usuario." });
            if (Roles.EsSuper(u.Rol) && !Yo.EsSuper) return Mal("Solo un Superadministrador puede cambiar la contraseña de otro Superadministrador.");
            var generada = string.IsNullOrWhiteSpace(d.Password);
            var clave = generada ? Generar() : d.Password!;
            var dummy = new Usuario { UserName = u.UserName, Email = u.Email, Nombre = u.Nombre, Apellido = u.Apellido, Clave = "" };
            var err = await ValidarClave(dummy, clave);
            if (err is not null) return Mal(err);
            await _repo.CambiarClaveAsync(u.CodigoUsuario, _users.PasswordHasher.HashPassword(dummy, clave));
            await Auditar("Usuario_Clave", new { u.CodigoUsuario, u.UserName, generada });
            return Json(new { ok = true, clave = generada ? clave : null, mensaje = $"Contraseña de {u.UserName} cambiada." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Estado(int id, bool activo)
        {
            var u = await _repo.ObtenerAsync(id);
            if (u is null) return NotFound(new { mensaje = "No encontramos ese usuario." });
            if (!activo && string.Equals(u.UserName, YoUser, StringComparison.OrdinalIgnoreCase)) return Mal("No puedes desactivarte a ti misma/o.");
            if (Roles.EsSuper(u.Rol) && !Yo.EsSuper) return Mal("Solo un Superadministrador puede cambiar a un Superadministrador.");
            if (!activo && Roles.EsSuper(u.Rol) && await _repo.SuperActivosAsync(id) == 0) return Mal("Tiene que quedar al menos un Superadministrador activo.");
            await _repo.EstadoAsync(id, activo);
            await Auditar("Usuario_Estado", new { id, u.UserName, activo });
            return Json(new { ok = true, mensaje = activo ? $"{u.UserName} puede entrar de nuevo." : $"{u.UserName} ya no puede entrar." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            var u = await _repo.ObtenerAsync(id);
            if (u is null) return NotFound(new { mensaje = "No encontramos ese usuario." });
            if (string.Equals(u.UserName, YoUser, StringComparison.OrdinalIgnoreCase)) return Mal("No puedes borrarte a ti misma/o.");
            if (Roles.EsSuper(u.Rol) && !Yo.EsSuper) return Mal("Solo un Superadministrador puede borrar a un Superadministrador.");
            if (Roles.EsSuper(u.Rol) && await _repo.SuperActivosAsync(id) == 0) return Mal("Tiene que quedar al menos un Superadministrador activo.");
            await _repo.EliminarAsync(id, u.UserName);
            await Auditar("Usuario_Eliminar", new { id, u.UserName, u.Email, u.Rol });
            return Json(new { ok = true, mensaje = $"Usuario {u.UserName} eliminado." });
        }

        // ---------------- accesos ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Accesos([FromBody] AccesosDto d)
        {
            var u = (await _repo.ListarAsync()).FirstOrDefault(x => string.Equals(x.UserName, d.Usuario, StringComparison.OrdinalIgnoreCase));
            if (u is null) return NotFound(new { mensaje = "No encontramos ese usuario." });
            var err = await NoQuitarmeUsuarios(u.UserName, d.Modulos);
            if (err is not null) return Mal(err);
            await _repo.GuardarAccesosAsync(u.UserName, d.Modulos);
            await Auditar("Usuario_Accesos", new { u.UserName, modulos = d.Modulos });
            return Json(new { ok = true, mensaje = $"Accesos de {u.UserName} guardados ({d.Modulos.Count} módulo(s))." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Acceso(string usuario, int idModulo, bool acceso)
        {
            var u = (await _repo.ListarAsync()).FirstOrDefault(x => string.Equals(x.UserName, usuario, StringComparison.OrdinalIgnoreCase));
            if (u is null) return NotFound(new { mensaje = "No encontramos ese usuario." });
            if (!acceso)
            {
                var actuales = await _repo.AccesosAsync(u.UserName);
                var err = await NoQuitarmeUsuarios(u.UserName, actuales.Where(x => x != idModulo));
                if (err is not null) return Mal(err);
            }
            await _repo.CambiarAccesoAsync(u.UserName, idModulo, acceso);
            await Auditar("Usuario_Acceso", new { u.UserName, idModulo, acceso });
            return Json(new { ok = true });
        }

        /// <summary>Evita que alguien que no es Superadministrador se quite a sí mismo "Usuarios y accesos" y se quede fuera.</summary>
        private async Task<string?> NoQuitarmeUsuarios(string usuario, IEnumerable<int> modulos)
        {
            if (Yo.EsSuper || !string.Equals(usuario, YoUser, StringComparison.OrdinalIgnoreCase)) return null;
            var id = (await _repo.ModulosAsync()).FirstOrDefault(m => m.Clave == Models.Modulos.Usuarios)?.Id_Modulo;
            return id is not null && !modulos.Contains(id.Value) ? "No puedes quitarte el acceso a \"Usuarios y accesos\": te quedarías fuera." : null;
        }

        // ---------------- módulos ----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Modulo([FromBody] ModuloEditarDto m)
        {
            if (!ModelState.IsValid) return Error();
            if (!Yo.EsSuper) return Mal("Solo un Superadministrador puede cambiar la lista de módulos.");
            var id = await _repo.GuardarModuloAsync(m);
            await Auditar(m.Id_Modulo > 0 ? "Modulo_Editar" : "Modulo_Crear", m);
            return Json(new { ok = true, id, mensaje = "Módulo guardado." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarModulo(int id)
        {
            if (!Yo.EsSuper) return Mal("Solo un Superadministrador puede borrar módulos.");
            if (!await _repo.EliminarModuloAsync(id)) return Mal("Ese módulo es de Eurovip (lo usa el programa) y no se puede borrar. Solo se borran los creados a mano.");
            await Auditar("Modulo_Eliminar", new { id });
            return Json(new { ok = true, mensaje = "Módulo eliminado." });
        }

        // ---------------- ayudas ----------------
        private async Task<string?> ValidarClave(Usuario u, string clave)
        {
            foreach (var v in _users.PasswordValidators)
            {
                var r = await v.ValidateAsync(_users, u, clave);
                if (!r.Succeeded) return string.Join(" ", r.Errors.Select(e => e.Description));
            }
            return null;
        }

        /// <summary>Contraseña segura de 12 caracteres (mayúscula, minúscula, número y símbolo), sin letras que se confunden.</summary>
        private static string Generar()
        {
            const string may = "ABCDEFGHJKLMNPQRSTUVWXYZ", min = "abcdefghijkmnpqrstuvwxyz", num = "23456789", sim = "!@#$%*?";
            var todos = may + min + num + sim;
            var c = new List<char> { Uno(may), Uno(min), Uno(num), Uno(sim) };
            while (c.Count < 12) c.Add(Uno(todos));
            return new string(c.OrderBy(_ => RandomNumberGenerator.GetInt32(1000)).ToArray());
            static char Uno(string s) => s[RandomNumberGenerator.GetInt32(s.Length)];
        }

        private IActionResult Mal(string mensaje) => BadRequest(new { mensaje });
        private IActionResult Error() => BadRequest(new { mensaje = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage });

        private Task Auditar(string tipo, object cambios) =>
            _db.AuditarAsync(tipo, Array.Empty<int>(), cambios, YoUser,
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
    }
}
