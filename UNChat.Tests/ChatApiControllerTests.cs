using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Linq;
using UNChat.Context;
using UNChat.Controllers;
using UNChat.Models;
using UNChat.Hubs;

public class ChatApiControllerTests
{
    private readonly DbContextOptions<UNChatDbContext> _options;

    public ChatApiControllerTests()
    {
        _options = new DbContextOptionsBuilder<UNChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // Ensures isolation
            .Options;
    }

    [Fact]
    public async Task Send_ReturnsNotFound_WhenChatDoesNotExist()
    {
        // Arrange
        using var context = new UNChatDbContext(_options);
        var controller = new ChatApiController(context);

        // Act
        var result = await controller.Send("user1", "nonexistent_chat", "Hello", null);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Send_ReturnsForbid_WhenSenderIsNotParticipant()
    {
        using var context = new UNChatDbContext(_options);

        var chat = new Chat
        {
            Id = "chat1",
            Participants = new List<UserChat>()
        };

        context.Chats.Add(chat);
        await context.SaveChangesAsync();

        var controller = new ChatApiController(context);

        // Act
        var result = await controller.Send("user1", "chat1", "Hi", null);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Send_ReturnsBadRequest_WhenFileTypeIsInvalid()
    {
        using var context = new UNChatDbContext(_options);

        var chat = new Chat
        {
            Id = "chat1",
            Participants = new List<UserChat>
            {
                new UserChat { ChatId = "chat1", UserId = "user1" }
            }
        };
        context.Chats.Add(chat);
        await context.SaveChangesAsync();

        var formFileMock = new Mock<IFormFile>();
        formFileMock.Setup(f => f.Length).Returns(1);
        formFileMock.Setup(f => f.FileName).Returns("malicious.exe");

        var controller = new ChatApiController(context);

        // Act
        var result = await controller.Send("user1", "chat1", "Test", formFileMock.Object);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("File type not allowed.", badRequest.Value);
    }

    [Fact]
    public async Task Send_AddsMessage_WhenValidInput()
    {
        using var context = new UNChatDbContext(_options);

        var user = new User { Id = "user1", Name = "John" };
        var chat = new Chat
        {
            Id = "chat1",
            Name = "Test Chat",
            Participants = new List<UserChat>
        {
            new UserChat { UserId = "user1", ChatId = "chat1" }
        }
        };

        context.Users.Add(user);
        context.Chats.Add(chat);
        await context.SaveChangesAsync();

        var controller = new ChatApiController(context);

        var hubContextMock = new Mock<IHubContext<ChatHub>>();
        var clientProxyMock = new Mock<IClientProxy>();
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(x => x.User(It.IsAny<string>())).Returns(clientProxyMock.Object);
        hubContextMock.Setup(x => x.Clients).Returns(clientsMock.Object);

        var httpContext = new DefaultHttpContext();
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IHubContext<ChatHub>)))
            .Returns(hubContextMock.Object);
        httpContext.RequestServices = serviceProviderMock.Object;
        controller.ControllerContext.HttpContext = httpContext;

        // Act
        var result = await controller.Send("user1", "chat1", "Hello World", null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);

