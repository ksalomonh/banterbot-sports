using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for AccountController.Profile():
/// - Authenticated user → ViewResult with ProfileViewModel model
/// - UserManager returns null → NotFoundResult (defensive guard)
/// - GetUserAsync is called exactly once per request
/// - Telegram link state comes from ITelegramVinculacionService, not AppUser.PhoneNumber
/// </summary>
public class AccountControllerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static (
        AccountController Sut,
        Mock<UserManager<AppUser>> UserManagerMock,
        Mock<ITelegramVinculacionService> TelegramServiceMock
    ) BuildSut(
        AppUser? userToReturn,
        UsuarioTelegram? telegramRecord = null,
        string botUsername = "BanterBotSports_bot")
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

        // ITelegramVinculacionService: mock GetByUserIdAsync to return the provided record.
        var telegramServiceMock = new Mock<ITelegramVinculacionService>();
        telegramServiceMock
            .Setup(ts => ts.GetByUserIdAsync(It.IsAny<string>()))
            .ReturnsAsync(telegramRecord);

        // IConfiguration: provide Telegram:BotUsername key.
        var configMock = new Mock<IConfiguration>();
        configMock
            .Setup(c => c["Telegram:BotUsername"])
            .Returns(botUsername);

        var controller = new AccountController(
            userManagerMock.Object,
            signInManagerMock.Object,
            NullLogger<AccountController>.Instance,
            telegramServiceMock.Object,
            configMock.Object);

        // Authenticated user identity so [Authorize] semantics are satisfied at the controller level.
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "test@test.com") }, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        return (controller, userManagerMock, telegramServiceMock);
    }

    // ---------------------------------------------------------------------------
    // Existing tests (updated to use new BuildSut signature)
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

        var (sut, _, _) = BuildSut(userToReturn: appUser);

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
        var (sut, _, _) = BuildSut(userToReturn: null);

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
        var (sut, userManagerMock, _) = BuildSut(userToReturn: appUser);

        // Act
        await sut.Profile();

        // Assert: GetUserAsync must be called exactly once with the controller's User principal
        userManagerMock.Verify(
            um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()),
            Times.Once,
            "Profile() must resolve the current user via UserManager.GetUserAsync exactly once");
    }

    // ---------------------------------------------------------------------------
    // Telegram link state tests — Scenario 1a, 1b, 1c, 2a (REQ-01, REQ-02, REQ-07)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Profile_LinkedTelegram_VmHasTelegramUsernamePopulated()
    {
        // Arrange (Scenario 1a): user has a UsuarioTelegram record with username.
        var appUser = new AppUser { Id = "user-linked", UserName = "+5491112345678", Email = "linked@test.com" };
        var telegramRecord = new UsuarioTelegram
        {
            UserId = "user-linked",
            TelegramUserId = 999888777,
            TelegramUsername = "@john_doe",
            FechaVinculacion = DateTimeOffset.UtcNow
        };
        var (sut, _, telegramServiceMock) = BuildSut(userToReturn: appUser, telegramRecord: telegramRecord);

        // Act
        var result = await sut.Profile();

        // Assert: TelegramUsername populated, GetByUserIdAsync called once
        var vm = (ProfileViewModel)((ViewResult)result).Model!;
        vm.TelegramUsername.Should().Be("@john_doe", "Profile must show Telegram username when linked");
        telegramServiceMock.Verify(
            ts => ts.GetByUserIdAsync("user-linked"),
            Times.Once,
            "GetByUserIdAsync must be called exactly once per Profile GET");
    }

    [Fact]
    public async Task Profile_NoTelegramLink_VmHasNullUsernameAndPopulatedDeepLink()
    {
        // Arrange (Scenario 1b): no UsuarioTelegram record exists.
        var appUser = new AppUser { Id = "user-unlinked", UserName = "+5491112345678", Email = "unlinked@test.com" };
        var (sut, _, _) = BuildSut(userToReturn: appUser, telegramRecord: null, botUsername: "BanterBotSports_bot");

        // Act
        var result = await sut.Profile();

        // Assert: TelegramUsername null, deep link correctly formed (Scenario 2a)
        var vm = (ProfileViewModel)((ViewResult)result).Model!;
        vm.TelegramUsername.Should().BeNull("no Telegram link → TelegramUsername must be null");
        vm.TelegramDeepLink.Should().Be(
            "https://t.me/BanterBotSports_bot?start=user-unlinked",
            "deep link must include BotUsername from config and userId as start param");
    }

    [Fact]
    public async Task Profile_LinkedTelegramNoUsername_VmShowsTelegramUserIdFallback()
    {
        // Arrange (Scenario 1c): linked but no username — show TelegramUserId as fallback.
        var appUser = new AppUser { Id = "user-nousername", UserName = "+5491112345678", Email = "nousername@test.com" };
        var telegramRecord = new UsuarioTelegram
        {
            UserId = "user-nousername",
            TelegramUserId = 123456789,
            TelegramUsername = null,
            FechaVinculacion = DateTimeOffset.UtcNow
        };
        var (sut, _, _) = BuildSut(userToReturn: appUser, telegramRecord: telegramRecord);

        // Act
        var result = await sut.Profile();

        // Assert: TelegramUsername shows numeric user ID as fallback string
        var vm = (ProfileViewModel)((ViewResult)result).Model!;
        vm.TelegramUsername.Should().Be("123456789",
            "when TelegramUsername is null, controller must fall back to TelegramUserId as string");
    }

    [Fact]
    public async Task Profile_DeepLink_UsesConfiguredBotUsername()
    {
        // Arrange (Scenario 3a): different bot username from config.
        var appUser = new AppUser { Id = "user-abc-123", UserName = "+5491112345678", Email = "config@test.com" };
        var (sut, _, _) = BuildSut(userToReturn: appUser, telegramRecord: null, botUsername: "MyTestBot");

        // Act
        var result = await sut.Profile();

        // Assert: deep link uses MyTestBot, not hardcoded value
        var vm = (ProfileViewModel)((ViewResult)result).Model!;
        vm.TelegramDeepLink.Should().Be(
            "https://t.me/MyTestBot?start=user-abc-123",
            "deep link must use Telegram:BotUsername from configuration, not a hardcoded value");
    }

    [Fact]
    public async Task Profile_POST_ValidationFailure_PreservesTelegramState()
    {
        // Arrange (REQ-06): POST returns view on validation failure — Telegram state must be populated.
        var appUser = new AppUser { Id = "user-postfail", UserName = "+5491112345678", Email = "postfail@test.com" };
        var telegramRecord = new UsuarioTelegram
        {
            UserId = "user-postfail",
            TelegramUserId = 555444333,
            TelegramUsername = "@postfail_user",
            FechaVinculacion = DateTimeOffset.UtcNow
        };
        var (sut, _, telegramServiceMock) = BuildSut(userToReturn: appUser, telegramRecord: telegramRecord);

        // Simulate validation failure
        sut.ModelState.AddModelError("NombreDisplay", "Required");

        // Act
        var result = await sut.Profile(new ProfileEditViewModel { NombreDisplay = "" });

        // Assert: view returned with Telegram state preserved
        result.Should().BeOfType<ViewResult>();
        var vm = (ProfileViewModel)((ViewResult)result).Model!;
        vm.TelegramUsername.Should().Be("@postfail_user",
            "Profile POST validation failure must preserve Telegram state same as GET");
        telegramServiceMock.Verify(
            ts => ts.GetByUserIdAsync("user-postfail"),
            Times.Once,
            "GetByUserIdAsync must be called once in POST validation failure path");
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

        var telegramServiceMock = new Mock<ITelegramVinculacionService>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Telegram:BotUsername"]).Returns("BanterBotSports_bot");

        var controller = new AccountController(
            userManagerMock.Object,
            signInManagerMock.Object,
            NullLogger<AccountController>.Instance,
            telegramServiceMock.Object,
            configMock.Object);

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

        var telegramServiceMock = new Mock<ITelegramVinculacionService>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Telegram:BotUsername"]).Returns("BanterBotSports_bot");

        var controller = new AccountController(
            userManagerMock.Object,
            signInManagerMock.Object,
            NullLogger<AccountController>.Instance,
            telegramServiceMock.Object,
            configMock.Object);

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
