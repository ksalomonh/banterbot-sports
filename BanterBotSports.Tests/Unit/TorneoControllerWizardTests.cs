using System.Net;
using System.Net.Http;
using System.Text;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Integrations.ApiFootball;
using BanterBotSports.Web.Controllers;
using BanterBotSports.Web.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using BanterBotSports.BL.Models;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for cycle6 wizard features:
/// - BuscarPartidos GET action (valid/invalid liga, date range)
/// - POST Nuevo prize sum validation (InitialStep routing)
/// - ApiFootballClient.MapFixtureToDto logo URL mapping (via public API + mocked HttpClient)
/// - POST Nuevo match assignment loop (PartidosSeleccionados, per-match failure handling)
/// </summary>
public class TorneoControllerWizardTests
{
    // ---------------------------------------------------------------------------
    // Controller factory
    // ---------------------------------------------------------------------------

    private static (TorneoController Controller, Mock<ITorneoService> TorneoSvc,
        Mock<IJornadaService> JornadaSvc, Mock<IAdminService> AdminSvc,
        Mock<IPartidoService> PartidoSvc)
        BuildSut()
    {
        var torneoSvc = new Mock<ITorneoService>();
        var jornadaSvc = new Mock<IJornadaService>();
        var adminSvc = new Mock<IAdminService>();
        var partidoSvc = new Mock<IPartidoService>();

        // Default admin config so the controller doesn't throw when reading it
        adminSvc.Setup(s => s.GetConfiguracionAsync())
            .ReturnsAsync(new ConfiguracionGlobal
            {
                Id = 1,
                PorcentajePlataforma = 10m,
                PorcentajeOrganizadorMin = 5m,
                PorcentajeOrganizadorMax = 30m,
                MontoInscripcionMinimo = 500m
            });

        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManager = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

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

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Wire TempData so TempData[key] = value doesn't throw
        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<ITempDataProvider>());

