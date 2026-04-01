using BanterBotSports.BanterAI;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for ChatBanterService:
/// - Generates banter and saves to chat on score update.
/// - AI failure does not throw — logs and silently skips.
/// - No torneo found → skips gracefully.
/// </summary>
public class ChatBanterServiceTests
{
    private const int TorneoId = 1;
    private const int PartidoId = 42;

    private static Torneo BuildTorneo(int id = TorneoId)
        => new()
        {
            Id = id,
            Nombre = "Test Torneo",
            OrganizadorId = "org",
            Estado = EstadoTorneo.Activo,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2
        };

    private static (ChatBanterService sut,
        Mock<IBanterEngine> banterEngineMock,
        Mock<IChatService> chatServiceMock,
        Mock<ITorneoRepository> torneoRepoMock,
        Mock<IChatBroadcaster> broadcasterMock)
        BuildSut(Torneo? torneo = null, string? banterReply = "¡Golazo, loco!")
    {
        var banterEngineMock = new Mock<IBanterEngine>();
        var chatServiceMock = new Mock<IChatService>();
        var torneoRepoMock = new Mock<ITorneoRepository>();
        var broadcasterMock = new Mock<IChatBroadcaster>();

        torneoRepoMock
            .Setup(r => r.GetByIdAsync(TorneoId))
            .ReturnsAsync(torneo);

        if (banterReply is not null)
        {
            banterEngineMock
                .Setup(e => e.GenerateChatReplyAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
                .ReturnsAsync(banterReply);
        }

        chatServiceMock
            .Setup(s => s.SaveBanterBotMessageAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TipoMensajeChat>()))
            .ReturnsAsync(new MensajeChat());

        var sut = new ChatBanterService(
            banterEngineMock.Object,
            chatServiceMock.Object,
            torneoRepoMock.Object,
            broadcasterMock.Object,
            NullLogger<ChatBanterService>.Instance);

        return (sut, banterEngineMock, chatServiceMock, torneoRepoMock, broadcasterMock);
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_WhenTorneoExists_GeneratesBanterAndSavesToChat()
    {
        // Arrange
        var torneo = BuildTorneo();
        var (sut, banterEngineMock, chatServiceMock, _, _) = BuildSut(torneo);

        banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), torneo))
            .ReturnsAsync("¡Golazo de River, loco!");

        // Act
        await sut.OnScoreUpdatedAsync(TorneoId, PartidoId, 1, 0, "River", "Boca");

        // Assert
        chatServiceMock.Verify(
            s => s.SaveBanterBotMessageAsync(TorneoId, It.IsAny<string>(), TipoMensajeChat.ResultadoBanter),
            Times.Once);
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_WhenTorneoNotFound_SkipsGracefully()
    {
        // Arrange — torneo = null
        var (sut, _, chatServiceMock, _, _) = BuildSut(torneo: null);

        // Act — must not throw
        var act = async () => await sut.OnScoreUpdatedAsync(TorneoId, PartidoId, 1, 0, "River", "Boca");
        await act.Should().NotThrowAsync();

        // Assert — no message saved
        chatServiceMock.Verify(
            s => s.SaveBanterBotMessageAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TipoMensajeChat>()),
            Times.Never);
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_WhenAIFails_DoesNotThrowAndSkipsMessage()
    {
        // Arrange
        var torneo = BuildTorneo();
        var banterEngineMock = new Mock<IBanterEngine>();
        var chatServiceMock = new Mock<IChatService>();
        var torneoRepoMock = new Mock<ITorneoRepository>();
        var broadcasterMock = new Mock<IChatBroadcaster>();

        torneoRepoMock.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);

        // AI fails
        banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var sut = new ChatBanterService(
            banterEngineMock.Object,
            chatServiceMock.Object,
            torneoRepoMock.Object,
            broadcasterMock.Object,
            NullLogger<ChatBanterService>.Instance);

        // Act — must not throw
        var act = async () => await sut.OnScoreUpdatedAsync(TorneoId, PartidoId, 1, 0, "River", "Boca");
        await act.Should().NotThrowAsync();

        // Assert — no message persisted on AI failure
        chatServiceMock.Verify(
            s => s.SaveBanterBotMessageAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TipoMensajeChat>()),
            Times.Never);
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_WhenAIReturnsEmpty_SkipsMessage()
    {
        // Arrange — AI returns empty string (its own fallback)
        var torneo = BuildTorneo();
        var banterEngineMock = new Mock<IBanterEngine>();
        var chatServiceMock = new Mock<IChatService>();
        var torneoRepoMock = new Mock<ITorneoRepository>();
        var broadcasterMock = new Mock<IChatBroadcaster>();

        torneoRepoMock.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);

        banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ReturnsAsync(string.Empty);

        var sut = new ChatBanterService(
            banterEngineMock.Object,
            chatServiceMock.Object,
            torneoRepoMock.Object,
            broadcasterMock.Object,
            NullLogger<ChatBanterService>.Instance);

        // Act
        await sut.OnScoreUpdatedAsync(TorneoId, PartidoId, 1, 0, "River", "Boca");

        // Assert — no empty message saved
        chatServiceMock.Verify(
            s => s.SaveBanterBotMessageAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TipoMensajeChat>()),
            Times.Never);
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_ValidScore_BroadcastCalledWithSavedMessage()
    {
        // Arrange
        var torneo = BuildTorneo();
        var savedMessage = new MensajeChat { Id = 99, Contenido = "¡Golazo de River, loco!" };

        var banterEngineMock = new Mock<IBanterEngine>();
        var chatServiceMock = new Mock<IChatService>();
        var torneoRepoMock = new Mock<ITorneoRepository>();
        var broadcasterMock = new Mock<IChatBroadcaster>();

        torneoRepoMock.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);

        banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ReturnsAsync("¡Golazo de River, loco!");

        chatServiceMock
            .Setup(s => s.SaveBanterBotMessageAsync(TorneoId, It.IsAny<string>(), TipoMensajeChat.ResultadoBanter))
            .ReturnsAsync(savedMessage);

        var sut = new ChatBanterService(
            banterEngineMock.Object,
            chatServiceMock.Object,
            torneoRepoMock.Object,
            broadcasterMock.Object,
            NullLogger<ChatBanterService>.Instance);

        // Act
        await sut.OnScoreUpdatedAsync(TorneoId, PartidoId, 1, 0, "River", "Boca");

        // Assert — broadcaster called with the exact persisted message
        broadcasterMock.Verify(
            b => b.BroadcastMessageAsync(TorneoId, savedMessage),
            Times.Once);
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_ClaudeApiUnreachable_BroadcastNotCalled()
    {
        // Arrange
        var torneo = BuildTorneo();
        var banterEngineMock = new Mock<IBanterEngine>();
        var chatServiceMock = new Mock<IChatService>();
        var torneoRepoMock = new Mock<ITorneoRepository>();
        var broadcasterMock = new Mock<IChatBroadcaster>();

        torneoRepoMock.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);

        banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ThrowsAsync(new HttpRequestException("Claude API unreachable"));

        var sut = new ChatBanterService(
            banterEngineMock.Object,
            chatServiceMock.Object,
            torneoRepoMock.Object,
            broadcasterMock.Object,
            NullLogger<ChatBanterService>.Instance);

        // Act — must not throw
        var act = async () => await sut.OnScoreUpdatedAsync(TorneoId, PartidoId, 1, 0, "River", "Boca");
        await act.Should().NotThrowAsync();

        // Assert — broadcaster never called when AI fails
        broadcasterMock.Verify(
            b => b.BroadcastMessageAsync(It.IsAny<int>(), It.IsAny<MensajeChat>()),
            Times.Never);
    }
}
