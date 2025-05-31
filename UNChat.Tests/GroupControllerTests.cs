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

}
