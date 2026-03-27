using BanterBotSports.BanterAI;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Integrations.Telegram;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for BanterDispatchService.
/// Verifies that:
///   1. GetDisplayNamesByIdsAsync is called to resolve display names
///   2. Raw UserId GUIDs are NOT passed to the banter engine
///   3. The resolved display name IS used in the banter call
/// </summary>
public class BanterDispatchServiceTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static (BanterDispatchService sut,
        Mock<IBanterEngine> banterEngineMock,
        Mock<ITelegramBotService> telegramMock,
        Mock<ITorneoRepository> torneoRepoMock,
        Mock<IPartidoRepository> partidoRepoMock,
        Mock<IParticipanteRepository> participanteRepoMock,
        Mock<IUsuarioTelegramRepository> usuarioTelegramRepoMock)
        BuildSut()
    {
        var banterEngineMock = new Mock<IBanterEngine>();
        var telegramMock = new Mock<ITelegramBotService>();
        var torneoRepoMock = new Mock<ITorneoRepository>();
        var partidoRepoMock = new Mock<IPartidoRepository>();
        var participanteRepoMock = new Mock<IParticipanteRepository>();
        var usuarioTelegramRepoMock = new Mock<IUsuarioTelegramRepository>();

        var sut = new BanterDispatchService(
            banterEngineMock.Object,
            telegramMock.Object,
            torneoRepoMock.Object,
            partidoRepoMock.Object,
            participanteRepoMock.Object,
            usuarioTelegramRepoMock.Object,
            NullLogger<BanterDispatchService>.Instance);

        return (sut, banterEngineMock, telegramMock, torneoRepoMock, partidoRepoMock,
            participanteRepoMock, usuarioTelegramRepoMock);
    }

    private static Torneo BuildTorneoWithParticipantes(params (string userId, string? displayName)[] users)
    {
        var torneo = new Torneo
        {
            Id = 1,
            Nombre = "Test Torneo",
            OrganizadorId = users.FirstOrDefault().userId ?? "org",
            Estado = EstadoTorneo.Activo,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2
        };

        int id = 1;
        foreach (var (userId, _) in users)
        {
            torneo.Participantes.Add(new Participante
            {
                Id = id++,
                TorneoId = torneo.Id,
                UserId = userId,
                Rol = RolParticipante.Jugador,
                Torneo = torneo
            });
        }

        return torneo;
    }

    private static Jornada BuildJornada(int torneoId = 1) => new()
    {
        Id = 10,
        TorneoId = torneoId,
        Numero = 1,
        Estado = EstadoJornada.Finalizada
    };

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task OnJornadaFinalizadaAsync_CallsGetDisplayNamesByIdsAsync()
    {
        // Arrange
        var (sut, banterEngineMock, telegramMock, torneoRepoMock,
            partidoRepoMock, participanteRepoMock, usuarioTelegramRepoMock) = BuildSut();

        const string userId = "user-abc-123";
        var torneo = BuildTorneoWithParticipantes((userId, "Alice"));
        var jornada = BuildJornada(torneo.Id);

        torneoRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(torneo.Id))
            .ReturnsAsync(torneo);

        partidoRepoMock
            .Setup(r => r.GetByJornadaWithPrediccionesAsync(jornada.Id))
            .ReturnsAsync(new List<Partido>().AsReadOnly());

        participanteRepoMock
            .Setup(r => r.GetDisplayNamesByIdsAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new Dictionary<string, string> { [userId] = "Alice" });

        usuarioTelegramRepoMock
            .Setup(r => r.GetTelegramIdsByUserIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, long>());

        banterEngineMock
            .Setup(e => e.GenerateBanterAsync(It.IsAny<ParticipanteStats>(), torneo))
            .ReturnsAsync("¡Bien jugado, Alice!");

        // Act
        await sut.OnJornadaFinalizadaAsync(jornada);

        // Assert: GetDisplayNamesByIdsAsync was called with the participant's userId
        participanteRepoMock.Verify(
            r => r.GetDisplayNamesByIdsAsync(
                It.Is<IReadOnlyList<string>>(ids => ids.Contains(userId))),
            Times.Once,
            "display names must be resolved via GetDisplayNamesByIdsAsync — not from raw UserId");
    }

    [Fact]
    public async Task OnJornadaFinalizadaAsync_UsesDisplayName_NotRawUserId()
    {
        // Arrange: userId is a GUID-like string; display name is "Bob"
        var (sut, banterEngineMock, telegramMock, torneoRepoMock,
            partidoRepoMock, participanteRepoMock, usuarioTelegramRepoMock) = BuildSut();

        const string userId = "a1b2c3d4-0000-0000-0000-111111111111";
        const string displayName = "Bob";
        var torneo = BuildTorneoWithParticipantes((userId, displayName));
        var jornada = BuildJornada(torneo.Id);

        torneoRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(torneo.Id))
            .ReturnsAsync(torneo);

        partidoRepoMock
            .Setup(r => r.GetByJornadaWithPrediccionesAsync(jornada.Id))
            .ReturnsAsync(new List<Partido>().AsReadOnly());

        participanteRepoMock
            .Setup(r => r.GetDisplayNamesByIdsAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new Dictionary<string, string> { [userId] = displayName });

        usuarioTelegramRepoMock
            .Setup(r => r.GetTelegramIdsByUserIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, long>());

        ParticipanteStats? capturedStats = null;
        banterEngineMock
            .Setup(e => e.GenerateBanterAsync(It.IsAny<ParticipanteStats>(), torneo))
            .Callback<ParticipanteStats, Torneo>((stats, _) => capturedStats = stats)
            .ReturnsAsync("¡Bien jugado, Bob!");

        // Act
        await sut.OnJornadaFinalizadaAsync(jornada);

        // Assert: the banter engine received the DISPLAY NAME, not the raw userId
        capturedStats.Should().NotBeNull("GenerateBanterAsync must have been called");
        capturedStats!.NombreParticipante.Should().Be(displayName,
            "the display name resolved by GetDisplayNamesByIdsAsync must be used");
        capturedStats.NombreParticipante.Should().NotBe(userId,
            "raw UserId GUID must NEVER be passed to the banter engine");
    }

    [Fact]
    public async Task OnJornadaFinalizadaAsync_WhenTorneoNotFound_ExitsGracefully()
    {
        // Arrange
        var (sut, banterEngineMock, _, torneoRepoMock, _, participanteRepoMock, _) = BuildSut();

        torneoRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>()))
            .ReturnsAsync((Torneo?)null);

        var jornada = BuildJornada(torneoId: 999);

        // Act & Assert: must not throw — just logs a warning and returns
        var act = async () => await sut.OnJornadaFinalizadaAsync(jornada);
        await act.Should().NotThrowAsync("missing torneo must be handled gracefully");

        // No banter engine calls
        banterEngineMock.Verify(
            e => e.GenerateBanterAsync(It.IsAny<ParticipanteStats>(), It.IsAny<Torneo>()),
            Times.Never);
    }

    [Fact]
    public async Task OnJornadaFinalizadaAsync_BanterTooLong_SkipsTelegramSend()
    {
        // Arrange: banter engine returns a 300+ char string (over 280 limit)
        var (sut, banterEngineMock, telegramMock, torneoRepoMock,
            partidoRepoMock, participanteRepoMock, usuarioTelegramRepoMock) = BuildSut();

        const string userId = "user-long-banter";
        const long chatId = 55555L;
        var torneo = BuildTorneoWithParticipantes((userId, "Charlie"));
        var jornada = BuildJornada(torneo.Id);

        torneoRepoMock
            .Setup(r => r.GetByIdWithDetailsAsync(torneo.Id))
            .ReturnsAsync(torneo);

        partidoRepoMock
            .Setup(r => r.GetByJornadaWithPrediccionesAsync(jornada.Id))
            .ReturnsAsync(new List<Partido>().AsReadOnly());

        participanteRepoMock
            .Setup(r => r.GetDisplayNamesByIdsAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new Dictionary<string, string> { [userId] = "Charlie" });

        usuarioTelegramRepoMock
            .Setup(r => r.GetTelegramIdsByUserIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new Dictionary<string, long> { [userId] = chatId });

        // Return banter that exceeds 280 chars
        banterEngineMock
            .Setup(e => e.GenerateBanterAsync(It.IsAny<ParticipanteStats>(), torneo))
            .ReturnsAsync(new string('X', 300));  // 300 chars > limit

        // Act
        await sut.OnJornadaFinalizadaAsync(jornada);

        // Assert: Telegram must NOT be called because the banter was invalid
        telegramMock.Verify(
            t => t.SendMessageAsync(chatId, It.IsAny<string>()),
            Times.Never,
            "banter exceeding 280 chars must be discarded — no Telegram send");
    }
}
