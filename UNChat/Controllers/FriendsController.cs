using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using UNChat.Models;
using Microsoft.EntityFrameworkCore;
using UNChat.Context;
using Microsoft.AspNetCore.SignalR;
using UNChat.Hubs;
using Swashbuckle.AspNetCore.Annotations;

namespace UNChat.Controllers
{
    [ApiController]
    public class FriendsController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly UNChatDbContext _context;

        public FriendsController(UserManager<User> userManager, UNChatDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        /// <summary>
        /// Sends a friend request to another user
        /// </summary>
        /// <param name="model">Friend request details</param>
        /// <returns>Result of the friend request operation</returns>
        [HttpPost("/api/friends/add")]
        [SwaggerOperation(Summary = "Send friend request", Description = "Sends a friend request from one user to another")]
        [SwaggerResponse(200, "Friend request sent successfully")]
        [SwaggerResponse(400, "Invalid request data or friend request already exists")]
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

        /// <summary>
        /// Accepts a pending friend request
        /// </summary>
        /// <param name="model">Friend request details</param>
        /// <returns>Result of the friend request acceptance</returns>
        [HttpPost("/api/friends/accept")]
        [SwaggerOperation(Summary = "Accept friend request", Description = "Accepts a pending friend request and creates a private chat if one doesn't exist")]
        [SwaggerResponse(200, "Friend request accepted successfully")]
        [SwaggerResponse(404, "Friend request not found")]
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
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
            await hubContext.Clients.User(model.Friend1Id).SendAsync("FriendAdded", model.Friend2Id);
            await hubContext.Clients.User(model.Friend2Id).SendAsync("FriendAdded", model.Friend1Id);

            return Ok("Zaproszenie zaakceptowane.");
        }

        /// <summary>
        /// Denies a pending friend request
        /// </summary>
        /// <param name="model">Friend request details</param>
        /// <returns>Result of the friend request denial</returns>
        [HttpPost("/api/friends/deny")]
        [SwaggerOperation(Summary = "Deny friend request", Description = "Denies a pending friend request")]
        [SwaggerResponse(200, "Friend request denied successfully")]
        [SwaggerResponse(404, "Friend request not found")]
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

        /// <summary>
        /// Removes a friend from the friends list
        /// </summary>
        /// <param name="model">Friend relationship details</param>
        /// <returns>Result of the friend removal</returns>
        [HttpPost("/api/friends/remove")]
        [SwaggerOperation(Summary = "Remove friend", Description = "Removes a friend from the friends list")]
        [SwaggerResponse(200, "Friend removed successfully")]
        [SwaggerResponse(400, "Users are not friends")]
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

        /// <summary>
        /// Gets all friends for a user with additional information
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <returns>List of friends with status and chat information</returns>
        [HttpGet("/api/friends/{userId}")]
        [SwaggerOperation(Summary = "Get user's friends", Description = "Retrieves all friends for a user with their online status and chat information")]
        [SwaggerResponse(200, "List of friends retrieved successfully")]
        public async Task<IActionResult> GetFriends(string userId)
        {
            // Pobierz przyjaciół
            var friends = await _context.Friends
                .Where(f => (f.Friend1Id == userId || f.Friend2Id == userId) && f.Status == FriendStatus.Accepted)
                .ToListAsync();

            var friendIds = friends
                .Select(f => f.Friend1Id == userId ? f.Friend2Id : f.Friend1Id)
                .ToList();

            var now = DateTime.UtcNow;
            var onlineThreshold = TimeSpan.FromMinutes(10);

            // Pobierz dane użytkowników
            var users = await _context.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();

            // Pobierz wszystkie czaty użytkownika (userId), które nie są grupowe
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

            // Pobierz ostatnie wiadomości dla wszystkich czatów
            var chatIds = privateChats.Select(pc => pc.ChatId).ToList();
            var lastMessages = await _context.ChatMessages
                .Where(m => chatIds.Contains(m.ChatId))
                .GroupBy(m => m.ChatId)
                .Select(g => new
                {
                    ChatId = g.Key,
                    LastMessageTime = g.Max(m => m.Timestamp)
                })
                .ToListAsync();

            // Zbuduj wynik
            var result = users.Select(friend =>
            {
                var chat = privateChats
                    .FirstOrDefault(pc => pc.Chat.Participants.Any(p => p.UserId == friend.Id));

                var chatId = chat?.ChatId;

                // Znajdź czas ostatniej wiadomości dla tego czatu
                var lastMsg = lastMessages.FirstOrDefault(lm => lm.ChatId == chatId);
                DateTime? lastMessageTime = lastMsg?.LastMessageTime;

                return new
                {
                    friend.Id,
                    friend.Name,
                    IsOnline = friend.IsOnline && friend.LastOnline >= now - onlineThreshold,
                    LastOnline = friend.LastOnline,
                    ChatId = chatId,
                    LastMessageTime = lastMessageTime
                };
            });

            return Ok(result);
        }

        /// <summary>
        /// Gets pending friend requests for a user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <returns>List of pending friend requests</returns>
        [HttpGet("/api/friends/requests/{userId}")]
        [SwaggerOperation(Summary = "Get pending friend requests", Description = "Retrieves all pending friend requests for a user")]
        [SwaggerResponse(200, "List of pending friend requests retrieved successfully")]
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