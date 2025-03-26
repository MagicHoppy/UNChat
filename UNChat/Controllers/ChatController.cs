using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using UNChat.Models;
using UNChat.DTO;

namespace UNChat.Controllers
{

    [Route("api/chat")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly UNChatDBContext _context;

        public ChatController(UNChatDBContext context)
        {
            _context = context;
        }

        // 1. Tworzenie prywatnego czatu między dwoma użytkownikami
        [HttpPost("create-private")]
        public async Task<IActionResult> CreatePrivateChat([FromBody] CreateChatDTO chatDto)
        {
            var user1 = await _context.Users.FindAsync(chatDto.User1Id);
            var user2 = await _context.Users.FindAsync(chatDto.User2Id);

            if (user1 == null || user2 == null)
                return NotFound("Użytkownik nie istnieje");

            // Sprawdzenie, czy czat już istnieje
            var existingChat = await _context.Chats
                .Include(c => c.UserChats)
                .ThenInclude(uc => uc.User)
                .FirstOrDefaultAsync(c => c.IsPrivate &&
                                          c.UserChats.Any(uc => uc.UserId == user1.Id) &&
                                          c.UserChats.Any(uc => uc.UserId == user2.Id));

            if (existingChat != null)
                return Ok(existingChat.Id);

            var chat = new Chat
            {
                Name = $"Chat {user1.Username} - {user2.Username}",
                IsPrivate = true,
                UserChats = new List<UserChat>
        {
            new UserChat { User = user1 },
            new UserChat { User = user2 }
        }
            };

            _context.Chats.Add(chat);
            await _context.SaveChangesAsync();

            return Ok(chat.Id);
        }


        // 2. Wysyłanie wiadomości
        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDTO messageDto)
        {
            var chat = await _context.Chats.FindAsync(messageDto.ChatId);
            var sender = await _context.Users.FindAsync(messageDto.SenderId);

            if (chat == null || sender == null)
                return NotFound("Czat lub użytkownik nie istnieje");

            var message = new Message
            {
                Content = messageDto.Content,
                SenderId = sender.Id,
                ChatId = chat.Id,
                SentAt = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            return Ok("Wiadomość wysłana");
        }


        // 3. Pobieranie wiadomości w czacie
        [HttpGet("messages/{chatId}")]
        public async Task<IActionResult> GetMessages(int chatId)
        {
            var chat = await _context.Chats.FindAsync(chatId);
            if (chat == null)
                return NotFound("Czat nie istnieje");

            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    Sender = m.Sender.Username,
                    m.SentAt
                })
                .ToListAsync();

            return Ok(messages);
        }
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users.ToListAsync();
            return Ok(users);
        }

    }
}
