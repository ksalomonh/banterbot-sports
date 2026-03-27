using BanterBotSports.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Principal;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for HomeController.Index():
/// - Authenticated user → RedirectToActionResult to Torneo/Index
/// - Anonymous user    → ViewResult (branded landing)
/// </summary>
public class HomeControllerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static HomeController BuildSut(bool isAuthenticated)
    {
        var controller = new HomeController();

        // Build a fake ClaimsPrincipal
        var identity = isAuthenticated
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "test@test.com") }, "TestAuth")
            : new ClaimsIdentity(); // no authenticationType → IsAuthenticated = false

        var user = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Index_AuthenticatedUser_RedirectsToTorneo()
    {
        // Arrange
        var sut = BuildSut(isAuthenticated: true);

        // Act
        var result = sut.Index();

        // Assert: must redirect to Torneo/Index
        result.Should().BeOfType<RedirectToActionResult>("authenticated user must be redirected");
        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("Index", "must redirect to Index action");
        redirect.ControllerName.Should().Be("Torneo", "must redirect to Torneo controller");
    }

    [Fact]
    public void Index_AnonymousUser_ReturnsViewResult()
    {
        // Arrange
        var sut = BuildSut(isAuthenticated: false);

        // Act
        var result = sut.Index();

        // Assert: anonymous users see the landing page
        result.Should().BeOfType<ViewResult>("anonymous user must see the landing page view");
    }

    [Fact]
    public void Index_AnonymousUser_DoesNotRedirect()
    {
        // Arrange
        var sut = BuildSut(isAuthenticated: false);

        // Act
        var result = sut.Index();

        // Assert: must NOT be a redirect
        result.Should().NotBeOfType<RedirectToActionResult>(
            "anonymous users must not be redirected — they must see the landing page");
        result.Should().NotBeOfType<RedirectResult>(
            "anonymous users must not be redirected to an external URL");
    }
}
