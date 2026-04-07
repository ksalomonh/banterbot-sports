using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for PrediccionController Form GET pre-population and POST totalGoles wiring.
/// </summary>
public class PrediccionControllerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private const string TestUserId = "test-user-42";
    private const int JornadaId = 7;
    private const int ParticipanteId = 3;
    private const int TorneoId = 1;

    private static (
        PrediccionController sut,
        Mock<IJornadaService> jornadaSvc,
        Mock<IPrediccionService> prediccionSvc,
        Mock<ITorneoService> torneoSvc)
        BuildSut()
    {
        var jornadaSvc = new Mock<IJornadaService>();
        var prediccionSvc = new Mock<IPrediccionService>();
        var torneoSvc = new Mock<ITorneoService>();

#pragma warning disable CS8625
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManager = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

        userManager
            .Setup(um => um.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(TestUserId);

        var controller = new PrediccionController(
            jornadaSvc.Object,
            prediccionSvc.Object,
            torneoSvc.Object,
            userManager.Object);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Set up TempData so TempData[key] = value doesn't throw
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        return (controller, jornadaSvc, prediccionSvc, torneoSvc);
    }

    private static Jornada MakeOpenJornada() => new()
    {
        Id = JornadaId,
        TorneoId = TorneoId,
        Numero = 1,
        Estado = EstadoJornada.Abierta,
        DeadlineUtc = DateTimeOffset.UtcNow.AddHours(2),
        Partidos = new List<Partido>()
    };

    private static Participante MakeParticipante() => new()
    {
        Id = ParticipanteId,
        TorneoId = TorneoId,
        UserId = TestUserId,
        Rol = RolParticipante.Jugador
    };

    private static Torneo MakeTorneo() => new()
    {
        Id = TorneoId,
        Nombre = "Test Torneo",
        OrganizadorId = "org-user-1",
        PtosResultado = 3,
        PtosMarcador = 5,
        PtosGolesJornada = 2
    };

    // ---------------------------------------------------------------------------
    // Task 2 — GET: pre-populate ViewBag.GolesPronosticados
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Form_Get_WithExistingPrediccionJornada_SetsGolesPronosticadosViewBag()
    {
        // Arrange
        var (sut, jornadaSvc, prediccionSvc, torneoSvc) = BuildSut();

        var jornada = MakeOpenJornada();
        var participante = MakeParticipante();
        var torneo = MakeTorneo();

        jornadaSvc.Setup(s => s.GetDetalleAsync(JornadaId)).ReturnsAsync(jornada);
        torneoSvc.Setup(s => s.GetParticipanteAsync(TorneoId, TestUserId)).ReturnsAsync(participante);
        torneoSvc.Setup(s => s.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);

        prediccionSvc
            .Setup(s => s.GetPorJornadaYParticipanteAsync(JornadaId, ParticipanteId))
            .ReturnsAsync(new Dictionary<int, PrediccionPartido>());

        // Existing PrediccionJornada with GolesPronosticados = 7
        prediccionSvc
            .Setup(s => s.GetByJornadaAsync(JornadaId))
            .ReturnsAsync(new List<PrediccionJornada>
            {
                new() { JornadaId = JornadaId, ParticipanteId = ParticipanteId, GolesPronosticados = 7 }
            });

        // Act
        var result = await sut.Form(JornadaId);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var golesPronosticados = viewResult.ViewData["GolesPronosticados"] as int?;
        golesPronosticados.Should().Be(7, "ViewBag.GolesPronosticados must be set from existing PrediccionJornada");
    }

    // ---------------------------------------------------------------------------
    // Task 3 — POST: totalGoles wiring
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Form_Post_WithTotalGoles_CallsGuardarPrediccionJornadaAsync()
    {
        // Arrange
        var (sut, jornadaSvc, prediccionSvc, torneoSvc) = BuildSut();

        var jornada = MakeOpenJornada();
        var participante = MakeParticipante();
        var torneo = MakeTorneo();

        jornadaSvc.Setup(s => s.GetDetalleAsync(JornadaId)).ReturnsAsync(jornada);
        torneoSvc.Setup(s => s.GetParticipanteAsync(TorneoId, TestUserId)).ReturnsAsync(participante);
        torneoSvc.Setup(s => s.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);

        prediccionSvc
            .Setup(s => s.GuardarPrediccionJornadaAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        // Act: POST with totalGoles = 15
        var result = await sut.Form(
            JornadaId,
            new Dictionary<int, int[]>(),
            totalGoles: 15);

        // Assert: GuardarPrediccionJornadaAsync called exactly once with correct args
        prediccionSvc.Verify(
            s => s.GuardarPrediccionJornadaAsync(JornadaId, ParticipanteId, 15, false),
            Times.Once,
            "POST with totalGoles = 15 must call GuardarPrediccionJornadaAsync with correct parameters");
    }

    [Fact]
    public async Task Form_Post_WithoutTotalGoles_DoesNotCallGuardarPrediccionJornadaAsync()
    {
        // Arrange
        var (sut, jornadaSvc, prediccionSvc, torneoSvc) = BuildSut();

        var jornada = MakeOpenJornada();
        var participante = MakeParticipante();
        var torneo = MakeTorneo();

        jornadaSvc.Setup(s => s.GetDetalleAsync(JornadaId)).ReturnsAsync(jornada);
        torneoSvc.Setup(s => s.GetParticipanteAsync(TorneoId, TestUserId)).ReturnsAsync(participante);
        torneoSvc.Setup(s => s.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);

        // Act: POST with totalGoles = null
        var result = await sut.Form(
            JornadaId,
            new Dictionary<int, int[]>(),
            totalGoles: null);

        // Assert: GuardarPrediccionJornadaAsync must NOT be called
        prediccionSvc.Verify(
            s => s.GuardarPrediccionJornadaAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()),
            Times.Never,
            "POST without totalGoles must not call GuardarPrediccionJornadaAsync");
    }
}
