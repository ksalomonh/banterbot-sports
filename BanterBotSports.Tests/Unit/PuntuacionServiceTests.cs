using BanterBotSports.BL.Services;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;

namespace BanterBotSports.Tests.Unit;

public class PuntuacionServiceTests
{
    private readonly PuntuacionService _sut;

    private static Torneo BuildTorneo(int ptosResultado = 3, int ptosMarcador = 5, int ptosGolesJornada = 2)
        => new()
        {
            Id = 1,
            Nombre = "Test Torneo",
            PtosResultado = ptosResultado,
            PtosMarcador = ptosMarcador,
            PtosGolesJornada = ptosGolesJornada
        };

    private static Partido BuildPartido(int? goles1Oficial = null, int? goles2Oficial = null, int? golesReglamento = null)
        => new()
        {
            Id = 1,
            JornadaId = 1,
            Equipo1 = "River",
            Equipo2 = "Boca",
            KickOffUtc = DateTimeOffset.UtcNow,
            GolesEquipo1Oficial = goles1Oficial,
            GolesEquipo2Oficial = goles2Oficial,
            GolesReglamento = golesReglamento,
            Estado = EstadoPartido.Finalizado,
            Jornada = null!
        };

    private static PrediccionPartido BuildPrediccion(int goles1, int goles2)
        => new()
        {
            Id = 1,
            PartidoId = 1,
            ParticipanteId = 1,
            GolesEquipo1 = goles1,
            GolesEquipo2 = goles2,
            Fuente = FuentePrediccion.Web
        };

    public PuntuacionServiceTests()
    {
        _sut = new PuntuacionService();
    }

    [Fact]
    public void CalcularPuntos_ResultadoCorrecto_ReturnsPuntosResultadoOnly()
    {
        // Arrange: local wins 2-0, prediction says 3-0 (1X2 correct, score wrong)
        var torneo = BuildTorneo(ptosResultado: 3, ptosMarcador: 5);
        var partido = BuildPartido(goles1Oficial: 2, goles2Oficial: 0, golesReglamento: 2);
        var prediccion = BuildPrediccion(3, 0);

        // Act
        var result = _sut.CalcularPuntos(prediccion, partido, torneo);

        // Assert
        result.PuntosResultado.Should().Be(3);
        result.PuntosExacto.Should().Be(0);
        result.PuntosGolesJornada.Should().Be(0);
        result.Total.Should().Be(3);
    }

    [Fact]
    public void CalcularPuntos_MarcadorExacto_ReturnsPtosMarcadorOnly()
    {
        // Arrange: exact score prediction, no double-counting
        var torneo = BuildTorneo(ptosResultado: 3, ptosMarcador: 5);
        var partido = BuildPartido(goles1Oficial: 2, goles2Oficial: 1, golesReglamento: 3);
        var prediccion = BuildPrediccion(2, 1);

        // Act
        var result = _sut.CalcularPuntos(prediccion, partido, torneo);

        // Assert
        result.PuntosResultado.Should().Be(0, "exact score must not double-count result points");
        result.PuntosExacto.Should().Be(5);
        result.PuntosGolesJornada.Should().Be(0);
        result.Total.Should().Be(5);
    }

    [Fact]
    public void CalcularPuntos_ResultadoIncorrecto_ReturnsZero()
    {
        // Arrange: local wins but prediction says draw
        var torneo = BuildTorneo(ptosResultado: 3, ptosMarcador: 5);
        var partido = BuildPartido(goles1Oficial: 2, goles2Oficial: 0, golesReglamento: 2);
        var prediccion = BuildPrediccion(1, 1); // draw prediction

        // Act
        var result = _sut.CalcularPuntos(prediccion, partido, torneo);

        // Assert
        result.Total.Should().Be(0);
    }

    [Fact]
    public void CalcularPuntos_PtosGolesJornada_NotAwardedAtMatchLevel()
    {
        // PuntosGolesJornada is applied at jornada level, not here — should always be 0
        var torneo = BuildTorneo(ptosResultado: 3, ptosMarcador: 5, ptosGolesJornada: 2);
        var partido = BuildPartido(goles1Oficial: 2, goles2Oficial: 1, golesReglamento: 3);
        var prediccion = BuildPrediccion(2, 1); // exact score

        var result = _sut.CalcularPuntos(prediccion, partido, torneo);

        result.PuntosGolesJornada.Should().Be(0, "PtosGolesJornada is applied at jornada level, not match level");
    }

