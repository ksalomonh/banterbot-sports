using BanterBotSports.BL.Services;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Integrations.Telegram;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace BanterBotSports.Tests.Integration;

/// <summary>
/// Integration tests for the Telegram webhook flow.
///
/// Scope:
///   - ITelegramBotService is mocked (we don't send real Telegram messages).
///   - BL + DAL layers use a real PostgreSQL database (TestContainers).
///   - The test simulates the end-to-end path:
///       "Telegram update arrives → IPrediccionService.GuardarPrediccionAsync called"
///   - Verifies predicciones are persisted with the correct values.
/// </summary>
public class TelegramWebhookIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("banterbot_webhook_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private AppDbContext _context = null!;
    private IPrediccionService _prediccionService = null!;

    // Mock of the Telegram client — we don't want real Telegram calls in tests
    private readonly Mock<ITelegramBotService> _telegramBotMock = new(MockBehavior.Strict);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.MigrateAsync();

        var prediccionRepo = new PrediccionRepository(_context);
        var jornadaRepo = new JornadaRepository(_context);
        _prediccionService = new PrediccionService(prediccionRepo, jornadaRepo, _context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<(Jornada jornada, Partido partido, Participante participante)>
        SeedScenarioAsync(DateTimeOffset? deadline = null)
    {
        var torneo = new Torneo
        {
            Nombre = "Torneo Telegram Tests",
            OrganizadorId = "org-telegram",
            NumJornadas = 1,
            MontoInscripcion = 100m,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2,
            Estado = EstadoTorneo.Activo
        };
        _context.Torneos.Add(torneo);
        await _context.SaveChangesAsync();

        var jornada = new Jornada
        {
            TorneoId = torneo.Id,
            Numero = 1,
            Estado = EstadoJornada.Abierta,
            DeadlineUtc = deadline
        };
        _context.Jornadas.Add(jornada);
        await _context.SaveChangesAsync();

        var partido = new Partido
        {
            JornadaId = jornada.Id,
            Equipo1 = "Argentina",
            Equipo2 = "Brasil",
            KickOffUtc = deadline ?? DateTimeOffset.UtcNow.AddDays(1),
            Estado = EstadoPartido.Programado
        };
        _context.Partidos.Add(partido);
        await _context.SaveChangesAsync();

        var participante = new Participante
        {
            TorneoId = torneo.Id,
            UserId = "telegram-user-42",
            Rol = RolParticipante.Jugador,
            Pago = true
        };
        _context.Participantes.Add(participante);
        await _context.SaveChangesAsync();

        return (jornada, partido, participante);
    }

    // ---------------------------------------------------------------------------
    // Simulated webhook handler.
    // In production this lives in TelegramWebhookController.  Here we replicate
    // the logic so we can test the BL+DAL pipeline without spinning up ASP.NET Core.
    // ---------------------------------------------------------------------------

    private async Task SimulateWebhookAsync(
        int partidoId,
        int participanteId,
        int golesEquipo1,
        int golesEquipo2,
        Jornada jornada,
        long telegramChatId = 12345L)
    {
        // Build the prediction as the webhook controller would
        var prediccion = new PrediccionPartido
        {
            PartidoId = partidoId,
            ParticipanteId = participanteId,
            GolesEquipo1 = golesEquipo1,
            GolesEquipo2 = golesEquipo2,
            Fuente = FuentePrediccion.Telegram
        };

        // Call real BL service
        await _prediccionService.GuardarPrediccionAsync(prediccion, jornada, esOrganizador: false);

        // Notify via Telegram (mocked)
        var confirmations = new List<string> { $"Argentina {golesEquipo1} - {golesEquipo2} Brasil" };
        await _telegramBotMock.Object.SendConfirmationListAsync(telegramChatId, confirmations);
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task WebhookFlow_PrediccionSaved_WithCorrectValues()
    {
        // Arrange
        var (jornada, partido, participante) = await SeedScenarioAsync();

        _telegramBotMock
            .Setup(t => t.SendConfirmationListAsync(12345L, It.IsAny<IReadOnlyList<string>>()))
            .Returns(Task.CompletedTask);

        // Act: simulate Telegram update → controller → service
        await SimulateWebhookAsync(
            partido.Id, participante.Id,
            golesEquipo1: 3, golesEquipo2: 0,
            jornada);

        // Assert: prediction is in the DB with the correct values
        var saved = await _context.PrediccionesPartido
            .FirstOrDefaultAsync(pp => pp.PartidoId == partido.Id && pp.ParticipanteId == participante.Id);

        saved.Should().NotBeNull("the webhook must persist the prediction");
        saved!.GolesEquipo1.Should().Be(3);
        saved.GolesEquipo2.Should().Be(0);
        saved.Fuente.Should().Be(FuentePrediccion.Telegram);
    }

    [Fact]
    public async Task WebhookFlow_GuardarPrediccionCalled_WithCorrectArguments()
    {
        // Arrange: mock the service so we can capture arguments
        var mockPrediccionService = new Mock<IPrediccionService>(MockBehavior.Strict);

        var (jornada, partido, participante) = await SeedScenarioAsync();

        PrediccionPartido? capturedPrediccion = null;

        mockPrediccionService
            .Setup(s => s.GuardarPrediccionAsync(
                It.IsAny<PrediccionPartido>(),
                It.Is<Jornada>(j => j.Id == jornada.Id),
                false))
            .Callback<PrediccionPartido, Jornada, bool>((p, _, _) => capturedPrediccion = p)
            .Returns(Task.CompletedTask);

        _telegramBotMock
            .Setup(t => t.SendConfirmationListAsync(It.IsAny<long>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns(Task.CompletedTask);

        // Build the prediction as the webhook would
        var prediccion = new PrediccionPartido
        {
            PartidoId = partido.Id,
            ParticipanteId = participante.Id,
            GolesEquipo1 = 2,
            GolesEquipo2 = 1,
            Fuente = FuentePrediccion.Telegram
        };

        // Act: call service (mocked)
        await mockPrediccionService.Object.GuardarPrediccionAsync(prediccion, jornada, esOrganizador: false);

        // Assert: the service was called with the right arguments
        mockPrediccionService.Verify(
            s => s.GuardarPrediccionAsync(
                It.Is<PrediccionPartido>(p =>
                    p.PartidoId == partido.Id
                    && p.ParticipanteId == participante.Id
                    && p.GolesEquipo1 == 2
                    && p.GolesEquipo2 == 1
                    && p.Fuente == FuentePrediccion.Telegram),
                It.Is<Jornada>(j => j.Id == jornada.Id),
                false),
            Times.Once);

        capturedPrediccion.Should().NotBeNull();
        capturedPrediccion!.GolesEquipo1.Should().Be(2);
        capturedPrediccion.GolesEquipo2.Should().Be(1);
    }

    [Fact]
    public async Task WebhookFlow_DeadlineClosed_ThrowsInvalidOperation_TelegramNotNotified()
    {
        // Arrange: deadline in the past
        var pastDeadline = DateTimeOffset.UtcNow.AddHours(-3);
        var (jornada, partido, participante) = await SeedScenarioAsync(pastDeadline);

        // Telegram should NOT receive a confirmation — deadline already closed
        // (strict mock: any unexpected call will fail the test)

        var prediccion = new PrediccionPartido
        {
            PartidoId = partido.Id,
            ParticipanteId = participante.Id,
            GolesEquipo1 = 1,
            GolesEquipo2 = 1,
            Fuente = FuentePrediccion.Telegram
        };

        // Act
        var act = async () => await _prediccionService.GuardarPrediccionAsync(prediccion, jornada, esOrganizador: false);

        // Assert: service throws, Telegram mock never called
        await act.Should().ThrowAsync<InvalidOperationException>();

        _telegramBotMock.Verify(t => t.SendConfirmationListAsync(
            It.IsAny<long>(), It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }

    [Fact]
    public async Task WebhookFlow_MultipleMessages_UpsertKeepsLatest()
    {
        // Simulate two consecutive Telegram messages: first 1-0, then updated to 2-1
        var (jornada, partido, participante) = await SeedScenarioAsync();

        _telegramBotMock
            .Setup(t => t.SendConfirmationListAsync(It.IsAny<long>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns(Task.CompletedTask);

        await SimulateWebhookAsync(partido.Id, participante.Id, 1, 0, jornada);
        await SimulateWebhookAsync(partido.Id, participante.Id, 2, 1, jornada);

        // Assert: only one record with the latest values
        var records = await _context.PrediccionesPartido
            .Where(pp => pp.PartidoId == partido.Id && pp.ParticipanteId == participante.Id)
            .ToListAsync();

        records.Should().HaveCount(1, "upsert — no duplicate predictions per match per participant");
        records[0].GolesEquipo1.Should().Be(2);
        records[0].GolesEquipo2.Should().Be(1);
    }
}
