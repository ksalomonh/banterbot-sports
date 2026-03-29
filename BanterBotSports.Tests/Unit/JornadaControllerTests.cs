using BanterBotSports.BanterAI;
using BanterBotSports.BL.Services;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Integrations.ApiFootball;
using BanterBotSports.Integrations.Telegram;
using BanterBotSports.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for JornadaController.
/// Tests the new Resumen action: happy path and 404 on missing jornada.
/// </summary>
public class JornadaControllerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static JornadaAbiertaNotifier BuildNotifier()
    {
        // JornadaAbiertaNotifier is sealed — build a real instance with mocked/null deps.
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var telegramBotSvc = new Mock<ITelegramBotService>();
        return new JornadaAbiertaNotifier(
            scopeFactory.Object,
            telegramBotSvc.Object,
            NullLogger<JornadaAbiertaNotifier>.Instance);
    }

    private static JornadaController BuildSut(
        Mock<IJornadaService>? jornadaServiceMock = null,
        Mock<IPartidoService>? partidoServiceMock = null,
        Mock<ITorneoService>? torneoServiceMock = null,
        Mock<IPrediccionService>? prediccionServiceMock = null)
    {
        var jornadaSvc = jornadaServiceMock ?? new Mock<IJornadaService>();
        var partidoSvc = partidoServiceMock ?? new Mock<IPartidoService>();
        var torneoSvc = torneoServiceMock ?? new Mock<ITorneoService>();
        var prediccionSvc = prediccionServiceMock ?? new Mock<IPrediccionService>();
        var apiFootball = new Mock<IApiFootballSyncService>();
        var banterDispatch = new Mock<IBanterDispatchService>();

        // UserManager<AppUser>: mock the minimum surface needed.
#pragma warning disable CS8625
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManager = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

        userManager
            .Setup(um => um.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns("test-user-id");

        // JornadaAbiertaNotifier is sealed — instantiate a real one with no-op deps
        var notifier = BuildNotifier();

        var controller = new JornadaController(
            jornadaSvc.Object,
            partidoSvc.Object,
            torneoSvc.Object,
            prediccionSvc.Object,
            apiFootball.Object,
            userManager.Object,
            NullLogger<JornadaController>.Instance,
            notifier,
            banterDispatch.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        return controller;
    }

    // ---------------------------------------------------------------------------
    // Resumen Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Resumen_Get_ReturnsViewWithResumenViewModel()
    {
        // Arrange
        var resumen = new ResumenViewModel(
            JornadaId: 5,
            JornadaNumero: 5,
            TorneoNombre: "Test Liga",
            TorneoId: 1,
            Participantes: new List<ResumenParticipanteRow>
            {
                new ResumenParticipanteRow(
                    NombreDisplay: "TestUser",
                    PuntosJornada: 15,
                    Predicciones: new List<PrediccionConResultado>())
            });

        var jornadaSvcMock = new Mock<IJornadaService>();
        jornadaSvcMock
            .Setup(s => s.GetResumenJornadaAsync(5))
            .ReturnsAsync(resumen);

        var sut = BuildSut(jornadaServiceMock: jornadaSvcMock);

        // Act
        var result = await sut.Resumen(5);

        // Assert
        result.Should().BeOfType<ViewResult>("Resumen GET must return a view");
        var viewResult = (ViewResult)result;
        viewResult.Model.Should().BeOfType<ResumenViewModel>("model must be ResumenViewModel");

        var vm = (ResumenViewModel)viewResult.Model!;
        vm.JornadaId.Should().Be(5);
        vm.TorneoNombre.Should().Be("Test Liga");
        vm.Participantes.Should().HaveCount(1);
        vm.Participantes[0].NombreDisplay.Should().Be("TestUser");
        vm.Participantes[0].PuntosJornada.Should().Be(15);
    }

    [Fact]
    public async Task Resumen_Get_JornadaNotFound_Returns404()
    {
        // Arrange: service returns null (jornada doesn't exist)
        var jornadaSvcMock = new Mock<IJornadaService>();
        jornadaSvcMock
            .Setup(s => s.GetResumenJornadaAsync(It.IsAny<int>()))
            .ReturnsAsync((ResumenViewModel?)null);

        var sut = BuildSut(jornadaServiceMock: jornadaSvcMock);

        // Act
        var result = await sut.Resumen(99999);

        // Assert
        result.Should().BeOfType<NotFoundResult>("non-existent jornada must return 404");
    }
}

/// <summary>
/// Unit tests for PrediccionClassifier.Clasificar() — all 5 classification branches.
/// </summary>
public class PrediccionClassifierTests
{
    [Fact]
    public void Clasificar_NoPredictionSubmitted_ReturnsSinPrediccion()
    {
        // No prediction was submitted (nulls on predicted goals)
        var result = PrediccionClassifier.Clasificar(
            golesPredichos1: null, golesPredichos2: null,
            golesOficiales1: 2, golesOficiales2: 1);

        result.Should().Be(ResultadoPrediccion.SinPrediccion,
            "a missing prediction must classify as SinPrediccion");
    }

    [Fact]
    public void Clasificar_OfficialResultNotYetAvailable_ReturnsSinPrediccion()
    {
        // Prediction exists but official result has not been entered yet
        var result = PrediccionClassifier.Clasificar(
            golesPredichos1: 1, golesPredichos2: 0,
            golesOficiales1: null, golesOficiales2: null);

        result.Should().Be(ResultadoPrediccion.SinPrediccion,
            "pending official result must classify as SinPrediccion");
    }

    [Fact]
    public void Clasificar_ExactScoreMatch_ReturnsExacto()
    {
        // Prediction matches official result exactly
        var result = PrediccionClassifier.Clasificar(
            golesPredichos1: 2, golesPredichos2: 1,
            golesOficiales1: 2, golesOficiales2: 1);

        result.Should().Be(ResultadoPrediccion.Exacto,
            "an exact score prediction must classify as Exacto");
    }

    [Fact]
    public void Clasificar_SameOutcomeDifferentScore_ReturnsResultadoCorrecto()
    {
        // Home win predicted correctly but score was different
        var result = PrediccionClassifier.Clasificar(
            golesPredichos1: 1, golesPredichos2: 0,
            golesOficiales1: 3, golesOficiales2: 1);

        result.Should().Be(ResultadoPrediccion.ResultadoCorrecto,
            "correct outcome with wrong score must classify as ResultadoCorrecto");
    }

    [Fact]
    public void Clasificar_WrongOutcome_ReturnsFallido()
    {
        // Draw predicted but home side won
        var result = PrediccionClassifier.Clasificar(
            golesPredichos1: 1, golesPredichos2: 1,
            golesOficiales1: 2, golesOficiales2: 0);

        result.Should().Be(ResultadoPrediccion.Fallido,
            "wrong outcome prediction must classify as Fallido");
    }
}
