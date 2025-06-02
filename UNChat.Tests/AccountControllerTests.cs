using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using UNChat.Context;
using UNChat.Controllers;
using UNChat.Models;
using Xunit;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

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
    [Fact]
    public async Task Login_ValidCredentials_RedirectsToChat()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var user = new User { Email = "test@example.com", UserName = "test@example.com", Name = "TestUser" };
        await userManager.CreateAsync(user, "Test123!");

        var signInManagerMock = GetMockSignInManager(userManager);
        signInManagerMock.Setup(s => s.PasswordSignInAsync(user.Email, "Test123!", false, true))
                         .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var controller = CreateController(userManager, signInManagerMock, db);

        var model = new LoginViewModel
        {
            Email = "test@example.com",
            Password = "Test123!",
            RememberMe = false
        };

        // Act
        var result = await controller.Login(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Chat", redirect.ActionName);
        Assert.Equal("Chat", redirect.ControllerName);
    }

    [Fact]
    public async Task Login_InvalidModelState_ReturnsViewWithModel()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var signInManagerMock = GetMockSignInManager(userManager);
        var controller = CreateController(userManager, signInManagerMock, db);
        controller.ModelState.AddModelError("Email", "Required");

        var model = new LoginViewModel
        {
            Email = "", // invalid
            Password = "Test123!",
            RememberMe = false
        };

        // Act
        var result = await controller.Login(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(model, view.Model);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Login_UserLockedOut_ReturnsViewWithError()
    {
        // Arrange
        var user = new User { Email = "locked@example.com", UserName = "locked@example.com", Name = "LockedUser" };

        var userManagerMock = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(),
            null, null, null, null, null, null, null, null
        );

        userManagerMock.Setup(u => u.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        userManagerMock.Setup(u => u.IsLockedOutAsync(user)).ReturnsAsync(true);

        var signInManagerMock = new Mock<SignInManager<User>>(
            userManagerMock.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            null, null, null, Mock.Of<IUserConfirmation<User>>()
        );

        var db = GetInMemoryDbContext();
        var controller = CreateController(userManagerMock.Object, signInManagerMock, db);

        var model = new LoginViewModel
        {
            Email = "locked@example.com",
            Password = "Test123!",
            RememberMe = false
        };

        // Act
        var result = await controller.Login(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[string.Empty].Errors,
            e => e.ErrorMessage.Contains("zablokowane"));
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsViewWithError()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var user = new User { Email = "fail@example.com", UserName = "fail@example.com", Name = "FailUser" };
        await userManager.CreateAsync(user, "Test123!");

        var signInManagerMock = GetMockSignInManager(userManager);
        signInManagerMock.Setup(s => s.PasswordSignInAsync(user.Email, "WrongPass", false, true))
                         .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var controller = CreateController(userManager, signInManagerMock, db);

        var model = new LoginViewModel
        {
            Email = "fail@example.com",
            Password = "WrongPass",
            RememberMe = false
        };

        // Act
        var result = await controller.Login(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[string.Empty].Errors,
            e => e.ErrorMessage == "Nieprawidłowa nazwa użytkownika lub hasło");
    }

    [Fact]
    public async Task Login_LockedOutBySignInResult_ReturnsViewWithError()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var user = new User { Email = "lockedout@example.com", UserName = "lockedout@example.com", Name = "LockedOut" };
        await userManager.CreateAsync(user, "Test123!");

        var signInManagerMock = GetMockSignInManager(userManager);
        signInManagerMock.Setup(s => s.PasswordSignInAsync(user.Email, "Test123!", false, true))
                         .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var controller = CreateController(userManager, signInManagerMock, db);

        var model = new LoginViewModel
        {
            Email = "lockedout@example.com",
            Password = "Test123!",
            RememberMe = false
        };

        // Act
        var result = await controller.Login(model);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[string.Empty].Errors,
            e => e.ErrorMessage.Contains("Konto zablokowane"));
    }
    [Fact]
    public async Task Logout_LogsUserOutAndRedirectsToHome()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var signInManagerMock = GetMockSignInManager(userManager);

        signInManagerMock.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask).Verifiable();

        var controller = CreateController(userManager, signInManagerMock, db);

        // Act
        var result = await controller.Logout();

        // Assert
        signInManagerMock.Verify(s => s.SignOutAsync(), Times.Once);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }
    [Fact]
    public void ExternalLogin_ReturnsChallengeResult_WithCorrectProviderAndRedirectUrl()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);

        var signInManagerMock = GetMockSignInManager(userManager);
        signInManagerMock.Setup(s => s.ConfigureExternalAuthenticationProperties(
                                It.IsAny<string>(),
                                It.IsAny<string>(),
                                null))
                         .Returns(new AuthenticationProperties());

        var controller = CreateController(userManager, signInManagerMock, db);

        // Mock UrlHelper
        var urlHelperMock = new Mock<IUrlHelper>();
        // Mock the underlying Link method that the Action extension method uses
        urlHelperMock.Setup(u => u.Link(It.IsAny<string>(), It.IsAny<object>()))
                     .Returns("/Account/ExternalLoginCallback");
        // Or if the controller uses Action directly, you might need:
        // urlHelperMock.Setup(u => u.ActionContext)
        //              .Returns(new ActionContext());
        // urlHelperMock.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>()))
        //              .Returns("/Account/ExternalLoginCallback");

        controller.Url = urlHelperMock.Object;

        // Act
        var result = controller.ExternalLogin("Google");

        // Assert
        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains("Google", challenge.AuthenticationSchemes);
    }


    [Fact]
    public async Task ExternalLoginCallback_InfoIsNull_RedirectsToLogin()
    {
        // Arrange
        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var signInManagerMock = GetMockSignInManager(userManager);

        signInManagerMock.Setup(s => s.SignInAsync(It.IsAny<User>(), false, It.IsAny<string>())).Returns(Task.CompletedTask);

        var controller = CreateController(userManager, signInManagerMock, db);

        // Act
        var result = await controller.ExternalLoginCallback();

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }
    [Fact]
    public async Task ExternalLoginCallback_ExistingExternalLogin_RedirectsToChat()
    {
        // Arrange
        var loginInfo = new ExternalLoginInfo(new ClaimsPrincipal(), "Google", "providerKey", "displayName");

        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var signInManagerMock = GetMockSignInManager(userManager);

        signInManagerMock.Setup(s => s.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(loginInfo);
        signInManagerMock.Setup(s => s.ExternalLoginSignInAsync("Google", "providerKey", false)).ReturnsAsync(SignInResult.Success);

        var controller = CreateController(userManager, signInManagerMock, db);

        // Act
        var result = await controller.ExternalLoginCallback();

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Chat", redirect.ActionName);
        Assert.Equal("Chat", redirect.ControllerName);
    }
    /* TODO: poprawić 
    [Fact]
    public async Task ExternalLoginCallback_NewUserCreated_RedirectsToChat()
    {
        // Arrange
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Email, "newuser@example.com"),
        new Claim(ClaimTypes.GivenName, "New"),
        new Claim(ClaimTypes.Surname, "User")
    };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var loginInfo = new ExternalLoginInfo(principal, "Google", "providerKey", "Google");

        var db = GetInMemoryDbContext();
        var userManager = GetUserManager(db);
        var signInManagerMock = GetMockSignInManager(userManager);

        signInManagerMock.Setup(s => s.GetExternalLoginInfoAsync(It.IsAny<string>())).ReturnsAsync(loginInfo);
        signInManagerMock.Setup(s => s.ExternalLoginSignInAsync("Google", "providerKey", false))
                         .ReturnsAsync(SignInResult.Failed);

        var userManagerMock = Mock.Get(userManager);
        userManagerMock.Setup(u => u.CreateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
        userManagerMock.Setup(u => u.AddLoginAsync(It.IsAny<User>(), loginInfo)).ReturnsAsync(IdentityResult.Success);

        signInManagerMock.Setup(s => s.SignInAsync(It.IsAny<User>(), false, null)).Returns(Task.CompletedTask);

        var controller = CreateController(userManagerMock.Object, signInManagerMock, db);

        // Act
        var result = await controller.ExternalLoginCallback();

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Chat", redirect.ActionName);
        Assert.Equal("Chat", redirect.ControllerName);
    }*/

}
