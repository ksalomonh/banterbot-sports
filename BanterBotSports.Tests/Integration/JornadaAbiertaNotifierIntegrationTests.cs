using BanterBotSports.BL.Services;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Integrations.Telegram;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace BanterBotSports.Tests.Integration;

/// <summary>
/// Integration tests for JornadaAbiertaNotifier.
///
/// Verifies that when JornadaService fires the JornadaAbierta event:
///   1. The notifier is invoked
///   2. ITelegramBotService.SendMatchesListAsync is called for participants with linked Telegram
///   3. Participants without a Telegram account are silently skipped
/// </summary>
public class JornadaAbiertaNotifierIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("banterbot_notifier_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private AppDbContext _context = null!;
    private JornadaService _jornadaService = null!;
    private Mock<ITelegramBotService> _telegramMock = null!;
    private ServiceProvider _serviceProvider = null!;

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

        // Build a service provider that can serve scoped repositories
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IPartidoRepository, PartidoRepository>(
            sp => new PartidoRepository(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped<ITorneoRepository, TorneoRepository>(
            sp => new TorneoRepository(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped<IUsuarioTelegramRepository, UsuarioTelegramRepository>(
            sp => new UsuarioTelegramRepository(sp.GetRequiredService<AppDbContext>()));

        _serviceProvider = services.BuildServiceProvider();

        _telegramMock = new Mock<ITelegramBotService>(MockBehavior.Loose);

        var jornadaRepo = new JornadaRepository(_context);
        var partidoRepo = new PartidoRepository(_context);
        var participanteRepo = new ParticipanteRepository(_context);
        var uow = new UnitOfWork(_context);

        _jornadaService = new JornadaService(
            jornadaRepo,
            partidoRepo,
            participanteRepo,
            new Mock<ITorneoService>().Object,
            uow,
            NullLogger<JornadaService>.Instance);

        // Wire the notifier to the event
        var notifier = new JornadaAbiertaNotifier(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _telegramMock.Object,
            NullLogger<JornadaAbiertaNotifier>.Instance);

        _jornadaService.JornadaAbierta += notifier.OnJornadaAbiertaAsync;
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<(Torneo torneo, Jornada jornada, Participante participante)>
        SeedTorneoConJornadaYParticipanteAsync()
    {
        var torneo = new Torneo
        {
            Nombre = "Torneo Notifier Tests",
            OrganizadorId = "org-notifier",
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
            Estado = EstadoJornada.PendientePartidos
        };
        _context.Jornadas.Add(jornada);
        await _context.SaveChangesAsync();

        var participante = new Participante
        {
            TorneoId = torneo.Id,
            UserId = "user-notifier-1",
            Rol = RolParticipante.Jugador,
            Pago = true
        };
        _context.Participantes.Add(participante);
        await _context.SaveChangesAsync();

        return (torneo, jornada, participante);
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AbrirJornada_WithLinkedTelegramParticipants_CallsSendMatchesListAsync()
    {
        // Arrange: seed torneo, jornada, participante and a linked Telegram account
        var (_, jornada, participante) = await SeedTorneoConJornadaYParticipanteAsync();
        const long chatId = 77777L;

        // Add a partido so the jornada can be opened
        var partido = new Partido
        {
            JornadaId = jornada.Id,
            Equipo1 = "Liverpool",
            Equipo2 = "Manchester City",
            KickOffUtc = DateTimeOffset.UtcNow.AddDays(3),
            Estado = EstadoPartido.Programado
        };
        _context.Partidos.Add(partido);

        // Link the participante's Telegram account
        _context.UsuariosTelegram.Add(new UsuarioTelegram
        {
            UserId = participante.UserId,
            TelegramUserId = chatId,
            TelegramUsername = "notifier_user"
        });
        await _context.SaveChangesAsync();

        _telegramMock
            .Setup(t => t.SendMatchesListAsync(chatId, It.IsAny<IReadOnlyList<PartidoDto>>()))
            .Returns(Task.CompletedTask);

        // Act: opening the jornada fires the JornadaAbierta event → notifier sends matches
        await _jornadaService.AbrirJornadaAsync(jornada.Id);

        // Assert: SendMatchesListAsync was called with the correct chat ID
        _telegramMock.Verify(
            t => t.SendMatchesListAsync(chatId, It.IsAny<IReadOnlyList<PartidoDto>>()),
            Times.Once,
            "notifier must call SendMatchesListAsync for each participant with a linked Telegram");
    }

    [Fact]
    public async Task AbrirJornada_WithoutLinkedTelegram_DoesNotCallSendMatchesListAsync()
    {
        // Arrange: participante has no UsuarioTelegram row
        var (_, jornada, _) = await SeedTorneoConJornadaYParticipanteAsync();

        _context.Partidos.Add(new Partido
        {
            JornadaId = jornada.Id,
            Equipo1 = "Arsenal",
            Equipo2 = "Chelsea",
            KickOffUtc = DateTimeOffset.UtcNow.AddDays(2),
            Estado = EstadoPartido.Programado
        });
        await _context.SaveChangesAsync();

        // Act
        await _jornadaService.AbrirJornadaAsync(jornada.Id);

        // Assert: no Telegram message should be sent — participant has no linked account
        _telegramMock.Verify(
            t => t.SendMatchesListAsync(It.IsAny<long>(), It.IsAny<IReadOnlyList<PartidoDto>>()),
            Times.Never,
            "no Telegram send when participant has no linked account");
    }

    [Fact]
    public async Task AbrirJornada_SendsCorrectPartidosToEachParticipant()
    {
        // Arrange: two partidos in the jornada; one participant with Telegram
        var (_, jornada, participante) = await SeedTorneoConJornadaYParticipanteAsync();
        const long chatId = 88888L;

        var kickOff1 = DateTimeOffset.UtcNow.AddDays(1);
        var kickOff2 = DateTimeOffset.UtcNow.AddDays(2);

        _context.Partidos.AddRange(
            new Partido
            {
                JornadaId = jornada.Id,
                Equipo1 = "Bayern",
                Equipo2 = "Dortmund",
                KickOffUtc = kickOff1,
                Estado = EstadoPartido.Programado
            },
            new Partido
            {
                JornadaId = jornada.Id,
                Equipo1 = "Inter",
                Equipo2 = "AC Milan",
                KickOffUtc = kickOff2,
                Estado = EstadoPartido.Programado
            });

        _context.UsuariosTelegram.Add(new UsuarioTelegram
        {
            UserId = participante.UserId,
            TelegramUserId = chatId,
            TelegramUsername = "fan_user"
        });
        await _context.SaveChangesAsync();

        IReadOnlyList<PartidoDto>? capturedPartidos = null;
        _telegramMock
            .Setup(t => t.SendMatchesListAsync(chatId, It.IsAny<IReadOnlyList<PartidoDto>>()))
            .Callback<long, IReadOnlyList<PartidoDto>>((_, dtos) => capturedPartidos = dtos)
            .Returns(Task.CompletedTask);

        // Act
        await _jornadaService.AbrirJornadaAsync(jornada.Id);

        // Assert: both partidos were included in the notification
        capturedPartidos.Should().NotBeNull("SendMatchesListAsync must have been called");
        capturedPartidos!.Should().HaveCount(2, "both partidos in the jornada must be notified");
        capturedPartidos.Should().Contain(p => p.Equipo1 == "Bayern" && p.Equipo2 == "Dortmund");
        capturedPartidos.Should().Contain(p => p.Equipo1 == "Inter" && p.Equipo2 == "AC Milan");
    }
}
