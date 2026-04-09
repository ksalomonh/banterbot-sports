using BanterBotSports.BL;
using BanterBotSports.BL.Services;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Entities.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for TorneoService organizer-percentage resolution in CrearTorneoAsync:
/// - Percentage override from model
/// - Fallback to user global
/// - Fallback to config minimum
/// - Validation (above max, below min)
/// - Prize sum validation against dynamic pool
/// - Organizador role assignment
/// </summary>
public class TorneoServiceOrganizadorTests
{
    private const string OrganizadorId = "org-user-id";

    private readonly Mock<ITorneoRepository> _torneoRepo = new();
    private readonly Mock<IParticipanteRepository> _participanteRepo = new();
    private readonly Mock<IJornadaRepository> _jornadaRepo = new();
    private readonly Mock<IPrediccionRepository> _prediccionRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAdminService> _adminService = new();
    private readonly Mock<UserManager<AppUser>> _userManager;

    public TorneoServiceOrganizadorTests()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManager = new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Default admin config: platform=10, min=5, max=30
        _adminService.Setup(s => s.GetConfiguracionAsync())
            .ReturnsAsync(new ConfiguracionGlobal
            {
                Id = 1,
                PorcentajePlataforma = 10m,
                PorcentajeOrganizadorMin = 5m,
                PorcentajeOrganizadorMax = 30m,
                MontoInscripcionMinimo = 500m
            });

        // Default: user exists with no global percentage set
        _userManager.Setup(m => m.FindByIdAsync(OrganizadorId))
            .ReturnsAsync(new AppUser { Id = OrganizadorId, PorcentajeOrganizadorGlobal = null });

        // Default: user is NOT in Organizador role
        _userManager.Setup(m => m.IsInRoleAsync(It.IsAny<AppUser>(), AppRoles.Organizador))
            .ReturnsAsync(false);

        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), AppRoles.Organizador))
            .ReturnsAsync(IdentityResult.Success);

        // Default repo behaviors
        _torneoRepo.Setup(r => r.AddAsync(It.IsAny<Torneo>()))
            .ReturnsAsync((Torneo t) => { t.Id = 1; return t; });
        _participanteRepo.Setup(r => r.AddAsync(It.IsAny<Participante>()))
            .Returns(Task.CompletedTask);
        _jornadaRepo.Setup(r => r.AddAsync(It.IsAny<Jornada>()))
            .Returns(Task.CompletedTask);
    }

    private TorneoService BuildSut() => new(
        _torneoRepo.Object,
        _participanteRepo.Object,
        _jornadaRepo.Object,
        _prediccionRepo.Object,
        _unitOfWork.Object,
        _adminService.Object,
        _userManager.Object);

    /// <summary>Builds a valid model with prizes summing to the given expected pool.</summary>
    private static TorneoCreateViewModel BuildModel(decimal? orgOverride = null, decimal prizeSum = 85m)
        => new()
        {
            Nombre = "Test Torneo",
            NumJornadas = 1,
            MontoInscripcion = 1000m,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2,
            PorcentajeOrganizador = orgOverride,
            ConfiguracionPremios = new List<ConfiguracionPremioViewModel>
            {
                new() { Posicion = 1, Porcentaje = prizeSum }
            }
        };

    // ─── Percentage resolution chain ─────────────────────────────────────────

    [Fact]
    public async Task CrearTorneo_ResolvesOverride_WhenProvided()
    {
        // Override=20 → pool=100-10-20=70 → prizes must be 70
        var model = BuildModel(orgOverride: 20m, prizeSum: 70m);
        var sut = BuildSut();

        var torneo = await sut.CrearTorneoAsync(model, OrganizadorId);

        torneo.PorcentajeOrganizador.Should().Be(20m);
    }

    [Fact]
    public async Task CrearTorneo_FallsBackToGlobal_WhenNoOverride()
    {
        // User global=15 → pool=100-10-15=75 → prizes must be 75
        _userManager.Setup(m => m.FindByIdAsync(OrganizadorId))
            .ReturnsAsync(new AppUser { Id = OrganizadorId, PorcentajeOrganizadorGlobal = 15m });

        var model = BuildModel(orgOverride: null, prizeSum: 75m);
        var sut = BuildSut();

        var torneo = await sut.CrearTorneoAsync(model, OrganizadorId);

        torneo.PorcentajeOrganizador.Should().Be(15m);
    }

    [Fact]
    public async Task CrearTorneo_FallsBackToMin_WhenNeitherSet()
    {
        // No override, no global → fallback to config.min=5 → pool=100-10-5=85
        var model = BuildModel(orgOverride: null, prizeSum: 85m);
        var sut = BuildSut();

        var torneo = await sut.CrearTorneoAsync(model, OrganizadorId);

        torneo.PorcentajeOrganizador.Should().Be(5m);
    }

    [Fact]
    public async Task CrearTorneo_RejectsOverride_WhenAboveMax()
    {
        // Override=35, max=30 → must throw
        var model = BuildModel(orgOverride: 35m, prizeSum: 55m);
        var sut = BuildSut();

        var act = () => sut.CrearTorneoAsync(model, OrganizadorId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*porcentaje*");
    }

    [Fact]
    public async Task CrearTorneo_RejectsOverride_WhenBelowMin()
    {
        // Override=2, min=5 → must throw
        var model = BuildModel(orgOverride: 2m, prizeSum: 88m);
        var sut = BuildSut();

        var act = () => sut.CrearTorneoAsync(model, OrganizadorId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*porcentaje*");
    }

    // ─── Prize sum validation against dynamic pool ───────────────────────────

    [Fact]
    public async Task CrearTorneo_ValidatesPrizeSum_AgainstPool()
    {
        // platform=10, organizer=5 → pool=85, but prizes=70 (wrong) → throws
        var model = BuildModel(orgOverride: null, prizeSum: 70m);
        var sut = BuildSut();

        var act = () => sut.CrearTorneoAsync(model, OrganizadorId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*85%*");
    }

    // ─── Role assignment ─────────────────────────────────────────────────────

    [Fact]
    public async Task CrearTorneo_AssignsOrganizadorRole_WhenNotAlreadyAssigned()
    {
        // User is NOT in Organizador role → AddToRoleAsync must be called
        var model = BuildModel(orgOverride: null, prizeSum: 85m);
        var sut = BuildSut();

        await sut.CrearTorneoAsync(model, OrganizadorId);

        _userManager.Verify(m => m.AddToRoleAsync(
            It.Is<AppUser>(u => u.Id == OrganizadorId),
            AppRoles.Organizador), Times.Once);
    }

    [Fact]
    public async Task CrearTorneo_IsIdempotent_WhenRoleAlreadyExists()
    {
        // User already has Organizador role → AddToRoleAsync must NOT be called
        _userManager.Setup(m => m.IsInRoleAsync(It.IsAny<AppUser>(), AppRoles.Organizador))
            .ReturnsAsync(true);

        var model = BuildModel(orgOverride: null, prizeSum: 85m);
        var sut = BuildSut();

        await sut.CrearTorneoAsync(model, OrganizadorId);

        _userManager.Verify(m => m.AddToRoleAsync(It.IsAny<AppUser>(), AppRoles.Organizador), Times.Never);
    }
}
