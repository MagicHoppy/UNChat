using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UNChat.Context;
using UNChat.Controllers;
using UNChat.Models;
using Xunit;

public class ChatControllerTests
{
    private Mock<UserManager<User>> GetMockUserManager()
    {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
    }

    private ClaimsPrincipal GetMockUser(string userId)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }

    private UNChatDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<UNChatDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb_" + System.Guid.NewGuid())
            .Options;
        return new UNChatDbContext(options);
    }

    [Fact]
    public async Task Chat_ReturnsViewWithModel()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        dbContext.Emojis.Add(new Emoji { Symbol = "😀" });
        dbContext.Emojis.Add(new Emoji { Symbol = "🚀" });
        dbContext.SaveChanges();

        var userManager = GetMockUserManager();
        var mockUser = new User { Id = "user123" };
        userManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(mockUser);

        var controller = new ChatController(userManager.Object, dbContext);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = GetMockUser("user123")
            }
        };

        // Act
        var result = await controller.Chat();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<ChatViewModel>(viewResult.Model);
        Assert.Equal("user123", model.UserId);
        Assert.Contains("😀", model.Emojis);
        Assert.Contains("🚀", model.Emojis);
    }

    [Fact]
    public async Task GetUsers_ReturnsNonFriends()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();

        var user1 = new User { Id = "1", Name = "User1" };
        var user2 = new User { Id = "2", Name = "User2" };
        var user3 = new User { Id = "3", Name = "User3" };

        dbContext.Users.AddRange(user1, user2, user3);
        dbContext.Friends.Add(new Friend { Friend1Id = "1", Friend2Id = "2", Status = FriendStatus.Accepted });
        dbContext.SaveChanges();

        var userManager = GetMockUserManager();
        userManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user1);
        userManager.Setup(um => um.Users).Returns(dbContext.Users.AsQueryable());

        var controller = new ChatController(userManager.Object, dbContext);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = GetMockUser("1")
            }
        };

        // Act
        var result = await controller.GetUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
    var users = okResult.Value as IEnumerable<dynamic>; // Use dynamic to handle anonymous type
    Assert.NotNull(users);
    Assert.Single(users); // only user3 is not a friend
    }
}
