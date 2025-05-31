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
    private UNChatDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<UNChatDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb")
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

}
