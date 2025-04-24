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
public class ChatApiController : ControllerBase
{
    private readonly UNChatDbContext _context;

    public ChatApiController(UNChatDbContext context)
    {
        _context = context;
    }
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromForm] string senderId, [FromForm] string receiverId, [FromForm] string? message, [FromForm] IFormFile? file)
    {
        if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
            return BadRequest("Brakuje danych.");
        if (string.IsNullOrEmpty(message) && file == null)
            return BadRequest("Wiadomość lub plik jest wymagany.");

        var chatMessage = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Message = message ?? string.Empty,
            Timestamp = DateTime.UtcNow
        };

        string fileUrl = null;

        if (file != null && file.Length > 0)
        {
            var uploadsFolder = Path.Combine("wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            fileUrl = $"/uploads/{uniqueFileName}";
            chatMessage.Attachments.Add(new ChatAttachment
            {
                FileName = file.FileName,
                FilePath = fileUrl
            });
        }

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
        await hubContext.Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, message, fileUrl, chatMessage.Timestamp);

        return Ok(new { message, attachmentUrl = fileUrl, timestamp = chatMessage.Timestamp });
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
            .Include(m => m.Attachments)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        var messageDtos = messages.Select(m => new ChatMessageDto
        {
            SenderId = m.SenderId,
            Message = m.Message,
            Timestamp = m.Timestamp,
            Attachments = m.Attachments.Select(a => new ChatAttachmentDto
            {
                FileName = a.FileName,
                FilePath = a.FilePath
            }).ToList()
        }).ToList();

        return Ok(messageDtos);
    }

}
