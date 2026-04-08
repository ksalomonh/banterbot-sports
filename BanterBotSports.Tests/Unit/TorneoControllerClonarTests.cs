using BanterBotSports.BL.Models;
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
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for the GET and POST /torneo/{id}/clonar-jugadores actions
/// introduced by the clonacion-jugadores feature.
/// </summary>
public class TorneoControllerClonarTests
{
    private const string OrganizadorId = "org-user-id";
    private const string OtherUserId = "other-user-id";
    private const int TorneoDestinoId = 1;
    private const int TorneoOrigenId = 2;

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    private static (TorneoController Controller, Mock<ITorneoService> TorneoSvc)
        BuildSut(string userId = OrganizadorId)
    {
        var torneoSvc = new Mock<ITorneoService>();
        var jornadaSvc = new Mock<IJornadaService>();
        var apiSyncSvc = new Mock<IApiFootballSyncService>();
        var partidoSvc = new Mock<IPartidoService>();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManager = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManager
            .Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(userId);

        var controller = new TorneoController(
            torneoSvc.Object,
            jornadaSvc.Object,
            apiSyncSvc.Object,
            partidoSvc.Object,
            dataProtectionProvider,
            userManager.Object,
            NullLogger<TorneoController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<ITempDataProvider>());

        return (controller, torneoSvc);
    }

    private static Torneo BuildTorneo(int id, string organizadorId = OrganizadorId)
        => new() { Id = id, Nombre = $"Torneo {id}", OrganizadorId = organizadorId };

    private static IReadOnlyList<TorneoResumen> BuildClonables()
        => new List<TorneoResumen>
        {
            new(TorneoOrigenId, "Torneo Anterior", 5)
        };

    // ---------------------------------------------------------------------------
    // GET ClonarJugadores
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GET_ClonarJugadores_OrganizerAccess_ReturnsViewWithTorneosClonables()
    {
        var (controller, torneoSvc) = BuildSut();
        var torneo = BuildTorneo(TorneoDestinoId);
        var clonables = BuildClonables();

        torneoSvc.Setup(s => s.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneo);
        torneoSvc.Setup(s => s.GetTorneosClonablesAsync(TorneoDestinoId, OrganizadorId))
            .ReturnsAsync(clonables);

        var result = await controller.ClonarJugadores(TorneoDestinoId);

        result.Should().BeOfType<ViewResult>();
        var view = (ViewResult)result;
        view.Model.Should().Be(torneo);
        ((IReadOnlyList<TorneoResumen>?)view.ViewData["TorneosClonables"]).Should().BeEquivalentTo(clonables);
    }

    [Fact]
    public async Task GET_ClonarJugadores_NonOrganizer_ReturnsForbid()
    {
        var (controller, torneoSvc) = BuildSut(userId: OtherUserId);
        var torneo = BuildTorneo(TorneoDestinoId, organizadorId: OrganizadorId);

        torneoSvc.Setup(s => s.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneo);

        var result = await controller.ClonarJugadores(TorneoDestinoId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GET_ClonarJugadores_TorneoNotFound_ReturnsNotFound()
    {
        var (controller, torneoSvc) = BuildSut();
        torneoSvc.Setup(s => s.GetByIdAsync(TorneoDestinoId)).ReturnsAsync((Torneo?)null);

        var result = await controller.ClonarJugadores(TorneoDestinoId);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ---------------------------------------------------------------------------
    // POST ClonarJugadores
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task POST_ClonarJugadores_AllCloned_SetsTempDataSuccess_AndRedirects()
    {
        var (controller, torneoSvc) = BuildSut();
        var torneo = BuildTorneo(TorneoDestinoId);

        torneoSvc.Setup(s => s.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneo);
        torneoSvc.Setup(s => s.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId))
            .ReturnsAsync(new ClonarJugadoresResult(3, 0));

        var result = await controller.ClonarJugadoresPost(TorneoDestinoId, TorneoOrigenId);

        result.Should().BeOfType<RedirectToActionResult>();
        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("Dashboard");
        redirect.RouteValues!["id"].Should().Be(TorneoDestinoId);

        controller.TempData[TempDataKeys.Success]!.ToString()
            .Should().Be("Se clonaron 3 jugadores correctamente.");
    }

    [Fact]
    public async Task POST_ClonarJugadores_SomeClonedSomeOmitted_SetsTempDataInfo_AndRedirects()
    {
        var (controller, torneoSvc) = BuildSut();
        var torneo = BuildTorneo(TorneoDestinoId);

        torneoSvc.Setup(s => s.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneo);
        torneoSvc.Setup(s => s.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId))
            .ReturnsAsync(new ClonarJugadoresResult(2, 1));

        var result = await controller.ClonarJugadoresPost(TorneoDestinoId, TorneoOrigenId);

        result.Should().BeOfType<RedirectToActionResult>();

        var message = controller.TempData[TempDataKeys.Info]!.ToString()!;
        message.Should().Contain("Se clonaron 2 jugadores");
        message.Should().Contain("1 ya estaba");
    }

    [Fact]
    public async Task POST_ClonarJugadores_AllAlreadyEnrolled_SetsTempDataInfo_AndRedirects()
    {
        var (controller, torneoSvc) = BuildSut();
        var torneo = BuildTorneo(TorneoDestinoId);

        torneoSvc.Setup(s => s.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneo);
        torneoSvc.Setup(s => s.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId))
            .ReturnsAsync(new ClonarJugadoresResult(0, 3));

        var result = await controller.ClonarJugadoresPost(TorneoDestinoId, TorneoOrigenId);

        result.Should().BeOfType<RedirectToActionResult>();

        controller.TempData[TempDataKeys.Info]!.ToString()
            .Should().Be("Todos los jugadores ya estaban inscritos.");
    }

    [Fact]
    public async Task POST_ClonarJugadores_NoPlayersInSource_SetsTempDataInfo_AndRedirects()
    {
        var (controller, torneoSvc) = BuildSut();
        var torneo = BuildTorneo(TorneoDestinoId);

        torneoSvc.Setup(s => s.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneo);
        torneoSvc.Setup(s => s.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId))
            .ReturnsAsync(new ClonarJugadoresResult(0, 0));

        var result = await controller.ClonarJugadoresPost(TorneoDestinoId, TorneoOrigenId);

        result.Should().BeOfType<RedirectToActionResult>();

        controller.TempData[TempDataKeys.Info]!.ToString()
            .Should().Be("El torneo origen no tiene jugadores para clonar.");
    }

    [Fact]
    public async Task POST_ClonarJugadores_NonOrganizer_ReturnsForbid()
    {
        var (controller, torneoSvc) = BuildSut(userId: OtherUserId);
        var torneo = BuildTorneo(TorneoDestinoId, organizadorId: OrganizadorId);

        torneoSvc.Setup(s => s.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneo);

        var result = await controller.ClonarJugadoresPost(TorneoDestinoId, TorneoOrigenId);

        result.Should().BeOfType<ForbidResult>();
    }
}
