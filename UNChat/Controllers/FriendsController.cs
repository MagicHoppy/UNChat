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
            {
                return BadRequest("Brak danych znajomości.");
            }

            // Sprawdź czy już istnieje taka znajomość
            bool exists = await _context.Friends.AnyAsync(f =>
                (f.Friend1Id == model.Friend1Id && f.Friend2Id == model.Friend2Id) ||
                (f.Friend1Id == model.Friend2Id && f.Friend2Id == model.Friend1Id));

            if (exists)
            {
                return BadRequest("Już jesteście znajomymi.");
            }

            _context.Friends.Add(model);
 
            await _context.SaveChangesAsync();

            return Ok("Dodano do znajomych.");
        }

        [HttpPost("/api/friends/remove")]
        public async Task<IActionResult> RemoveFriend([FromBody] Friend model)
        {
            if (string.IsNullOrEmpty(model.Friend1Id) || string.IsNullOrEmpty(model.Friend2Id))
            {
                return BadRequest("Brak danych znajomości.");
            }

            // Sprawdź czy już istnieje taka znajomość
            bool exists = await _context.Friends.AnyAsync(f =>
                (f.Friend1Id == model.Friend1Id && f.Friend2Id == model.Friend2Id) ||
                (f.Friend1Id == model.Friend2Id && f.Friend2Id == model.Friend1Id));

            if (exists)
            {
                _context.Friends.Remove(model);

                await _context.SaveChangesAsync();

                return Ok("Usunieto z znajomych.");
            }

           return BadRequest("Nie jestescie znajomymi");
        }


        [HttpGet("/api/friends/{userId}")]
        public async Task<IActionResult> GetFriends(string userId)
        {
            var friends = await _context.Friends
                .Where(f => f.Friend1Id == userId || f.Friend2Id == userId)
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

    }
}
