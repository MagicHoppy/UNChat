using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UNChat.Context;
using UNChat.Controllers;
using UNChat.Models;
using Xunit;

public class StatsApiControllerTests
{
    private readonly UNChatDbContext _context;
    private readonly StatsApiController _controller;

    public StatsApiControllerTests()
    {
        var options = new DbContextOptionsBuilder<UNChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new UNChatDbContext(options);
        _controller = new StatsApiController(_context);
    }

    [Fact]
    public async Task GetTotals_ReturnsOkWithCorrectCounts()
    {
        // Arrange
        _context.Chats.AddRange(
            new Chat { Id = "chat1" },
            new Chat { Id = "chat2" }
        );
        _context.Users.AddRange(
            new User { Id = "user1", UserName = "User1", Name = "User1" },
            new User { Id = "user2", UserName = "User2", Name = "User2" }
        );
        _context.ChatMessages.AddRange(
            new ChatMessage { Id = 1, SenderId = "user1", ChatId = "chat1", Message = "Hello", Timestamp = DateTime.UtcNow },
            new ChatMessage { Id = 2, SenderId = "user2", ChatId = "chat2", Message = "World", Timestamp = DateTime.UtcNow }
        );
        _context.SaveChanges();

        // Act
        var result = await _controller.GetTotals();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = okResult.Value;

        var totalMessages = (int?)returnValue.GetType().GetProperty("totalMessages")?.GetValue(returnValue);
        var totalChats = (int?)returnValue.GetType().GetProperty("totalChats")?.GetValue(returnValue);
        var totalUsers = (int?)returnValue.GetType().GetProperty("totalUsers")?.GetValue(returnValue);

        Assert.Equal(2, totalMessages);
        Assert.Equal(2, totalChats);
        Assert.Equal(2, totalUsers);
    }


    [Fact]
    public async Task GetTopUser_ReturnsOkWithTopUserDetails_WhenMessagesExist()
    {
        // Arrange
        _context.Chats.AddRange(
            new Chat { Id = "chat1" },
            new Chat { Id = "chat2" }
        );
        _context.Users.AddRange(
            new User { Id = "user1", UserName = "TopUser", Name = "TopUser" },
            new User { Id = "user2", UserName = "OtherUser", Name = "OtherUser" }
        );
        _context.ChatMessages.AddRange(
            new ChatMessage { Id = 1, SenderId = "user1", ChatId = "chat1", Message = "A", Timestamp = DateTime.UtcNow },
            new ChatMessage { Id = 2, SenderId = "user1", ChatId = "chat1", Message = "B", Timestamp = DateTime.UtcNow },
            new ChatMessage { Id = 3, SenderId = "user2", ChatId = "chat2", Message = "C", Timestamp = DateTime.UtcNow }
        );
        _context.SaveChanges();

        // Act
        var result = await _controller.GetTopUser();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = okResult.Value;

        var id = returnValue.GetType().GetProperty("Id")?.GetValue(returnValue) as string;
        var userName = returnValue.GetType().GetProperty("UserName")?.GetValue(returnValue) as string;
        var messageCount = (int?)returnValue.GetType().GetProperty("MessageCount")?.GetValue(returnValue);

        Assert.Equal("user1", id);
        Assert.Equal("TopUser", userName);
        Assert.Equal(2, messageCount);
    }
    [Fact]
    public async Task GetMessagesLast7Days_ReturnsCorrectCounts()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _context.ChatMessages.AddRange(
            new ChatMessage { Id = 1, SenderId = "user1", ChatId = "chat1", Message = "A", Timestamp = now.AddDays(-1) },
            new ChatMessage { Id = 2, SenderId = "user2", ChatId = "chat1", Message = "B", Timestamp = now.AddDays(-2) },
            new ChatMessage { Id = 3, SenderId = "user1", ChatId = "chat2", Message = "C", Timestamp = now.AddDays(-8) } // poza zakresem
        );
        _context.SaveChanges();

