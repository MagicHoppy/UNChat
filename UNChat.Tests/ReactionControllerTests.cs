using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using UNChat.Context;
using UNChat.Controllers;
using UNChat.DTOs;
using UNChat.Hubs;
using UNChat.Models;

public class ReactionControllerTests
{
    private readonly DbContextOptions<UNChatDbContext> _options;

    public ReactionControllerTests()
    {
        _options = new DbContextOptionsBuilder<UNChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private UNChatDbContext CreateContext()
    {
        var context = new UNChatDbContext(_options);
        context.Database.EnsureCreated();
        return context;
    }

    private User CreateTestUser(string userId)
    {
        return new User
        {
            Id = userId,
            Name = "Test User",
            UserName = $"user{userId}@test.com",
            Email = $"user{userId}@test.com",
            NormalizedEmail = $"USER{userId}@TEST.COM",
            NormalizedUserName = $"USER{userId}@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
    }

    private ChatMessage CreateTestMessage(int id, string chatId, string senderId)
    {
        return new ChatMessage
        {
            Id = id,
            ChatId = chatId,
            SenderId = senderId,
            Message = "Test message",
            Timestamp = DateTime.UtcNow
        };
    }

    private ReactionController CreateControllerWithHubMock(UNChatDbContext context)
    {
        var hubContextMock = new Mock<IHubContext<ChatHub>>();
        var clientProxyMock = new Mock<IClientProxy>();
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(x => x.All).Returns(clientProxyMock.Object);
        hubContextMock.Setup(x => x.Clients).Returns(clientsMock.Object);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IHubContext<ChatHub>)))
            .Returns(hubContextMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = serviceProviderMock.Object;

        var controller = new ReactionController(context);
        controller.ControllerContext.HttpContext = httpContext;

        return controller;
    }

    private void SetUser(ControllerBase controller, string userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "Test");

        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext.HttpContext.User = principal;
    }

    [Fact]
    public async Task AddReaction_AddsNewReaction_WhenNotExists()
    {
        using var context = CreateContext();

        var user1 = CreateTestUser("user1");
        var user2 = CreateTestUser("user2");
        var message = CreateTestMessage(1, "chat1", "user2");

        context.Users.AddRange(user1, user2);
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync();

        var controller = CreateControllerWithHubMock(context);
        SetUser(controller, "user1");

        var request = new ReactionDto
        {
            ChatMessageId = 1,
            EmojiId = 5
        };

        var result = await controller.AddReaction(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)okResult.Value?.GetType().GetProperty("added")?.GetValue(okResult.Value));

        var reaction = await context.MessageReactions.FirstOrDefaultAsync();
        Assert.NotNull(reaction);
        Assert.Equal(5, reaction.EmojiId);
        Assert.Equal("user1", reaction.UserId);
        Assert.Equal(1, reaction.ChatMessageId);
    }

    [Fact]
    public async Task AddReaction_RemovesReaction_WhenAlreadyExists()
    {
        using var context = CreateContext();

        var user1 = CreateTestUser("user1");
        var user2 = CreateTestUser("user2");
        var message = CreateTestMessage(1, "chat1", "user2");
        var reaction = new MessageReaction
        {
            ChatMessageId = 1,
            UserId = "user1",
            EmojiId = 5
        };

        context.Users.AddRange(user1, user2);
        context.ChatMessages.Add(message);
        context.MessageReactions.Add(reaction);
        await context.SaveChangesAsync();

        var controller = CreateControllerWithHubMock(context);
        SetUser(controller, "user1");

        var request = new ReactionDto
        {
            ChatMessageId = 1,
            EmojiId = 5
        };

        var result = await controller.AddReaction(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)okResult.Value?.GetType().GetProperty("removed")?.GetValue(okResult.Value));

        var reactionExists = await context.MessageReactions.AnyAsync();
        Assert.False(reactionExists);
    }

    [Fact]
    public async Task AddReaction_ReturnsUnauthorized_WhenUserIdMissing()
    {
        using var context = CreateContext();

        var controller = CreateControllerWithHubMock(context); // No user set

        var request = new ReactionDto
        {
            ChatMessageId = 1,
            EmojiId = 3
        };

        var result = await controller.AddReaction(request);
        Assert.IsType<UnauthorizedResult>(result);
    }
}