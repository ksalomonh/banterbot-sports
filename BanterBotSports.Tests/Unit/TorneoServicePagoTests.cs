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
/// Unit tests for TorneoService payment-related methods:
/// ConfirmarPagoAsync, RevocarPagoAsync, DarDeBajaImpagosAsync.
/// </summary>
public class TorneoServicePagoTests
{
    private const string OrganizadorId = "org-user-id";
    private const int TorneoId = 1;

    private readonly Mock<ITorneoRepository> _torneoRepo = new();
    private readonly Mock<IParticipanteRepository> _participanteRepo = new();
    private readonly Mock<IJornadaRepository> _jornadaRepo = new();
    private readonly Mock<IPrediccionRepository> _prediccionRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAdminService> _adminService = new();
    private readonly Mock<UserManager<AppUser>> _userManager;

    public TorneoServicePagoTests()
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

    private Torneo BuildTorneo() => new()
    {
        Id = TorneoId,
        Nombre = "Test Torneo",
        OrganizadorId = OrganizadorId
    };

    // ---------------------------------------------------------------------------
    // ConfirmarPagoAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConfirmarPago_HappyPath_SetsPagoTrue()
    {
        var torneo = BuildTorneo();
        var participante = new Participante { Id = 10, TorneoId = TorneoId, UserId = "player-1", Pago = false, Rol = RolParticipante.Jugador };

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);
        _participanteRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(participante);

        var sut = BuildSut();
        await sut.ConfirmarPagoAsync(TorneoId, 10, OrganizadorId);

        participante.Pago.Should().BeTrue();
        _participanteRepo.Verify(r => r.UpdateAsync(participante), Times.Once);
        _unitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task ConfirmarPago_AlreadyPaid_IsIdempotent()
    {
        var torneo = BuildTorneo();
        var participante = new Participante { Id = 10, TorneoId = TorneoId, UserId = "player-1", Pago = true, Rol = RolParticipante.Jugador };

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);
        _participanteRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(participante);

        var sut = BuildSut();
        await sut.ConfirmarPagoAsync(TorneoId, 10, OrganizadorId);

        participante.Pago.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmarPago_NonOrganizer_ThrowsUnauthorized()
    {
        var torneo = BuildTorneo();
        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);

        var sut = BuildSut();
        var act = () => sut.ConfirmarPagoAsync(TorneoId, 10, "random-user");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ConfirmarPago_TorneoNotFound_ThrowsInvalidOperation()
    {
        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync((Torneo?)null);

        var sut = BuildSut();
        var act = () => sut.ConfirmarPagoAsync(TorneoId, 10, OrganizadorId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ConfirmarPago_ParticipanteNotFound_ThrowsInvalidOperation()
    {
        var torneo = BuildTorneo();
        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);
        _participanteRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Participante?)null);

        var sut = BuildSut();
        var act = () => sut.ConfirmarPagoAsync(TorneoId, 999, OrganizadorId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------------------------------------------------------------------------
    // RevocarPagoAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RevocarPago_HappyPath_SetsPagoFalse()
    {
        var torneo = BuildTorneo();
        var participante = new Participante { Id = 10, TorneoId = TorneoId, UserId = "player-1", Pago = true, Rol = RolParticipante.Jugador };

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);
        _participanteRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(participante);

        var sut = BuildSut();
        await sut.RevocarPagoAsync(TorneoId, 10, OrganizadorId);

        participante.Pago.Should().BeFalse();
        _participanteRepo.Verify(r => r.UpdateAsync(participante), Times.Once);
        _unitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task RevocarPago_OrganizerSelf_ThrowsInvalidOperation()
    {
        var torneo = BuildTorneo();
        var participante = new Participante { Id = 1, TorneoId = TorneoId, UserId = OrganizadorId, Pago = true, Rol = RolParticipante.Ambos };

        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);
        _participanteRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(participante);

        var sut = BuildSut();
        var act = () => sut.RevocarPagoAsync(TorneoId, 1, OrganizadorId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*organizador*");
    }

    [Fact]
    public async Task RevocarPago_NonOrganizer_ThrowsUnauthorized()
    {
        var torneo = BuildTorneo();
        _torneoRepo.Setup(r => r.GetByIdAsync(TorneoId)).ReturnsAsync(torneo);

        var sut = BuildSut();
        var act = () => sut.RevocarPagoAsync(TorneoId, 10, "random-user");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ---------------------------------------------------------------------------
    // DarDeBajaImpagosAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DarDeBajaImpagos_RemovesUnpaidAndDeletesPredictions()
    {
        var participantes = new List<Participante>
        {
            new() { Id = 1, TorneoId = TorneoId, UserId = OrganizadorId, Pago = true, Rol = RolParticipante.Ambos },
            new() { Id = 2, TorneoId = TorneoId, UserId = "player-a", Pago = true, Rol = RolParticipante.Jugador },
            new() { Id = 3, TorneoId = TorneoId, UserId = "player-b", Pago = false, Rol = RolParticipante.Jugador }
        };

        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoId))
            .ReturnsAsync(participantes);

        var sut = BuildSut();
        var removed = await sut.DarDeBajaImpagosAsync(TorneoId);

        removed.Should().Be(1);
        _prediccionRepo.Verify(r => r.DeleteByParticipanteIdAsync(3), Times.Once);
        _participanteRepo.Verify(r => r.DeleteAsync(It.Is<Participante>(p => p.Id == 3)), Times.Once);
        _participanteRepo.Verify(r => r.DeleteAsync(It.Is<Participante>(p => p.Id == 1)), Times.Never);
        _participanteRepo.Verify(r => r.DeleteAsync(It.Is<Participante>(p => p.Id == 2)), Times.Never);
        _unitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task DarDeBajaImpagos_AllPaid_ReturnsZeroAndDoesNotSave()
    {
        var participantes = new List<Participante>
        {
            new() { Id = 1, TorneoId = TorneoId, UserId = OrganizadorId, Pago = true, Rol = RolParticipante.Ambos },
            new() { Id = 2, TorneoId = TorneoId, UserId = "player-a", Pago = true, Rol = RolParticipante.Jugador }
        };

        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoId))
            .ReturnsAsync(participantes);

        var sut = BuildSut();
        var removed = await sut.DarDeBajaImpagosAsync(TorneoId);

        removed.Should().Be(0);
        _unitOfWork.Verify(u => u.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task DarDeBajaImpagos_OrganizerUnpaid_NeverRemovesOrganizer()
    {
        // Edge case: organizer somehow has Pago=false but Rol=Ambos — should NOT be removed
        var participantes = new List<Participante>
        {
            new() { Id = 1, TorneoId = TorneoId, UserId = OrganizadorId, Pago = false, Rol = RolParticipante.Ambos }
        };

        _participanteRepo.Setup(r => r.GetByTorneoIdAsync(TorneoId))
            .ReturnsAsync(participantes);

        var sut = BuildSut();
        var removed = await sut.DarDeBajaImpagosAsync(TorneoId);

        removed.Should().Be(0);
        _participanteRepo.Verify(r => r.DeleteAsync(It.IsAny<Participante>()), Times.Never);
    }
}
