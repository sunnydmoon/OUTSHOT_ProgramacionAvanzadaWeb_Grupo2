using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Outshot.Data;
using Outshot.Models;
using Outshot.ViewModels;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Outshot.Controllers
{
    public class AutenticacionController : Controller
    {
        private readonly OutshotContext _context;
        private readonly PasswordHasher<Usuario> _passwordHasher;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AutenticacionController> _logger;

        public AutenticacionController(
            OutshotContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AutenticacionController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;

            _passwordHasher = new PasswordHasher<Usuario>();
        }

        // Mostrar formulario de registro
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Registro()
        {
            await CargarPaisesAsync();
            return View();
        }

        // Procesar registro de cliente
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(RegistroViewModel modelo)
        {
            var paises = await ObtenerPaisesAsync();

            if (paises == null)
            {
                ModelState.AddModelError(
                    "Pais",
                    "No fue posible cargar los países. Verificá tu conexión e intentá nuevamente.");

                ViewBag.Paises = new List<string>();
                ViewBag.ErrorPaises = true;

                return View(modelo);
            }

            if (string.IsNullOrWhiteSpace(modelo.Pais) ||
                !paises.Contains(modelo.Pais))
            {
                ModelState.AddModelError(
                    "Pais",
                    "Seleccioná un país válido de la lista.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Paises = paises;
                ViewBag.ErrorPaises = false;
                return View(modelo);
            }

            string correo = modelo.Correo.Trim().ToLower();

            bool correoExiste = await _context.Usuarios
                .AnyAsync(u => u.Correo.ToLower() == correo);

            bool clienteExiste = await _context.Clientes
                .AnyAsync(c => c.Correo.ToLower() == correo);

            if (correoExiste || clienteExiste)
            {
                ModelState.AddModelError(
                    "Correo",
                    "Ya existe una cuenta registrada con este correo electrónico.");

                ViewBag.Paises = paises;
                ViewBag.ErrorPaises = false;

                return View(modelo);
            }

            var usuario = new Usuario
            {
                Nombre = modelo.Nombre.Trim(),
                Correo = correo,
                Rol = "Cliente",
                Activo = true
            };

            usuario.Contrasena = _passwordHasher.HashPassword(
                usuario,
                modelo.Contrasena
            );

            var cliente = new Cliente
            {
                Nombre = modelo.Nombre.Trim(),
                Correo = correo,
                Telefono = modelo.Telefono.Trim(),
                Direccion = modelo.Direccion.Trim(),
                Pais = modelo.Pais,
                FechaRegistro = DateTime.Now,
                Usuario = usuario
            };

            _context.Clientes.Add(cliente);

            await _context.SaveChangesAsync();

            TempData["Mensaje"] =
                "Tu cuenta fue creada correctamente. Ya puedes iniciar sesión.";

            return RedirectToAction(nameof(Login));
        }

        // Mostrar formulario de inicio de sesión
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // Procesar inicio de sesión
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel modelo,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(modelo);

            string correo = modelo.Correo.Trim().ToLower();

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Correo.ToLower() == correo);

            if (usuario == null || !usuario.Activo)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "El correo electrónico o la contraseña son incorrectos.");

                return View(modelo);
            }

            var resultado = _passwordHasher.VerifyHashedPassword(
                usuario,
                usuario.Contrasena,
                modelo.Contrasena
            );

            if (resultado == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "El correo electrónico o la contraseña son incorrectos.");

                return View(modelo);
            }

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    usuario.UsuarioId.ToString()),

                new Claim(ClaimTypes.Name, usuario.Nombre),
                new Claim(ClaimTypes.Email, usuario.Correo),
                new Claim(ClaimTypes.Role, usuario.Rol)
            };

            var identidad = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            var principal = new ClaimsPrincipal(identidad);

            var propiedades = new AuthenticationProperties
            {
                IsPersistent = modelo.Recordarme
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                propiedades
            );

            if (!string.IsNullOrEmpty(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        // Cerrar sesión
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            return RedirectToAction(nameof(Login));
        }

        // Mostrar página de acceso denegado
        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccesoDenegado()
        {
            return View();
        }

        // Consultar países mediante REST Countries v5
        private async Task<List<string>?> ObtenerPaisesAsync()
        {
            try
            {
                string? apiKey = _configuration["RestCountries:ApiKey"];

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogError(
                        "No se encontró la API Key de REST Countries en User Secrets.");

                    return null;
                }

                var clienteHttp = _httpClientFactory.CreateClient();

                clienteHttp.Timeout = TimeSpan.FromSeconds(20);

                clienteHttp.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                var paises = new List<string>();

                int limite = 100;
                int offset = 0;
                bool hayMasRegistros = true;

                while (hayMasRegistros)
                {
                    string url =
                        $"https://api.restcountries.com/countries/v5?response_fields=names.common&limit={limite}&offset={offset}";

                    var respuestaHttp = await clienteHttp.GetAsync(url);

                    respuestaHttp.EnsureSuccessStatusCode();

                    var respuesta = await respuestaHttp.Content
                        .ReadFromJsonAsync<RespuestaApi>();

                    if (respuesta?.Data?.Objects == null)
                        return null;

                    foreach (var pais in respuesta.Data.Objects)
                    {
                        if (!string.IsNullOrWhiteSpace(pais.Nombres?.Comun))
                        {
                            paises.Add(pais.Nombres.Comun);
                        }
                    }

                    hayMasRegistros =
                        respuesta.Data.Meta?.More == true;

                    offset += limite;
                }

                var resultado = paises
                    .Distinct()
                    .OrderBy(p => p)
                    .ToList();

                if (resultado.Count == 0)
                    return null;

                _logger.LogInformation(
                    "Se cargaron {Cantidad} países desde REST Countries.",
                    resultado.Count);

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al consultar la API REST Countries v5.");

                return null;
            }
        }

        // Cargar países en el formulario
        private async Task CargarPaisesAsync()
        {
            var paises = await ObtenerPaisesAsync();

            ViewBag.Paises = paises ?? new List<string>();
            ViewBag.ErrorPaises = paises == null;
        }

        // Estructura principal de respuesta
        private class RespuestaApi
        {
            [JsonPropertyName("data")]
            public DatosApi? Data { get; set; }
        }

        private class DatosApi
        {
            [JsonPropertyName("objects")]
            public List<PaisApi>? Objects { get; set; }

            [JsonPropertyName("meta")]
            public MetaApi? Meta { get; set; }
        }

        // Información del país
        private class PaisApi
        {
            [JsonPropertyName("names")]
            public NombresPaisApi? Nombres { get; set; }
        }

        private class NombresPaisApi
        {
            [JsonPropertyName("common")]
            public string? Comun { get; set; }
        }

        // Información de paginación
        private class MetaApi
        {
            [JsonPropertyName("more")]
            public bool More { get; set; }
        }
    }
}
