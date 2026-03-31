using BanterBotSports.DAL;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for AccountController.Profile():
/// - Authenticated user → ViewResult with ProfileViewModel model
/// - UserManager returns null → NotFoundResult (defensive guard)
/// - GetUserAsync is called exactly once per request
/// </summary>
public class AccountControllerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static (AccountController Sut, Mock<UserManager<AppUser>> UserManagerMock) BuildSut(
        AppUser? userToReturn)
    {
        // UserManager<AppUser>: mock with minimal surface — standard pattern (see TorneoControllerTests).
#pragma warning disable CS8625
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

        // GetUserAsync resolves to the provided AppUser (can be null to test the 404 branch).
        userManagerMock
            .Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(userToReturn);

        // SignInManager requires HttpContextAccessor + UserManager — use a real mock chain.
#pragma warning disable CS8625
        var signInManagerMock = new Mock<SignInManager<AppUser>>(
            userManagerMock.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<AppUser>>().Object,
            null, null, null, null);
#pragma warning restore CS8625

        var controller = new AccountController(
            userManagerMock.Object,
            signInManagerMock.Object,
            NullLogger<AccountController>.Instance);

        // Authenticated user identity so [Authorize] semantics are satisfied at the controller level.
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "test@test.com") }, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        return (controller, userManagerMock);
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Profile_AuthenticatedUser_ReturnsViewResultWithProfileViewModel()
    {
        // Arrange: UserManager returns a valid AppUser.
        var appUser = new AppUser
        {
            Id = "user-123",
            UserName = "test@test.com",
            Email = "test@test.com",
            NombreDisplay = "Jugador Test"
        };

        var (sut, _) = BuildSut(userToReturn: appUser);

        // Act
        var result = await sut.Profile();

        // Assert: must return a ViewResult
        result.Should().BeOfType<ViewResult>("authenticated user must see the profile view");

        var viewResult = (ViewResult)result;
        viewResult.Model.Should().BeOfType<ProfileViewModel>(
            "Profile() must map AppUser to ProfileViewModel — DAL entity must not reach the view");

        var vm = (ProfileViewModel)viewResult.Model!;
        vm.NombreDisplay.Should().Be(appUser.NombreDisplay, "NombreDisplay must be mapped from AppUser");
        vm.Email.Should().Be(appUser.Email, "Email must be mapped from AppUser");
    }

    [Fact]
    public async Task Profile_UserManagerReturnsNull_ReturnsNotFound()
    {
        // Arrange: UserManager returns null (defensive — should not happen with [Authorize]).
        var (sut, _) = BuildSut(userToReturn: null);

        // Act
        var result = await sut.Profile();

        // Assert: must return 404 — no unhandled exception, no redirect
        result.Should().BeOfType<NotFoundResult>("null user from UserManager must return NotFound, not throw");
    }

    [Fact]
    public async Task Profile_AuthenticatedUser_CallsGetUserAsyncOnce()
    {
        // Arrange
        var appUser = new AppUser { Id = "u-42", UserName = "player@arena.com", Email = "player@arena.com" };
        var (sut, userManagerMock) = BuildSut(userToReturn: appUser);

        // Act
        await sut.Profile();

        // Assert: GetUserAsync must be called exactly once with the controller's User principal
        userManagerMock.Verify(
            um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()),
            Times.Once,
            "Profile() must resolve the current user via UserManager.GetUserAsync exactly once");
    }

    [Fact]
    public async Task Profile_MapsTegramChatIdFromNewColumn_NotFromPhoneNumber()
    {
        // Arrange: AppUser has TelegramChatId in the new column and a real phone in PhoneNumber (SCENARIO-7c, SCENARIO-9a)
        var appUser = new AppUser
        {
            Id = "user-tg",
            UserName = "+5491112345678",
            Email = "tg@test.com",
            PhoneNumber = "+5491112345678",
            TelegramChatId = "123"
        };
        var (sut, _) = BuildSut(userToReturn: appUser);

        // Act
        var result = await sut.Profile();

        // Assert: TelegramChatId comes from AppUser.TelegramChatId, not PhoneNumber
        var vm = (ProfileViewModel)((ViewResult)result).Model!;
        vm.TelegramChatId.Should().Be("123", "TelegramChatId must be mapped from AppUser.TelegramChatId (dedicated column)");
        vm.TelegramChatId.Should().NotBe(appUser.PhoneNumber, "TelegramChatId must NOT come from PhoneNumber — that column is now the login identifier");
    }

    // ---------------------------------------------------------------------------
    // Register POST tests (SCENARIO-2a)
    // ---------------------------------------------------------------------------

    private static (AccountController Sut, Mock<UserManager<AppUser>> UserManagerMock) BuildSutForRegister(
        IdentityResult createResult)
    {
#pragma warning disable CS8625
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

        // Capture the AppUser passed to CreateAsync so tests can inspect it
        userManagerMock
            .Setup(um => um.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(createResult);

#pragma warning disable CS8625
        var signInManagerMock = new Mock<SignInManager<AppUser>>(
            userManagerMock.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<AppUser>>().Object,
            null, null, null, null);
#pragma warning restore CS8625

        signInManagerMock
            .Setup(sm => sm.SignInAsync(It.IsAny<AppUser>(), It.IsAny<bool>(), null))
            .Returns(Task.CompletedTask);

        var controller = new AccountController(
            userManagerMock.Object,
            signInManagerMock.Object,
            NullLogger<AccountController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return (controller, userManagerMock);
    }

    [Fact]
    public async Task Register_Post_SetsUserNameAndPhoneNumberFromTelefono()
    {
        // Arrange (SCENARIO-2a): Register POST must create AppUser with UserName = Telefono and PhoneNumber = Telefono
        var (sut, userManagerMock) = BuildSutForRegister(IdentityResult.Success);

        var model = new RegisterViewModel
        {
            NombreDisplay   = "El Crack",
            Telefono        = "+5491112345678",
            Email           = "crack@arena.com",
            Password        = "Password1",
            ConfirmPassword = "Password1"
        };

        AppUser? capturedUser = null;
        userManagerMock
            .Setup(um => um.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .Callback<AppUser, string>((u, _) => capturedUser = u)
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await sut.Register(model);

        // Assert
        capturedUser.Should().NotBeNull();
        capturedUser!.UserName.Should().Be(model.Telefono, "UserName must be the phone number — it is the login identifier");
        capturedUser.PhoneNumber.Should().Be(model.Telefono, "PhoneNumber must store the phone for semantic correctness");
        capturedUser.Email.Should().Be(model.Email, "Email must be stored separately for password recovery");
    }

    // ---------------------------------------------------------------------------
    // Login POST tests (SCENARIO-1a, SCENARIO-1b)
    // ---------------------------------------------------------------------------

    private static (AccountController Sut, Mock<SignInManager<AppUser>> SignInManagerMock) BuildSutForLogin(
        Microsoft.AspNetCore.Identity.SignInResult signInResult)
    {
#pragma warning disable CS8625
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

#pragma warning disable CS8625
        var signInManagerMock = new Mock<SignInManager<AppUser>>(
            userManagerMock.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<AppUser>>().Object,
            null, null, null, null);
#pragma warning restore CS8625

        signInManagerMock
            .Setup(sm => sm.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(signInResult);

        var controller = new AccountController(
            userManagerMock.Object,
            signInManagerMock.Object,
            NullLogger<AccountController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return (controller, signInManagerMock);
    }

    [Fact]
    public async Task Login_Post_ValidTelefono_CallsPasswordSignInWithTelefono()
    {
        // Arrange (SCENARIO-1a): Login POST must call PasswordSignInAsync with the phone number
        var (sut, signInManagerMock) = BuildSutForLogin(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var model = new LoginViewModel
        {
            Telefono  = "+5491112345678",
            Password  = "Password1"
        };

        // Act
        await sut.Login(model);

        // Assert: PasswordSignInAsync must receive Telefono as the userName argument
        signInManagerMock.Verify(
            sm => sm.PasswordSignInAsync(model.Telefono, model.Password, model.RememberMe, false),
            Times.Once,
            "Login POST must authenticate by phone number, not email");
    }

    [Fact]
    public async Task Login_Post_FailedSignIn_ReturnsViewWithPhoneErrorMessage()
    {
        // Arrange (SCENARIO-1b): failed sign-in must show the phone-specific error message
        var (sut, _) = BuildSutForLogin(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var model = new LoginViewModel
        {
            Telefono  = "+5491112345678",
            Password  = "wrong"
        };

        // Act
        var result = await sut.Login(model);

        // Assert: ViewResult returned with model error containing phone-specific text
        result.Should().BeOfType<ViewResult>();
        var viewResult = (ViewResult)result;
        viewResult.ViewData.ModelState[string.Empty]!.Errors
            .Should().Contain(e => e.ErrorMessage == "Teléfono o contraseña incorrectos.",
                "the error message must reference teléfono, not email");
    }
}
