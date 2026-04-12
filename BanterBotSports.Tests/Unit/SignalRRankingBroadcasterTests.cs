using BanterBotSports.BL.Models;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Web.Hubs;
using BanterBotSports.Web.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for SignalRRankingBroadcaster.
/// Verifies that it delegates to IHubContext&lt;TorneoHub&gt; sending "RankingActualizado".
/// </summary>
public class SignalRRankingBroadcasterTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<RankingParticipante> BuildRanking(int count = 2)
    {
        return Enumerable.Range(1, count)
            .Select(i => new RankingParticipante(
                ParticipanteId: i,
                NombreDisplay: $"Player{i}",
                PuntosTotal: (count - i + 1) * 3,
                Posicion: i))
            .ToList();
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BroadcastRankingAsync_SendsRankingActualizadoToTorneoGroup()
    {
        // Arrange
        var torneoId = 42;
        var ranking = BuildRanking();

        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        var hubContextMock = new Mock<IHubContext<TorneoHub>>();

        clientsMock
            .Setup(c => c.Group($"torneo-{torneoId}"))
            .Returns(clientProxyMock.Object);

        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        clientProxyMock
            .Setup(p => p.SendCoreAsync(
                "RankingActualizado",
                It.Is<object?[]>(args => args.Length == 1 && args[0] == ranking),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new SignalRRankingBroadcaster(hubContextMock.Object);

        // Act
        await sut.BroadcastRankingAsync(torneoId, ranking);

        // Assert
        clientProxyMock.Verify(
            p => p.SendCoreAsync(
                "RankingActualizado",
                It.Is<object?[]>(args => args.Length == 1 && args[0] == ranking),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
