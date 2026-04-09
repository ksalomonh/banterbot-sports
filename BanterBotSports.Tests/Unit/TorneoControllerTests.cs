using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for TorneoController.
/// Verifies graceful error handling on service exceptions:
/// - No stack traces exposed to the view
/// - Adds a user-friendly model error
/// - Returns the same view (not an error view or redirect that would lose form state)
/// </summary>
public class TorneoControllerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TorneoController BuildSut(
        Mock<ITorneoService>? torneoServiceMock = null,
        Mock<IJornadaService>? jornadaServiceMock = null,
        Mock<IAdminService>? adminServiceMock = null,
        Mock<IPartidoService>? partidoServiceMock = null)
    {
        var torneoSvc = torneoServiceMock ?? new Mock<ITorneoService>();
        var jornadaSvc = jornadaServiceMock ?? new Mock<IJornadaService>();
        var adminSvc = adminServiceMock ?? new Mock<IAdminService>();
        var partidoSvc = partidoServiceMock ?? new Mock<IPartidoService>();

        // Default admin config so the controller doesn't throw when reading it
        if (adminServiceMock is null)
        {
            adminSvc.Setup(s => s.GetConfiguracionAsync())
                .ReturnsAsync(new ConfiguracionGlobal
                {
                    Id = 1,
                    PorcentajePlataforma = 10m,
                    PorcentajeOrganizadorMin = 5m,
                    PorcentajeOrganizadorMax = 30m,
                    MontoInscripcionMinimo = 500m
                });
        }

        // DataProtectionProvider: use ephemeral (in-memory) keys for unit tests
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        // UserManager<AppUser>: mock the minimum surface needed.
        // Nullable optional params suppressed — standard UserManager test pattern.
#pragma warning disable CS8625
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManager = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

        // GetUserId returns a stable test user ID
        userManager
            .Setup(um => um.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns("test-user-id");

        userManager
            .Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new AppUser { Id = "test-user-id" });

        var controller = new TorneoController(
            torneoSvc.Object,
            jornadaSvc.Object,
            partidoSvc.Object,
            adminSvc.Object,
            dataProtectionProvider,
            userManager.Object,
            NullLogger<TorneoController>.Instance);

        // Provide a fake HttpContext so ControllerContext is not null
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };

        // Wire TempData so TempData[key] = value doesn't throw
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

        return controller;
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Nuevo_Post_ServiceThrows_AddsModelError_ReturnsView()
    {
        // Arrange: service throws a generic exception (DB failure, etc.)
        var torneoSvcMock = new Mock<ITorneoService>();
        torneoSvcMock
            .Setup(s => s.CrearTorneoAsync(It.IsAny<TorneoCreateViewModel>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Internal DB failure with stack trace detail"));

        var sut = BuildSut(torneoServiceMock: torneoSvcMock);
        var model = new TorneoCreateViewModel
        {
            Nombre = "Test Torneo",
            NumJornadas = 5,
            MontoInscripcion = 100m,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2
        };

        // Act
        var result = await sut.Nuevo(model);

        // Assert: must return the same view (not redirect, not error page)
        result.Should().BeOfType<ViewResult>("service exception must not bubble up — returns view");
        var viewResult = (ViewResult)result;

        viewResult.ViewName.Should().BeNullOrEmpty("no explicit view name = uses action name 'Nuevo'");
        viewResult.Model.Should().Be(model, "the original model must be passed back to the view");
    }

    [Fact]
    public async Task Nuevo_Post_ServiceThrows_AddsGenericUserFriendlyError_NoStackTrace()
    {
        // Arrange: service throws with a stack-trace-revealing message
        var torneoSvcMock = new Mock<ITorneoService>();
        const string internalMessage = "at BanterBotSports.BL.TorneoService.CrearTorneoAsync() line 42";

        torneoSvcMock
            .Setup(s => s.CrearTorneoAsync(It.IsAny<TorneoCreateViewModel>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException(internalMessage));

        var sut = BuildSut(torneoServiceMock: torneoSvcMock);
        var model = new TorneoCreateViewModel
        {
            Nombre = "Test Torneo",
            NumJornadas = 3,
            MontoInscripcion = 50m,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2
        };

        // Act
        var result = await sut.Nuevo(model);

        // Assert: ModelState has an error, but it must NOT contain the internal exception message
        sut.ModelState.IsValid.Should().BeFalse("an error must be added to ModelState");

        var errors = sut.ModelState
            .SelectMany(ms => ms.Value!.Errors.Select(e => e.ErrorMessage))
            .ToList();

        errors.Should().NotBeEmpty("at least one user-friendly error must be present");

        // The raw internal exception message must NOT be exposed
        errors.Should().NotContain(msg => msg.Contains(internalMessage),
            "raw exception details must never be exposed to the user — no stack traces");

        // The displayed message must be a safe, generic user-facing string
        errors.Should().Contain(msg => msg.Contains("error") || msg.Contains("torneo") || msg.Contains("intenta"),
            "the user must receive a friendly, non-technical error message");
    }

    [Fact]
    public async Task Nuevo_Post_ServiceThrows_DoesNotBubbleException()
    {
        // Arrange
        var torneoSvcMock = new Mock<ITorneoService>();
        torneoSvcMock
            .Setup(s => s.CrearTorneoAsync(It.IsAny<TorneoCreateViewModel>(), It.IsAny<string>()))
            .ThrowsAsync(new TimeoutException("DB timeout"));

        var sut = BuildSut(torneoServiceMock: torneoSvcMock);
        var model = new TorneoCreateViewModel
        {
            Nombre = "Test Torneo",
            NumJornadas = 2,
            MontoInscripcion = 75m,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2
        };

        // Act & Assert: the controller must catch the exception and NOT re-throw
        var act = async () => await sut.Nuevo(model);
        await act.Should().NotThrowAsync("controller must swallow service exceptions gracefully");
    }

    // ---------------------------------------------------------------------------
    // Leaderboard Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Leaderboard_Get_ReturnsViewWithLeaderboardViewModel()
    {
        // Arrange: torneo with one participant matching the test user
        const string testUserId = "test-user-id";
        var participante = new Participante { Id = 1, UserId = testUserId };
        var torneo = new Torneo
        {
            Id = 42,
            Nombre = "Test Liga",
            OrganizadorId = testUserId,
            Participantes = new List<Participante> { participante }
        };

        IReadOnlyList<RankingParticipante> ranking = new List<RankingParticipante>
        {
            new RankingParticipante(ParticipanteId: 1, NombreDisplay: "TestUser", PuntosTotal: 100, Posicion: 1)
        };

        var torneoSvcMock = new Mock<ITorneoService>();
        torneoSvcMock
            .Setup(s => s.GetByIdWithDetailsAsync(42))
            .ReturnsAsync(torneo);
        torneoSvcMock
            .Setup(s => s.BuildRankingAsync(torneo))
            .ReturnsAsync(ranking);

        var sut = BuildSut(torneoServiceMock: torneoSvcMock);

        // Act
        var result = await sut.Leaderboard(42);

        // Assert: returns ViewResult with LeaderboardViewModel
        result.Should().BeOfType<ViewResult>("Leaderboard GET must return a view");
        var viewResult = (ViewResult)result;
        viewResult.Model.Should().BeOfType<LeaderboardViewModel>("model must be LeaderboardViewModel");

        var vm = (LeaderboardViewModel)viewResult.Model!;
        vm.TorneoId.Should().Be(42);
        vm.TorneoNombre.Should().Be("Test Liga");
        vm.Ranking.Should().HaveCount(1);
        vm.Ranking[0].NombreDisplay.Should().Be("TestUser");
    }

    [Fact]
    public async Task Leaderboard_Get_TorneoNotFound_Returns404()
    {
        // Arrange: service returns null
        var torneoSvcMock = new Mock<ITorneoService>();
        torneoSvcMock
            .Setup(s => s.GetByIdWithDetailsAsync(It.IsAny<int>()))
            .ReturnsAsync((Torneo?)null);

        var sut = BuildSut(torneoServiceMock: torneoSvcMock);

        // Act
        var result = await sut.Leaderboard(999);

        // Assert: must return NotFound
        result.Should().BeOfType<NotFoundResult>("non-existent torneo must return 404");
    }

    [Fact]
    public async Task Leaderboard_Get_UserNotParticipant_ReturnsForbid()
    {
        // Arrange: torneo exists but test user is NOT a participant
        var torneo = new Torneo
        {
            Id = 10,
            Nombre = "Private Torneo",
            OrganizadorId = "other-user",
            Participantes = new List<Participante>
            {
                new Participante { Id = 99, UserId = "other-user" }
            }
        };

        var torneoSvcMock = new Mock<ITorneoService>();
        torneoSvcMock
            .Setup(s => s.GetByIdWithDetailsAsync(10))
            .ReturnsAsync(torneo);

        var sut = BuildSut(torneoServiceMock: torneoSvcMock);

        // Act
        var result = await sut.Leaderboard(10);

        // Assert: must return Forbid (not the view, not a redirect)
        result.Should().BeOfType<ForbidResult>("non-participant must be forbidden from viewing leaderboard");
    }
}
