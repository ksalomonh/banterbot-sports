using BanterBotSports.BL.Services;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Verifies that AbrirJornadaAsync calls DarDeBajaImpagosAsync
/// before transitioning the jornada to Abierta.
/// </summary>
public class JornadaServiceAutoBajaTests
{
    private readonly Mock<IJornadaRepository> _jornadaRepo = new();
    private readonly Mock<IPartidoRepository> _partidoRepo = new();
    private readonly Mock<IParticipanteRepository> _participanteRepo = new();
    private readonly Mock<ITorneoService> _torneoService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private JornadaService BuildSut() => new(
        _jornadaRepo.Object,
        _partidoRepo.Object,
        _participanteRepo.Object,
        _torneoService.Object,
        _unitOfWork.Object,
        NullLogger<JornadaService>.Instance);

    [Fact]
    public async Task AbrirJornada_CallsDarDeBajaImpagos_BeforeStateTransition()
    {
        // Arrange
        var jornada = new Jornada { Id = 1, TorneoId = 10, Numero = 1, Estado = EstadoJornada.PendientePartidos };
        var partido = new Partido
        {
            Id = 100, JornadaId = 1, Equipo1 = "A", Equipo2 = "B",
            KickOffUtc = DateTime.UtcNow.AddDays(1)
        };

        _jornadaRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(jornada);
        _partidoRepo.Setup(r => r.GetByJornadaIdAsync(1)).ReturnsAsync(new List<Partido> { partido });
        _torneoService.Setup(s => s.DarDeBajaImpagosAsync(10)).ReturnsAsync(2);

        var callOrder = new List<string>();
        _torneoService
            .Setup(s => s.DarDeBajaImpagosAsync(10))
            .Callback(() => callOrder.Add("DarDeBaja"))
            .ReturnsAsync(2);
        _jornadaRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Jornada>()))
            .Callback(() => callOrder.Add("UpdateJornada"));

        var sut = BuildSut();

        // Act
        await sut.AbrirJornadaAsync(1);

        // Assert: DarDeBaja was called, and BEFORE the jornada state was saved
        _torneoService.Verify(s => s.DarDeBajaImpagosAsync(10), Times.Once);
        callOrder.Should().ContainInOrder("DarDeBaja", "UpdateJornada");
    }

    [Fact]
    public async Task AbrirJornada_NoUnpaidPlayers_StillTransitions()
    {
        // Arrange
        var jornada = new Jornada { Id = 2, TorneoId = 10, Numero = 1, Estado = EstadoJornada.PendientePartidos };
        var partido = new Partido
        {
            Id = 200, JornadaId = 2, Equipo1 = "C", Equipo2 = "D",
            KickOffUtc = DateTime.UtcNow.AddDays(1)
        };

        _jornadaRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(jornada);
        _partidoRepo.Setup(r => r.GetByJornadaIdAsync(2)).ReturnsAsync(new List<Partido> { partido });
        _torneoService.Setup(s => s.DarDeBajaImpagosAsync(10)).ReturnsAsync(0);

        var sut = BuildSut();

        // Act
        await sut.AbrirJornadaAsync(2);

        // Assert
        jornada.Estado.Should().Be(EstadoJornada.Abierta);
        _torneoService.Verify(s => s.DarDeBajaImpagosAsync(10), Times.Once);
    }
}