        return (controller, torneoSvc, jornadaSvc, adminSvc, partidoSvc);
    }

    // ---------------------------------------------------------------------------
    // 5.1 — BuscarPartidos
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task BuscarPartidos_ValidLiga_ReturnsOkWithMatches()
    {
        // Arrange: use a known valid liga id from LeagueCatalog
        var validLigaId = LeagueCatalog.Leagues[0].Id; // e.g. Premier League = 39
        var expectedMatches = new List<PartidoDto>
        {
            new PartidoDto(1, "1", "Real Madrid", "Barcelona",
                DateTimeOffset.UtcNow.AddDays(3), null, null, EstadoPartido.Programado)
        };

        var (controller, _, _, _, partidoSvc) = BuildSut();
        partidoSvc
            .Setup(s => s.EsLigaValida(validLigaId))
            .Returns(true);
        partidoSvc
            .Setup(s => s.GetProximosPartidosAsync(validLigaId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(expectedMatches);

        // Act
        var result = await controller.BuscarPartidos(validLigaId);

        // Assert
        result.Should().BeOfType<JsonResult>("valid liga must return OkObjectResult/JsonResult");
        var jsonResult = (JsonResult)result;
        jsonResult.Value.Should().BeEquivalentTo(expectedMatches);
    }

    [Fact]
    public async Task BuscarPartidos_InvalidLiga_ReturnsBadRequest()
    {
        // Arrange: liga id that is NOT in LeagueCatalog
        const int invalidLigaId = 99999;
        LeagueCatalog.ValidIds.Should().NotContain(invalidLigaId, "test precondition: id must be invalid");

        var (controller, _, _, _, _) = BuildSut();

        // Act
        var result = await controller.BuscarPartidos(invalidLigaId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>("unknown liga must return 400 Bad Request");
    }

    [Fact]
    public async Task BuscarPartidos_CallsService_WithTodayToTodayPlus35()
    {
        // Arrange
        var validLigaId = LeagueCatalog.Leagues[0].Id;
        DateOnly capturedFrom = default;
        DateOnly capturedTo = default;

        var (controller, _, _, _, partidoSvc) = BuildSut();
        partidoSvc
            .Setup(s => s.EsLigaValida(validLigaId))
            .Returns(true);
        partidoSvc
            .Setup(s => s.GetProximosPartidosAsync(
                It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .Callback<int, DateOnly, DateOnly>((_, from, to) =>
            {
                capturedFrom = from;
                capturedTo = to;
            })
            .ReturnsAsync(Array.Empty<PartidoDto>());

        // Act
        var before = DateOnly.FromDateTime(DateTime.UtcNow);
        await controller.BuscarPartidos(validLigaId);
        var after = DateOnly.FromDateTime(DateTime.UtcNow);

        // Assert: "from" must be today (or very close), "to" must be from + 35
        capturedFrom.Should().BeOnOrAfter(before, "from date must be today");
        capturedFrom.Should().BeOnOrBefore(after, "from date must not be in the future");
        capturedTo.Should().Be(capturedFrom.AddDays(35), "to date must be exactly 35 days after from");
    }

    // ---------------------------------------------------------------------------
    // 5.2 — Prize sum validation in POST Nuevo
    // ---------------------------------------------------------------------------

    private static TorneoCreateViewModel ValidModelWithPrizeSum(decimal sum)
    {
        return new TorneoCreateViewModel
        {
            Nombre = "Test Torneo",
            NumJornadas = 3,
            MontoInscripcion = 100m,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2,
            ConfiguracionPremios = new List<ConfiguracionPremioViewModel>
            {
                new ConfiguracionPremioViewModel { Posicion = 1, Porcentaje = sum }
            }
        };
    }

    // Dynamic prize pool = 100 - platform(10%) - organizer(5% min from config) = 85%
    private const decimal DynamicPool = 85m;

    [Fact]
    public async Task Nuevo_Post_PrizeSumEqualsPool_DoesNotAddModelError()
    {
        // Arrange: prizes sum exactly the dynamic pool (100 - platform% - organizer%)
        var (controller, torneoSvc, jornadaSvc, _, _) = BuildSut();

        torneoSvc
            .Setup(s => s.CrearTorneoAsync(It.IsAny<TorneoCreateViewModel>(), It.IsAny<string>()))
            .ReturnsAsync(new Torneo { Id = 1 });

        // GetByTorneoIdAsync returns empty list so the match assignment loop is a no-op
        jornadaSvc
            .Setup(s => s.GetByTorneoIdAsync(1))
            .ReturnsAsync(new List<Jornada>());

        var model = ValidModelWithPrizeSum(DynamicPool);

        // Act
        var result = await controller.Nuevo(model);

        // Assert: no prize-sum error was added — valid sum must not invalidate the form
        controller.ModelState.ContainsKey(string.Empty).Should().BeFalse(
            "sum == dynamic pool must not add a prize validation error to ModelState");
        result.Should().BeOfType<RedirectToActionResult>(
            "valid form with prize sum == dynamic pool must redirect to Dashboard");
    }

    [Fact]
    public async Task Nuevo_Post_PrizeSumLessThanPool_AddsModelError_SetsInitialStep2()
    {
        // Arrange: prizes sum 60 % (less than dynamic pool of 85%)
        var (controller, _, _, _, _) = BuildSut();
        var model = ValidModelWithPrizeSum(60m);

        // Act
        var result = await controller.Nuevo(model);

        // Assert
        result.Should().BeOfType<ViewResult>("validation failure must return the form view");
        controller.ModelState.IsValid.Should().BeFalse("prize sum < pool must invalidate model");
        controller.ModelState.ContainsKey(string.Empty).Should().BeTrue("error must be on empty key");
        controller.ModelState[string.Empty]!.Errors.Should().ContainSingle();
        controller.ModelState[string.Empty]!.Errors[0].ErrorMessage
            .Should().Be("Los premios deben sumar exactamente 85% (100% − 10% plataforma − 5% organizador)");

        // ViewBag.InitialStep must route user to Premios step (index 2)
        var viewResult = (ViewResult)result;
        ((int?)viewResult.ViewData["InitialStep"]).Should().Be(2,
            "validation error on prizes must route to Premios step (index 2)");
    }

    [Fact]
    public async Task Nuevo_Post_PrizeSumGreaterThanPool_AddsModelError_SetsInitialStep2()
    {
        // Arrange: prizes sum 100 % (more than dynamic pool of 85%)
        var (controller, _, _, _, _) = BuildSut();
        var model = ValidModelWithPrizeSum(100m);

        // Act
        var result = await controller.Nuevo(model);

        // Assert
        result.Should().BeOfType<ViewResult>("validation failure must return the form view");
        controller.ModelState.IsValid.Should().BeFalse("prize sum > pool must invalidate model");
        controller.ModelState.ContainsKey(string.Empty).Should().BeTrue();
        controller.ModelState[string.Empty]!.Errors.Should().ContainSingle();
        controller.ModelState[string.Empty]!.Errors[0].ErrorMessage
            .Should().Be("Los premios deben sumar exactamente 85% (100% − 10% plataforma − 5% organizador)");

        var viewResult = (ViewResult)result;
        ((int?)viewResult.ViewData["InitialStep"]).Should().Be(2,
            "validation error on prizes must route to Premios step (index 2)");
    }

    // ---------------------------------------------------------------------------
    // 5.3 — ApiFootballClient.MapFixtureToDto logo mapping
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds an <see cref="ApiFootballClient"/> backed by a fake HTTP handler that
    /// returns the supplied JSON body for any request.
    /// </summary>
    private static ApiFootballClient BuildApiFootballClient(string responseJson)
    {
        var handlerMock = new FakeHttpMessageHandler(responseJson);
        var httpClient = new HttpClient(handlerMock);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var configMock = new Mock<IConfiguration>();
        configMock
            .Setup(c => c["ApiFootball:ApiKey"])
            .Returns("test-key");

        return new ApiFootballClient(
            httpClientFactoryMock.Object,
            configMock.Object,
            NullLogger<ApiFootballClient>.Instance);
    }

    private static string BuildApiResponseJson(
        int fixtureId,
        long timestamp,
        string homeTeam,
        string awayTeam,
        string? homeLogo,
        string? awayLogo)
    {
        // Build the minimal API-Football v3 JSON response shape
        var homePart = homeLogo is not null
            ? $@"{{ ""name"": ""{homeTeam}"", ""logo"": ""{homeLogo}"" }}"
            : $@"{{ ""name"": ""{homeTeam}"" }}";
        var awayPart = awayLogo is not null
            ? $@"{{ ""name"": ""{awayTeam}"", ""logo"": ""{awayLogo}"" }}"
            : $@"{{ ""name"": ""{awayTeam}"" }}";

        return $@"{{
            ""response"": [
                {{
                    ""fixture"": {{
                        ""id"": {fixtureId},
                        ""timestamp"": {timestamp},
                        ""status"": {{ ""short"": ""NS"" }}
                    }},
                    ""teams"": {{
                        ""home"": {homePart},
                        ""away"": {awayPart}
                    }},
                    ""goals"": {{
                        ""home"": null,
                        ""away"": null
                    }}
                }}
            ]
        }}";
    }

    [Fact]
    public async Task ApiFootballClient_GetMatchesAsync_PopulatesLogoUrls()
    {
        // Arrange: API returns a fixture with both logos
        const string homeLogo = "https://media.api-sports.io/football/teams/33.png";
        const string awayLogo = "https://media.api-sports.io/football/teams/40.png";

        var json = BuildApiResponseJson(
            fixtureId: 100,
            timestamp: DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds(),
            homeTeam: "Manchester United",
            awayTeam: "Liverpool",
            homeLogo: homeLogo,
            awayLogo: awayLogo);

        var client = BuildApiFootballClient(json);

        // Act
        var result = await client.GetMatchesAsync(
            39,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

        // Assert
        result.Should().HaveCount(1, "one fixture in the response");
        result[0].LogoUrlEquipo1.Should().Be(homeLogo, "home logo must map to LogoUrlEquipo1");
        result[0].LogoUrlEquipo2.Should().Be(awayLogo, "away logo must map to LogoUrlEquipo2");
    }

    [Fact]
    public async Task ApiFootballClient_GetMatchesAsync_NullLogo_MapsToNull()
    {
        // Arrange: API returns a fixture where logo fields are absent (null)
        var json = BuildApiResponseJson(
            fixtureId: 200,
            timestamp: DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds(),
            homeTeam: "Team A",
            awayTeam: "Team B",
            homeLogo: null,
            awayLogo: null);

        var client = BuildApiFootballClient(json);

        // Act
        var result = await client.GetMatchesAsync(
            39,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

        // Assert
        result.Should().HaveCount(1);
        result[0].LogoUrlEquipo1.Should().BeNull("absent logo field must map to null");
        result[0].LogoUrlEquipo2.Should().BeNull("absent logo field must map to null");
    }

    [Fact]
    public async Task ApiFootballClient_GetFixtureByIdAsync_PopulatesLogoUrls()
    {
        // Arrange
        const string homeLogo = "https://media.api-sports.io/football/teams/50.png";
        const string awayLogo = "https://media.api-sports.io/football/teams/51.png";

        var json = BuildApiResponseJson(
            fixtureId: 300,
            timestamp: DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds(),
            homeTeam: "Sevilla",
            awayTeam: "Valencia",
            homeLogo: homeLogo,
            awayLogo: awayLogo);

        var client = BuildApiFootballClient(json);

        // Act
        var result = await client.GetFixtureByIdAsync(300);

        // Assert
        result.Should().NotBeNull();
        result!.LogoUrlEquipo1.Should().Be(homeLogo);
        result!.LogoUrlEquipo2.Should().Be(awayLogo);
    }

    // ---------------------------------------------------------------------------
    // 5.4 — POST Nuevo match assignment loop
    // ---------------------------------------------------------------------------

    private static TorneoCreateViewModel ValidModelWith100Prizes(List<string>? partidos = null)
    {
        return new TorneoCreateViewModel
        {
            Nombre = "Test Torneo",
            NumJornadas = 3,
            MontoInscripcion = 100m,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2,
            ConfiguracionPremios = new List<ConfiguracionPremioViewModel>
            {
                new ConfiguracionPremioViewModel { Posicion = 1, Porcentaje = DynamicPool }
            },
            PartidosSeleccionados = partidos ?? new List<string>()
        };
    }

    [Fact]
    public async Task Nuevo_Post_WithPartidosSeleccionados_TriggersAssignmentLoop()
    {
        // Arrange
        var (controller, torneoSvc, jornadaSvc, _, partidoSvc) = BuildSut();

        var createdTorneo = new Torneo { Id = 10 };
        torneoSvc
            .Setup(s => s.CrearTorneoAsync(It.IsAny<TorneoCreateViewModel>(), It.IsAny<string>()))
            .ReturnsAsync(createdTorneo);

        var primeraJornada = new Jornada { Id = 1, Numero = 1 };
        jornadaSvc
            .Setup(s => s.GetByTorneoIdAsync(10))
            .ReturnsAsync(new List<Jornada> { primeraJornada });

        var fixture = new PartidoDto(
            Id: 555,
            ExternalId: "555",
            Equipo1: "Boca Juniors",
            Equipo2: "River Plate",
            KickOffUtc: DateTimeOffset.UtcNow.AddDays(3),
            GolesEquipo1: null,
            GolesEquipo2: null,
            Estado: EstadoPartido.Programado);

        partidoSvc
            .Setup(s => s.GetFixturePorExternalIdAsync(555))
            .ReturnsAsync(fixture);

        torneoSvc
            .Setup(s => s.AsignarPartidosInicialesAsync(1, It.IsAny<IReadOnlyList<PartidoDto>>()))
            .ReturnsAsync(new List<string>());

        var model = ValidModelWith100Prizes(new List<string> { "555" });

        // Act
        var result = await controller.Nuevo(model);

        // Assert: assignment was called for the selected fixture
        torneoSvc.Verify(
            s => s.AsignarPartidosInicialesAsync(1, It.Is<IReadOnlyList<PartidoDto>>(list => list.Any(p => p.ExternalId == "555"))),
            Times.Once,
            "AsignarPartidosInicialesAsync must be called once for the selected fixture");

        result.Should().BeOfType<RedirectToActionResult>("successful creation must redirect to Dashboard");
    }

    [Fact]
    public async Task Nuevo_Post_PerMatchFailure_AddsTempDataWarning_TorneoStillCreated()
    {
        // Arrange: assignment returns failures — torneo creation should still succeed
        var (controller, torneoSvc, jornadaSvc, _, partidoSvc) = BuildSut();

        var createdTorneo = new Torneo { Id = 20 };
        torneoSvc
            .Setup(s => s.CrearTorneoAsync(It.IsAny<TorneoCreateViewModel>(), It.IsAny<string>()))
            .ReturnsAsync(createdTorneo);

        var primeraJornada = new Jornada { Id = 2, Numero = 1 };
        jornadaSvc
            .Setup(s => s.GetByTorneoIdAsync(20))
            .ReturnsAsync(new List<Jornada> { primeraJornada });

        var fixture = new PartidoDto(666, "666", "Team X", "Team Y",
            DateTimeOffset.UtcNow.AddDays(4), null, null, EstadoPartido.Programado);

        partidoSvc
            .Setup(s => s.GetFixturePorExternalIdAsync(666))
            .ReturnsAsync(fixture);

        // Assignment returns a failure list (non-empty = partial failure)
        torneoSvc
            .Setup(s => s.AsignarPartidosInicialesAsync(2, It.IsAny<IReadOnlyList<PartidoDto>>()))
            .ReturnsAsync(new List<string> { "666" });

        var model = ValidModelWith100Prizes(new List<string> { "666" });

        // Act
        var result = await controller.Nuevo(model);

        // Assert: torneo was still created (redirect to Dashboard, not an error view)
        result.Should().BeOfType<RedirectToActionResult>("torneo creation must succeed even when match assignment fails");
        var redirect = (RedirectToActionResult)result;
        redirect.ActionName.Should().Be("Dashboard");

        // TempData must contain an info/warning message about the failed assignment
        controller.TempData.Keys.Should().Contain(TempDataKeys.Info,
            "a TempData info message must be set when match assignment fails");
        controller.TempData[TempDataKeys.Info]!.ToString().Should().Contain("partido",
            "the message must mention partidos so the user understands what happened");
    }
}

// ---------------------------------------------------------------------------
// Test helper: fake HttpMessageHandler that returns a fixed JSON response
// ---------------------------------------------------------------------------

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseJson;

    public FakeHttpMessageHandler(string responseJson)
    {
        _responseJson = responseJson;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
