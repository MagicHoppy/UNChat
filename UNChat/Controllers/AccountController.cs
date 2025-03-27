using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using UNChat.Models;

namespace UNChat.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public AccountController(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // ==============================
        // 🚀 1️⃣ WIDOKI HTML (Dla użytkowników na stronie)
        // ==============================

        // Rejestracja - GET (Widok formularza)
        [HttpGet]
        public IActionResult Register() => View();

        // Rejestracja - POST (Obsługa formularza)
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new User { UserName = model.Email, Email = model.Email, Name = model.Name };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // Logowanie - GET (Widok formularza)
        [HttpGet]
        public IActionResult Login() => View();

        // Logowanie - POST (Obsługa formularza)
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);

            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Nieprawidłowa nazwa użytkownika lub hasło");
            return View(model);
        }

        // Wylogowanie (Tylko dla zalogowanych)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ==============================
        // 🌍 2️⃣ API REST (Dla Postmana / aplikacji mobilnych)
        // ==============================

        [Route("api/account")]
        [ApiController]
        public class AccountApiController : ControllerBase
        {
            private readonly UserManager<User> _userManager;
            private readonly SignInManager<User> _signInManager;

            public AccountApiController(UserManager<User> userManager, SignInManager<User> signInManager)
            {
                _userManager = userManager;
                _signInManager = signInManager;
            }

            // API: Rejestracja użytkownika
            [HttpPost("register")]
            public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var user = new User { UserName = model.Email, Email = model.Email, Name = model.Name };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (!result.Succeeded)
                    return BadRequest(result.Errors);

                await _signInManager.SignInAsync(user, isPersistent: false);
                return Ok(new { message = "Rejestracja udana", userId = user.Id });
            }

            // API: Logowanie użytkownika
            [HttpPost("login")]
            public async Task<IActionResult> Login([FromBody] LoginViewModel model)
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);

                if (!result.Succeeded)
                    return Unauthorized(new { message = "Nieprawidłowe dane logowania" });

                return Ok(new { message = "Logowanie udane" });
            }

            // API: Wylogowanie użytkownika
            [HttpPost("logout")]
            [Authorize]
            public async Task<IActionResult> Logout()
            {
                await _signInManager.SignOutAsync();
                return Ok(new { message = "Wylogowano pomyślnie" });
            }
        }
    }
}
