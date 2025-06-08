using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UNChat.Context;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UNChat.Models;
using Microsoft.AspNetCore.Mvc.Filters;
using static JwtTokenService;


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
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
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
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
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
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
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
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
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
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
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
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> GetAverageMessagesPerUser()
        {
            var totalMessages = await _context.ChatMessages.CountAsync();
            var totalUsers = await _context.Users.CountAsync();

            var average = totalUsers == 0 ? 0 : (double)totalMessages / totalUsers;

            return Ok(new { averageMessagesPerUser = average });
        }

        // GET: api/statsapi/average-users-per-chat
        [HttpGet("average-users-per-chat")]
        [ServiceFilter(typeof(ApiKeyAuthFilter))]
        public async Task<IActionResult> GetAverageUsersPerChat()
        {
            var totalUserChats = await _context.UserChats.CountAsync();
            var totalChats = await _context.Chats.CountAsync();

            var average = totalChats == 0 ? 0 : (double)totalUserChats / totalChats;

            return Ok(new { averageUsersPerChat = average });
        }

        [HttpGet("export")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ExportStatsAsPdf()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var totalMessages = await _context.ChatMessages.CountAsync();
            var totalChats = await _context.Chats.CountAsync();
            var totalUsers = await _context.Users.CountAsync();

            var topUser = await _context.ChatMessages
                .GroupBy(m => m.SenderId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .FirstOrDefaultAsync();

            var topUserName = "N/A";
            if (topUser != null)
            {
                var user = await _context.Users.FindAsync(topUser.UserId);
                topUserName = user?.UserName ?? "Unknown";
            }

            var mostActiveChat = await _context.ChatMessages
                .GroupBy(m => m.ChatId)
                .Select(g => new
                {
                    ChatId = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .FirstOrDefaultAsync();

            var mostActiveChatName = "N/A";
            if (mostActiveChat != null)
            {
                var chat = await _context.Chats.FindAsync(mostActiveChat.ChatId);
                mostActiveChatName = chat?.Name ?? "Unknown";
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text("Statystyki UNChat").FontSize(20).Bold();
                    page.Content().Column(col =>
                    {
                        col.Item().Text($"📨 Wiadomości: {totalMessages}");
                        col.Item().Text($"💬 Czaty: {totalChats}");
                        col.Item().Text($"👥 Użytkownicy: {totalUsers}");
                        col.Item().Text($"🏆 Top użytkownik: {topUserName} ({topUser?.Count ?? 0} wiadomości)");
                        col.Item().Text($"🔥 Najaktywniejszy czat: {mostActiveChatName} ({mostActiveChat?.Count ?? 0} wiadomości)");
                        col.Spacing(5);
                    });
                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span("Wygenerowano: ");
                        txt.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")).SemiBold();
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", "Statystyki_UNChat.pdf");
        }
    }


}
public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddYears(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public class ApiKeyAuthFilter : IAuthorizationFilter
    {
        private readonly UNChatDbContext _context;

        public ApiKeyAuthFilter(UNChatDbContext context)
        {
            _context = context;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();

            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var token = authHeader.Substring("Bearer ".Length);

            var user = _context.Users.FirstOrDefault(u => u.ApiKey == token);
            if (user == null)
            {
                context.Result = new UnauthorizedResult();
            }
        }
    }

}