    [Fact]
    public void CalcularPuntos_PartidoSinResultado_ReturnsZero()
    {
        // No official goals set yet
        var torneo = BuildTorneo();
        var partido = BuildPartido(goles1Oficial: null, goles2Oficial: null, golesReglamento: null);
        var prediccion = BuildPrediccion(1, 0);

        var result = _sut.CalcularPuntos(prediccion, partido, torneo);

        result.Total.Should().Be(0);
    }

    [Fact]
    public void CalcularPuntos_PenalesExcluidosDelMarcadorReglamentario_UsesOnlyFtAetGoals()
    {
        // Game ends 1-1 in regulation. Penalties: team1 wins 4-3.
        // GolesEquipo1Oficial and GolesEquipo2Oficial EXCLUDE penalties (FT+AET only).
        // A prediction of 1-1 should yield exact score using these official values.
        var torneo = BuildTorneo(ptosResultado: 3, ptosMarcador: 5);
        var partido = BuildPartido(goles1Oficial: 1, goles2Oficial: 1, golesReglamento: 2);
        var prediccion = BuildPrediccion(1, 1); // matches FT+AET, not penalty result

        var result = _sut.CalcularPuntos(prediccion, partido, torneo);

        result.PuntosExacto.Should().Be(5, "penalties are excluded; 1-1 FT+AET is the official score");
        result.PuntosResultado.Should().Be(0, "exact match — no double counting");
    }

    [Fact]
    public void CalcularPuntos_PtosResultadoIsConfigurable_UsesTorneoValue()
    {
        // Different torneo with different PtosResultado
        var torneo = BuildTorneo(ptosResultado: 10, ptosMarcador: 20);
        var partido = BuildPartido(goles1Oficial: 3, goles2Oficial: 1, golesReglamento: 4);
        var prediccion = BuildPrediccion(2, 0); // correct 1X2, wrong score

        var result = _sut.CalcularPuntos(prediccion, partido, torneo);

        result.PuntosResultado.Should().Be(10, "must use configurable torneo.PtosResultado");
    }

    [Fact]
    public void CalcularPuntos_PtosMarcadorIsConfigurable_UsesTorneoValue()
    {
        var torneo = BuildTorneo(ptosResultado: 3, ptosMarcador: 15);
        var partido = BuildPartido(goles1Oficial: 2, goles2Oficial: 0, golesReglamento: 2);
        var prediccion = BuildPrediccion(2, 0); // exact

        var result = _sut.CalcularPuntos(prediccion, partido, torneo);

        result.PuntosExacto.Should().Be(15, "must use configurable torneo.PtosMarcador");
    }

    [Fact]
    public void CalcularPuntos_EmpateResult_CorrectPrediction_ReturnsPtosResultado()
    {
        var torneo = BuildTorneo(ptosResultado: 3, ptosMarcador: 5);
        var partido = BuildPartido(goles1Oficial: 1, goles2Oficial: 1, golesReglamento: 2);
        var prediccion = BuildPrediccion(2, 2); // draw correct, score wrong

        var result = _sut.CalcularPuntos(prediccion, partido, torneo);

        result.PuntosResultado.Should().Be(3);
        result.PuntosExacto.Should().Be(0);
    }

    [Fact]
    public void CalcularPuntos_NullPrediccion_ThrowsArgumentNullException()
    {
        var torneo = BuildTorneo();
        var partido = BuildPartido(1, 0, 1);

        var act = () => _sut.CalcularPuntos(null!, partido, torneo);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalcularPuntos_NullPartido_ThrowsArgumentNullException()
    {
        var torneo = BuildTorneo();
        var prediccion = BuildPrediccion(1, 0);

        var act = () => _sut.CalcularPuntos(prediccion, null!, torneo);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalcularPuntos_NullTorneo_ThrowsArgumentNullException()
    {
        var partido = BuildPartido(1, 0, 1);
        var prediccion = BuildPrediccion(1, 0);

        var act = () => _sut.CalcularPuntos(prediccion, partido, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
