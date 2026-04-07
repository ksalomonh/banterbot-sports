using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Integrations.ApiFootball;
using BanterBotSports.Web.Controllers;
using BanterBotSports.Web.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for the GET /torneo/{id}/unirse flow introduced by the inline-invitation feature.
///
/// Scenarios covered:
/// 1. Authenticated user with valid token → ViewResult with torneo model
/// 2. Unauthenticated user with valid token → RedirectToAction to Login with returnUrl containing token
/// 3. Invalid/expired token → TempData error + redirect to Index
/// 4. POST Unirse — valid token, authenticated user joins torneo (regression guard)
/// </summary>
public class TorneoControllerUnirseTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generates a valid (protected) invite token for the given torneo and expiry using the
    /// same EphemeralDataProtectionProvider that BuildSut() wires into the controller.
    /// Uses <see cref="TorneoController.InviteProtectorPurpose"/> to avoid duplicating the
    /// magic string.
    /// </summary>
    private static (TorneoController Sut, string ValidToken) BuildSutWithToken(
        Mock<ITorneoService>? torneoServiceMock = null,
        bool authenticated = true,
        int torneoId = 5)
    {
        var torneoSvc = torneoServiceMock ?? new Mock<ITorneoService>();
        var jornadaSvc = new Mock<IJornadaService>();
        var apiFootballSyncSvc = new Mock<IApiFootballSyncService>();
        var partidoSvc = new Mock<IPartidoService>();

        // EphemeralDataProtectionProvider produces tokens that the controller can unprotect.
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protector = dataProtectionProvider.CreateProtector(TorneoController.InviteProtectorPurpose);

        // Build a valid, unexpired token for torneoId.
        var payload = $"{torneoId}:{DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds()}";
        var validToken = protector.Protect(payload);

        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManager = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManager
            .Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns("test-user-id");

        var controller = new TorneoController(
            torneoSvc.Object,
            jornadaSvc.Object,
            apiFootballSyncSvc.Object,
            partidoSvc.Object,
            dataProtectionProvider,
            userManager.Object,
            NullLogger<TorneoController>.Instance);

        // Build HttpContext with or without an authenticated identity.
        ClaimsPrincipal user;
        if (authenticated)
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "test-user-id") },
                "TestAuth");
            user = new ClaimsPrincipal(identity);
        }
        else
        {
            // Unauthenticated: ClaimsIdentity with no auth type → IsAuthenticated = false.
            user = new ClaimsPrincipal(new ClaimsIdentity());
        }

        // Set up a mock IUrlHelper so Url.Action(…) produces a predictable result.
        var urlHelperMock = new Mock<IUrlHelper>();
        urlHelperMock
            .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
            .Returns<UrlActionContext>(ctx =>
            {
                // Replicate the relative path the controller will build for the returnUrl.
                if (ctx.Action == nameof(TorneoController.Unirse))
                {
                    var routeValues = ctx.Values as Microsoft.AspNetCore.Routing.RouteValueDictionary
                        ?? new Microsoft.AspNetCore.Routing.RouteValueDictionary(ctx.Values);
                    var id = routeValues["id"];
                    var token = routeValues["token"];
                    return $"/torneo/{id}/unirse?token={token}";
                }

                return "/";
            });

        controller.Url = urlHelperMock.Object;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Wire TempData so TempData[...] = "..." doesn't throw.
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

        return (controller, validToken);
    }

    // ---------------------------------------------------------------------------
    // Test 1: Authenticated user with valid token → ViewResult
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GET_Unirse_ValidToken_AuthenticatedUser_ReturnsView()
    {
        // Arrange
        const int torneoId = 5;
        var torneo = new Torneo { Id = torneoId, Nombre = "Liga Test" };

        var torneoSvcMock = new Mock<ITorneoService>();
        torneoSvcMock
            .Setup(s => s.GetByIdAsync(torneoId))
            .ReturnsAsync(torneo);

        var (sut, validToken) = BuildSutWithToken(
            torneoServiceMock: torneoSvcMock,
            authenticated: true,
            torneoId: torneoId);

        // Act
        var result = await sut.Unirse(torneoId, validToken);

        // Assert: authenticated user with valid token must see the Unirse landing view.
        result.Should().BeOfType<ViewResult>(
            "authenticated user with valid token must be shown the Unirse landing page");

        var viewResult = (ViewResult)result;
        viewResult.Model.Should().Be(torneo,
            "the view must receive the Torneo entity so it can display its name");
    }

    // ---------------------------------------------------------------------------
    // Test 2: Unauthenticated user with valid token → redirect to Login with returnUrl
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GET_Unirse_ValidToken_UnauthenticatedUser_RedirectsToLoginWithReturnUrl()
    {
        // Arrange
        const int torneoId = 5;
        var torneo = new Torneo { Id = torneoId, Nombre = "Liga Test" };

        var torneoSvcMock = new Mock<ITorneoService>();
        torneoSvcMock
            .Setup(s => s.GetByIdAsync(torneoId))
            .ReturnsAsync(torneo);

        var (sut, validToken) = BuildSutWithToken(
            torneoServiceMock: torneoSvcMock,
            authenticated: false,
            torneoId: torneoId);

        // Act
        var result = await sut.Unirse(torneoId, validToken);

        // Assert: unauthenticated user must be redirected to Login, NOT Forbidden.
        result.Should().BeOfType<RedirectToActionResult>(
            "unauthenticated user with valid token must be redirected to Login, not forbidden");

        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("Login",
            "the redirect must point to the Login action so the user can authenticate");
        redirect.ControllerName.Should().Be("Account",
            "Login lives in AccountController");

        // returnUrl must include the token so the flow survives the login round-trip.
        var returnUrl = redirect.RouteValues?["returnUrl"]?.ToString();
        returnUrl.Should().NotBeNullOrEmpty("returnUrl must be set so the token is not lost");
        returnUrl.Should().Contain($"/torneo/{torneoId}/unirse",
            "returnUrl must point back to the Unirse action for the correct torneo");
        returnUrl.Should().Contain("token=",
            "returnUrl MUST include the token — this is the core of the bug being fixed");
    }

    // ---------------------------------------------------------------------------
    // Test 3: Invalid token → TempData error + redirect to Index
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GET_Unirse_InvalidToken_RedirectsWithError()
    {
        // Arrange: authenticated user but with a garbage/forged token.
        const int torneoId = 5;
        var torneo = new Torneo { Id = torneoId, Nombre = "Liga Test" };

        var torneoSvcMock = new Mock<ITorneoService>();
        torneoSvcMock
            .Setup(s => s.GetByIdAsync(torneoId))
            .ReturnsAsync(torneo);

        var (sut, _) = BuildSutWithToken(
            torneoServiceMock: torneoSvcMock,
            authenticated: true,
            torneoId: torneoId);

        // Act: pass a clearly invalid token string.
        var result = await sut.Unirse(torneoId, "invalid-token-that-cannot-be-decrypted");

        // Assert: must redirect (not show a view, not throw) and set an error in TempData.
        result.Should().BeOfType<RedirectToActionResult>(
            "invalid token must redirect away, never show partial content");

        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be(nameof(TorneoController.Index),
            "invalid token must redirect to the torneo list, not stay on Unirse");

        sut.TempData[TempDataKeys.Error].Should().NotBeNull(
            "a user-facing error message must be set so the user knows why the link failed");
    }

    // ---------------------------------------------------------------------------
    // Test 4: POST Unirse not broken — valid token, authenticated user joins torneo
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task POST_Unirse_ValidToken_AuthenticatedUser_JoinsTorneoAndRedirects()
    {
        // Arrange
        const int torneoId = 5;
        const string userId = "test-user-id";
        var torneo = new Torneo { Id = torneoId, Nombre = "Liga Test" };

        var torneoSvcMock = new Mock<ITorneoService>();
        torneoSvcMock
            .Setup(s => s.GetByIdAsync(torneoId))
            .ReturnsAsync(torneo);
        torneoSvcMock
            .Setup(s => s.UnirseConTokenAsync(torneoId, userId))
            .Returns(Task.CompletedTask);

        var (sut, validToken) = BuildSutWithToken(
            torneoServiceMock: torneoSvcMock,
            authenticated: true,
            torneoId: torneoId);

        // Act: call the POST action directly (bypasses HTTP pipeline — no antiforgery in unit tests).
        // The POST is renamed to UnirsePost in the controller with [ActionName("Unirse")] so both
        // GET and POST can coexist on the same route without C# method name collision.
        var result = await sut.UnirsePost(torneoId, validToken);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>(
            "successful join must redirect to the Dashboard");

        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be(nameof(TorneoController.Dashboard),
            "after joining, user must land on the torneo Dashboard");

        torneoSvcMock.Verify(s => s.UnirseConTokenAsync(torneoId, userId), Times.Once,
            "UnirseConTokenAsync must be called exactly once with the correct arguments");

        sut.TempData[TempDataKeys.Success].Should().NotBeNull(
            "a success message must confirm the user joined the torneo");
    }
}
