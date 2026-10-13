using Common.Data.Entities;
using EuroVip.Helpers;
using EuroVip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EuroVip.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly IRepository<Usuario> _repositorioUsuario;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UserManager<Usuario> userManager, SignInManager<Usuario> signInManager,
            IRepository<Usuario> repositorioUsuario, ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _repositorioUsuario = repositorioUsuario;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel modelo, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
                return View(modelo);

            var usuario = await _userManager.FindByNameAsync(modelo.User);
            if (usuario is null || !usuario.Activo)
            {
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                return View(modelo);
            }

            var resultado = await _signInManager.PasswordSignInAsync(usuario, modelo.Password, modelo.Remember, lockoutOnFailure: false);
            if (!resultado.Succeeded)
            {
                _logger.LogWarning("Login fallido para {Email}", modelo.User);
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                return View(modelo);
            }

            GuardarSesion(usuario);
            _logger.LogInformation("Login OK {UserName}", usuario.UserName);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel modelo)
        {
            if (!ModelState.IsValid)
                return View(modelo);

            var email = modelo.Email.Trim().ToLowerInvariant();
            var userName = modelo.UserName.Trim();

            if (await _repositorioUsuario.GetUserByEmailAsync(email) is not null)
                ModelState.AddModelError(nameof(modelo.Email), "Ese correo ya tiene un usuario.");
            if (await _repositorioUsuario.ObtenerPorIdAsync(userName, "userName") is not null)
                ModelState.AddModelError(nameof(modelo.UserName), "Ese usuario ya existe.");
            if (!ModelState.IsValid)
                return View(modelo);

            // Arranque del sistema: el primer usuario de Eurovip queda como Superadministrador.
            var esPrimero = !(await _repositorioUsuario.GetAllAsync()).Any();

            var usuario = new Usuario
            {
                Nombre = modelo.Nombre.Trim(),
                Apellido = modelo.Apellido.Trim(),
                Email = email,
                UserName = userName,
                Telefono = string.IsNullOrWhiteSpace(modelo.Telefono) ? null : modelo.Telefono.Trim(),
                Rol = esPrimero ? "Superadministrador" : "Usuario",
                Activo = true
            };

            var resultado = await _userManager.CreateAsync(usuario, modelo.Password);
            if (!resultado.Succeeded)
            {
                foreach (var e in resultado.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return View(modelo);
            }

            _logger.LogInformation("Usuario creado {UserName} ({Rol})", userName, usuario.Rol);

            var creado = await _userManager.FindByNameAsync(email);
            if (creado is null)
            {
                TempData["Registrado"] = "Usuario creado. Ya puedes iniciar sesión.";
                return RedirectToAction(nameof(Login));
            }

            await _signInManager.SignInAsync(creado, isPersistent: false);
            GuardarSesion(creado);
            return RedirectToAction("Index", "Home");
        }

        private void GuardarSesion(Usuario usuario)
        {
            HttpContext.Session.SetString("UserName", usuario.UserName ?? string.Empty);
            HttpContext.Session.SetString("NombreUsuario", $"{usuario.Nombre} {usuario.Apellido}".Trim());
            HttpContext.Session.SetString("Rol", usuario.Rol ?? string.Empty);
            var foto = PerfilFoto.Url(usuario.Imagen);
            if (foto is not null) HttpContext.Session.SetString("Imagen", foto); else HttpContext.Session.Remove("Imagen");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult Denied() => View();
    }
}
