using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for TorneoService cloning methods:
/// GetTorneosClonablesAsync, ClonarJugadoresAsync.
/// </summary>
public class TorneoServiceClonarTests
{
    private const string OrganizadorId = "org-user-id";
    private const int TorneoDestinoId = 1;
    private const int TorneoOrigenId = 2;

    private readonly Mock<ITorneoRepository> _torneoRepo = new();
    private readonly Mock<IParticipanteRepository> _participanteRepo = new();
    private readonly Mock<IJornadaRepository> _jornadaRepo = new();
    private readonly Mock<IPrediccionRepository> _prediccionRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAdminService> _adminService = new();
    private readonly Mock<UserManager<AppUser>> _userManager;

    public TorneoServiceClonarTests()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManager = new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private TorneoService BuildSut() => new(
        _torneoRepo.Object,
        _participanteRepo.Object,
        _jornadaRepo.Object,
        _prediccionRepo.Object,
        _unitOfWork.Object,
        _adminService.Object,
        _userManager.Object);

    private static Torneo BuildTorneo(int id, string organizadorId = OrganizadorId, EstadoTorneo estado = EstadoTorneo.Activo)
        => new() { Id = id, Nombre = $"Torneo {id}", OrganizadorId = organizadorId, Estado = estado };

    private static Participante BuildParticipante(int id, int torneoId, string userId, RolParticipante rol = RolParticipante.Jugador, bool pago = false)
        => new() { Id = id, TorneoId = torneoId, UserId = userId, Rol = rol, Pago = pago };

