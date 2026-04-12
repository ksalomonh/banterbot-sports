using BanterBotSports.BL.Services;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for OrganizadorService:
/// GetConfiguracionAsync and UpdateConfiguracionAsync.
/// </summary>
public class OrganizadorServiceTests
{
    private const string UserId = "org-user-id";

    private static Mock<UserManager<AppUser>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<AppUser>>();
        return new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<IAdminService> BuildAdminServiceMock(
        decimal plataforma = 10m,
        decimal min = 5m,
        decimal max = 30m)
    {
        var mock = new Mock<IAdminService>();
        mock.Setup(s => s.GetConfiguracionAsync())
            .ReturnsAsync(new ConfiguracionGlobal
            {
                Id = 1,
                PorcentajePlataforma = plataforma,
                PorcentajeOrganizadorMin = min,
                PorcentajeOrganizadorMax = max,
                MontoInscripcionMinimo = 500
            });
        return mock;
    }

    private static OrganizadorService BuildSut(
        Mock<IAdminService> adminSvc,
        Mock<UserManager<AppUser>> userManager)
        => new(adminSvc.Object, userManager.Object);

    // ─── GetConfiguracionAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetConfiguracion_ReturnsViewModel_WithCurrentUserValues()
    {
        // Arrange
        var user = new AppUser { Id = UserId, PorcentajeOrganizadorGlobal = 15m };
        var adminSvc = BuildAdminServiceMock(plataforma: 10m, min: 5m, max: 30m);
        var userMgr = BuildUserManagerMock();
        userMgr.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);

        var sut = BuildSut(adminSvc, userMgr);

        // Act
        var result = await sut.GetConfiguracionAsync(UserId);

        // Assert
        result.Should().NotBeNull();
        result.PorcentajeOrganizadorGlobal.Should().Be(15m);
        result.PorcentajeMinimo.Should().Be(5m);
        result.PorcentajeMaximo.Should().Be(30m);
        result.PorcentajePlataforma.Should().Be(10m);
    }

    [Fact]
    public async Task GetConfiguracion_ReturnsNull_WhenUserHasNoGlobal()
    {
        // Arrange: user with no PorcentajeOrganizadorGlobal set
        var user = new AppUser { Id = UserId, PorcentajeOrganizadorGlobal = null };
        var adminSvc = BuildAdminServiceMock();
        var userMgr = BuildUserManagerMock();
        userMgr.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);

        var sut = BuildSut(adminSvc, userMgr);

        // Act
        var result = await sut.GetConfiguracionAsync(UserId);

        // Assert: ViewModel is returned but global value is null
        result.Should().NotBeNull();
        result.PorcentajeOrganizadorGlobal.Should().BeNull();
    }

    [Fact]
    public async Task GetConfiguracion_UserNotFound_ThrowsInvalidOperation()
    {
        // Arrange
        var adminSvc = BuildAdminServiceMock();
        var userMgr = BuildUserManagerMock();
        userMgr.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync((AppUser?)null);

        var sut = BuildSut(adminSvc, userMgr);

        // Act
        var act = () => sut.GetConfiguracionAsync(UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*'{UserId}'*");
    }

    // ─── UpdateConfiguracionAsync ────────────────────────────────────────────

    [Fact]
    public async Task UpdateConfiguracion_ValidValue_SavesOnUser()
    {
        // Arrange
        var user = new AppUser { Id = UserId, PorcentajeOrganizadorGlobal = null };
        var adminSvc = BuildAdminServiceMock(min: 5m, max: 30m);
        var userMgr = BuildUserManagerMock();
        userMgr.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        userMgr.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = BuildSut(adminSvc, userMgr);

        // Act
        await sut.UpdateConfiguracionAsync(UserId, 20m);

        // Assert
        user.PorcentajeOrganizadorGlobal.Should().Be(20m);
        userMgr.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateConfiguracion_BelowMin_ThrowsArgumentException()
    {
        // Arrange: min=5, value=3
        var adminSvc = BuildAdminServiceMock(min: 5m, max: 30m);
        var userMgr = BuildUserManagerMock();

        var sut = BuildSut(adminSvc, userMgr);

        // Act
        var act = () => sut.UpdateConfiguracionAsync(UserId, 3m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("El porcentaje debe ser al menos el mínimo permitido (5%)");
    }

    [Fact]
    public async Task UpdateConfiguracion_AboveMax_ThrowsArgumentException()
    {
        // Arrange: max=30, value=35
        var adminSvc = BuildAdminServiceMock(min: 5m, max: 30m);
        var userMgr = BuildUserManagerMock();

        var sut = BuildSut(adminSvc, userMgr);

        // Act
        var act = () => sut.UpdateConfiguracionAsync(UserId, 35m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("El porcentaje no puede superar el máximo permitido (30%)");
    }

    [Fact]
    public async Task UpdateConfiguracion_AtBoundaries_Succeeds()
    {
        // Arrange: test both min and max boundary values
        var user = new AppUser { Id = UserId };
        var adminSvc = BuildAdminServiceMock(min: 5m, max: 30m);
        var userMgr = BuildUserManagerMock();
        userMgr.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        userMgr.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = BuildSut(adminSvc, userMgr);

        // Act: min boundary
        await sut.UpdateConfiguracionAsync(UserId, 5m);
        user.PorcentajeOrganizadorGlobal.Should().Be(5m);

        // Act: max boundary
        await sut.UpdateConfiguracionAsync(UserId, 30m);
        user.PorcentajeOrganizadorGlobal.Should().Be(30m);
    }
}
