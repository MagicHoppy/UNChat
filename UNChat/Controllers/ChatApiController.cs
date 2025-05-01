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
using UNChat.DTOs;

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
        await hubContext.Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, message, fileUrl, chatMessage.Timestamp, chatMessage.Id);

        return Ok(new { message, attachmentUrl = fileUrl, timestamp = chatMessage.Timestamp,id = chatMessage.Id });
    }
    [HttpDelete("remove/{messageId}")]
    public async Task<IActionResult> RemoveMessage(int messageId)
    {
        var message = await _context.ChatMessages
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            return NotFound("Wiadomość nie została znaleziona."); // "Message not found."
        }

        // Delete attached files if any
        if (message.Attachments != null && message.Attachments.Count > 0)
        {
            foreach (var attachment in message.Attachments)
            {
                var filePath = Path.Combine("wwwroot", attachment.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
        }

        _context.ChatMessages.Remove(message);
        await _context.SaveChangesAsync();

        // Notify clients via SignalR
        var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
        await hubContext.Clients.Users(message.SenderId, message.ReceiverId)
            .SendAsync("MessageRemoved", messageId);

        return Ok(new { message = "Wiadomość została usunięta." });
    }


    [HttpPut("edit/{messageId}")]
    public async Task<IActionResult> EditMessage(int messageId, [FromBody] EditMessageDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.NewMessage))
            return BadRequest("Brak nowej treści wiadomości.");

        var message = await _context.ChatMessages
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            return NotFound("Wiadomość nie została znaleziona."); // "Message not found."
        }

        message.Message = dto.NewMessage;
        //message.Timestamp = DateTime.UtcNow;

        _context.ChatMessages.Update(message);
        await _context.SaveChangesAsync();

        // Notify clients via SignalR
        var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
        await hubContext.Clients.Users(message.SenderId, message.ReceiverId)
            .SendAsync("MessageEdited", message.Id, message.Message);

        return Ok(new { message = "Wiadomość została zedytowana." });
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
            Id = m.Id,
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
