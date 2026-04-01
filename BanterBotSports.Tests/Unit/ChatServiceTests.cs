using BanterBotSports.BL.Services;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for ChatService deadline visibility, message truncation,
/// non-participant rejection, and BanterBot save.
/// </summary>
public class ChatServiceTests
{
    private const int TorneoId = 1;
    private const string UserId = "user-abc";

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static Participante BuildParticipante(string userId = UserId, int torneoId = TorneoId)
        => new() { Id = 1, UserId = userId, TorneoId = torneoId };

    private static Jornada BuildJornada(EstadoJornada estado, int torneoId = TorneoId)
        => new() { Id = 10, TorneoId = torneoId, Numero = 1, Estado = estado };

    private static MensajeChat BuildMensaje(string? userId, TipoMensajeChat tipo = TipoMensajeChat.Normal)
        => new()
        {
            Id = 1,
            TorneoId = TorneoId,
            UserId = userId,
            Contenido = "hola",
            FechaUtc = DateTimeOffset.UtcNow,
            TipoMensaje = tipo,
            NombreDisplay = userId is null ? "BanterBot" : "Player One"
        };

    private static (ChatService sut,
        Mock<IChatRepository> chatRepoMock,
        Mock<IJornadaRepository> jornadaRepoMock,
        Mock<IParticipanteRepository> participanteRepoMock,
        Mock<IUnitOfWork> uowMock)
        BuildSut(
            Participante? participante = null,
            Jornada? jornadaAbierta = null,
            IReadOnlyList<MensajeChat>? messages = null)
    {
        var chatRepoMock = new Mock<IChatRepository>();
        var jornadaRepoMock = new Mock<IJornadaRepository>();
        var participanteRepoMock = new Mock<IParticipanteRepository>();
        var uowMock = new Mock<IUnitOfWork>();

        participanteRepoMock
            .Setup(r => r.GetByTorneoAndUserAsync(TorneoId, UserId))
            .ReturnsAsync(participante);

        participanteRepoMock
            .Setup(r => r.GetDisplayNamesByIdsAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new Dictionary<string, string> { [UserId] = "Player One" });

        jornadaRepoMock
            .Setup(r => r.GetByTorneoAndEstadoAsync(TorneoId, EstadoJornada.Abierta))
            .ReturnsAsync(jornadaAbierta);

        chatRepoMock
            .Setup(r => r.GetByTorneoAsync(TorneoId, It.IsAny<int>(), It.IsAny<long?>()))
            .ReturnsAsync(messages ?? new List<MensajeChat>());

        chatRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MensajeChat>()))
            .ReturnsAsync((MensajeChat m) => m);

        uowMock
            .Setup(u => u.SaveAsync(default))
            .Returns(Task.CompletedTask);

        var sut = new ChatService(
            chatRepoMock.Object,
            jornadaRepoMock.Object,
            participanteRepoMock.Object,
            uowMock.Object);

        return (sut, chatRepoMock, jornadaRepoMock, participanteRepoMock, uowMock);
    }

    // ─── GetHistoryAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistoryAsync_BeforeDeadline_ReturnsOnlyOwnMessagesAndBanterBot()
    {
        // Arrange — jornada is open → deadline not passed
        var participante = BuildParticipante();
        var jornadaAbierta = BuildJornada(EstadoJornada.Abierta);

        var messages = new List<MensajeChat>
        {
            BuildMensaje(userId: UserId, tipo: TipoMensajeChat.Normal),             // own
            BuildMensaje(userId: "other-user", tipo: TipoMensajeChat.Normal),       // other player
            BuildMensaje(userId: null, tipo: TipoMensajeChat.AnuncioJornada),        // BanterBot
        };

        var (sut, chatRepoMock, _, _, _) = BuildSut(participante, jornadaAbierta, messages);

        // Act
        var result = await sut.GetHistoryAsync(TorneoId, UserId, limit: 50);

        // Assert — other player's message filtered out
        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.UserId == UserId || m.UserId == null);
    }

    [Fact]
    public async Task GetHistoryAsync_AfterDeadline_ReturnsAllMessages()
    {
        // Arrange — jornada is Cerrada → deadline passed (no Abierta jornada found)
        var participante = BuildParticipante();

        var messages = new List<MensajeChat>
        {
            BuildMensaje(userId: UserId),
            BuildMensaje(userId: "other-user"),
            BuildMensaje(userId: null, tipo: TipoMensajeChat.ResultadoBanter),
        };

        // jornadaAbierta = null → deadline is passed
        var (sut, _, _, _, _) = BuildSut(participante, jornadaAbierta: null, messages);

        // Act
        var result = await sut.GetHistoryAsync(TorneoId, UserId, limit: 50);

        // Assert — all messages visible
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetHistoryAsync_NoActiveJornada_ReturnsAllMessages()
    {
        // Arrange — null jornada → no active jornada → no deadline → see all
        var participante = BuildParticipante();

        var messages = new List<MensajeChat>
        {
            BuildMensaje(userId: UserId),
            BuildMensaje(userId: "other-user"),
        };

        var (sut, _, _, _, _) = BuildSut(participante, jornadaAbierta: null, messages);

        // Act
        var result = await sut.GetHistoryAsync(TorneoId, UserId, limit: 50);

        // Assert
        result.Should().HaveCount(2);
    }

    // ─── SaveMessageAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveMessageAsync_TruncatesContenidoTo500Chars()
    {
        // Arrange
        var participante = BuildParticipante();
        var longContent = new string('x', 600);

        var (sut, chatRepoMock, _, _, uowMock) = BuildSut(participante);

        MensajeChat? saved = null;
        chatRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MensajeChat>()))
            .Callback<MensajeChat>(m => saved = m)
            .ReturnsAsync((MensajeChat m) => m);

        // Act
        await sut.SaveMessageAsync(TorneoId, UserId, longContent);

        // Assert
        saved.Should().NotBeNull();
        saved!.Contenido.Length.Should().BeLessThanOrEqualTo(500);
    }

    [Fact]
    public async Task SaveMessageAsync_NonParticipant_ThrowsUnauthorizedAccessException()
    {
        // Arrange — participante is null → not in torneo
        var (sut, _, _, participanteRepoMock, _) = BuildSut(participante: null);

        // Act
        var act = async () => await sut.SaveMessageAsync(TorneoId, UserId, "hola");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SaveMessageAsync_ValidPlayer_SavesAndReturnsMessage()
    {
        // Arrange
        var participante = BuildParticipante();
        var (sut, chatRepoMock, _, _, uowMock) = BuildSut(participante);

        // Act
        var result = await sut.SaveMessageAsync(TorneoId, UserId, "¡Dale River!");

        // Assert
        result.Should().NotBeNull();
        result.Contenido.Should().Be("¡Dale River!");
        result.UserId.Should().Be(UserId);
        result.TipoMensaje.Should().Be(TipoMensajeChat.Normal);
        chatRepoMock.Verify(r => r.AddAsync(It.IsAny<MensajeChat>()), Times.Once);
        uowMock.Verify(u => u.SaveAsync(default), Times.Once);
    }

    // ─── SaveBanterBotMessageAsync ────────────────────────────────────────────

    [Fact]
    public async Task SaveBanterBotMessageAsync_TruncatesContenidoTo280Chars()
    {
        // Arrange
        var longBanter = new string('x', 400);
        var (sut, chatRepoMock, _, _, _) = BuildSut();

        MensajeChat? saved = null;
        chatRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MensajeChat>()))
            .Callback<MensajeChat>(m => saved = m)
            .ReturnsAsync((MensajeChat m) => m);

        // Act
        await sut.SaveBanterBotMessageAsync(TorneoId, longBanter, TipoMensajeChat.ResultadoBanter);

        // Assert
        saved.Should().NotBeNull();
        saved!.Contenido.Length.Should().BeLessThanOrEqualTo(280);
    }

    [Fact]
    public async Task SaveBanterBotMessageAsync_SavesWithNullUserIdAndBanterBotName()
    {
        // Arrange
        var (sut, chatRepoMock, _, _, uowMock) = BuildSut();

        MensajeChat? saved = null;
        chatRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MensajeChat>()))
            .Callback<MensajeChat>(m => saved = m)
            .ReturnsAsync((MensajeChat m) => m);

        // Act
        await sut.SaveBanterBotMessageAsync(TorneoId, "¡Qué golazo!", TipoMensajeChat.ResultadoBanter);

        // Assert
        saved.Should().NotBeNull();
        saved!.UserId.Should().BeNull();
        saved.NombreDisplay.Should().Be("BanterBot");
        saved.TipoMensaje.Should().Be(TipoMensajeChat.ResultadoBanter);
        uowMock.Verify(u => u.SaveAsync(default), Times.Once);
    }
}