        // Use reflection to read anonymous type properties
        var obj = okResult.Value!;
        var messageProp = obj.GetType().GetProperty("message")?.GetValue(obj, null);
        Assert.Equal("Hello World", messageProp);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Send_Allows_File_With_EmptyOrNull_Message(string inputMessage)
    {
        // Arrange
        using var context = new UNChatDbContext(_options);

        var user = new User { Id = "user1", Name = "Alice" };
        var chat = new Chat
        {
            Id = "chat1",
            Name = "ChatName",
            Participants = new List<UserChat>
        {
            new UserChat { UserId = "user1", ChatId = "chat1" }
        }
        };

        context.Users.Add(user);
        context.Chats.Add(chat);
        await context.SaveChangesAsync();

        var controller = new ChatApiController(context);

        // Mock file
        var fileMock = new Mock<IFormFile>();
        var fileContent = "Fake file content";
        var fileName = "test.jpg";
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(fileContent);
        writer.Flush();
        stream.Position = 0;

        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default))
            .Returns<Stream, System.Threading.CancellationToken>((s, _) => stream.CopyToAsync(s));

        // Mock IHubContext
        var hubContextMock = new Mock<IHubContext<ChatHub>>();
        var clientProxyMock = new Mock<IClientProxy>();
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(x => x.User(It.IsAny<string>())).Returns(clientProxyMock.Object);
        hubContextMock.Setup(x => x.Clients).Returns(clientsMock.Object);

        // Inject mocked services
        var httpContext = new DefaultHttpContext();
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IHubContext<ChatHub>)))
            .Returns(hubContextMock.Object);
        httpContext.RequestServices = serviceProviderMock.Object;
        controller.ControllerContext.HttpContext = httpContext;

        // Act
        var result = await controller.Send("user1", "chat1", inputMessage, fileMock.Object);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value!;
        var messageProp = value.GetType().GetProperty("message")?.GetValue(value);
        var fileUrlProp = value.GetType().GetProperty("attachmentUrl")?.GetValue(value);

        Assert.Equal(string.Empty, messageProp); // null message becomes empty string
        Assert.NotNull(fileUrlProp);             // File was saved and URL returned
        Assert.Contains("/uploads/", fileUrlProp!.ToString());
    }
    [Fact]
    public async Task Send_SendsMentionNotification_ToMentionedParticipants()
    {
        // Arrange
        using var context = new UNChatDbContext(_options);

        var sender = new User { Id = "user1", Name = "Alice" };
        var mentionedUser = new User { Id = "user2", Name = "Bob" };
        var unrelatedUser = new User { Id = "user3", Name = "Charlie" };

        var chat = new Chat
        {
            Id = "chat1",
            Name = "Test Chat",
            Participants = new List<UserChat>
        {
            new UserChat { UserId = "user1", ChatId = "chat1" },
            new UserChat { UserId = "user2", ChatId = "chat1" }
        }
        };

        context.Users.AddRange(sender, mentionedUser, unrelatedUser);
        context.Chats.Add(chat);
        await context.SaveChangesAsync();

        var controller = new ChatApiController(context);

        // Setup hub context and injection
        var clientProxyMock = new Mock<IClientProxy>();

        clientProxyMock.Setup(x => x.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            default
        )).Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(x => x.User("user2")).Returns(clientProxyMock.Object); // mentioned
        clientsMock.Setup(x => x.User("user3")).Returns(Mock.Of<IClientProxy>()); // should not be called

        var hubContextMock = new Mock<IHubContext<ChatHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(clientsMock.Object);

        var httpContext = new DefaultHttpContext();
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IHubContext<ChatHub>)))
            .Returns(hubContextMock.Object);
        httpContext.RequestServices = serviceProviderMock.Object;
        controller.ControllerContext.HttpContext = httpContext;

        // Act
        var result = await controller.Send("user1", "chat1", "Hello @Bob", null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        clientsMock.Verify(x => x.User("user2"), Times.AtLeastOnce);
        clientProxyMock.Verify(x => x.SendCoreAsync(
            "MentionNotification",
            It.Is<object[]>(args =>
                args != null &&
                args.Length == 1 &&
                args[0].ToString()!.Contains("Bob")
            ),
            default
        ), Times.Once);
    }
    [Fact]
    public async Task Send_SavesMessageAndFile_WhenBothProvided()
    {
        // Arrange
        using var context = new UNChatDbContext(_options);

        var user = new User { Id = "user1", Name = "Alice" };
        var chat = new Chat
        {
            Id = "chat1",
            Participants = new List<UserChat>
        {
            new UserChat { UserId = "user1", ChatId = "chat1" }
        }
        };
        context.Users.Add(user);
        context.Chats.Add(chat);
        await context.SaveChangesAsync();

        var fileMock = new Mock<IFormFile>();
        var content = "data";
        var fileName = "test.png";
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;

        fileMock.Setup(f => f.Length).Returns(stream.Length);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default))
            .Returns<Stream, CancellationToken>((s, _) => stream.CopyToAsync(s));

        var controller = new ChatApiController(context);

        var clientProxyMock = new Mock<IClientProxy>();
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(x => x.User(It.IsAny<string>())).Returns(clientProxyMock.Object);

        var hubContextMock = new Mock<IHubContext<ChatHub>>();
        hubContextMock.Setup(x => x.Clients).Returns(clientsMock.Object);

        var httpContext = new DefaultHttpContext();
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IHubContext<ChatHub>)))
            .Returns(hubContextMock.Object);
        httpContext.RequestServices = serviceProviderMock.Object;
        controller.ControllerContext.HttpContext = httpContext;

        // Act
        var result = await controller.Send("user1", "chat1", "Check this out", fileMock.Object);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value!;
        var messageProp = value.GetType().GetProperty("message")?.GetValue(value);
        var fileUrlProp = value.GetType().GetProperty("attachmentUrl")?.GetValue(value);
        Assert.Equal("Check this out", messageProp);
        Assert.NotNull(fileUrlProp);
        Assert.Contains("/uploads/", fileUrlProp!.ToString());

        // Check that the ChatMessage has 1 attachment
        var savedMessage = await context.ChatMessages.Include(m => m.Attachments).FirstOrDefaultAsync();
        Assert.Single(savedMessage!.Attachments);
    }

}