    // ---------------------------------------------------------------------------
    // ClonarJugadoresAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClonarJugadores_HappyPath_TwoJugadores_Returns2Clonados0Omitidos_AddsX2SavesX1()
    {
        var torneoDestino = BuildTorneo(TorneoDestinoId);
        var torneoOrigen = BuildTorneo(TorneoOrigenId);

        var jugadoresOrigen = new List<Participante>
        {
            BuildParticipante(1, TorneoOrigenId, "userA"),
            BuildParticipante(2, TorneoOrigenId, "userB")
        };

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneoDestino);
        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoOrigenId)).ReturnsAsync(torneoOrigen);
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoOrigenId)).ReturnsAsync(jugadoresOrigen);
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoDestinoId)).ReturnsAsync(new List<Participante>());
        _participanteRepo.Setup(r => r.AddAsync(It.IsAny<Participante>()))
            .ReturnsAsync((Participante p) => p);

        var sut = BuildSut();
        var result = await sut.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId);

        result.Clonados.Should().Be(2);
        result.Omitidos.Should().Be(0);

        _participanteRepo.Verify(r => r.AddAsync(It.Is<Participante>(p =>
            p.TorneoId == TorneoDestinoId &&
            p.Rol == RolParticipante.Jugador &&
            p.Pago == false)), Times.Exactly(2));

        _unitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task ClonarJugadores_SkipsAlreadyEnrolled_Returns1Clonado1Omitido()
    {
        var torneoDestino = BuildTorneo(TorneoDestinoId);
        var torneoOrigen = BuildTorneo(TorneoOrigenId);

        var jugadoresOrigen = new List<Participante>
        {
            BuildParticipante(1, TorneoOrigenId, "userA"),
            BuildParticipante(2, TorneoOrigenId, "userB")
        };

        var participantesDestino = new List<Participante>
        {
            BuildParticipante(10, TorneoDestinoId, "userA")
        };

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneoDestino);
        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoOrigenId)).ReturnsAsync(torneoOrigen);
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoOrigenId)).ReturnsAsync(jugadoresOrigen);
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoDestinoId)).ReturnsAsync(participantesDestino);
        _participanteRepo.Setup(r => r.AddAsync(It.IsAny<Participante>()))
            .ReturnsAsync((Participante p) => p);

        var sut = BuildSut();
        var result = await sut.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId);

        result.Clonados.Should().Be(1);
        result.Omitidos.Should().Be(1);

        _participanteRepo.Verify(r => r.AddAsync(It.Is<Participante>(p => p.UserId == "userB")), Times.Once);
        _unitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task ClonarJugadores_AllAlreadyEnrolled_Returns0Clonados_SaveNeverCalled()
    {
        var torneoDestino = BuildTorneo(TorneoDestinoId);
        var torneoOrigen = BuildTorneo(TorneoOrigenId);

        var jugadoresOrigen = new List<Participante>
        {
            BuildParticipante(1, TorneoOrigenId, "userA")
        };

        var participantesDestino = new List<Participante>
        {
            BuildParticipante(10, TorneoDestinoId, "userA")
        };

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneoDestino);
        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoOrigenId)).ReturnsAsync(torneoOrigen);
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoOrigenId)).ReturnsAsync(jugadoresOrigen);
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoDestinoId)).ReturnsAsync(participantesDestino);

        var sut = BuildSut();
        var result = await sut.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId);

        result.Clonados.Should().Be(0);
        result.Omitidos.Should().Be(1);

        _participanteRepo.Verify(r => r.AddAsync(It.IsAny<Participante>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task ClonarJugadores_ExcludesAmbosAndOrganizadorRoles_OnlyJugadorCloned()
    {
        var torneoDestino = BuildTorneo(TorneoDestinoId);
        var torneoOrigen = BuildTorneo(TorneoOrigenId);

        var participantesOrigen = new List<Participante>
        {
            BuildParticipante(1, TorneoOrigenId, "userAmbos", RolParticipante.Ambos),
            BuildParticipante(2, TorneoOrigenId, "userOrg", RolParticipante.Organizador),
            BuildParticipante(3, TorneoOrigenId, "userA", RolParticipante.Jugador)
        };

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneoDestino);
        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoOrigenId)).ReturnsAsync(torneoOrigen);
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoOrigenId)).ReturnsAsync(participantesOrigen);
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoDestinoId)).ReturnsAsync(new List<Participante>());
        _participanteRepo.Setup(r => r.AddAsync(It.IsAny<Participante>()))
            .ReturnsAsync((Participante p) => p);

        var sut = BuildSut();
        var result = await sut.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId);

        result.Clonados.Should().Be(1);
        result.Omitidos.Should().Be(0);

        _participanteRepo.Verify(r => r.AddAsync(It.Is<Participante>(p => p.UserId == "userA")), Times.Once);
        _unitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task ClonarJugadores_DestinoNotOwnedByOrganizer_ThrowsUnauthorizedAccessException()
    {
        var torneoDestino = BuildTorneo(TorneoDestinoId, organizadorId: "other-user");

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneoDestino);

        var sut = BuildSut();
        var act = () => sut.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ClonarJugadores_OrigenNotOwnedByOrganizer_ThrowsUnauthorizedAccessException()
    {
        var torneoDestino = BuildTorneo(TorneoDestinoId);
        var torneoOrigen = BuildTorneo(TorneoOrigenId, organizadorId: "other-user");

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoDestinoId)).ReturnsAsync(torneoDestino);
        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoOrigenId)).ReturnsAsync(torneoOrigen);

        var sut = BuildSut();
        var act = () => sut.ClonarJugadoresAsync(TorneoDestinoId, TorneoOrigenId, OrganizadorId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ---------------------------------------------------------------------------
    // GetTorneosClonablesAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetTorneosClonables_ReturnsActivoAndFinalizado_ExcludesCurrentTorneo()
    {
        var torneos = new List<Torneo>
        {
            BuildTorneo(1, estado: EstadoTorneo.Activo),   // excluido
            BuildTorneo(2, estado: EstadoTorneo.Activo),
            BuildTorneo(3, estado: EstadoTorneo.Finalizado)
        };

        _torneoRepo.Setup(r => r.GetByOrganizadorIdAsync(OrganizadorId)).ReturnsAsync(torneos);

        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(2)).ReturnsAsync(new List<Participante>
        {
            BuildParticipante(1, 2, "userA")
        });
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(3)).ReturnsAsync(new List<Participante>
        {
            BuildParticipante(2, 3, "userB")
        });

        var sut = BuildSut();
        var result = await sut.GetTorneosClonablesAsync(excluirTorneoId: 1, OrganizadorId);

        result.Should().HaveCount(2);
        result.Select(t => t.Id).Should().Contain(new[] { 2, 3 });
        result.Select(t => t.Id).Should().NotContain(1);
    }

    [Fact]
    public async Task GetTorneosClonables_ExcludesPendienteAndCancelado()
    {
        var torneos = new List<Torneo>
        {
            BuildTorneo(10, estado: EstadoTorneo.Pendiente),
            BuildTorneo(11, estado: EstadoTorneo.Cancelado)
        };

        _torneoRepo.Setup(r => r.GetByOrganizadorIdAsync(OrganizadorId)).ReturnsAsync(torneos);

        var sut = BuildSut();
        var result = await sut.GetTorneosClonablesAsync(excluirTorneoId: 99, OrganizadorId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTorneosClonables_CantidadJugadores_CountsOnlyRolJugador()
    {
        var torneos = new List<Torneo>
        {
            BuildTorneo(2, estado: EstadoTorneo.Activo)
        };

        var participantes = new List<Participante>
        {
            BuildParticipante(1, 2, "userAmbos", RolParticipante.Ambos),
            BuildParticipante(2, 2, "userA", RolParticipante.Jugador),
            BuildParticipante(3, 2, "userB", RolParticipante.Jugador),
            BuildParticipante(4, 2, "userOrg", RolParticipante.Organizador)
        };

        _torneoRepo.Setup(r => r.GetByOrganizadorIdAsync(OrganizadorId)).ReturnsAsync(torneos);
        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(2)).ReturnsAsync(participantes);

        var sut = BuildSut();
        var result = await sut.GetTorneosClonablesAsync(excluirTorneoId: 99, OrganizadorId);

        result.Should().HaveCount(1);
        result[0].CantidadJugadores.Should().Be(2);
    }

    [Fact]
    public async Task GetTorneosClonables_NoOtherTorneos_ReturnsEmpty()
    {
        var torneos = new List<Torneo>
        {
            BuildTorneo(1, estado: EstadoTorneo.Activo)
        };

        _torneoRepo.Setup(r => r.GetByOrganizadorIdAsync(OrganizadorId)).ReturnsAsync(torneos);

        var sut = BuildSut();
        var result = await sut.GetTorneosClonablesAsync(excluirTorneoId: 1, OrganizadorId);

        result.Should().BeEmpty();
    }
}
