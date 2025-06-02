using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Threading.Tasks;
using UNChat.Context;
using UNChat.Controllers;
using UNChat.Models;
using Xunit;
using Microsoft.AspNetCore.SignalR;
using UNChat.Hubs;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace UNChat.Tests.Controllers
{
    public class FriendsControllerTests
    {
        private readonly FriendsController _controller;
        private readonly UNChatDbContext _context;
        private readonly Mock<UserManager<User>> _userManagerMock;
        private readonly Mock<IHubContext<ChatHub>> _hubContextMock;
        private readonly Mock<IClientProxy> _clientProxyMock;

        public FriendsControllerTests()
        {
            var options = new DbContextOptionsBuilder<UNChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

            _context = new UNChatDbContext(options);

            var store = new Mock<IUserStore<User>>();
            _userManagerMock = new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);

            _clientProxyMock = new Mock<IClientProxy>();
            _hubContextMock = new Mock<IHubContext<ChatHub>>();
            var clientsMock = new Mock<IHubClients>();
            clientsMock.Setup(clients => clients.User(It.IsAny<string>())).Returns(_clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

            _controller = new FriendsController(_userManagerMock.Object, _context);

            var services = new ServiceCollection();
            services.AddSingleton(_hubContextMock.Object);
            var provider = services.BuildServiceProvider();
            _controller.ControllerContext.HttpContext = new DefaultHttpContext
            {
                RequestServices = provider
            };
        }

        [Fact]
        public async Task AddFriend_ShouldReturnOk_WhenFriendRequestIsValid()
        {
            // Arrange
            var model = new Friend
            {
                Friend1Id = "user1",
                Friend2Id = "user2"
            };

            // Act
            var result = await _controller.AddFriend(model);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Wysłano zaproszenie.", okResult.Value);
        }

        [Fact]
        public async Task AddFriend_ShouldReturnBadRequest_WhenAlreadyFriends()
        {
            // Arrange
            var existing = new Friend
            {
                Friend1Id = "user1",
                Friend2Id = "user2",
                Status = FriendStatus.Pending
            };
            _context.Friends.Add(existing);
            await _context.SaveChangesAsync();

            var model = new Friend
            {
                Friend1Id = "user1",
                Friend2Id = "user2"
            };

            // Act
            var result = await _controller.AddFriend(model);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Zaproszenie już istnieje lub jesteście znajomymi.", badRequest.Value);
        }

        [Fact]
        public async Task AcceptFriend_ShouldReturnOk_AndCreateChat_IfNoChatExists()
        {

            // Arrange
            var model = new Friend
            {
                Friend1Id = "user1",
                Friend2Id = "user2",
                Status = FriendStatus.Pending
            };
            _context.Friends.Add(model);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.AcceptFriend(model);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Zaproszenie zaakceptowane.", okResult.Value);

            var updated = await _context.Friends.FindAsync(model.Friend1Id, model.Friend2Id);
            Assert.Equal(FriendStatus.Accepted, model.Status);

            var userChats = await _context.UserChats.ToListAsync();
            Assert.Equal(2, userChats.Count);
        }

        [Fact]
        public async Task AcceptFriend_ShouldReturnNotFound_IfRequestDoesNotExist()
        {
            // Arrange
            var model = new Friend
            {
                Friend1Id = "userX",
                Friend2Id = "userY"
            };

            // Act
            var result = await _controller.AcceptFriend(model);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Zaproszenie nie istnieje.", notFound.Value);
        }
    }
}
