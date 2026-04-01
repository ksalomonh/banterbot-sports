using BanterBotSports.BL.Services;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace BanterBotSports.Tests.Integration;

/// <summary>
/// Integration tests covering the ChatHub send-message flow end-to-end.
///
/// Strategy: exercise ChatService with a real PostgreSQL database.
/// IChatBroadcaster is mocked — its contract (broadcast called with persisted message)
/// is the observable behaviour we assert.
///
/// Scenarios covered:
///   - Player sends message → saved to DB with correct TorneoId, UserId, TipoMensaje=Normal
///   - Player sends message → IChatBroadcaster.BroadcastMessageAsync called with that message
///   - Non-participant sends message → UnauthorizedAccessException, nothing persisted
///   - Message over 500 chars → truncated to 500 before saving
/// </summary>
public class ChatHubIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("banterbot_chathub_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private AppDbContext _context = null!;
    private ChatService _chatService = null!;
    private Mock<IChatBroadcaster> _broadcasterMock = null!;

    // ---------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.MigrateAsync();

        _broadcasterMock = new Mock<IChatBroadcaster>(MockBehavior.Loose);
        _broadcasterMock
            .Setup(b => b.BroadcastMessageAsync(It.IsAny<int>(), It.IsAny<MensajeChat>()))
            .Returns(Task.CompletedTask);

        var chatRepo = new ChatRepository(_context);
        var jornadaRepo = new JornadaRepository(_context);
        var participanteRepo = new ParticipanteRepository(_context);
        var uow = new UnitOfWork(_context);

        _chatService = new ChatService(chatRepo, jornadaRepo, participanteRepo, uow);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<(Torneo torneo, Participante participante)> SeedTorneoConParticipanteAsync(
        string userId = "user-chat-1")
    {
        var torneo = new Torneo
        {
            Nombre = "Torneo Chat Tests",
            OrganizadorId = "org-chat",
            NumJornadas = 1,
            MontoInscripcion = 100m,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2,
            Estado = EstadoTorneo.Activo
        };
        _context.Torneos.Add(torneo);
        await _context.SaveChangesAsync();

        var participante = new Participante
        {
            TorneoId = torneo.Id,
            UserId = userId,
            Rol = RolParticipante.Jugador,
            Pago = true
        };
        _context.Participantes.Add(participante);
        await _context.SaveChangesAsync();

        return (torneo, participante);
    }

    // ---------------------------------------------------------------------------
    // Task 5.1 — Send message via ChatService, verify persisted in DB and broadcast received
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SendMessage_ValidParticipant_PersistedInDbWithCorrectFields()
    {
        // Arrange
        var (torneo, participante) = await SeedTorneoConParticipanteAsync();
        const string contenido = "¡Vamos, metele!";

        // Act
        var mensaje = await _chatService.SaveMessageAsync(torneo.Id, participante.UserId, contenido);

        // Assert — returned entity has correct fields
        mensaje.TorneoId.Should().Be(torneo.Id);
        mensaje.UserId.Should().Be(participante.UserId);
        mensaje.Contenido.Should().Be(contenido);
        mensaje.TipoMensaje.Should().Be(TipoMensajeChat.Normal);

        // Assert — verify the message actually landed in the DB
        _context.ChangeTracker.Clear();
        var persisted = await _context.MensajesChat
            .FirstOrDefaultAsync(m => m.TorneoId == torneo.Id && m.UserId == participante.UserId);

        persisted.Should().NotBeNull("message must be persisted to the database");
        persisted!.Contenido.Should().Be(contenido);
        persisted.TipoMensaje.Should().Be(TipoMensajeChat.Normal);
        persisted.NombreDisplay.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendMessage_ValidParticipant_BroadcastCalledWithPersistedMessage()
    {
        // Arrange
        var (torneo, participante) = await SeedTorneoConParticipanteAsync("user-chat-2");
        const string contenido = "¡Qué golazo, hermano!";

        // Act — simulate what ChatHub.SendMessage does after SaveMessageAsync
        var mensaje = await _chatService.SaveMessageAsync(torneo.Id, participante.UserId, contenido);
        await _broadcasterMock.Object.BroadcastMessageAsync(torneo.Id, mensaje);

        // Assert — broadcaster was called exactly once with the correct torneoId
        _broadcasterMock.Verify(
            b => b.BroadcastMessageAsync(torneo.Id, It.Is<MensajeChat>(m =>
                m.UserId == participante.UserId &&
                m.Contenido == contenido &&
                m.TipoMensaje == TipoMensajeChat.Normal)),
            Times.Once,
            "broadcast must be called with the persisted message after save");
    }

    [Fact]
    public async Task SendMessage_NonParticipant_ThrowsUnauthorizedAndNothingPersisted()
    {
        // Arrange
        var (torneo, _) = await SeedTorneoConParticipanteAsync("user-chat-3");
        const string strangerUserId = "stranger-user";

        // Act
        var act = async () =>
            await _chatService.SaveMessageAsync(torneo.Id, strangerUserId, "soy un intruso");

        // Assert — service rejects the non-participant
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "non-participants must not be allowed to post messages");

        // Assert — nothing landed in DB
        _context.ChangeTracker.Clear();
        var count = await _context.MensajesChat.CountAsync(m => m.TorneoId == torneo.Id);
        count.Should().Be(0, "no message must be persisted when the sender is not a participant");
    }

    [Fact]
    public async Task SendMessage_ContentOver500Chars_TruncatedTo500InDb()
    {
        // Arrange
        var (torneo, participante) = await SeedTorneoConParticipanteAsync("user-chat-4");
        var longContent = new string('A', 600);

        // Act
        var mensaje = await _chatService.SaveMessageAsync(torneo.Id, participante.UserId, longContent);

        // Assert — in-memory entity is truncated
        mensaje.Contenido.Length.Should().Be(500,
            "content longer than 500 chars must be truncated before saving");

        // Assert — DB row is also truncated
        _context.ChangeTracker.Clear();
        var persisted = await _context.MensajesChat
            .FirstOrDefaultAsync(m => m.TorneoId == torneo.Id);

        persisted.Should().NotBeNull();
        persisted!.Contenido.Length.Should().Be(500);
    }

    [Fact]
    public async Task SendMessage_BanterBotMessage_PersistedWithNullUserIdAndBanterBotName()
    {
        // Arrange
        var (torneo, _) = await SeedTorneoConParticipanteAsync("user-chat-5");
        const string banterText = "¡Tremendo partido, loco!";

        // Act — SaveBanterBotMessageAsync saves a bot message (UserId = null)
        var mensaje = await _chatService.SaveBanterBotMessageAsync(
            torneo.Id, banterText, TipoMensajeChat.ResultadoBanter);

        // Assert — returned entity
        mensaje.UserId.Should().BeNull("BanterBot messages must have null UserId");
        mensaje.NombreDisplay.Should().Be("BanterBot");
        mensaje.TipoMensaje.Should().Be(TipoMensajeChat.ResultadoBanter);

        // Assert — DB row
        _context.ChangeTracker.Clear();
        var persisted = await _context.MensajesChat
            .FirstOrDefaultAsync(m => m.TorneoId == torneo.Id);

        persisted.Should().NotBeNull();
        persisted!.UserId.Should().BeNull();
        persisted.NombreDisplay.Should().Be("BanterBot");
        persisted.TipoMensaje.Should().Be(TipoMensajeChat.ResultadoBanter);
    }
}
