using BanterBotSports.BanterAI;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Web.Hubs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for ChatHub:
/// - SendMessage persists + broadcasts
/// - SendMessage with @banterbot triggers reply
/// - Non-participant sends are silently ignored
/// </summary>
public class ChatHubTests
{
    private const int TorneoId = 1;
    private const string UserId = "user-abc";
    private const string PlayerName = "Player One";

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static MensajeChat BuildMensaje(string? userId = UserId, TipoMensajeChat tipo = TipoMensajeChat.Normal)
        => new()
        {
            Id = 1,
            TorneoId = TorneoId,
            UserId = userId,
            Contenido = "hola",
            FechaUtc = DateTimeOffset.UtcNow,
            TipoMensaje = tipo,
            NombreDisplay = userId is null ? "BanterBot" : PlayerName
        };

    private static Torneo BuildTorneo()
        => new()
        {
            Id = TorneoId,
            Nombre = "Test Torneo",
            OrganizadorId = "org",
            Estado = EstadoTorneo.Activo,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2
        };

    /// <summary>
    /// Creates a ChatHub wired with mocked dependencies and a fake HubCallerContext
    /// where Context.UserIdentifier returns the given userId.
    /// </summary>
    private static (ChatHub hub,
        Mock<IChatService> chatServiceMock,
        Mock<IChatBroadcaster> broadcasterMock,
        Mock<IBanterEngine> banterEngineMock)
        BuildHub(
            Participante? participante = null,
            MensajeChat? savedMessage = null,
            string? contextUserId = UserId)
    {
        var chatServiceMock = new Mock<IChatService>();
        var broadcasterMock = new Mock<IChatBroadcaster>();
        var banterEngineMock = new Mock<IBanterEngine>();
        var participanteRepoMock = new Mock<IParticipanteRepository>();
        var torneoRepoMock = new Mock<ITorneoRepository>();

        participanteRepoMock
            .Setup(r => r.GetByTorneoAndUserAsync(TorneoId, It.IsAny<string>()))
            .ReturnsAsync(participante);

        torneoRepoMock
            .Setup(r => r.GetByIdAsync(TorneoId))
            .ReturnsAsync(BuildTorneo());

        chatServiceMock
            .Setup(s => s.SaveMessageAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(savedMessage ?? BuildMensaje());

        chatServiceMock
            .Setup(s => s.SaveBanterBotMessageAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TipoMensajeChat>()))
            .ReturnsAsync(BuildMensaje(userId: null, tipo: TipoMensajeChat.RespuestaMencion));

        broadcasterMock
            .Setup(b => b.BroadcastMessageAsync(It.IsAny<int>(), It.IsAny<MensajeChat>()))
            .Returns(Task.CompletedTask);

        banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ReturnsAsync("¡Buena pregunta, loco!");

        var hub = new ChatHub(
            chatServiceMock.Object,
            broadcasterMock.Object,
            banterEngineMock.Object,
            participanteRepoMock.Object,
            torneoRepoMock.Object,
            NullLogger<ChatHub>.Instance);

        // Set up hub context with fake user identifier
        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.UserIdentifier).Returns(contextUserId);

        hub.Context = contextMock.Object;

        var groupsMock = new Mock<IGroupManager>();
        groupsMock
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        hub.Groups = groupsMock.Object;

        return (hub, chatServiceMock, broadcasterMock, banterEngineMock);
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_PersistsAndBroadcastsMessage()
    {
        // Arrange
        var mensaje = BuildMensaje();
        var (hub, chatServiceMock, broadcasterMock, _) = BuildHub(savedMessage: mensaje);

        // Act
        await hub.SendMessage(TorneoId, "¡Dale River!");

        // Assert
        chatServiceMock.Verify(
            s => s.SaveMessageAsync(TorneoId, UserId, "¡Dale River!"),
            Times.Once);

        broadcasterMock.Verify(
            b => b.BroadcastMessageAsync(TorneoId, mensaje),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_WhenNonParticipant_DoesNotBroadcast()
    {
        // Arrange — SaveMessageAsync throws UnauthorizedAccessException (non-participant)
        var chatServiceMock = new Mock<IChatService>();
        var broadcasterMock = new Mock<IChatBroadcaster>();
        var banterEngineMock = new Mock<IBanterEngine>();
        var participanteRepoMock = new Mock<IParticipanteRepository>();
        var torneoRepoMock = new Mock<ITorneoRepository>();

        chatServiceMock
            .Setup(s => s.SaveMessageAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException("Not a participant"));

        var hub = new ChatHub(
            chatServiceMock.Object,
            broadcasterMock.Object,
            banterEngineMock.Object,
            participanteRepoMock.Object,
            torneoRepoMock.Object,
            NullLogger<ChatHub>.Instance);

        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.UserIdentifier).Returns(UserId);
        hub.Context = contextMock.Object;
        hub.Groups = new Mock<IGroupManager>().Object;

        // Act — must not throw
        var act = async () => await hub.SendMessage(TorneoId, "hola");
        await act.Should().NotThrowAsync();

        // Assert — nothing broadcast
        broadcasterMock.Verify(
            b => b.BroadcastMessageAsync(It.IsAny<int>(), It.IsAny<MensajeChat>()),
            Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithAtBanterBot_GeneratesAndBroadcastsReply()
    {
        // Arrange
        var playerMessage = "@banterbot ¿quién va a ganar?";
        var playerMensaje = BuildMensaje();
        var (hub, _, broadcasterMock, banterEngineMock) = BuildHub(savedMessage: playerMensaje);

        // Act
        await hub.SendMessage(TorneoId, playerMessage);

        // Assert — BanterBot reply generated
        banterEngineMock.Verify(
            e => e.GenerateChatReplyAsync(playerMessage, PlayerName, It.IsAny<Torneo>()),
            Times.Once);

        // Broadcast called twice: player message + bot reply
        broadcasterMock.Verify(
            b => b.BroadcastMessageAsync(TorneoId, It.IsAny<MensajeChat>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SendMessage_WithoutAtBanterBot_DoesNotGenerateBanterReply()
    {
        // Arrange
        var (hub, _, _, banterEngineMock) = BuildHub();

        // Act
        await hub.SendMessage(TorneoId, "hola, ¿cómo van?");

        // Assert — no BanterBot reply
        banterEngineMock.Verify(
            e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()),
            Times.Never);
    }
}
