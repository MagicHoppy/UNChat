using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UNChat.Models;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace UNChat.Controllers
{
    [Authorize]
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

            var model = new ChatViewModel
            {
                UserId = user?.Id, // Assign the logged-in user's ID
            };

            return View(model);
        }
        [HttpGet("/api/users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userManager.Users.Select(u => new { u.Id, u.Name }).ToListAsync();
            return Ok(users);
        }

    }
}
