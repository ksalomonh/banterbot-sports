using BanterBotSports.BanterAI;
using BanterBotSports.BL.Services;
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
/// Integration tests for the score-update → banter pipeline.
///
/// Verifies that when ResultSyncService calls ChatBanterService.OnScoreUpdatedAsync:
///   1. A ResultadoBanter message is saved to the database
///   2. The message is truncated to 280 chars (BanterBot limit)
///   3. When Claude API is unreachable (throws) → nothing is persisted, no exception propagated
///   4. When Claude returns an empty string → nothing is persisted
///
/// Uses a real PostgreSQL database (Testcontainers) for persistence assertions.
/// IBanterEngine is mocked to control AI output without real API calls.
/// </summary>
public class ChatBanterServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("banterbot_chatbanter_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private AppDbContext _context = null!;
    private ChatBanterService _chatBanterService = null!;
    private Mock<IBanterEngine> _banterEngineMock = null!;

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

        _banterEngineMock = new Mock<IBanterEngine>(MockBehavior.Loose);

        var chatRepo = new ChatRepository(_context);
        var jornadaRepo = new JornadaRepository(_context);
        var participanteRepo = new ParticipanteRepository(_context);
        var uow = new UnitOfWork(_context);
        var chatService = new ChatService(chatRepo, jornadaRepo, participanteRepo, uow);

        var torneoRepo = new TorneoRepository(_context);

        _chatBanterService = new ChatBanterService(
            _banterEngineMock.Object,
            chatService,
            torneoRepo,
            NullLogger<ChatBanterService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<Torneo> SeedTorneoAsync(string nombre = "Torneo Banter Tests")
    {
        var torneo = new Torneo
        {
            Nombre = nombre,
            OrganizadorId = "org-banter",
            NumJornadas = 1,
            MontoInscripcion = 100m,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2,
            Estado = EstadoTorneo.Activo
        };
        _context.Torneos.Add(torneo);
        await _context.SaveChangesAsync();
        return torneo;
    }

    // ---------------------------------------------------------------------------
    // Task 5.2 — Score update triggers banter message in chat
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task OnScoreUpdatedAsync_ScoreUpdate_BanterMessagePersistedInDb()
    {
        // Arrange
        var torneo = await SeedTorneoAsync();
        const string banterReply = "¡Golazo de River en el clásico!";

        _banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ReturnsAsync(banterReply);

        // Act
        await _chatBanterService.OnScoreUpdatedAsync(torneo.Id, 1, 1, 0, "River", "Boca");

        // Assert — a ResultadoBanter message must be in the DB
        _context.ChangeTracker.Clear();
        var persisted = await _context.MensajesChat
            .FirstOrDefaultAsync(m => m.TorneoId == torneo.Id);

        persisted.Should().NotBeNull("banter message must be persisted after score update");
        persisted!.TipoMensaje.Should().Be(TipoMensajeChat.ResultadoBanter,
            "score update must generate a ResultadoBanter type message");
        persisted.UserId.Should().BeNull("BanterBot messages have no UserId");
        persisted.NombreDisplay.Should().Be("BanterBot");
        persisted.Contenido.Should().Be(banterReply);
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_BanterExceeds280Chars_TruncatedInDb()
    {
        // Arrange
        var torneo = await SeedTorneoAsync("Torneo Banter Truncate");
        var longBanter = new string('B', 350); // AI can return up to 350 chars

        _banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ReturnsAsync(longBanter);

        // Act
        await _chatBanterService.OnScoreUpdatedAsync(torneo.Id, 2, 2, 1, "Boca", "Racing");

        // Assert — stored content must be at most 280 chars (ChatService enforces this for BanterBot)
        _context.ChangeTracker.Clear();
        var persisted = await _context.MensajesChat
            .FirstOrDefaultAsync(m => m.TorneoId == torneo.Id);

        persisted.Should().NotBeNull();
        persisted!.Contenido.Length.Should().BeLessThanOrEqualTo(280,
            "BanterBot messages must be truncated to 280 chars before persisting");
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_ClaudeApiUnreachable_NothingPersistedNoException()
    {
        // Arrange
        var torneo = await SeedTorneoAsync("Torneo Banter AI Fail");

        _banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ThrowsAsync(new HttpRequestException("Connection refused — Claude API unreachable"));

        // Act — must NOT throw; AI failure must be swallowed
        var act = async () =>
            await _chatBanterService.OnScoreUpdatedAsync(torneo.Id, 3, 0, 1, "Independiente", "San Lorenzo");

        await act.Should().NotThrowAsync(
            "AI failure must be logged and silently skipped, never propagated");

        // Assert — no message persisted
        _context.ChangeTracker.Clear();
        var count = await _context.MensajesChat.CountAsync(m => m.TorneoId == torneo.Id);
        count.Should().Be(0,
            "when Claude is unreachable no message must be saved to the database");
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_ClaudeReturnsEmpty_NothingPersisted()
    {
        // Arrange
        var torneo = await SeedTorneoAsync("Torneo Banter Empty Reply");

        _banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ReturnsAsync(string.Empty);

        // Act
        await _chatBanterService.OnScoreUpdatedAsync(torneo.Id, 4, 1, 1, "Atletico", "Huracan");

        // Assert — empty banter must not be persisted
        _context.ChangeTracker.Clear();
        var count = await _context.MensajesChat.CountAsync(m => m.TorneoId == torneo.Id);
        count.Should().Be(0,
            "empty AI reply must not produce a chat message");
    }

    [Fact]
    public async Task OnScoreUpdatedAsync_TorneoNotFound_NothingPersistedNoException()
    {
        // Arrange — non-existent torneoId
        const int nonExistentTorneoId = 99999;

        // Act
        var act = async () =>
            await _chatBanterService.OnScoreUpdatedAsync(nonExistentTorneoId, 5, 1, 0, "Lanus", "Velez");

        await act.Should().NotThrowAsync(
            "missing torneo must be handled gracefully");

        // Assert — BanterEngine never called
        _banterEngineMock.Verify(
            e => e.GenerateChatReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()),
            Times.Never,
            "BanterEngine must not be invoked when the torneo does not exist");
    }
}
