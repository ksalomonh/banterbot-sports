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
}
