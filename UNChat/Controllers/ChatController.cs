using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UNChat.Models;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

namespace UNChat.Controllers
{
    public class ChatController : Controller
    {
        private readonly UserManager<User> _userManager;

        public ChatController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Chat()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.UserId = user?.Id; // Przekazujemy UserId do widoku
            return View();
        }
        [HttpGet("/api/users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userManager.Users.Select(u => new { u.Id, u.Name }).ToListAsync();
            return Ok(users);
        }

    }
}