        // Act
        var result = await _controller.GetMessagesLast7Days();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);

        var items = list.Cast<object>().ToList();
        Assert.Equal(2, items.Count);

        var first = items[0];
        var second = items[1];

        var firstCount = (int?)first.GetType().GetProperty("Count")?.GetValue(first);
        var secondCount = (int?)second.GetType().GetProperty("Count")?.GetValue(second);

        Assert.Equal(1, firstCount);
        Assert.Equal(1, secondCount);
    }

    [Fact]
    public async Task GetMostActiveChat_ReturnsChatWithMostMessages()
    {
        // Arrange
        _context.Chats.AddRange(
            new Chat { Id = "chat1", Name = "Chat One" },
            new Chat { Id = "chat2", Name = "Chat Two" }
        );
        _context.ChatMessages.AddRange(
            new ChatMessage { Id = 1, SenderId = "user1", ChatId = "chat1", Message = "A", Timestamp = DateTime.UtcNow },
            new ChatMessage { Id = 2, SenderId = "user2", ChatId = "chat1", Message = "B", Timestamp = DateTime.UtcNow },
            new ChatMessage { Id = 3, SenderId = "user1", ChatId = "chat2", Message = "C", Timestamp = DateTime.UtcNow }
        );
        _context.SaveChanges();

        // Act
        var result = await _controller.GetMostActiveChat();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = okResult.Value;

        var id = returnValue.GetType().GetProperty("Id")?.GetValue(returnValue) as string;
        var name = returnValue.GetType().GetProperty("Name")?.GetValue(returnValue) as string;
        var messageCount = (int?)returnValue.GetType().GetProperty("MessageCount")?.GetValue(returnValue);

        Assert.Equal("chat1", id);
        Assert.Equal("Chat One", name);
        Assert.Equal(2, messageCount);
    }

    [Fact]
    public async Task GetTopEmojis_ReturnsTop5Emojis()
    {
        // Arrange
        _context.Emojis.AddRange(
            new Emoji { Id = 1, Symbol = "😀" },
            new Emoji { Id = 2, Symbol = "🔥" }
        );
        _context.Users.Add(
            new User { Id = "user1", UserName = "User1", Name = "User1" }
        );
        _context.MessageReactions.AddRange(
            new MessageReaction { Id = 1, EmojiId = 1, UserId = "user1" },
            new MessageReaction { Id = 2, EmojiId = 1, UserId = "user1" },
            new MessageReaction { Id = 3, EmojiId = 2, UserId = "user1" }
        );
        _context.SaveChanges();

        // Act
        var result = await _controller.GetTopEmojis();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);

        var items = list.Cast<object>().ToList();
        Assert.Equal(2, items.Count);

        var first = items[0];
        var second = items[1];

        var firstSymbol = first.GetType().GetProperty("Symbol")?.GetValue(first) as string;
        var firstCount = (int?)first.GetType().GetProperty("Count")?.GetValue(first);

        var secondSymbol = second.GetType().GetProperty("Symbol")?.GetValue(second) as string;
        var secondCount = (int?)second.GetType().GetProperty("Count")?.GetValue(second);

        Assert.Equal("😀", firstSymbol);
        Assert.Equal(2, firstCount);
        Assert.Equal("🔥", secondSymbol);
        Assert.Equal(1, secondCount);
    }

    [Fact]
    public async Task GetAverageMessagesPerUser_ReturnsAverageMessages()
    {
        // Arrange
        _context.Chats.AddRange(
            new Chat { Id = "chat1" },
            new Chat { Id = "chat2" }
        );
        _context.Users.AddRange(
            new User { Id = "user1", UserName = "TopUser", Name = "TopUser" },
            new User { Id = "user2", UserName = "OtherUser", Name = "OtherUser" }
        );
        _context.ChatMessages.AddRange(
            new ChatMessage { Id = 1, SenderId = "user1", ChatId = "chat1", Message = "A", Timestamp = DateTime.UtcNow },
            new ChatMessage { Id = 2, SenderId = "user1", ChatId = "chat1", Message = "B", Timestamp = DateTime.UtcNow },
            new ChatMessage { Id = 3, SenderId = "user2", ChatId = "chat2", Message = "C", Timestamp = DateTime.UtcNow }
        );
        _context.SaveChanges();
        // Act
        var result = await _controller.GetAverageMessagesPerUser();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = okResult.Value;
        var average = (double?)returnValue.GetType().GetProperty("averageMessagesPerUser")?.GetValue(returnValue);

        Assert.Equal(1.5, average);
    }
}
