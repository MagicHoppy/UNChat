using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UNChat.Models;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using UNChat.Context;
using Microsoft.AspNetCore.SignalR;
using UNChat.Hubs;

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
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
            await hubContext.Clients.User(model.Friend2Id).SendAsync("FriendRequestReceived", model.Friend1Id);

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

            // Sprawdź czy czat już istnieje
            var existingChat = await _context.UserChats
                .Where(uc => uc.UserId == model.Friend1Id || uc.UserId == model.Friend2Id)
                .GroupBy(uc => uc.ChatId)
                .Where(g => g.Count() == 2)
                .Select(g => g.Key)
                .FirstOrDefaultAsync();

            if (existingChat == null)
            {
                // Tworzymy nowy czat tylko jeśli nie istnieje
                var chat = new Chat
                {
                    IsGroup = false
                };

                _context.Chats.Add(chat);
                await _context.SaveChangesAsync();

                _context.UserChats.AddRange(new[]
                {
            new UserChat { UserId = model.Friend1Id, ChatId = chat.Id },
            new UserChat { UserId = model.Friend2Id, ChatId = chat.Id }
        });
            }

            friendship.Status = FriendStatus.Accepted;
            await _context.SaveChangesAsync();


            return Ok("Zaproszenie zaakceptowane.");
        }
        [HttpPost("/api/friends/deny")]
        public async Task<IActionResult> DenyFriend([FromBody] Friend model)
        {
            var friendship = await _context.Friends
                .FirstOrDefaultAsync(f =>
                    f.Friend1Id == model.Friend1Id && f.Friend2Id == model.Friend2Id);

            if (friendship == null)
                return NotFound("Zaproszenie nie istnieje.");
            _context.Friends.Remove(friendship);

            await _context.SaveChangesAsync();
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
            await hubContext.Clients.User(model.Friend1Id).SendAsync("FriendRequestDenied", model.Friend2Id);


            return Ok("Zaproszenie odrzucone.");
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
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
            await hubContext.Clients.User(model.Friend1Id).SendAsync("FriendRemoved", model.Friend2Id);
            await hubContext.Clients.User(model.Friend2Id).SendAsync("FriendRemoved", model.Friend1Id);

            return Ok("Usunięto z listy znajomych.");
        }



        [HttpGet("/api/friends/{userId}")]
        public async Task<IActionResult> GetFriends(string userId)
        {
            // Krok 1: Pobierz przyjaciół
            var friends = await _context.Friends
                .Where(f => (f.Friend1Id == userId || f.Friend2Id == userId) && f.Status == FriendStatus.Accepted)
                .ToListAsync();

            var friendIds = friends
                .Select(f => f.Friend1Id == userId ? f.Friend2Id : f.Friend1Id)
                .ToList();

            var now = DateTime.UtcNow;
            var onlineThreshold = TimeSpan.FromMinutes(10);

            // Krok 2: Pobierz dane użytkowników
            var users = await _context.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();

            // Krok 3: Pobierz wszystkie czaty użytkownika (userId), które nie są grupowe
            var privateChats = await _context.UserChats
                .Include(uc => uc.Chat)
                    .ThenInclude(c => c.Participants)
                .Where(uc => uc.UserId == userId && uc.Chat.IsGroup == false)
                .Select(uc => new
                {
                    uc.ChatId,
                    Chat = uc.Chat
                })
                .ToListAsync();

            // Krok 4: Zbuduj wynik
            var result = users.Select(friend =>
            {
                // Znajdź czat 1-na-1 pomiędzy userId i friend.Id
                var chatId = privateChats
                    .FirstOrDefault(pc => pc.Chat.Participants.Any(p => p.UserId == friend.Id))
                    ?.ChatId;

                return new
                {
                    friend.Id,
                    friend.Name,
                    IsOnline = friend.IsOnline && friend.LastOnline >= now - onlineThreshold,
                    LastOnline = friend.LastOnline,
                    ChatId = chatId
                };
            });

            return Ok(result);
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
