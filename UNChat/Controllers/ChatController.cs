using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using UNChat.Context;
using UNChat.Models;
using UNChat.Hubs;

[Route("api/chat")]
[ApiController]
public class ChatController : ControllerBase
{
    private readonly UNChatDbContext _context;

    public ChatController(UNChatDbContext context)
    {
        _context = context;
    }
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] ChatMessage model)
    {
        if (string.IsNullOrEmpty(model.SenderId) || string.IsNullOrEmpty(model.ReceiverId) || string.IsNullOrEmpty(model.Message))
        {
            return BadRequest("Invalid message details.");
        }

        var chatMessage = new ChatMessage
        {
            SenderId = model.SenderId,
            ReceiverId = model.ReceiverId,
            Message = model.Message,
            Timestamp = DateTime.UtcNow
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
        await hubContext.Clients.User(model.ReceiverId).SendAsync("ReceiveMessage", model.SenderId, model.Message);

        return Ok("Message sent successfully.");
    }
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUserId()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User not logged in.");
        }
        return Ok(new { userId });
    }

    [HttpGet("messages/{userId}/{contactId}")]
    public async Task<IActionResult> GetMessages(string userId, string contactId)
    {
        var messages = await _context.ChatMessages
            .Where(m => (m.SenderId == userId && m.ReceiverId == contactId) ||
                        (m.SenderId == contactId && m.ReceiverId == userId))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
        return Ok(messages);
    }
}
