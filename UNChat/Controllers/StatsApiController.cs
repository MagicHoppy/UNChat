using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UNChat.Context;
using System.Linq;
using System.Threading.Tasks;

namespace UNChat.Controllers
{
    [Route("api/stats")]
    [ApiController]
    public class StatsApiController : ControllerBase
    {
        private readonly UNChatDbContext _context;

        public StatsApiController(UNChatDbContext context)
        {
            _context = context;
        }

        [HttpGet("totals")]
        public async Task<IActionResult> GetTotals()
        {
            var totalMessages = await _context.ChatMessages.CountAsync();
            var totalChats = await _context.Chats.CountAsync();
            var totalUsers = await _context.Users.CountAsync();

            return Ok(new
            {
                totalMessages,
                totalChats,
                totalUsers
            });
        }

        [HttpGet("top-user")]
        public async Task<IActionResult> GetTopUser()
        {
            var topUser = await _context.ChatMessages
                .GroupBy(m => m.SenderId)
                .Select(g => new
                {
                    UserId = g.Key,
                    MessageCount = g.Count()
                })
                .OrderByDescending(g => g.MessageCount)
                .FirstOrDefaultAsync();

            if (topUser == null)
                return NotFound("No messages found");

            var user = await _context.Users.FindAsync(topUser.UserId);

            return Ok(new
            {
                user.Id,
                user.UserName,
                topUser.MessageCount
            });
        }

        [HttpGet("messages-last-7-days")]
        public async Task<IActionResult> GetMessagesLast7Days()
        {
            var dateLimit = DateTime.UtcNow.AddDays(-7);

            var data = await _context.ChatMessages
                .Where(m => m.Timestamp >= dateLimit)
                .GroupBy(m => m.Timestamp.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            return Ok(data);
        }

        // GET: api/statsapi/most-active-chat
        [HttpGet("most-active-chat")]
        public async Task<IActionResult> GetMostActiveChat()
        {
            var chat = await _context.ChatMessages
                .GroupBy(m => m.ChatId)
                .Select(g => new
                {
                    ChatId = g.Key,
                    MessageCount = g.Count()
                })
                .OrderByDescending(g => g.MessageCount)
                .FirstOrDefaultAsync();

            if (chat == null)
                return NotFound("No chat messages found");

            var chatInfo = await _context.Chats.FindAsync(chat.ChatId);

            return Ok(new
            {
                chatInfo.Id,
                chatInfo.Name,
                chat.MessageCount
            });
        }

        // GET: api/statsapi/top-emojis
        [HttpGet("top-emojis")]
        public async Task<IActionResult> GetTopEmojis()
        {
            var emojis = await _context.MessageReactions
                .GroupBy(r => r.EmojiId)
                .Select(g => new
                {
                    EmojiId = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .Take(5)
                .ToListAsync();

            var results = new List<object>();

            foreach (var e in emojis)
            {
                var emoji = await _context.Emojis.FindAsync(e.EmojiId);
                results.Add(new { emoji.Symbol, e.Count });
            }

            return Ok(results);
        }

        // GET: api/statsapi/average-messages-per-user
        [HttpGet("average-messages-per-user")]
        public async Task<IActionResult> GetAverageMessagesPerUser()
        {
            var totalMessages = await _context.ChatMessages.CountAsync();
            var totalUsers = await _context.Users.CountAsync();

            var average = totalUsers == 0 ? 0 : (double)totalMessages / totalUsers;

            return Ok(new { averageMessagesPerUser = average });
        }

        // GET: api/statsapi/average-users-per-chat
        [HttpGet("average-users-per-chat")]
        public async Task<IActionResult> GetAverageUsersPerChat()
        {
            var totalUserChats = await _context.UserChats.CountAsync();
            var totalChats = await _context.Chats.CountAsync();

            var average = totalChats == 0 ? 0 : (double)totalUserChats / totalChats;

            return Ok(new { averageUsersPerChat = average });
        }
    }
}
