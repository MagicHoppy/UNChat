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
            var uploadsFolder = Path.Combine("wwwroot", "uploads");
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

        var user = await _context.Users.FindAsync(senderId);
        if (user != null)
        {
            user.IsOnline = true;
            user.LastOnline = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var hub = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();

        // Wysyłaj tylko do uczestników tego konkretnego czatu
       // await hub.Clients.Group(chatId)
        //.SendAsync("ReceiveMessage", senderId, message, fileUrl, chatMessage.Timestamp, chatMessage.Id, chatId);

        
        foreach (var participant in chat.Participants)
        {
            if (participant.UserId != senderId)
            {
                await hub.Clients.User(participant.UserId)
                    .SendAsync("ReceiveMessage", senderId, message, fileUrl, chatMessage.Timestamp, chatMessage.Id, chatId);
            }
        }

        return Ok(new
        {
            message,
            attachmentUrl = fileUrl,
            timestamp = chatMessage.Timestamp,
            id = chatMessage.Id,
        });
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
            var messages = await _context.ChatMessages
                .Where(m => m.ChatId == chatId)
                .Include(m => m.Attachments)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
            foreach (var message in messages){
                Console.WriteLine(message.Id);
                Console.WriteLine(message.ChatId);
                Console.WriteLine(message.Message);


            }
            var messageDtos = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                Message = m.Message,
                Timestamp = m.Timestamp,
                Attachments = m.Attachments?.Select(a => new ChatAttachmentDto
                {
                    FileName = a.FileName,
                    FilePath = a.FilePath
                }).ToList() ?? new List<ChatAttachmentDto>()
            }).ToList();
            return Ok(messageDtos);
        }
        catch (Exception ex)
        {
            // Zaloguj błąd
            Console.WriteLine($"Błąd w GetMessages: {ex.Message}");
            return StatusCode(500, "Wystąpił błąd podczas pobierania wiadomości.");
        }
    }

    [HttpPost("create-group")]
    [Authorize]
    public async Task<IActionResult> CreateGroupChat([FromBody] GroupChatDto dto)
    {
        var creatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(creatorId))
            return Unauthorized("Brak ID użytkownika.");

        var chat = new Chat
        {
            Name = dto.Name,
            IsGroup = true
        };

        _context.Chats.Add(chat);
        await _context.SaveChangesAsync();

        // Dodaj twórcę jako admina
        var participants = dto.UserIds.Distinct().Append(creatorId).Distinct().Select(id => new UserChat
        {
            UserId = id,
            ChatId = chat.Id,
            IsAdmin = id == creatorId
        });

        _context.UserChats.AddRange(participants);
        await _context.SaveChangesAsync();

        return Ok(new { chatId = chat.Id });
    }

    [HttpGet("groups")]
    [Authorize]
    public async Task<IActionResult> GetUserGroupChats()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized("Brak ID użytkownika.");

        var groupChats = await _context.UserChats
            .Where(uc => uc.UserId == userId && uc.Chat.IsGroup)
            .Select(uc => new
            {
                chatId = uc.Chat.Id,
                chatName = uc.Chat.Name,
                isAdmin = uc.IsAdmin
            })
            .ToListAsync();

        return Ok(groupChats);
    }

    [HttpPost("leave-group/{chatId}")]
    [Authorize]
    public async Task<IActionResult> LeaveGroup(string chatId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var userChat = await _context.UserChats
            .FirstOrDefaultAsync(uc => uc.ChatId == chatId && uc.UserId == userId);

        var chat = await _context.Chats
            .Include(c => c.Participants)
            .Include(c => c.Messages).ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(c => c.Id == chatId && c.IsGroup);

        if (userChat == null)
            return NotFound("Nie należysz do tego czatu.");

        var isAdmin = chat.Participants.Any(p => p.UserId == userId && p.IsAdmin);

        // Usuń użytkownika z czatu
        _context.UserChats.Remove(userChat);
        await _context.SaveChangesAsync();

        if (isAdmin)
        {
            // Odśwież dane czatu i uczestników po usunięciu użytkownika
            var updatedChat = await _context.Chats
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            var otherParticipants = updatedChat.Participants.ToList();

            // Sprawdź, czy którykolwiek z pozostałych uczestników jest adminem
            bool hasOtherAdmins = otherParticipants.Any(p => p.IsAdmin);

            // Jeśli nie ma żadnych adminów, wylosuj nowego
            if (!hasOtherAdmins && otherParticipants.Any())
            {
                var random = new Random();
                var randomParticipant = otherParticipants[random.Next(otherParticipants.Count)];
                randomParticipant.IsAdmin = true;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Opuściłeś grupę jako administrator. Nowy administrator został wybrany." });
            }

            return Ok(new { message = "Opuściłeś grupę jako administrator." });
        }
        else
        {
            return Ok(new { message = "Opuściłeś grupę." });
        }
    }


    [HttpDelete("delete-group/{chatId}")]
    [Authorize]
    public async Task<IActionResult> DeleteGroup(string chatId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var chat = await _context.Chats
            .Include(c => c.Participants)
            .Include(c => c.Messages).ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(c => c.Id == chatId && c.IsGroup);

        if (chat == null)
            return NotFound("Grupa nie istnieje.");

        var isAdmin = chat.Participants.Any(p => p.UserId == userId && p.IsAdmin);

        if (!isAdmin)
            return Forbid("Tylko administrator może usunąć grupę.");

        // Usuń załączniki
        foreach (var message in chat.Messages)
        {
            foreach (var attachment in message.Attachments)
            {
                var filePath = Path.Combine("wwwroot", attachment.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
        }

        _context.Chats.Remove(chat);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Grupa została usunięta." });
    }
    [HttpGet("members/{chatId}")]
    [Authorize]
    public async Task<IActionResult> GetChatMembers(string chatId)
    {
        var members = await _context.UserChats
            .Where(uc => uc.ChatId == chatId)
            .Select(uc => new
            {
                userId = uc.UserId,
                name = uc.User.Name,
                isAdmin = uc.IsAdmin
            }).ToListAsync();

        return Ok(members);
    }

    [HttpPost("remove-member")]
    [Authorize]
    public async Task<IActionResult> RemoveMember([FromBody] MemberEditDto dto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var chat = await _context.Chats.Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == dto.ChatId && c.IsGroup);

        if (chat == null)
            return NotFound("Czat nie istnieje.");

        if (!chat.Participants.Any(p => p.UserId == userId && p.IsAdmin))
            return Forbid("Brak uprawnień.");

        var member = chat.Participants.FirstOrDefault(p => p.UserId == dto.UserId);
        if (member != null)
        {
            _context.UserChats.Remove(member);
            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpPost("promote")]
    [Authorize]
    public async Task<IActionResult> PromoteToAdmin([FromBody] MemberEditDto dto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var chat = await _context.Chats.Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == dto.ChatId && c.IsGroup);

        if (chat == null)
            return NotFound("Czat nie istnieje.");

        if (!chat.Participants.Any(p => p.UserId == userId && p.IsAdmin))
            return Forbid("Brak uprawnień.");

        var member = chat.Participants.FirstOrDefault(p => p.UserId == dto.UserId);
        if (member != null)
        {
            member.IsAdmin = true;
            await _context.SaveChangesAsync();
        }

        return Ok();
    }
    [HttpPost("toggle-admin")]
    [Authorize]
    public async Task<IActionResult> ToggleAdmin([FromBody] MemberEditDto dto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var chat = await _context.Chats.Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == dto.ChatId && c.IsGroup);

        if (chat == null)
            return NotFound("Czat nie istnieje.");

        if (!chat.Participants.Any(p => p.UserId == userId && p.IsAdmin))
            return Forbid("Brak uprawnień.");

        var member = chat.Participants.FirstOrDefault(p => p.UserId == dto.UserId);
        if (member != null)
        {
            member.IsAdmin = !member.IsAdmin; // toggle
            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpPost("invite")]
    [Authorize]
    public async Task<IActionResult> InviteToGroup([FromBody] MemberEditDto dto)
    {
        var alreadyInGroup = await _context.UserChats
            .AnyAsync(uc => uc.ChatId == dto.ChatId && uc.UserId == dto.UserId);

        if (!alreadyInGroup)
        {
            _context.UserChats.Add(new UserChat
            {
                ChatId = dto.ChatId,
                UserId = dto.UserId,
                IsAdmin = false
            });
            await _context.SaveChangesAsync();
        }

        return Ok();
    }



}
public class MemberEditDto
{
    public string ChatId { get; set; }
    public string UserId { get; set; }
}
