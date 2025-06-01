using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using UNChat.Controllers;
using UNChat.Context;
using UNChat.DTOs;
using UNChat.Models;
using UNChat.DTOs;
public class GroupControllerTests
{
    private UNChatDbContext GetInMemoryDbContext([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var options = new DbContextOptionsBuilder<UNChatDbContext>()
            .UseInMemoryDatabase(databaseName: dbName) // Unique per test
            .Options;

        return new UNChatDbContext(options);
    }

    private ControllerContext GetControllerContextWithUser(string userId)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "TestAuth"));

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task CreateGroupChat_ReturnsOkWithChatId()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("user1")
        };

        var dto = new GroupChatDto
        {
            Name = "Test Group",
            UserIds = new List<string> { "user2", "user3" }
        };

        // Act
        var result = await controller.CreateGroupChat(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("chatId", okResult.Value.ToString());

        var chat = await db.Chats.FirstOrDefaultAsync();
        Assert.NotNull(chat);
        Assert.True(chat.IsGroup);
        Assert.Equal("Test Group", chat.Name);

        var participants = await db.UserChats.ToListAsync();
        Assert.Equal(3, participants.Count); // user1, user2, user3
    }

    [Fact]
    public async Task CreateGroupChat_ReturnsUnauthorized_IfNoUserId()
    {
        // Arrange
        var db = GetInMemoryDbContext();

        var controller = new GroupController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    // User with empty identity (no claims)
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var dto = new GroupChatDto
        {
            Name = "Test Group",
            UserIds = new List<string> { "user2" }
        };

        // Act
        var result = await controller.CreateGroupChat(dto);

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Brak ID użytkownika.", unauthorized.Value);
    }
    [Fact]
    public async Task GetUserGroupChats_ReturnsUserGroups()
    {
        var db = GetInMemoryDbContext();
        var userId = "user1";

        var groupChat = new Chat { Name = "Group A", IsGroup = true };
        var directChat = new Chat { Name = "Private", IsGroup = false };
        db.Chats.AddRange(groupChat, directChat);
        await db.SaveChangesAsync();

        db.UserChats.AddRange(
            new UserChat { ChatId = groupChat.Id, UserId = userId, IsAdmin = true },
            new UserChat { ChatId = directChat.Id, UserId = userId, IsAdmin = false }
        );
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser(userId)
        };

        // Act
        var result = await controller.GetUserGroupChats();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var groupResults = Assert.IsAssignableFrom<List<GroupChatResultDto>>(okResult.Value);

        Assert.Single(groupResults);
        Assert.Equal(groupChat.Id, groupResults[0].ChatId);
        Assert.Equal("Group A", groupResults[0].ChatName);
        Assert.True(groupResults[0].IsAdmin);
    }


    [Fact]
    public async Task GetUserGroupChats_ReturnsUnauthorized_IfNoUserId()
    {
        // Arrange
        var db = GetInMemoryDbContext();

        var controller = new GroupController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity()) // No claims
                }
            }
        };

        // Act
        var result = await controller.GetUserGroupChats();

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Brak ID użytkownika.", unauthorized.Value);
    }
    [Fact]
    public async Task LeaveGroup_ReturnsNotFound_IfUserNotInGroup()
    {
        var db = GetInMemoryDbContext();
        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("user1")
        };

        // Setup a group without user1
        var chat = new Chat { Name = "Group A", IsGroup = true };
        db.Chats.Add(chat);
        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "user2", IsAdmin = true });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.LeaveGroup(chat.Id);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var dto = Assert.IsType<LeaveGroupResponseDto>(notFound.Value);
        Assert.Contains("Nie należysz do tego czatu.", dto.Message);
    }


    [Fact]
    public async Task LeaveGroup_RemovesRegularUser_ReturnsOk()
    {
        var db = GetInMemoryDbContext();
        var userId = "user1";
        var chat = new Chat { Name = "Group B", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = userId, IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser(userId)
        };

        // Act
        var result = await controller.LeaveGroup(chat.Id);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<LeaveGroupResponseDto>(ok.Value);
        Assert.Contains("Opuściłeś grupę.", dto.Message);
        Assert.DoesNotContain(await db.UserChats.ToListAsync(), uc => uc.UserId == userId);
    }

    [Fact]
    public async Task LeaveGroup_AdminLeaves_AndAnotherAdminExists()
    {
        var db = GetInMemoryDbContext();
        var userId = "admin1";
        var chat = new Chat { Name = "Group C", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.UserChats.AddRange(
            new UserChat { ChatId = chat.Id, UserId = userId, IsAdmin = true },
            new UserChat { ChatId = chat.Id, UserId = "admin2", IsAdmin = true }
        );
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser(userId)
        };

        // Act
        var result = await controller.LeaveGroup(chat.Id);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<LeaveGroupResponseDto>(ok.Value);
        Assert.Contains("Opuściłeś grupę jako administrator", dto.Message);
        Assert.DoesNotContain(await db.UserChats.ToListAsync(), uc => uc.UserId == userId);
    }

    [Fact]
    public async Task LeaveGroup_AdminLeaves_NoOtherAdmins_NewAdminSelected()
    {
        var db = GetInMemoryDbContext();
        var userId = "admin1";
        var otherUser = "user2";

        var chat = new Chat { Name = "Group D", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.UserChats.AddRange(
            new UserChat { ChatId = chat.Id, UserId = userId, IsAdmin = true },
            new UserChat { ChatId = chat.Id, UserId = otherUser, IsAdmin = false }
        );
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser(userId)
        };

        // Act
        var result = await controller.LeaveGroup(chat.Id);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<LeaveGroupResponseDto>(ok.Value);
        Assert.Contains("Nowy administrator został wybrany", dto.Message);

        var participants = await db.UserChats.ToListAsync();
        Assert.DoesNotContain(participants, p => p.UserId == userId);
        Assert.True(participants.First().IsAdmin);
    }
    [Fact]
    public async Task LeaveGroup_LastUser_DeletesGroup()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userId = "user1";

        var chat = new Chat { Name = "Group E", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = userId, IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser(userId)
        };

        // Act
        var result = await controller.LeaveGroup(chat.Id);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<LeaveGroupResponseDto>(ok.Value);
        Assert.Contains("Opuściłeś grupę. Grupa została usunięta.", dto.Message);

        var chatExists = await db.Chats.AnyAsync(c => c.Id == chat.Id);
        var userChatExists = await db.UserChats.AnyAsync(uc => uc.ChatId == chat.Id && uc.UserId == userId);

        Assert.False(chatExists, "Group chat should be deleted after last user leaves.");
        Assert.False(userChatExists, "UserChat relation should be deleted.");
    }
    [Fact]
    //TODO: Refactor żeby kontrolery zajmujące się plikami używały interfejsu IFIleService
    //i wtedy trzeba ten test też poprawić, ale mi się nie chce teraz.
    public async Task DeleteGroup_AdminDeletesGroup_ReturnsOkAndRemovesChat() 
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var chat = new Chat { Name = "Test Group", IsGroup = true };
        var adminId = "admin1";

        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = adminId, IsAdmin = true });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser(adminId)
        };

        // Act
        var result = await controller.DeleteGroup(chat.Id);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("Grupa została usunięta", ok.Value.ToString());

        Assert.False(await db.Chats.AnyAsync(c => c.Id == chat.Id), "Chat should be deleted");
    }

    [Fact]
    public async Task DeleteGroup_NonAdmin_ReturnsForbid()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var chat = new Chat { Name = "Test Group", IsGroup = true };
        var userId = "user1";

        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = userId, IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser(userId)
        };

        // Act
        var result = await controller.DeleteGroup(chat.Id);

        // Assert
        var forbid = Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeleteGroup_GroupNotFound_ReturnsNotFound()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("anyuser")
        };

        // Act
        var result = await controller.DeleteGroup("nonexistent-id");

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Grupa nie istnieje.", notFound.Value);
    }

    [Fact]
    public async Task GetChatMembers_ReturnsMembersOfChat()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var chatId = "chat123";

        var user1 = new User { Id = "user1", Name = "Alice" };
        var user2 = new User { Id = "user2", Name = "Bob" };
        await db.Users.AddRangeAsync(user1, user2);

        var chat = new Chat { Id = chatId, Name = "Test Group", IsGroup = true };
        await db.Chats.AddAsync(chat);

        var uc1 = new UserChat { ChatId = chatId, UserId = "user1", IsAdmin = true, User = user1 };
        var uc2 = new UserChat { ChatId = chatId, UserId = "user2", IsAdmin = false, User = user2 };
        await db.UserChats.AddRangeAsync(uc1, uc2);

        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("user1")
        };

        // Act
        var result = await controller.GetChatMembers(chatId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var members = Assert.IsAssignableFrom<List<ChatMemberDto>>(okResult.Value);

        Assert.Equal(2, members.Count);

        var alice = members.FirstOrDefault(m => m.UserId == "user1");
        Assert.NotNull(alice);
        Assert.Equal("Alice", alice.Name);
        Assert.True(alice.IsAdmin);

        var bob = members.FirstOrDefault(m => m.UserId == "user2");
        Assert.NotNull(bob);
        Assert.Equal("Bob", bob.Name);
        Assert.False(bob.IsAdmin);
    }
    [Fact]
    public async Task GetChatMembers_ReturnsEmptyList_IfNoMembers()
    {
        var db = GetInMemoryDbContext();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("user1")
        };

        var result = await controller.GetChatMembers("nonexistent-chat");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var members = Assert.IsAssignableFrom<List<ChatMemberDto>>(okResult.Value);

        Assert.Empty(members);
    }
    [Fact]
    public async Task RemoveMember_ChatDoesNotExist_ReturnsNotFound()
    {
        var db = GetInMemoryDbContext();
        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("admin1")
        };

        var dto = new MemberEditDto { ChatId = "nonexistent", UserId = "user1" };

        var result = await controller.RemoveMember(dto);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Czat nie istnieje.", notFound.Value);
    }

    [Fact]
    public async Task RemoveMember_UserNotAdmin_ReturnsForbidden()
    {
        var db = GetInMemoryDbContext();
        var chat = new Chat { Id = "chat1", Name = "Test Group", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        // Add a participant who is NOT admin (current user)
        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "user1", IsAdmin = false });

        // Add another participant who is admin
        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "admin2", IsAdmin = true });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("user1") // Not admin
        };

        var dto = new MemberEditDto { ChatId = chat.Id, UserId = "admin2" };

        var result = await controller.RemoveMember(dto);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        Assert.Equal("Brak uprawnień.", status.Value);
    }

    [Fact]
    public async Task RemoveMember_MemberDoesNotExist_ReturnsNotFound()
    {
        var db = GetInMemoryDbContext();
        var chat = new Chat { Id = "chat1", Name = "Test Group", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        // Current user is admin
        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "admin1", IsAdmin = true });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("admin1")
        };

        var dto = new MemberEditDto { ChatId = chat.Id, UserId = "nonmember" };

        var result = await controller.RemoveMember(dto);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Użytkownik nie jest członkiem grupy.", notFound.Value);
    }

    [Fact]
    public async Task RemoveMember_MemberExists_RemovesMemberAndReturnsOk()
    {
        var db = GetInMemoryDbContext();
        var chat = new Chat { Id = "chat1", Name = "Test Group", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        // Current user is admin
        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "admin1", IsAdmin = true });

        // Member to remove
        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "userToRemove", IsAdmin = false });

        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("admin1")
        };

        var dto = new MemberEditDto { ChatId = chat.Id, UserId = "userToRemove" };

        var result = await controller.RemoveMember(dto);

        var okResult = Assert.IsType<OkResult>(result);

        var userChatsLeft = db.UserChats.Where(uc => uc.ChatId == chat.Id).ToList();
        Assert.DoesNotContain(userChatsLeft, uc => uc.UserId == "userToRemove");
    }
    [Fact]
    public async Task PromoteToAdmin_ChatDoesNotExist_ReturnsNotFound()
    {
        var db = GetInMemoryDbContext();
        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("admin1")
        };

        var dto = new MemberEditDto { ChatId = "nonexistent", UserId = "user1" };

        var result = await controller.PromoteToAdmin(dto);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Czat nie istnieje.", notFound.Value);
    }

    [Fact]
    public async Task PromoteToAdmin_UserNotAdmin_ReturnsForbidden()
    {
        var db = GetInMemoryDbContext();
        var chat = new Chat { Id = "chat1", Name = "Test Group", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "user1", IsAdmin = false }); // current user, not admin
        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "userToPromote", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("user1")
        };

        var dto = new MemberEditDto { ChatId = chat.Id, UserId = "userToPromote" };

        var result = await controller.PromoteToAdmin(dto);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        Assert.Equal("Brak uprawnień.", status.Value);
    }

    [Fact]
    public async Task PromoteToAdmin_MemberDoesNotExist_ReturnsNotFound()
    {
        var db = GetInMemoryDbContext();
        var chat = new Chat { Id = "chat1", Name = "Test Group", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "admin1", IsAdmin = true });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("admin1")
        };

        var dto = new MemberEditDto { ChatId = chat.Id, UserId = "nonmember" };

        var result = await controller.PromoteToAdmin(dto);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Użytkownik nie jest członkiem grupy.", notFound.Value);
    }

    [Fact]
    public async Task PromoteToAdmin_MemberExists_SetsIsAdminTrueAndReturnsOk()
    {
        var db = GetInMemoryDbContext();
        var chat = new Chat { Id = "chat1", Name = "Test Group", IsGroup = true };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "admin1", IsAdmin = true });
        db.UserChats.Add(new UserChat { ChatId = chat.Id, UserId = "userToPromote", IsAdmin = false });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("admin1")
        };

        var dto = new MemberEditDto { ChatId = chat.Id, UserId = "userToPromote" };

        var result = await controller.PromoteToAdmin(dto);

        var okResult = Assert.IsType<OkResult>(result);

        var promotedMember = await db.UserChats
            .FirstOrDefaultAsync(uc => uc.ChatId == chat.Id && uc.UserId == "userToPromote");

        Assert.True(promotedMember.IsAdmin);
    }
    [Fact]
    public async Task ToggleAdmin_ReturnsNotFound_IfChatDoesNotExist()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("adminUser")
        };
        var dto = new MemberEditDto { ChatId = "nonexistent", UserId = "member1" };

        // Act
        var result = await controller.ToggleAdmin(dto);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Czat nie istnieje.", notFound.Value);
    }

    [Fact]
    public async Task ToggleAdmin_ReturnsForbid_IfUserNotAdmin()
    {
        var db = GetInMemoryDbContext();

        var chat = new Chat { Id = "chat1", IsGroup = true };
        var participant = new UserChat { ChatId = "chat1", UserId = "normalUser", IsAdmin = false };
        chat.Participants = new List<UserChat> { participant };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("normalUser")
        };
        var dto = new MemberEditDto { ChatId = "chat1", UserId = "member1" };

        var result = await controller.ToggleAdmin(dto);

        var forbid = Assert.IsType<ForbidResult>(result);
        // ForbidResult has no Value, but you could test the type only here
    }

    [Fact]
    public async Task ToggleAdmin_TogglesMemberAdminStatus()
    {
        var db = GetInMemoryDbContext();

        var chat = new Chat { Id = "chat1", IsGroup = true };
        var adminUser = new UserChat { ChatId = "chat1", UserId = "adminUser", IsAdmin = true };
        var member = new UserChat { ChatId = "chat1", UserId = "member1", IsAdmin = false };
        chat.Participants = new List<UserChat> { adminUser, member };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("adminUser")
        };
        var dto = new MemberEditDto { ChatId = "chat1", UserId = "member1" };

        var result = await controller.ToggleAdmin(dto);

        var ok = Assert.IsType<OkResult>(result);

        // Check if the admin status was toggled
        var updatedMember = await db.UserChats.FirstOrDefaultAsync(uc => uc.UserId == "member1" && uc.ChatId == "chat1");
        Assert.True(updatedMember.IsAdmin); // Initially false, now toggled to true

        // Toggle back
        result = await controller.ToggleAdmin(dto);
        Assert.IsType<OkResult>(result);
        updatedMember = await db.UserChats.FirstOrDefaultAsync(uc => uc.UserId == "member1" && uc.ChatId == "chat1");
        Assert.False(updatedMember.IsAdmin);
    }

    [Fact]
    public async Task ToggleAdmin_ReturnsOk_IfMemberNotFound()
    {
        var db = GetInMemoryDbContext();

        var chat = new Chat { Id = "chat1", IsGroup = true };
        var adminUser = new UserChat { ChatId = "chat1", UserId = "adminUser", IsAdmin = true };
        chat.Participants = new List<UserChat> { adminUser };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("adminUser")
        };
        var dto = new MemberEditDto { ChatId = "chat1", UserId = "nonexistentMember" };

        var result = await controller.ToggleAdmin(dto);

        Assert.IsType<OkResult>(result);
    }
    [Fact]
    public async Task InviteToGroup_DoesNotAddUserIfAlreadyInGroup()
    {
        // Arrange
        var db = GetInMemoryDbContext();

        var chatId = "chat1";
        var userId = "user1";

        db.UserChats.Add(new UserChat
        {
            ChatId = chatId,
            UserId = userId,
            IsAdmin = false
        });
        await db.SaveChangesAsync();

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("anyUser")
        };

        var dto = new MemberEditDto
        {
            ChatId = chatId,
            UserId = userId
        };

        // Act
        var result = await controller.InviteToGroup(dto);

        // Assert
        var okResult = Assert.IsType<OkResult>(result);

        // Confirm no duplicate was added
        var userChats = await db.UserChats.Where(uc => uc.ChatId == chatId && uc.UserId == userId).ToListAsync();
        Assert.Single(userChats);
    }

    [Fact]
    public async Task InviteToGroup_AddsUserIfNotInGroup()
    {
        // Arrange
        var db = GetInMemoryDbContext();

        var chatId = "chat1";
        var userId = "newUser";

        var controller = new GroupController(db)
        {
            ControllerContext = GetControllerContextWithUser("anyUser")
        };

        var dto = new MemberEditDto
        {
            ChatId = chatId,
            UserId = userId
        };

        // Act
        var result = await controller.InviteToGroup(dto);

        // Assert
        var okResult = Assert.IsType<OkResult>(result);

        var userChat = await db.UserChats.FirstOrDefaultAsync(uc => uc.ChatId == chatId && uc.UserId == userId);
        Assert.NotNull(userChat);
        Assert.False(userChat.IsAdmin);
    }


}