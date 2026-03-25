using BanterBotSports.BL.Services;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BanterBotSports.Tests.Integration;

/// <summary>
/// Integration tests for IPrediccionService using a real PostgreSQL database via TestContainers.
/// </summary>
public class PrediccionServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("banterbot_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private AppDbContext _context = null!;
    private PrediccionService _prediccionService = null!;

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

        var prediccionRepo = new PrediccionRepository(_context);
        var jornadaRepo = new JornadaRepository(_context);
        _prediccionService = new PrediccionService(prediccionRepo, jornadaRepo, new UnitOfWork(_context));
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<(Torneo torneo, Jornada jornada, Partido partido, Participante participante)>
        SeedBasicScenarioAsync(DateTimeOffset? deadline = null)
    {
        var torneo = new Torneo
        {
            Nombre = "Test Torneo",
            OrganizadorId = "org-user-1",
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
            Equipo1 = "River",
            Equipo2 = "Boca",
            KickOffUtc = deadline ?? DateTimeOffset.UtcNow.AddDays(1),
            Estado = EstadoPartido.Programado
        };
        _context.Partidos.Add(partido);
        await _context.SaveChangesAsync();

        var participante = new Participante
        {
            TorneoId = torneo.Id,
            UserId = "user-alice",
            Rol = RolParticipante.Jugador,
            Pago = true
        };
        _context.Participantes.Add(participante);
        await _context.SaveChangesAsync();

        return (torneo, jornada, partido, participante);
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GuardarPrediccionAsync_NuevoPrediccion_PersistsInDatabase()
    {
        // Arrange
        var (_, jornada, partido, participante) = await SeedBasicScenarioAsync();

        var prediccion = new PrediccionPartido
        {
            PartidoId = partido.Id,
            ParticipanteId = participante.Id,
            GolesEquipo1 = 2,
            GolesEquipo2 = 1,
            Fuente = FuentePrediccion.Web
        };

        // Act
        await _prediccionService.GuardarPrediccionAsync(prediccion, jornada);

        // Assert — verify it was actually persisted
        var saved = await _context.PrediccionesPartido
            .FirstOrDefaultAsync(pp => pp.PartidoId == partido.Id && pp.ParticipanteId == participante.Id);

        saved.Should().NotBeNull();
        saved!.GolesEquipo1.Should().Be(2);
        saved.GolesEquipo2.Should().Be(1);
    }

    [Fact]
    public async Task GuardarPrediccionAsync_ExistingPrediccion_UpdatesInDatabase()
    {
        // Arrange
        var (_, jornada, partido, participante) = await SeedBasicScenarioAsync();

        var prediccion = new PrediccionPartido
        {
            PartidoId = partido.Id,
            ParticipanteId = participante.Id,
            GolesEquipo1 = 1,
            GolesEquipo2 = 0,
            Fuente = FuentePrediccion.Web
        };

        await _prediccionService.GuardarPrediccionAsync(prediccion, jornada);

        // Act: update with a different score
        var updated = new PrediccionPartido
        {
            PartidoId = partido.Id,
            ParticipanteId = participante.Id,
            GolesEquipo1 = 3,
            GolesEquipo2 = 2,
            Fuente = FuentePrediccion.Telegram
        };

        await _prediccionService.GuardarPrediccionAsync(updated, jornada);

        // Assert: only one record, with the latest values
        var records = await _context.PrediccionesPartido
            .Where(pp => pp.PartidoId == partido.Id && pp.ParticipanteId == participante.Id)
            .ToListAsync();

        records.Should().HaveCount(1, "upsert must not duplicate");
        records[0].GolesEquipo1.Should().Be(3);
        records[0].GolesEquipo2.Should().Be(2);
        records[0].Fuente.Should().Be(FuentePrediccion.Telegram);
    }

    [Fact]
    public async Task GuardarPrediccionAsync_DeadlinePassed_NonOrganizador_ThrowsInvalidOperationException()
    {
        // Arrange: deadline in the past
        var pastDeadline = DateTimeOffset.UtcNow.AddHours(-1);
        var (_, jornada, partido, participante) = await SeedBasicScenarioAsync(pastDeadline);

        var prediccion = new PrediccionPartido
        {
            PartidoId = partido.Id,
            ParticipanteId = participante.Id,
            GolesEquipo1 = 1,
            GolesEquipo2 = 1,
            Fuente = FuentePrediccion.Web
        };

        // Act
        var act = async () => await _prediccionService.GuardarPrediccionAsync(prediccion, jornada, esOrganizador: false);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cerró*");
    }

    [Fact]
    public async Task GuardarPrediccionAsync_DeadlinePassed_Organizador_Succeeds()
    {
        // Arrange: deadline in the past — but organizer can still submit
        var pastDeadline = DateTimeOffset.UtcNow.AddHours(-1);
        var (_, jornada, partido, participante) = await SeedBasicScenarioAsync(pastDeadline);

        var prediccion = new PrediccionPartido
        {
            PartidoId = partido.Id,
            ParticipanteId = participante.Id,
            GolesEquipo1 = 2,
            GolesEquipo2 = 0,
            Fuente = FuentePrediccion.Web
        };

        // Act & Assert: should NOT throw
        var act = async () => await _prediccionService.GuardarPrediccionAsync(prediccion, jornada, esOrganizador: true);
        await act.Should().NotThrowAsync();

        var saved = await _context.PrediccionesPartido
            .FirstOrDefaultAsync(pp => pp.PartidoId == partido.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task ActualizarGolesJornadaAsync_AggregatesGolesAcrossMatches()
    {
        // Arrange: two matches, one participant with predictions on both
        var (torneo, jornada, partido1, participante) = await SeedBasicScenarioAsync();

        var partido2 = new Partido
        {
            JornadaId = jornada.Id,
            Equipo1 = "Racing",
            Equipo2 = "San Lorenzo",
            KickOffUtc = DateTimeOffset.UtcNow.AddDays(1),
            Estado = EstadoPartido.Programado
        };
        _context.Partidos.Add(partido2);
        await _context.SaveChangesAsync();

        // Add predictions: 2+1 = 3 goals for partido1, 1+3 = 4 goals for partido2 → total 7
        _context.PrediccionesPartido.AddRange(
            new PrediccionPartido
            {
                PartidoId = partido1.Id, ParticipanteId = participante.Id,
                GolesEquipo1 = 2, GolesEquipo2 = 1, Fuente = FuentePrediccion.Web
            },
            new PrediccionPartido
            {
                PartidoId = partido2.Id, ParticipanteId = participante.Id,
                GolesEquipo1 = 1, GolesEquipo2 = 3, Fuente = FuentePrediccion.Web
            });
        await _context.SaveChangesAsync();

        // Act
        await _prediccionService.ActualizarGolesJornadaAsync(jornada.Id);

        // Assert
        var prediccionJornada = await _context.PrediccionesJornada
            .FirstOrDefaultAsync(pj => pj.JornadaId == jornada.Id && pj.ParticipanteId == participante.Id);

        prediccionJornada.Should().NotBeNull();
        prediccionJornada!.GolesPronosticados.Should().Be(7, "2+1+1+3 = 7 predicted goals total");
    }

    [Fact]
    public async Task GetByJornadaAsync_ReturnsPrediccionesForJornada()
    {
        // Arrange
        var (_, jornada, partido, participante) = await SeedBasicScenarioAsync();

        // Seed a PrediccionJornada record directly
        _context.PrediccionesJornada.Add(new PrediccionJornada
        {
            JornadaId = jornada.Id,
            ParticipanteId = participante.Id,
            GolesPronosticados = 5
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _prediccionService.GetByJornadaAsync(jornada.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].ParticipanteId.Should().Be(participante.Id);
        result[0].GolesPronosticados.Should().Be(5);
    }
}
