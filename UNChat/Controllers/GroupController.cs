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
public class GroupController : ControllerBase
{
    private readonly UNChatDbContext _context;

    public GroupController(UNChatDbContext context)
    {
        _context = context;
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

        // Pobierz czaty grupowe użytkownika
        var groupChats = await _context.UserChats
            .Where(uc => uc.UserId == userId && uc.Chat.IsGroup)
            .Select(uc => new
            {
                uc.Chat.Id,
                uc.Chat.Name,
                uc.IsAdmin
            })
            .ToListAsync();

        var groupChatIds = groupChats.Select(g => g.Id).ToList();

        // Pobierz ostatnie wiadomości dla tych czatów
        var lastMessages = await _context.ChatMessages
            .Where(m => groupChatIds.Contains(m.ChatId))
            .GroupBy(m => m.ChatId)
            .Select(g => new
            {
                ChatId = g.Key,
                LastMessageTime = g.Max(m => m.Timestamp)
            })
            .ToListAsync();

        // Zbuduj wynik z LastMessageTime
        var result = groupChats.Select(g =>
        {
            var lastMsg = lastMessages.FirstOrDefault(lm => lm.ChatId == g.Id);
            DateTime? lastMessageTime = lastMsg?.LastMessageTime;

            return new
            {
                ChatId = g.Id,
                ChatName = g.Name,
                IsAdmin = g.IsAdmin,
                LastMessageTime = lastMessageTime
            };
        });

        return Ok(result);
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
            return NotFound(new LeaveGroupResponseDto { Message = "Nie należysz do tego czatu." });

        if (chat == null)
            return NotFound(new LeaveGroupResponseDto { Message = "Grupa nie istnieje" });

        var isAdmin = chat.Participants.Any(p => p.UserId == userId && p.IsAdmin);

        // Usuń użytkownika z czatu
        _context.UserChats.Remove(userChat);
        await _context.SaveChangesAsync();

        // Odśwież dane czatu i uczestników po usunięciu użytkownika
        var updatedChat = await _context.Chats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == chatId);

        if (updatedChat == null)
            return NotFound(new LeaveGroupResponseDto { Message = "Grupa nie istnieje" });

        var remainingParticipants = updatedChat.Participants.ToList();

        if (!remainingParticipants.Any())
        {
            // Usuń wiadomości i załączniki, jeśli potrzebne
            var messages = await _context.ChatMessages
                .Where(m => m.ChatId == chatId)
                .Include(m => m.Attachments)
                .ToListAsync();

            _context.ChatAttachments.RemoveRange(messages.SelectMany(m => m.Attachments));
            _context.ChatMessages.RemoveRange(messages);
            _context.Chats.Remove(updatedChat);
            await _context.SaveChangesAsync();

            return Ok(new LeaveGroupResponseDto
            {
                Message = "Opuściłeś grupę. Grupa została usunięta."
            });
        }

        if (isAdmin)
        {
            bool hasOtherAdmins = remainingParticipants.Any(p => p.IsAdmin);
            if (!hasOtherAdmins)
            {
                var random = new Random();
                var randomParticipant = remainingParticipants[random.Next(remainingParticipants.Count)];
                randomParticipant.IsAdmin = true;
                await _context.SaveChangesAsync();

                return Ok(new LeaveGroupResponseDto
                {
                    Message = "Opuściłeś grupę jako administrator. Nowy administrator został wybrany."
                });
            }

            return Ok(new LeaveGroupResponseDto
            {
                Message = "Opuściłeś grupę jako administrator."
            });
        }

        return Ok(new LeaveGroupResponseDto
        {
            Message = "Opuściłeś grupę."
        });
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
            .Select(uc => new ChatMemberDto
            {
                UserId = uc.UserId,
                Name = uc.User.Name,
                IsAdmin = uc.IsAdmin
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
            return StatusCode(StatusCodes.Status403Forbidden, "Brak uprawnień.");

        var member = chat.Participants.FirstOrDefault(p => p.UserId == dto.UserId);
        if (member == null)
        {
            return NotFound("Użytkownik nie jest członkiem grupy.");
        }

        _context.UserChats.Remove(member);
        await _context.SaveChangesAsync();

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
            return StatusCode(StatusCodes.Status403Forbidden, "Brak uprawnień.");

        var member = chat.Participants.FirstOrDefault(p => p.UserId == dto.UserId);
        if (member == null)
            return NotFound("Użytkownik nie jest członkiem grupy.");

        member.IsAdmin = true;
        await _context.SaveChangesAsync();

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
