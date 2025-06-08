using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UNChat.Context;
using UNChat.Models;
using UNChat.Hubs;
using UNChat.DTOs;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;

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
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Sends a chat message",
        Description = "Sends a message to a chat, with optional file attachment and @mention support.",
        OperationId = "SendChatMessage"
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Message sent successfully")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "User not a participant of the chat")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Chat not found")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Invalid file type")]
    public async Task<IActionResult> Send([FromForm] SendMessageRequest request)
    {
        var chat = await _context.Chats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == request.ChatId);

        if (chat == null)
        {
            Console.WriteLine($"Sender: {request.SenderId}, ChatId: {request.ChatId}, Message: {request.Message}, File: {request.File?.FileName}");
            return NotFound("Chat not found.");
        }

        if (!chat.Participants.Any(p => p.UserId == request.SenderId))
        {
            return Forbid("You are not a participant of this chat");
        }

        var chatMessage = new ChatMessage
        {
            SenderId = request.SenderId,
            ChatId = request.ChatId,
            Message = request.Message ?? string.Empty,
            Timestamp = DateTime.UtcNow
        };

        string? fileUrl = null;

        if (request.File != null && request.File.Length > 0)
        {
            var mediaExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mp3" };
            var docExtensions = new[] { ".pdf", ".txt", ".zip", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".json", ".log", ".md" };
            var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();

            if (!mediaExtensions.Contains(ext) && !docExtensions.Contains(ext))
                return BadRequest("File type not allowed.");

            var originalFileName = Path.GetFileName(request.File.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}_{originalFileName}";
            string filePath;

            if (mediaExtensions.Contains(ext))
            {
                var uploadsFolder = Path.Combine("wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);
                filePath = Path.Combine(uploadsFolder, uniqueFileName);
                fileUrl = $"/uploads/{uniqueFileName}";
            }
            else
            {
                var secureFolder = Path.Combine("UploadsSecure");
                Directory.CreateDirectory(secureFolder);
                filePath = Path.Combine(secureFolder, uniqueFileName);
                fileUrl = $"/download/{uniqueFileName}";
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            chatMessage.Attachments.Add(new ChatAttachment
            {
                FileName = originalFileName,
                FilePath = fileUrl
            });
        }

        _context.ChatMessages.Add(chatMessage);

        var user = await _context.Users.FindAsync(request.SenderId);
        if (user != null)
        {
            user.IsOnline = true;
            user.LastOnline = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var hub = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
        var sender = await _context.Users.FindAsync(request.SenderId);

        var mentionedUsers = new List<User>();
        if (!string.IsNullOrEmpty(request.Message))
        {
            var allUsers = await _context.Users.ToListAsync();
            foreach (var userr in allUsers)
            {
                if (!string.IsNullOrEmpty(userr.Name) && request.Message.Contains($"@{userr.Name}", StringComparison.OrdinalIgnoreCase))
                {
                    mentionedUsers.Add(userr);
                }
            }
        }

        var chatHub = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();

        foreach (var mentionedUser in mentionedUsers)
        {
            if (mentionedUser.Id != request.SenderId)
            {
                bool isParticipant = chat.Participants.Any(p => p.UserId == mentionedUser.Id);
                if (!isParticipant)
                    continue;

                await chatHub.Clients.User(mentionedUser.Id)
                    .SendAsync("MentionNotification", new
                    {
                        from = sender.Name,
                        senderId = request.SenderId,
                        chatId = request.ChatId,
                        chatName = chat.Name,
                        message = request.Message,
                        timestamp = chatMessage.Timestamp
                    });
            }
        }

        foreach (var participant in chat.Participants)
        {
            if (participant.UserId != request.SenderId)
            {
                await hub.Clients.User(participant.UserId)
                    .SendAsync("ReceiveMessage", request.SenderId, sender.Name, request.Message, fileUrl, chatMessage.Timestamp, chatMessage.Id, request.ChatId);
            }
        }

        return Ok(new
        {
            message = chatMessage.Message,
            attachmentUrl = fileUrl,
            timestamp = chatMessage.Timestamp,
            id = chatMessage.Id,
            senderName = sender.Name
        });
    }

    [HttpGet("/download/{filename}")]
    [SwaggerOperation(Summary = "Download file", Description = "Download a secure file by filename")]
    [SwaggerResponse(200, "File downloaded")]
    [SwaggerResponse(403, "Forbidden")]
    [SwaggerResponse(404, "File not found")]
    public async Task<IActionResult> Download(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        var allowed = new[] { ".pdf", ".txt", ".zip", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".json", ".log", ".md" };

        if (!allowed.Contains(ext) || filename.Contains(".."))
            return Forbid();

        var path = Path.Combine("UploadsSecure", filename);
        if (!System.IO.File.Exists(path))
            return NotFound();

        var memory = new MemoryStream();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            await stream.CopyToAsync(memory);

        memory.Position = 0;
        return File(memory, "application/octet-stream", filename);
    }
    [HttpDelete("remove/{messageId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Delete message", Description = "Delete a message by ID")]
    [SwaggerResponse(200, "Message deleted")]
    [SwaggerResponse(403, "Forbidden")]
    [SwaggerResponse(404, "Message not found")]
    public async Task<IActionResult> RemoveMessage(int messageId)
    {
        var message = await _context.ChatMessages.Include(m => m.Attachments).FirstOrDefaultAsync(m => m.Id == messageId);
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var chat = await _context.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == message.ChatId);

        if (message == null)
            return NotFound("Wiadomość nie została znaleziona.");

        if (message.SenderId != userId)
            return Forbid();

        if (message.Attachments != null)
        {
            foreach (var attachment in message.Attachments)
            {
                var filePath = Path.Combine("wwwroot", attachment.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
        }

        _context.ChatMessages.Remove(message);
        await _context.SaveChangesAsync();

        var hub = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();

        if(chat != null)
        { 
        foreach (var participant in chat.Participants)
        {
            if (participant.UserId != userId)
            {
                await hub.Clients.User(participant.UserId)
                    .SendAsync("MessageRemoved", messageId);
            }
        }
     }
        return Ok(new { message = "Wiadomość została usunięta." });
    }

    [HttpPut("edit/{messageId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Edit message", Description = "Edit the text content of a message")]
    [SwaggerResponse(200, "Message edited")]
    [SwaggerResponse(403, "Forbidden")]
    [SwaggerResponse(404, "Message not found")]
    [SwaggerResponse(400, "Invalid input")]
    public async Task<IActionResult> EditMessage(int messageId, [FromBody] EditMessageDto dto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var message = await _context.ChatMessages.Include(m => m.Attachments).FirstOrDefaultAsync(m => m.Id == messageId);
        var chat = await _context.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == message.ChatId);

        if (dto == null || string.IsNullOrWhiteSpace(dto.NewMessage))
            return BadRequest("Brak nowej treści wiadomości.");

        //var message = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
            return NotFound("Wiadomość nie została znaleziona.");

        if (message.SenderId != userId)
            return Forbid();

        message.Message = dto.NewMessage;
        await _context.SaveChangesAsync();

        var hub = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();

        if (chat != null)
        {
            foreach (var participant in chat.Participants)
            {
                if (participant.UserId != userId)
                {
                    await hub.Clients.User(participant.UserId)
                        .SendAsync("MessageEdited", messageId, message.Message);
                }
            }
        }
        return Ok(new { message = "Wiadomość została zedytowana." });
    }

    [HttpGet("me")]
    [Authorize]
    [SwaggerOperation(Summary = "Get current user ID", Description = "Returns user ID from JWT")]
    [SwaggerResponse(200, "User ID returned")]
    [SwaggerResponse(401, "Unauthorized")]
    public IActionResult GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User not logged in.");

        return Ok(new { userId });
    }

    [HttpGet("messages/{chatId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Get messages", Description = "Get all messages in a chat")]
    [SwaggerResponse(200, "Messages returned")]
    [SwaggerResponse(500, "Internal server error")]
    public async Task<IActionResult> GetMessages(string chatId)
    {
        try
        {
            // Pobieramy wiadomości
            var messages = await _context.ChatMessages
                .Where(m => m.ChatId == chatId)
                .Include(m => m.Attachments)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            // Wyciągamy unikalne senderId, aby jednym zapytaniem pobrać nazwy
            var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();

            var userNames = await _context.Users
                .Where(u => senderIds.Contains(u.Id))
                .Select(u => new { u.Id, DisplayName = u.Name})
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName);
            //var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Mapujemy do DTO
            var messageDtos = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = userNames.TryGetValue(m.SenderId, out var name) ? name : "(nieznany)",
                Message = m.Message,
                Timestamp = m.Timestamp,
                Attachments = m.Attachments?.Select(a => new ChatAttachmentDto
                {
                    FileName = a.FileName,
                    FilePath = a.FilePath
                }).ToList() ?? new List<ChatAttachmentDto>(),

                Delivered = _context.ChatMessageDeliveries
                    .Any(d => d.MessageId == m.Id && d.UserId != m.SenderId),
                Read = _context.ChatMessageReads
                    .Any(r => r.MessageId == m.Id && r.UserId != m.SenderId)
            }).ToList();

            return Ok(messageDtos);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd w GetMessages: {ex.Message}");
            return StatusCode(500, "Wystąpił błąd podczas pobierania wiadomości.");
        }
    }


    [HttpPost("pin/{messageId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Pin/unpin message", Description = "Toggle pin status of a message")]
    [SwaggerResponse(200, "Pin status changed")]
    [SwaggerResponse(403, "User not a participant")]
    [SwaggerResponse(404, "Message not found")]
    public async Task<IActionResult> TogglePinMessage(int messageId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var message = await _context.ChatMessages.Include(m => m.Chat).ThenInclude(c => c.Participants)
                                                 .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
            return NotFound("Wiadomość nie została znaleziona.");

        var chat = message.Chat;

        // Sprawdź, czy użytkownik jest uczestnikiem czatu
        if (!chat.Participants.Any(p => p.UserId == userId))
            return Forbid("Nie jesteś uczestnikiem tego czatu.");

        message.IsPinned = !message.IsPinned;
        await _context.SaveChangesAsync();

        var hub = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();

        foreach (var participant in chat.Participants)
        {
            if (participant.UserId != userId)
            {
                await hub.Clients.User(participant.UserId)
                    .SendAsync("MessagePinToggled", message.Id, message.IsPinned);
            }
        }

        return Ok(new { message = "Status przypięcia zmieniony.", isPinned = message.IsPinned });
    }


    [HttpGet("pinned/{chatId}")]
    [Authorize]
    [SwaggerOperation(Summary = "Get pinned messages", Description = "Get all pinned messages in a chat")]
    [SwaggerResponse(200, "Pinned messages returned")]
    [SwaggerResponse(403, "User not authorized")]
    public async Task<IActionResult> GetPinnedMessages(string chatId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var chat = await _context.Chats.Include(c => c.Participants)
                                       .FirstOrDefaultAsync(c => c.Id == chatId);

        if (chat == null || !chat.Participants.Any(p => p.UserId == userId))
            return Forbid();

        var pinnedMessages = await _context.ChatMessages
            .Where(m => m.ChatId == chatId && m.IsPinned)
            .Include(m => m.Attachments)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();

        var result = pinnedMessages.Select(m => new ChatMessageDto
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
        });

        return Ok(result);
    }

}

