using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using UNChat.Context;
using UNChat.Controllers;
using UNChat.Models;
using Xunit;

public class AccountControllerTests
{
    private UNChatDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<UNChatDbContext>()
            .UseInMemoryDatabase(databaseName: $"UNChatDb_{System.Guid.NewGuid()}")
            .Options;

        return new UNChatDbContext(options);
    }

    private UserManager<User> GetUserManager(UNChatDbContext context)
    {
        var store = new UserStore<User>(context);
        return new UserManager<User>(
            store,
            null,
            new PasswordHasher<User>(),
            new IUserValidator<User>[0],
            new IPasswordValidator<User>[0],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            new Mock<ILogger<UserManager<User>>>().Object);
    }

    private Mock<SignInManager<User>> GetMockSignInManager(UserManager<User> userManager)
    {
        return new Mock<SignInManager<User>>(
            userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            null,
            null,
            null,
            Mock.Of<IUserConfirmation<User>>());
    }

    private AccountController CreateController(UserManager<User> userManager, Mock<SignInManager<User>> signInManagerMock, UNChatDbContext db)
    {
        return new AccountController(userManager, signInManagerMock.Object, db);
    }

    [Fact]
    public async Task Register_ValidUser_CreatesUserAndRedirects()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var signInManagerMock = GetMockSignInManager(userManager);

        signInManagerMock.Setup(s => s.SignInAsync(It.IsAny<User>(), false, null))
                         .Returns(Task.CompletedTask);

        var controller = CreateController(userManager, signInManagerMock, db);

        var model = new RegisterViewModel
        {
            Name = "TestUser",
            Email = "test@example.com",
            Password = "Test123!"
        };

        // Act
        var result = await controller.Register(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Chat", redirect.ActionName);
        Assert.Equal("Chat", redirect.ControllerName);
    }

    [Fact]
    public async Task Register_EmailAlreadyExists_ReturnsViewWithError()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var existingUser = new User { Email = "existing@example.com", UserName = "existing@example.com", Name = "Existing" };
        await userManager.CreateAsync(existingUser, "Password1!");

        var signInManagerMock = GetMockSignInManager(userManager);
        var controller = CreateController(userManager, signInManagerMock, db);

        var model = new RegisterViewModel
        {
            Name = "NewUser",
            Email = "existing@example.com",
            Password = "Password2!"
        };

        // Act
        var result = await controller.Register(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Email"));
    }

    [Fact]
    public async Task Register_NameAlreadyExists_ReturnsViewWithError()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var existingUser = new User { Email = "other@example.com", UserName = "other@example.com", Name = "ExistingName" };
        await userManager.CreateAsync(existingUser, "Password1!");

        var signInManagerMock = GetMockSignInManager(userManager);
        var controller = CreateController(userManager, signInManagerMock, db);

        var model = new RegisterViewModel
        {
            Name = "existingname", // same as above, different case
            Email = "new@example.com",
            Password = "Password2!"
        };

        // Act
        var result = await controller.Register(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Name"));
    }
    [Fact]
    public async Task Register_InvalidModelState_ReturnsViewWithModel()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var signInManagerMock = GetMockSignInManager(userManager);
        var controller = CreateController(userManager, signInManagerMock, db);

        // Manually add model state error (simulates failed validation)
        controller.ModelState.AddModelError("Email", "Required");

        var model = new RegisterViewModel
        {
            Name = "SomeUser",
            Email = "", // Invalid/missing email
            Password = "SomePass123!"
        };

        // Act
        var result = await controller.Register(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(model, view.Model);
    }
    [Fact]
    public async Task Register_EmptyName_ReturnsViewWithModelError()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var signInManagerMock = GetMockSignInManager(userManager);
        var controller = CreateController(userManager, signInManagerMock, db);

        var model = new RegisterViewModel
        {
            Name = "",  // Empty name to trigger validation error
            Email = "test@example.com",
            Password = "ValidPass123!"
        };

        // Manually trigger model validation for the model
        var validationContext = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(model, validationContext, validationResults, true);

        foreach (var validationResult in validationResults)
        {
            foreach (var memberName in validationResult.MemberNames)
            {
                controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
            }
        }

        // Act
        var result = await controller.Register(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("Name"));
    }

}
