using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UNChat.Context;
using UNChat.Models;
using UNChat.Hubs;
using UNChat.DTOs;
using System.Security.Claims;

namespace UNChat.Controllers
{
    [Route("api/reactions")]
    [ApiController]
    [Authorize]
    public class ReactionController : ControllerBase
    {
        private readonly UNChatDbContext _context;

        public ReactionController(UNChatDbContext context)
        {
            _context = context;
        }

        // POST: /api/reactions
        [HttpPost("add")]
        public async Task<IActionResult> AddReaction([FromBody] ReactionDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var hub = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
            if (userId == null)
                return Unauthorized();

            var exists = await _context.MessageReactions.AnyAsync(r =>
                r.ChatMessageId == request.ChatMessageId &&
                r.UserId == userId &&
                r.EmojiId == request.EmojiId);

            if (exists)
            {
                // Usuń reakcję (unreact)
                var reaction = await _context.MessageReactions.FirstAsync(r =>
                    r.ChatMessageId == request.ChatMessageId &&
                    r.UserId == userId &&
                    r.EmojiId == request.EmojiId);

                _context.MessageReactions.Remove(reaction);
                await _context.SaveChangesAsync();
                await hub.Clients.All.SendAsync("ReactionUpdated", reaction.ChatMessageId);

                return Ok(new { removed = true });
            }
            else
            {
                // Dodaj nową reakcję
                var reaction = new MessageReaction
                {
                    ChatMessageId = request.ChatMessageId,
                    UserId = userId,
                    EmojiId = request.EmojiId
                };

                _context.MessageReactions.Add(reaction);
                await _context.SaveChangesAsync();
                
                await hub.Clients.All.SendAsync("ReactionUpdated", reaction.ChatMessageId);


                return Ok(new { added = true });
            }
        }

        // GET: /api/reactions/{messageId}
        [HttpGet("{messageId}")]
        public async Task<IActionResult> GetReactions(int messageId)
        {
            var reactions = await _context.MessageReactions
                .Where(r => r.ChatMessageId == messageId)
                .Include(r => r.Emoji)
                .Include(r => r.User)
                .ToListAsync();

            var result = reactions
                .GroupBy(r => r.Emoji.Symbol)
                .Select(g => new
                {
                    Emoji = g.Key,
                    Count = g.Count(),
                    Users = g.Select(u => new { u.UserId, u.User.UserName }).ToList()
                });

            return Ok(result);
        }

        // GET: /api/reactions/emojis
        [HttpGet("emojis")]
        public async Task<IActionResult> GetAvailableEmojis()
        {
            var emojis = await _context.Emojis.ToListAsync();
            return Ok(emojis);
        }

    }


}
