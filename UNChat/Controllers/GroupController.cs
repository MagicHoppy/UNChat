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

        var groupChats = await _context.UserChats
            .Where(uc => uc.UserId == userId && uc.Chat.IsGroup)
            .Select(uc => new GroupChatResultDto
            {
                ChatId = uc.Chat.Id,
                ChatName = uc.Chat.Name,
                IsAdmin = uc.IsAdmin
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
        if(chat == null)
        {
            return NotFound("Grupa nie istnieje");
        }
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
            if (updatedChat == null) 
            {
                return NotFound("Grupa nie istnieje");
            }
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
