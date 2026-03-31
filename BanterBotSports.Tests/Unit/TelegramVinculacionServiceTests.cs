using BanterBotSports.BL.Services;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for TelegramVinculacionService.GetJornadaAbiertaParaUsuarioAsync.
///
/// Verifies the multi-torneo fix: when a user participates in multiple torneos,
/// the method picks the open jornada with the highest Id (most recently opened).
/// </summary>
public class TelegramVinculacionServiceTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TelegramVinculacionService BuildSut(
        Mock<IParticipanteRepository> participanteMock,
        Mock<IJornadaRepository> jornadaMock)
    {
        var usuarioTelegramRepoMock = new Mock<IUsuarioTelegramRepository>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();

#pragma warning disable CS8625
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var userManager = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

        return new TelegramVinculacionService(
            usuarioTelegramRepoMock.Object,
            participanteMock.Object,
            jornadaMock.Object,
            userManager.Object,
            unitOfWorkMock.Object,
            NullLogger<TelegramVinculacionService>.Instance);
    }

    private static Participante MakeParticipante(int id, int torneoId, string userId = "user-1")
        => new() { Id = id, TorneoId = torneoId, UserId = userId, Rol = RolParticipante.Jugador };

    private static Jornada MakeJornada(int id, int torneoId)
        => new() { Id = id, TorneoId = torneoId, Numero = 1, Estado = EstadoJornada.Abierta };

    // ---------------------------------------------------------------------------
    // 3.1.1 — Single torneo: open jornada found → returns (jornada, participante)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetJornadaAbiertaParaUsuario_SingleTorneo_ReturnsOpenJornada()
    {
        // Arrange
        const string userId = "user-1";
        var participante = MakeParticipante(id: 10, torneoId: 1);
        var jornada = MakeJornada(id: 100, torneoId: 1);
        var jornadaDetallada = MakeJornada(id: 100, torneoId: 1);

        var participanteMock = new Mock<IParticipanteRepository>();
        participanteMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new[] { participante });

        var jornadaMock = new Mock<IJornadaRepository>();
        jornadaMock
            .Setup(r => r.GetByTorneoAndEstadoAsync(1, EstadoJornada.Abierta))
            .ReturnsAsync(jornada);
        jornadaMock
            .Setup(r => r.GetByIdWithDetailsAsync(100))
            .ReturnsAsync(jornadaDetallada);

        var sut = BuildSut(participanteMock, jornadaMock);

        // Act
        var result = await sut.GetJornadaAbiertaParaUsuarioAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Value.jornada.Id.Should().Be(100);
        result.Value.participante.Id.Should().Be(10);
    }

    // ---------------------------------------------------------------------------
    // 3.1.2 — Multiple torneos, only one has open jornada → returns that one
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetJornadaAbiertaParaUsuario_MultipleTorneos_OnlyOneOpen_ReturnsThatOne()
    {
        // Arrange
        const string userId = "user-1";
        var participante1 = MakeParticipante(id: 10, torneoId: 1);
        var participante2 = MakeParticipante(id: 20, torneoId: 2);
        var jornadaOpen = MakeJornada(id: 50, torneoId: 2);
        var jornadaDetallada = MakeJornada(id: 50, torneoId: 2);

        var participanteMock = new Mock<IParticipanteRepository>();
        participanteMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new[] { participante1, participante2 });

        var jornadaMock = new Mock<IJornadaRepository>();
        // Torneo 1 has no open jornada
        jornadaMock
            .Setup(r => r.GetByTorneoAndEstadoAsync(1, EstadoJornada.Abierta))
            .ReturnsAsync((Jornada?)null);
        // Torneo 2 has an open jornada
        jornadaMock
            .Setup(r => r.GetByTorneoAndEstadoAsync(2, EstadoJornada.Abierta))
            .ReturnsAsync(jornadaOpen);
        jornadaMock
            .Setup(r => r.GetByIdWithDetailsAsync(50))
            .ReturnsAsync(jornadaDetallada);

        var sut = BuildSut(participanteMock, jornadaMock);

        // Act
        var result = await sut.GetJornadaAbiertaParaUsuarioAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Value.jornada.Id.Should().Be(50);
        result.Value.participante.Id.Should().Be(20);
    }

    // ---------------------------------------------------------------------------
    // 3.1.3 — Multiple torneos, both open → returns jornada with highest Id
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetJornadaAbiertaParaUsuario_MultipleTorneos_BothOpen_PicksHighestId()
    {
        // Arrange
        const string userId = "user-1";
        var participante1 = MakeParticipante(id: 10, torneoId: 1);
        var participante2 = MakeParticipante(id: 20, torneoId: 2);
        var jornada1 = MakeJornada(id: 40, torneoId: 1); // older
        var jornada2 = MakeJornada(id: 80, torneoId: 2); // newer (higher Id)
        var jornadaDetallada2 = MakeJornada(id: 80, torneoId: 2);

        var participanteMock = new Mock<IParticipanteRepository>();
        participanteMock
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new[] { participante1, participante2 });

        var jornadaMock = new Mock<IJornadaRepository>();
        jornadaMock
            .Setup(r => r.GetByTorneoAndEstadoAsync(1, EstadoJornada.Abierta))
            .ReturnsAsync(jornada1);
        jornadaMock
            .Setup(r => r.GetByTorneoAndEstadoAsync(2, EstadoJornada.Abierta))
            .ReturnsAsync(jornada2);
        jornadaMock
            .Setup(r => r.GetByIdWithDetailsAsync(80))
            .ReturnsAsync(jornadaDetallada2);

        var sut = BuildSut(participanteMock, jornadaMock);

        // Act
        var result = await sut.GetJornadaAbiertaParaUsuarioAsync(userId);

        // Assert — must pick jornada with Id=80 (the most recent one)
        result.Should().NotBeNull();
        result!.Value.jornada.Id.Should().Be(80, "highest Id wins when multiple jornadas are open");
        result.Value.participante.Id.Should().Be(20, "participante for the winning torneo");
    }
}
