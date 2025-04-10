using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UNChat.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using UNChat.Context;

namespace UNChat.Controllers
{
    public class FriendsController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly UNChatDbContext _context;


        public FriendsController(UserManager<User> userManager, UNChatDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [HttpPost("/api/friends/add")]
        public async Task<IActionResult> AddFriend([FromBody] Friend model)
        {
            if (string.IsNullOrEmpty(model.Friend1Id) || string.IsNullOrEmpty(model.Friend2Id))
                return BadRequest("Brak danych.");

            bool exists = await _context.Friends.AnyAsync(f =>
                (f.Friend1Id == model.Friend1Id && f.Friend2Id == model.Friend2Id) ||
                (f.Friend1Id == model.Friend2Id && f.Friend2Id == model.Friend1Id));

            if (exists)
                return BadRequest("Zaproszenie już istnieje lub jesteście znajomymi.");

            model.Status = FriendStatus.Pending;
            _context.Friends.Add(model);
            await _context.SaveChangesAsync();

            return Ok("Wysłano zaproszenie.");
        }
        [HttpPost("/api/friends/accept")]
        public async Task<IActionResult> AcceptFriend([FromBody] Friend model)
        {
            var friendship = await _context.Friends
                .FirstOrDefaultAsync(f =>
                    f.Friend1Id == model.Friend1Id && f.Friend2Id == model.Friend2Id &&
                    f.Status == FriendStatus.Pending);

            if (friendship == null)
                return NotFound("Zaproszenie nie istnieje.");

            friendship.Status = FriendStatus.Accepted;
            await _context.SaveChangesAsync();

            return Ok("Zaproszenie zaakceptowane.");
        }


        [HttpPost("/api/friends/remove")]
        public async Task<IActionResult> RemoveFriend([FromBody] Friend model)
        {
            var friendship = await _context.Friends
                .FirstOrDefaultAsync(f =>
                    ((f.Friend1Id == model.Friend1Id && f.Friend2Id == model.Friend2Id) ||
                     (f.Friend1Id == model.Friend2Id && f.Friend2Id == model.Friend1Id)) &&
                     f.Status == FriendStatus.Accepted);

            if (friendship == null)
                return BadRequest("Nie jesteście znajomymi.");

            _context.Friends.Remove(friendship);
            await _context.SaveChangesAsync();

            return Ok("Usunięto z listy znajomych.");
        }



        [HttpGet("/api/friends/{userId}")]
        public async Task<IActionResult> GetFriends(string userId)
        {
            var friends = await _context.Friends
                .Where(f => (f.Friend1Id == userId || f.Friend2Id == userId) && f.Status == FriendStatus.Accepted)
                .ToListAsync();

            var friendIds = friends
                .Select(f => f.Friend1Id == userId ? f.Friend2Id : f.Friend1Id)
                .ToList();

            var users = await _context.Users
                .Where(u => friendIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToListAsync();

            return Ok(users);
        }
        [HttpGet("/api/friends/requests/{userId}")]
        public async Task<IActionResult> GetPendingRequests(string userId)
        {
            var requests = await _context.Friends
                .Where(f => f.Friend2Id == userId && f.Status == FriendStatus.Pending)
                .ToListAsync();

            var senders = requests.Select(r => r.Friend1Id).ToList();

            var users = await _context.Users
                .Where(u => senders.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToListAsync();

            return Ok(users);
        }


    }
}
