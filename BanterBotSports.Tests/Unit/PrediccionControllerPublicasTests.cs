using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for PrediccionController.Publicas action.
/// Covers: deadline not passed → 403; deadline passed → Publicas view; jornada not found → 404.
/// </summary>
public class PrediccionControllerPublicasTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static PrediccionController BuildSut(
        Mock<IJornadaService> jornadaServiceMock,
        Mock<IPrediccionService>? prediccionServiceMock = null)
    {
        var prediccionSvc = prediccionServiceMock ?? new Mock<IPrediccionService>();
        var torneoSvc = new Mock<ITorneoService>();

#pragma warning disable CS8625
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManager = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

        userManager
            .Setup(um => um.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns("test-user-id");

        var controller = new PrediccionController(
            jornadaServiceMock.Object,
            prediccionSvc.Object,
            torneoSvc.Object,
            userManager.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static Jornada MakeJornada(int id, DateTimeOffset? deadline)
        => new()
        {
            Id = id, TorneoId = 1, Numero = 3, Estado = EstadoJornada.Cerrada,
            DeadlineUtc = deadline
        };

    private static ResumenViewModel MakeResumen(int jornadaId)
        => new(
            JornadaId: jornadaId,
            JornadaNumero: 3,
            TorneoNombre: "Liga Test",
            TorneoId: 1,
            Participantes: new List<ResumenParticipanteRow>());

    // ---------------------------------------------------------------------------
    // 3.4.1 — Deadline not yet passed → 403 Forbidden
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Publicas_DeadlineNotPassed_Returns403()
    {
        // Arrange: deadline is in the future
        var futureDeadline = DateTimeOffset.UtcNow.AddHours(2);
        var jornada = MakeJornada(id: 1, deadline: futureDeadline);

        var jornadaSvc = new Mock<IJornadaService>();
        jornadaSvc.Setup(j => j.GetDetalleAsync(1)).ReturnsAsync(jornada);

        var sut = BuildSut(jornadaSvc);

        // Act
        var result = await sut.Publicas(1);

        // Assert
        result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(403);
    }

    // ---------------------------------------------------------------------------
    // 3.4.2 — Deadline passed → returns Publicas view with ResumenViewModel
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Publicas_DeadlinePassed_ReturnsPublicasView()
    {
        // Arrange: deadline was 1 hour ago
        var pastDeadline = DateTimeOffset.UtcNow.AddHours(-1);
        var jornada = MakeJornada(id: 2, deadline: pastDeadline);
        var resumen = MakeResumen(2);

        var jornadaSvc = new Mock<IJornadaService>();
        jornadaSvc.Setup(j => j.GetDetalleAsync(2)).ReturnsAsync(jornada);
        jornadaSvc.Setup(j => j.GetResumenJornadaAsync(2)).ReturnsAsync(resumen);

        var sut = BuildSut(jornadaSvc);

        // Act
        var result = await sut.Publicas(2);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().BeNullOrEmpty("default view name resolves to Publicas");
        viewResult.Model.Should().Be(resumen);
    }

    // ---------------------------------------------------------------------------
    // 3.4.3 — Jornada not found → 404
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Publicas_JornadaNotFound_Returns404()
    {
        // Arrange
        var jornadaSvc = new Mock<IJornadaService>();
        jornadaSvc.Setup(j => j.GetDetalleAsync(999)).ReturnsAsync((Jornada?)null);

        var sut = BuildSut(jornadaSvc);

        // Act
        var result = await sut.Publicas(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
