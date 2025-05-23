using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using UNChat.Context;
using UNChat.Models;
using UNChat.Context;

namespace UNChat.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly UNChatDbContext _context;

        public AccountController(UserManager<User> userManager, SignInManager<User> signInManager, UNChatDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Check for duplicate email and name
            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError("Email", "Ten adres e-mail jest już zarejestrowany.");
                return View(model);
            }

            if (await _userManager.Users.AnyAsync(u => u.Name.ToLower() == model.Name.Trim().ToLower()))
            {
                ModelState.AddModelError("Name", "Ta nazwa jest już zajęta.");
                return View(model);
            }

            var user = new User
            {
                UserName = model.Email,
                Email = model.Email,
                Name = model.Name.Trim()
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Chat", "Chat");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

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

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                if (await _userManager.IsLockedOutAsync(user))
                {
                    ModelState.AddModelError("", "Konto jest zablokowane. Spróbuj ponownie później.");
                    return View(model);
                }
            }

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (user != null)
                {
                    await _userManager.ResetAccessFailedCountAsync(user);
                }
                return RedirectToAction("Chat", "Chat");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Konto zablokowane z powodu zbyt wielu nieudanych prób logowania. Spróbuj ponownie później.");
            }
            else
            {
                ModelState.AddModelError("", "Nieprawidłowa nazwa użytkownika lub hasło");
            }

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

        // Logowanie przez Google
        [HttpGet]
        public IActionResult ExternalLogin(string provider, string returnUrl = "/")
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "/")
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToAction("Login");
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
            if (signInResult.Succeeded)
            {
                return RedirectToAction("Chat", "Chat");
            }

            var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            if (email == null)
            {
                return RedirectToAction("Login");
            }
            var firstName = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value;
            var lastName = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Surname)?.Value;
            var user = new User
            {
                UserName = email,
                Email = email,
                Name = $"{firstName} {lastName}".Trim()
            };
            var result = await _userManager.CreateAsync(user);
            if (result.Succeeded)
            {
                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, false);
                return RedirectToAction("Chat", "Chat");
            }

            return RedirectToAction("Login");
        }
        // GET: /Account/EditProfile
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new EditProfileViewModel
            {
                Name = user.Name
            };

            return View(model);
        }

        // POST: /Account/EditProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            user.Name = model.Name;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Profil został zaktualizowany.";
                return RedirectToAction("EditProfile");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
    }

}
