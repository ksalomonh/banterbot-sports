using BanterBotSports.BL.Services;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace BanterBotSports.Tests.Integration;

/// <summary>
/// Integration tests for JornadaService state transitions.
/// Uses a real PostgreSQL database via TestContainers.
/// </summary>
public class JornadaServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("banterbot_jornada_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private AppDbContext _context = null!;
    private JornadaService _jornadaService = null!;

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

        var jornadaRepo = new JornadaRepository(_context);
        var partidoRepo = new PartidoRepository(_context);
        var participanteRepo = new ParticipanteRepository(_context);
        var uow = new UnitOfWork(_context);

        _jornadaService = new JornadaService(
            jornadaRepo,
            partidoRepo,
            participanteRepo,
            uow,
            NullLogger<JornadaService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Truncates a DateTimeOffset to microsecond precision.
    /// PostgreSQL timestamptz stores up to microseconds (1 µs = 10 ticks).
    /// .NET DateTimeOffset uses 100 ns ticks, so without truncation the DB round-trip
    /// produces a value that differs in the last digit(s).
    /// </summary>
    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset dto)
    {
        const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        var truncatedTicks = dto.Ticks - (dto.Ticks % TicksPerMicrosecond);
        return new DateTimeOffset(truncatedTicks, dto.Offset);
    }

    private async Task<(Torneo torneo, Jornada jornada)> SeedTorneoConJornadaPendienteAsync()
    {
        var torneo = new Torneo
        {
            Nombre = "Torneo Jornada Tests",
            OrganizadorId = "org-jornada",
            NumJornadas = 1,
            MontoInscripcion = 50m,
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
            Estado = EstadoJornada.PendientePartidos,
            DeadlineUtc = null
        };
        _context.Jornadas.Add(jornada);
        await _context.SaveChangesAsync();

        return (torneo, jornada);
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AbrirJornadaAsync_WithPartidos_SetsDeadlineToEarliestKickOff()
    {
        // Arrange
        var (_, jornada) = await SeedTorneoConJornadaPendienteAsync();

        // Truncate to microseconds: PostgreSQL timestamptz has microsecond precision;
        // .NET DateTimeOffset has 100ns ticks. Without truncation the round-trip comparison fails.
        var kickOff1 = TruncateToMicroseconds(DateTimeOffset.UtcNow.AddDays(2));
        var kickOff2 = TruncateToMicroseconds(DateTimeOffset.UtcNow.AddDays(1));  // earliest

        _context.Partidos.AddRange(
            new Partido
            {
                JornadaId = jornada.Id,
                Equipo1 = "River",
                Equipo2 = "Boca",
                KickOffUtc = kickOff1,
                Estado = EstadoPartido.Programado
            },
            new Partido
            {
                JornadaId = jornada.Id,
                Equipo1 = "Racing",
                Equipo2 = "Independiente",
                KickOffUtc = kickOff2,
                Estado = EstadoPartido.Programado
            });
        await _context.SaveChangesAsync();

        // Act
        await _jornadaService.AbrirJornadaAsync(jornada.Id);

        // Assert: DeadlineUtc is set to the earliest kick-off
        _context.ChangeTracker.Clear();
        var updated = await _context.Jornadas.FindAsync(jornada.Id);
        updated.Should().NotBeNull();
        updated!.Estado.Should().Be(EstadoJornada.Abierta);
        updated.DeadlineUtc.Should().Be(kickOff2,
            "DeadlineUtc must equal the earliest KickOffUtc among all partidos");
    }

    [Fact]
    public async Task AbrirJornadaAsync_WithoutPartidos_ThrowsInvalidOperationException()
    {
        // Arrange: jornada with no partidos
        var (_, jornada) = await SeedTorneoConJornadaPendienteAsync();

        // Act
        var act = async () => await _jornadaService.AbrirJornadaAsync(jornada.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            "opening a jornada without partidos must be rejected");

        // Verify jornada state was NOT changed
        _context.ChangeTracker.Clear();
        var unchanged = await _context.Jornadas.FindAsync(jornada.Id);
        unchanged!.Estado.Should().Be(EstadoJornada.PendientePartidos,
            "failed open must not persist a state change");
    }

    [Fact]
    public async Task AbrirJornadaAsync_AlreadyAbierta_ThrowsInvalidOperationException()
    {
        // Arrange: jornada already in Abierta state
        var (_, jornada) = await SeedTorneoConJornadaPendienteAsync();

        jornada.Estado = EstadoJornada.Abierta;
        _context.Jornadas.Update(jornada);
        await _context.SaveChangesAsync();

        // Act
        var act = async () => await _jornadaService.AbrirJornadaAsync(jornada.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            "cannot transition from Abierta → Abierta");
    }

    [Fact]
    public async Task AbrirJornadaAsync_SinglePartido_DeadlineEqualsItsKickOff()
    {
        // Arrange: exactly one partido
        var (_, jornada) = await SeedTorneoConJornadaPendienteAsync();

        // Truncate to microseconds so the round-trip comparison against PostgreSQL is exact.
        var singleKickOff = TruncateToMicroseconds(DateTimeOffset.UtcNow.AddHours(48));
        _context.Partidos.Add(new Partido
        {
            JornadaId = jornada.Id,
            Equipo1 = "Atletico",
            Equipo2 = "Madrid",
            KickOffUtc = singleKickOff,
            Estado = EstadoPartido.Programado
        });
        await _context.SaveChangesAsync();

        // Act
        await _jornadaService.AbrirJornadaAsync(jornada.Id);

        // Assert
        _context.ChangeTracker.Clear();
        var updated = await _context.Jornadas.FindAsync(jornada.Id);
        updated!.DeadlineUtc.Should().Be(singleKickOff,
            "with a single partido, DeadlineUtc == that partido's KickOffUtc");
    }

    [Fact]
    public async Task AbrirJornadaAsync_WithPartidos_RaisesJornadaAbiertaEvent()
    {
        // Arrange
        var (_, jornada) = await SeedTorneoConJornadaPendienteAsync();
        _context.Partidos.Add(new Partido
        {
            JornadaId = jornada.Id,
            Equipo1 = "PSG",
            Equipo2 = "Marseille",
            KickOffUtc = DateTimeOffset.UtcNow.AddDays(1),
            Estado = EstadoPartido.Programado
        });
        await _context.SaveChangesAsync();

        Jornada? capturedJornada = null;
        _jornadaService.JornadaAbierta += j =>
        {
            capturedJornada = j;
            return Task.CompletedTask;
        };

        // Act
        await _jornadaService.AbrirJornadaAsync(jornada.Id);

        // Assert: event was raised with the correct jornada
        capturedJornada.Should().NotBeNull("JornadaAbierta event must be raised");
        capturedJornada!.Id.Should().Be(jornada.Id);
        capturedJornada.Estado.Should().Be(EstadoJornada.Abierta);
    }
}
