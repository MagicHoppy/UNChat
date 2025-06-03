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
using System.Security.Claims;
using System.IO;

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
    public async Task<IActionResult> Send([FromForm] string senderId, [FromForm] string chatId, [FromForm] string? message, [FromForm] IFormFile? file)
    {
        var chat = await _context.Chats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == chatId);

        if (chat == null)
        {
            Console.WriteLine($"Sender: {senderId}, ChatId: {chatId}, Message: {message}, File: {file?.FileName}");
            return NotFound("Chat not found.");
        }

        // Sprawdź czy nadawca jest uczestnikiem czatu
        if (!chat.Participants.Any(p => p.UserId == senderId))
        {
            return Forbid("You are not a participant of this chat");
        }

        var chatMessage = new ChatMessage
        {
            SenderId = senderId,
            ChatId = chatId,
            Message = message ?? string.Empty,
            Timestamp = DateTime.UtcNow
        };

        string? fileUrl = null;

        if (file != null && file.Length > 0)
        {
            var mediaExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mp3" };
            var docExtensions = new[] { ".pdf", ".txt", ".zip", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".json", ".log", ".md" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!mediaExtensions.Contains(ext) && !docExtensions.Contains(ext))
                return BadRequest("File type not allowed.");

            var originalFileName = Path.GetFileName(file.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}_{originalFileName}";
            string filePath;

            if (mediaExtensions.Contains(ext))
            {
                // Store in wwwroot/uploads (public renderable media)
                var uploadsFolder = Path.Combine("wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);
                filePath = Path.Combine(uploadsFolder, uniqueFileName);
                fileUrl = $"/uploads/{uniqueFileName}";
            }
            else
            {
                // Store outside wwwroot, serve via /download
                var secureFolder = Path.Combine("UploadsSecure");
                Directory.CreateDirectory(secureFolder);
                filePath = Path.Combine(secureFolder, uniqueFileName);
                fileUrl = $"/download/{uniqueFileName}";
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            chatMessage.Attachments.Add(new ChatAttachment
            {
                FileName = originalFileName,
                FilePath = fileUrl
            });
        }

        _context.ChatMessages.Add(chatMessage);

        var user = await _context.Users.FindAsync(senderId);
        if (user != null)
        {
            user.IsOnline = true;
            user.LastOnline = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        var hub = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
        var sender = await _context.Users.FindAsync(senderId);
        // Wykryj wzmianki @nazwaUzytkownika

        var mentionedUsers = new List<User>();
        if (!string.IsNullOrEmpty(message))
        {
            var allUsers = await _context.Users.ToListAsync();
            foreach (var userr in allUsers)
            {
                if (!string.IsNullOrEmpty(userr.Name) && message.Contains($"@{userr.Name}", StringComparison.OrdinalIgnoreCase))
                {
                    mentionedUsers.Add(userr);
                }
            }
        }

        // Wyślij powiadomienia do wzmiankowanych użytkowników
        var chatHub = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();

        foreach (var mentionedUser in mentionedUsers)
        {
            if (mentionedUser.Id != senderId)
            {
                // ✅ Check if the mentioned user is still in the chat
                bool isParticipant = chat.Participants.Any(p => p.UserId == mentionedUser.Id);
                if (!isParticipant)
                    continue;

                await chatHub.Clients.User(mentionedUser.Id)
                    .SendAsync("MentionNotification", new
                    {
                        from = sender.Name,
                        senderId,
                        chatId,
                        chatName = chat.Name,
                        message,
                        timestamp = chatMessage.Timestamp
                    });
            }
        }



        // Wysyłaj tylko do uczestników tego konkretnego czatu
        // await hub.Clients.Group(chatId)
        //.SendAsync("ReceiveMessage", senderId, message, fileUrl, chatMessage.Timestamp, chatMessage.Id, chatId);


        foreach (var participant in chat.Participants)
        {
            if (participant.UserId != senderId)
            {
                await hub.Clients.User(participant.UserId)
                    .SendAsync("ReceiveMessage", senderId, sender.Name, message, fileUrl, chatMessage.Timestamp, chatMessage.Id, chatId);
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
        // Można powiadomić innych użytkowników czatu:
        //await hub.Clients.Group(message.ChatId).SendAsync("MessageEdited", message.Id, message.Message);
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
    public IActionResult GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User not logged in.");

        return Ok(new { userId });
    }

    [HttpGet("messages/{chatId}")]
    [Authorize]
    public async Task<IActionResult> GetMessages(string chatId)
    {
        try
        {
            // 1) Pobieramy wiadomości
            var messages = await _context.ChatMessages
                .Where(m => m.ChatId == chatId)
                .Include(m => m.Attachments)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            // 2) Wyciągamy unikalne senderId, aby jednym zapytaniem pobrać nazwy
            var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();

            var userNames = await _context.Users
                .Where(u => senderIds.Contains(u.Id))
                .Select(u => new { u.Id, DisplayName = u.Name})
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName);
            //var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 3) Mapujemy do DTO
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
                    .Any(d => d.MessageId == m.Id && d.UserId != m.SenderId), // lub == currentUserId jeśli chcesz dokładnie
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

