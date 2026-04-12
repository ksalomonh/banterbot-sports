using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using FluentAssertions;

namespace BanterBotSports.Tests.Unit;

public class PremioServiceTests
{
    private readonly PremioService _sut;

    public PremioServiceTests()
    {
        _sut = new PremioService();
    }

    /// <summary>
    /// Builds a torneo with configurable prize percentages and
    /// the specified number of participants (each contributes MontoInscripcion).
    /// </summary>
    private static Torneo BuildTorneo(
        decimal montoInscripcion,
        int numParticipantes,
        params (int posicion, decimal porcentaje)[] config)
    {
        var torneo = new Torneo
        {
            Id = 1,
            Nombre = "Test Torneo",
            MontoInscripcion = montoInscripcion,
            PorcentajeOrganizador = 5m
        };

        for (int i = 1; i <= numParticipantes; i++)
        {
            torneo.Participantes.Add(new Participante { Id = i, TorneoId = 1, UserId = $"user{i}" });
        }

        foreach (var (posicion, porcentaje) in config)
        {
            torneo.ConfiguracionPremios.Add(new ConfiguracionPremio
            {
                TorneoId = 1,
                Posicion = posicion,
                Porcentaje = porcentaje
            });
        }

        return torneo;
    }

    [Fact]
    public void CalcularDistribucion_SinEmpate_GanadorUnico_CorrectAmount()
    {
        // 4 players × $100 = $400 pool.  1st gets 60%, 2nd gets 40%
        var torneo = BuildTorneo(100m, 4, (1, 60m), (2, 40m));

        var rankings = new List<RankingParticipante>
        {
            new(1, "Alice", 30, 1),
            new(2, "Bob",   20, 2),
            new(3, "Carol", 10, 3),
            new(4, "Dave",   5, 4)
        };

        var result = _sut.CalcularDistribucion(rankings, torneo);

        result.Should().HaveCount(4);
        result.First(r => r.ParticipanteId == 1).MontoGanado.Should().Be(240m, "60% of $400");
        result.First(r => r.ParticipanteId == 2).MontoGanado.Should().Be(160m, "40% of $400");
        result.Where(r => r.ParticipanteId is 3 or 4).Should().AllSatisfy(r => r.MontoGanado.Should().Be(0m));
    }

    [Fact]
    public void CalcularDistribucion_EmpatePrimerPuesto_SplitPrize()
    {
        // 4 players × $100 = $400 pool.  1st gets 60%, 2nd gets 40%
        // Two players tied at first place → each gets (60% + 40%) / 2 = $200
        var torneo = BuildTorneo(100m, 4, (1, 60m), (2, 40m));

        var rankings = new List<RankingParticipante>
        {
            new(1, "Alice", 30, 1),
            new(2, "Bob",   30, 1), // tied with Alice
            new(3, "Carol", 10, 3),
            new(4, "Dave",   5, 4)
        };

        var result = _sut.CalcularDistribucion(rankings, torneo);

        result.Should().HaveCount(4);
        result.First(r => r.ParticipanteId == 1).MontoGanado.Should().Be(200m, "split (60%+40%) = $400/2 each");
        result.First(r => r.ParticipanteId == 2).MontoGanado.Should().Be(200m, "split (60%+40%) = $400/2 each");
        result.Where(r => r.ParticipanteId is 3 or 4).Should().AllSatisfy(r => r.MontoGanado.Should().Be(0m));
    }

    [Fact]
    public void CalcularDistribucion_EmpatSegundoPuesto_SplitSecondPrize()
    {
        // 4 players × $100 = $400 pool.  1st gets 60%, 2nd gets 30%, 3rd gets 10%
        // 2nd and 3rd tied → each gets (30%+10%)/2 = $80
        var torneo = BuildTorneo(100m, 4, (1, 60m), (2, 30m), (3, 10m));

        var rankings = new List<RankingParticipante>
        {
            new(1, "Alice", 40, 1),
            new(2, "Bob",   20, 2), // tied 2nd
            new(3, "Carol", 20, 2), // tied 2nd
            new(4, "Dave",   5, 4)
        };

        var result = _sut.CalcularDistribucion(rankings, torneo);

        result.First(r => r.ParticipanteId == 1).MontoGanado.Should().Be(240m, "60% of $400");
        result.First(r => r.ParticipanteId == 2).MontoGanado.Should().Be(80m, "(30%+10%)/2 of $400");
        result.First(r => r.ParticipanteId == 3).MontoGanado.Should().Be(80m, "(30%+10%)/2 of $400");
        result.First(r => r.ParticipanteId == 4).MontoGanado.Should().Be(0m);
    }

    [Fact]
    public void CalcularDistribucion_UnSoloGanador_MultipleConfiguraciones_CorrectDistribution()
    {
        // Single player in rankings who wins everything with one prize config entry
        var torneo = BuildTorneo(200m, 3, (1, 70m));

        var rankings = new List<RankingParticipante>
        {
            new(1, "Alice", 50, 1),
            new(2, "Bob",   30, 2),
            new(3, "Carol", 10, 3)
        };

        var result = _sut.CalcularDistribucion(rankings, torneo);

        // Pool = 3 × $200 = $600; 1st = 70% = $420
        result.First(r => r.ParticipanteId == 1).MontoGanado.Should().Be(420m);
        result.First(r => r.ParticipanteId == 2).MontoGanado.Should().Be(0m, "no config for 2nd place");
        result.First(r => r.ParticipanteId == 3).MontoGanado.Should().Be(0m, "no config for 3rd place");
    }

    [Fact]
    public void CalcularDistribucion_EmptyRankings_ReturnsEmpty()
    {
        var torneo = BuildTorneo(100m, 2, (1, 60m), (2, 40m));

        var result = _sut.CalcularDistribucion(Array.Empty<RankingParticipante>(), torneo);

        result.Should().BeEmpty();
    }

    [Fact]
    public void CalcularDistribucion_NullRankings_ThrowsArgumentNullException()
    {
        var torneo = BuildTorneo(100m, 2, (1, 100m));

        var act = () => _sut.CalcularDistribucion(null!, torneo);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalcularDistribucion_NullTorneo_ThrowsArgumentNullException()
    {
        var rankings = new List<RankingParticipante> { new(1, "Alice", 10, 1) };

        var act = () => _sut.CalcularDistribucion(rankings, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalcularDistribucion_Posicion_IsAssignedCorrectly_WhenNoTie()
    {
        var torneo = BuildTorneo(100m, 3, (1, 50m), (2, 30m), (3, 20m));

        var rankings = new List<RankingParticipante>
        {
            new(1, "Alice", 30, 1),
            new(2, "Bob",   20, 2),
            new(3, "Carol", 10, 3)
        };

        var result = _sut.CalcularDistribucion(rankings, torneo);

        result.First(r => r.ParticipanteId == 1).Posicion.Should().Be(1);
        result.First(r => r.ParticipanteId == 2).Posicion.Should().Be(2);
        result.First(r => r.ParticipanteId == 3).Posicion.Should().Be(3);
    }
}
