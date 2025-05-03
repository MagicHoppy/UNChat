using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UNChat.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using UNChat.Context;

namespace UNChat.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly UNChatDbContext _context;


        public ChatController(UserManager<User> userManager, UNChatDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Chat()
        {
            var user = await _userManager.GetUserAsync(User);

            var model = new ChatViewModel
            {
                UserId = user?.Id, // Assign the logged-in user's ID
                Emojis = _context.Emojis
                .Select(e => e.Symbol)
                .ToList()

            };

            return View(model);
        }
        [HttpGet("/api/users")]
        public async Task<IActionResult> GetUsers()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserId = currentUser.Id;

            // Get all accepted friend relationships involving the current user
            var friendIds = await _context.Friends
                .Where(f =>
                    f.Status == FriendStatus.Accepted &&
                    (f.Friend1Id == currentUserId || f.Friend2Id == currentUserId))
                .Select(f => f.Friend1Id == currentUserId ? f.Friend2Id : f.Friend1Id)
                .ToListAsync();

            // Filter users: exclude current user and users who are already friends
            var users = await _userManager.Users
                .Where(u => u.Id != currentUserId && !friendIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToListAsync();

            return Ok(users);
        }

    }
}
