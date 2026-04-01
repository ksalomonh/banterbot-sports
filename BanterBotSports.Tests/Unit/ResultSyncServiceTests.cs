using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Integrations.ApiFootball;
using BanterBotSports.Integrations.Hosted;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for ResultSyncService ranking broadcast behavior.
/// Tests verify that after a score change the ranking is broadcast,
/// and that broadcast errors do not stop processing of remaining matches.
/// </summary>
public class ResultSyncServiceTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static Partido BuildPartido(int id = 1, int jornadaId = 10, string externalId = "999",
        int? goles1 = null, int? goles2 = null, EstadoPartido estado = EstadoPartido.EnCurso)
        => new()
        {
            Id = id,
            JornadaId = jornadaId,
            ExternalId = externalId,
            Equipo1 = "River",
            Equipo2 = "Boca",
            KickOffUtc = DateTimeOffset.UtcNow,
            GolesEquipo1Oficial = goles1,
            GolesEquipo2Oficial = goles2,
            Estado = estado,
            Jornada = null!
        };

    private static Jornada BuildJornada(int id = 10, int torneoId = 5)
        => new()
        {
            Id = id,
            TorneoId = torneoId,
            Numero = 1,
            Estado = EstadoJornada.Abierta
        };

    private static Torneo BuildTorneo(int id = 5)
        => new()
        {
            Id = id,
            Nombre = "Test Torneo",
            OrganizadorId = "user1",
            Estado = EstadoTorneo.Activo,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2
        };

    private static IReadOnlyList<RankingParticipante> BuildRanking()
        => new List<RankingParticipante>
        {
            new(ParticipanteId: 1, NombreDisplay: "Pepe", PuntosTotal: 9, Posicion: 1),
            new(ParticipanteId: 2, NombreDisplay: "Juan", PuntosTotal: 6, Posicion: 2)
        };

    private static PartidoDto BuildLiveScore(int externalId, int goles1, int goles2,
        EstadoPartido estado = EstadoPartido.EnCurso)
        => new(Id: externalId, ExternalId: externalId.ToString(), Equipo1: "River", Equipo2: "Boca",
            KickOffUtc: DateTimeOffset.UtcNow, GolesEquipo1: goles1, GolesEquipo2: goles2, Estado: estado);

    /// <summary>
    /// Builds a ResultSyncService with all dependencies mocked.
    /// Returns the service and mock objects needed for assertions.
    /// </summary>
    private static (ResultSyncService sut,
        Mock<IPartidoRepository> partidoRepoMock,
        Mock<IPartidoService> partidoServiceMock,
        Mock<IApiFootballSyncService> apiFootballMock,
        Mock<IJornadaService> jornadaServiceMock,
        Mock<ITorneoService> torneoServiceMock,
        Mock<IRankingBroadcaster> broadcasterMock)
        BuildSut(
            IReadOnlyList<Partido>? activeMatches = null,
            PartidoDto? liveScore = null,
            Jornada? jornada = null,
            Torneo? torneo = null,
            IReadOnlyList<RankingParticipante>? ranking = null)
    {
        var partidoRepoMock = new Mock<IPartidoRepository>();
        var partidoServiceMock = new Mock<IPartidoService>();
        var apiFootballMock = new Mock<IApiFootballSyncService>();
        var jornadaServiceMock = new Mock<IJornadaService>();
        var torneoServiceMock = new Mock<ITorneoService>();
        var broadcasterMock = new Mock<IRankingBroadcaster>();
        var chatBanterMock = new Mock<IChatBanterService>();

        // Setup defaults
        partidoRepoMock
            .Setup(r => r.GetByEstadoAsync(EstadoPartido.EnCurso))
            .ReturnsAsync(activeMatches ?? new List<Partido>());

        if (liveScore is not null)
        {
            apiFootballMock
                .Setup(a => a.GetLiveScoreAsync(It.IsAny<int>()))
                .ReturnsAsync(liveScore);
        }

        if (jornada is not null)
        {
            jornadaServiceMock
                .Setup(j => j.GetDetalleAsync(jornada.Id))
                .ReturnsAsync(jornada);
        }

        if (torneo is not null)
        {
            torneoServiceMock
                .Setup(t => t.GetByIdWithDetailsAsync(torneo.Id))
                .ReturnsAsync(torneo);
        }

        if (ranking is not null)
        {
            torneoServiceMock
                .Setup(t => t.BuildRankingAsync(It.IsAny<Torneo>()))
                .ReturnsAsync(ranking);
        }

        broadcasterMock
            .Setup(b => b.BroadcastRankingAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RankingParticipante>>()))
            .Returns(Task.CompletedTask);

        chatBanterMock
            .Setup(s => s.OnScoreUpdatedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Wire up service scope factory
        var services = new ServiceCollection();
        services.AddSingleton(partidoRepoMock.Object);
        services.AddSingleton(partidoServiceMock.Object);
        services.AddSingleton(apiFootballMock.Object);
        services.AddSingleton(jornadaServiceMock.Object);
        services.AddSingleton(torneoServiceMock.Object);
        services.AddSingleton(broadcasterMock.Object);
        services.AddSingleton(chatBanterMock.Object);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var sut = new ResultSyncService(scopeFactory, NullLogger<ResultSyncService>.Instance);

        return (sut, partidoRepoMock, partidoServiceMock, apiFootballMock,
            jornadaServiceMock, torneoServiceMock, broadcasterMock);
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncLiveMatchResults_WhenScoreChanges_BroadcastsRankingWithCorrectTorneoId()
    {
        // Arrange
        var partido = BuildPartido(id: 1, jornadaId: 10, externalId: "999", goles1: 0, goles2: 0);
        var liveScore = BuildLiveScore(999, goles1: 1, goles2: 0); // score changed
        var jornada = BuildJornada(id: 10, torneoId: 5);
        var torneo = BuildTorneo(id: 5);
        var ranking = BuildRanking();

        var (sut, _, _, _, jornadaServiceMock, torneoServiceMock, broadcasterMock) =
            BuildSut(
                activeMatches: new List<Partido> { partido },
                liveScore: liveScore,
                jornada: jornada,
                torneo: torneo,
                ranking: ranking);

        // Act — trigger sync via internal method by using IHostedService start + tick
        // We invoke the sync directly by calling StartAsync and letting the service execute once.
        // Since we can't tick the PeriodicTimer externally, we use the internal sync via reflection.
        await InvokeSyncAsync(sut);

        // Assert
        broadcasterMock.Verify(
            b => b.BroadcastRankingAsync(5, ranking),
            Times.Once);
    }

    [Fact]
    public async Task SyncLiveMatchResults_WhenScoresUnchanged_DoesNotBroadcast()
    {
        // Arrange — partido already has goles 1-0, liveScore returns same 1-0, same state
        var partido = BuildPartido(id: 1, jornadaId: 10, externalId: "999", goles1: 1, goles2: 0,
            estado: EstadoPartido.EnCurso);
        var liveScore = BuildLiveScore(999, goles1: 1, goles2: 0, estado: EstadoPartido.EnCurso);

        var (sut, _, _, _, _, _, broadcasterMock) =
            BuildSut(
                activeMatches: new List<Partido> { partido },
                liveScore: liveScore);

        // Act
        await InvokeSyncAsync(sut);

        // Assert — no broadcast when scores are the same
        broadcasterMock.Verify(
            b => b.BroadcastRankingAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RankingParticipante>>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncLiveMatchResults_WhenBroadcastThrows_RemainingMatchesStillProcessed()
    {
        // Arrange — two matches, broadcaster throws on first, second should still be processed
        var partido1 = BuildPartido(id: 1, jornadaId: 10, externalId: "100", goles1: 0, goles2: 0);
        var partido2 = BuildPartido(id: 2, jornadaId: 20, externalId: "200", goles1: 0, goles2: 0);
        var liveScore1 = BuildLiveScore(100, goles1: 1, goles2: 0); // score changed
        var liveScore2 = BuildLiveScore(200, goles1: 2, goles2: 1); // score changed

        var jornada1 = BuildJornada(id: 10, torneoId: 5);
        var jornada2 = BuildJornada(id: 20, torneoId: 7);
        var torneo1 = BuildTorneo(id: 5);
        var torneo2 = BuildTorneo(id: 7);
        var ranking = BuildRanking();

        var partidoRepoMock = new Mock<IPartidoRepository>();
        var partidoServiceMock = new Mock<IPartidoService>();
        var apiFootballMock = new Mock<IApiFootballSyncService>();
        var jornadaServiceMock = new Mock<IJornadaService>();
        var torneoServiceMock = new Mock<ITorneoService>();
        var broadcasterMock = new Mock<IRankingBroadcaster>();

        partidoRepoMock
            .Setup(r => r.GetByEstadoAsync(EstadoPartido.EnCurso))
            .ReturnsAsync(new List<Partido> { partido1, partido2 });

        apiFootballMock
            .Setup(a => a.GetLiveScoreAsync(100))
            .ReturnsAsync(liveScore1);
        apiFootballMock
            .Setup(a => a.GetLiveScoreAsync(200))
            .ReturnsAsync(liveScore2);

        jornadaServiceMock.Setup(j => j.GetDetalleAsync(10)).ReturnsAsync(jornada1);
        jornadaServiceMock.Setup(j => j.GetDetalleAsync(20)).ReturnsAsync(jornada2);

        torneoServiceMock.Setup(t => t.GetByIdWithDetailsAsync(5)).ReturnsAsync(torneo1);
        torneoServiceMock.Setup(t => t.GetByIdWithDetailsAsync(7)).ReturnsAsync(torneo2);

        torneoServiceMock
            .Setup(t => t.BuildRankingAsync(It.IsAny<Torneo>()))
            .ReturnsAsync(ranking);

        // First broadcast throws, second should succeed
        broadcasterMock
            .SetupSequence(b => b.BroadcastRankingAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RankingParticipante>>()))
            .ThrowsAsync(new InvalidOperationException("SignalR failure"))
            .Returns(Task.CompletedTask);

        var chatBanterMock1 = new Mock<IChatBanterService>();
        chatBanterMock1
            .Setup(s => s.OnScoreUpdatedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(partidoRepoMock.Object);
        services.AddSingleton(partidoServiceMock.Object);
        services.AddSingleton(apiFootballMock.Object);
        services.AddSingleton(jornadaServiceMock.Object);
        services.AddSingleton(torneoServiceMock.Object);
        services.AddSingleton(broadcasterMock.Object);
        services.AddSingleton(chatBanterMock1.Object);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var sut = new ResultSyncService(scopeFactory, NullLogger<ResultSyncService>.Instance);

        // Act — must not throw
        var act = async () => await InvokeSyncAsync(sut);
        await act.Should().NotThrowAsync();

        // Assert — second match was processed (broadcast called twice, once throwing)
        broadcasterMock.Verify(
            b => b.BroadcastRankingAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RankingParticipante>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SyncLiveMatchResults_WhenGetDetalleAsyncThrows_ErrorLoggedAndSyncContinues()
    {
        // Arrange — two matches, GetDetalleAsync throws on first jornada
        var partido1 = BuildPartido(id: 1, jornadaId: 10, externalId: "100", goles1: 0, goles2: 0);
        var partido2 = BuildPartido(id: 2, jornadaId: 20, externalId: "200", goles1: 0, goles2: 0);
        var liveScore1 = BuildLiveScore(100, goles1: 1, goles2: 0);
        var liveScore2 = BuildLiveScore(200, goles1: 2, goles2: 1);

        var jornada2 = BuildJornada(id: 20, torneoId: 7);
        var torneo2 = BuildTorneo(id: 7);
        var ranking = BuildRanking();

        var partidoRepoMock = new Mock<IPartidoRepository>();
        var partidoServiceMock = new Mock<IPartidoService>();
        var apiFootballMock = new Mock<IApiFootballSyncService>();
        var jornadaServiceMock = new Mock<IJornadaService>();
        var torneoServiceMock = new Mock<ITorneoService>();
        var broadcasterMock = new Mock<IRankingBroadcaster>();

        partidoRepoMock
            .Setup(r => r.GetByEstadoAsync(EstadoPartido.EnCurso))
            .ReturnsAsync(new List<Partido> { partido1, partido2 });

        apiFootballMock.Setup(a => a.GetLiveScoreAsync(100)).ReturnsAsync(liveScore1);
        apiFootballMock.Setup(a => a.GetLiveScoreAsync(200)).ReturnsAsync(liveScore2);

        // First jornada throws
        jornadaServiceMock
            .Setup(j => j.GetDetalleAsync(10))
            .ThrowsAsync(new InvalidOperationException("DB error"));
        jornadaServiceMock.Setup(j => j.GetDetalleAsync(20)).ReturnsAsync(jornada2);

        torneoServiceMock.Setup(t => t.GetByIdWithDetailsAsync(7)).ReturnsAsync(torneo2);
        torneoServiceMock.Setup(t => t.BuildRankingAsync(It.IsAny<Torneo>())).ReturnsAsync(ranking);

        broadcasterMock
            .Setup(b => b.BroadcastRankingAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<RankingParticipante>>()))
            .Returns(Task.CompletedTask);

        var chatBanterMock2 = new Mock<IChatBanterService>();
        chatBanterMock2
            .Setup(s => s.OnScoreUpdatedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(partidoRepoMock.Object);
        services.AddSingleton(partidoServiceMock.Object);
        services.AddSingleton(apiFootballMock.Object);
        services.AddSingleton(jornadaServiceMock.Object);
        services.AddSingleton(torneoServiceMock.Object);
        services.AddSingleton(broadcasterMock.Object);
        services.AddSingleton(chatBanterMock2.Object);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var sut = new ResultSyncService(scopeFactory, NullLogger<ResultSyncService>.Instance);

        // Act — must not throw even with GetDetalleAsync failing
        var act = async () => await InvokeSyncAsync(sut);
        await act.Should().NotThrowAsync();

        // Assert — second match still had its ranking broadcast
        broadcasterMock.Verify(
            b => b.BroadcastRankingAsync(7, ranking),
            Times.Once);
    }

    // ─── Test Infrastructure ──────────────────────────────────────────────────

    /// <summary>
    /// Invokes the private SyncLiveMatchResultsAsync via reflection
    /// to test without needing to tick a PeriodicTimer.
    /// </summary>
    private static async Task InvokeSyncAsync(ResultSyncService sut)
    {
        var method = typeof(ResultSyncService)
            .GetMethod("SyncLiveMatchResultsAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull("SyncLiveMatchResultsAsync must exist on ResultSyncService");

        var task = (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;
        await task;
    }
}
