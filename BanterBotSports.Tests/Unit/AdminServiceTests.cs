using BanterBotSports.BL;
using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for AdminService: configuration management and user (organizer/player) operations.
/// Uses EF InMemory provider and a mocked UserManager.
/// </summary>
public class AdminServiceTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static AppDbContext BuildDb(string dbName)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(opts);
    }

    private static Mock<UserManager<AppUser>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<AppUser>>();
        return new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static AppUser BuildUser(string id, string? phone = null, string? nombre = null, DateTimeOffset? lockoutEnd = null)
        => new() { Id = id, UserName = phone ?? id, PhoneNumber = phone, NombreDisplay = nombre, LockoutEnd = lockoutEnd };

    private static Torneo BuildTorneo(int id, string orgId, EstadoTorneo estado = EstadoTorneo.Activo)
        => new() { Id = id, Nombre = $"Torneo {id}", OrganizadorId = orgId, Estado = estado };

    // ─── GetConfiguracion ────────────────────────────────────────────────────

    [Fact]
    public async Task GetConfiguracion_ExistingRow_ReturnsIt()
    {
        await using var db = BuildDb(nameof(GetConfiguracion_ExistingRow_ReturnsIt));
        var config = new ConfiguracionGlobal { Id = 1, PorcentajePlataforma = 15, PorcentajeOrganizadorMin = 8, PorcentajeOrganizadorMax = 25, MontoInscripcionMinimo = 300 };
        db.ConfiguracionGlobal.Add(config);
        await db.SaveChangesAsync();

        var sut = new AdminService(db, BuildUserManagerMock().Object);
        var result = await sut.GetConfiguracionAsync();

        result.PorcentajePlataforma.Should().Be(15);
        result.MontoInscripcionMinimo.Should().Be(300);
    }

    [Fact]
    public async Task GetConfiguracion_EmptyDb_ReturnsDefaults()
    {
        await using var db = BuildDb(nameof(GetConfiguracion_EmptyDb_ReturnsDefaults));
        var sut = new AdminService(db, BuildUserManagerMock().Object);
        var result = await sut.GetConfiguracionAsync();

        result.Id.Should().Be(1);
        result.PorcentajePlataforma.Should().Be(10);
        result.PorcentajeOrganizadorMin.Should().Be(5);
        result.PorcentajeOrganizadorMax.Should().Be(30);
        result.MontoInscripcionMinimo.Should().Be(500);
    }

    // ─── UpdateConfiguracion ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateConfiguracion_ValidValues_Persists()
    {
        await using var db = BuildDb(nameof(UpdateConfiguracion_ValidValues_Persists));
        db.ConfiguracionGlobal.Add(new ConfiguracionGlobal { Id = 1, PorcentajePlataforma = 10, PorcentajeOrganizadorMin = 5, PorcentajeOrganizadorMax = 30, MontoInscripcionMinimo = 500 });
        await db.SaveChangesAsync();

        var sut = new AdminService(db, BuildUserManagerMock().Object);
        var updated = new ConfiguracionGlobal { Id = 1, PorcentajePlataforma = 12, PorcentajeOrganizadorMin = 6, PorcentajeOrganizadorMax = 28, MontoInscripcionMinimo = 600 };
        await sut.UpdateConfiguracionAsync(updated);

        var saved = await db.ConfiguracionGlobal.FindAsync(1);
        saved!.PorcentajePlataforma.Should().Be(12);
        saved.MontoInscripcionMinimo.Should().Be(600);
    }

    [Fact]
    public async Task UpdateConfiguracion_OrgMinGreaterThanOrgMax_ThrowsArgumentException()
    {
        await using var db = BuildDb(nameof(UpdateConfiguracion_OrgMinGreaterThanOrgMax_ThrowsArgumentException));
        var sut = new AdminService(db, BuildUserManagerMock().Object);
        var bad = new ConfiguracionGlobal { Id = 1, PorcentajePlataforma = 10, PorcentajeOrganizadorMin = 40, PorcentajeOrganizadorMax = 20, MontoInscripcionMinimo = 500 };

        await sut.Invoking(s => s.UpdateConfiguracionAsync(bad))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateConfiguracion_PorcentajePlataformaOver50_ThrowsArgumentException()
    {
        await using var db = BuildDb(nameof(UpdateConfiguracion_PorcentajePlataformaOver50_ThrowsArgumentException));
        var sut = new AdminService(db, BuildUserManagerMock().Object);
        var bad = new ConfiguracionGlobal { Id = 1, PorcentajePlataforma = 51, PorcentajeOrganizadorMin = 5, PorcentajeOrganizadorMax = 30, MontoInscripcionMinimo = 500 };

        await sut.Invoking(s => s.UpdateConfiguracionAsync(bad))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateConfiguracion_AnyValueZeroOrNegative_ThrowsArgumentException()
    {
        await using var db = BuildDb(nameof(UpdateConfiguracion_AnyValueZeroOrNegative_ThrowsArgumentException));
        var sut = new AdminService(db, BuildUserManagerMock().Object);
        var bad = new ConfiguracionGlobal { Id = 1, PorcentajePlataforma = 0, PorcentajeOrganizadorMin = 5, PorcentajeOrganizadorMax = 30, MontoInscripcionMinimo = 500 };

        await sut.Invoking(s => s.UpdateConfiguracionAsync(bad))
            .Should().ThrowAsync<ArgumentException>();
    }

    // ─── GetOrganizadores ────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrganizadores_OnlyUsersWithTorneos()
    {
        await using var db = BuildDb(nameof(GetOrganizadores_OnlyUsersWithTorneos));
        var userWithTorneo = BuildUser("org1", "5551111111");
        var userWithoutTorneo = BuildUser("org2", "5552222222");
        db.Users.AddRange(userWithTorneo, userWithoutTorneo);
        db.Torneos.Add(BuildTorneo(1, "org1"));
        await db.SaveChangesAsync();

        var sut = new AdminService(db, BuildUserManagerMock().Object);
        var result = await sut.GetOrganizadoresAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("org1");
    }

    // ─── UpdateOrganizador ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateOrganizador_SavesNombreDisplayAndEmail()
    {
        await using var db = BuildDb(nameof(UpdateOrganizador_SavesNombreDisplayAndEmail));
        var um = BuildUserManagerMock();
        var user = BuildUser("org1", "5551111111", "OldName");
        um.Setup(m => m.FindByIdAsync("org1")).ReturnsAsync(user);
        um.Setup(m => m.UpdateAsync(It.IsAny<AppUser>())).ReturnsAsync(IdentityResult.Success);

        var sut = new AdminService(db, um.Object);
        await sut.UpdateOrganizadorAsync("org1", new AdminUserEditDto("NewName", "new@email.com"));

        user.NombreDisplay.Should().Be("NewName");
        user.Email.Should().Be("new@email.com");
        um.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    // ─── DeactivateOrganizador ───────────────────────────────────────────────

    [Fact]
    public async Task DeactivateOrganizador_NoActiveTorneos_SetsLockoutMax()
    {
        await using var db = BuildDb(nameof(DeactivateOrganizador_NoActiveTorneos_SetsLockoutMax));
        db.Torneos.Add(BuildTorneo(1, "org1", EstadoTorneo.Finalizado));
        await db.SaveChangesAsync();

        var um = BuildUserManagerMock();
        var user = BuildUser("org1");
        um.Setup(m => m.FindByIdAsync("org1")).ReturnsAsync(user);
        um.Setup(m => m.UpdateAsync(It.IsAny<AppUser>())).ReturnsAsync(IdentityResult.Success);

        var sut = new AdminService(db, um.Object);
        await sut.DeactivateOrganizadorAsync("org1");

        user.LockoutEnd.Should().Be(DateTimeOffset.MaxValue);
        user.LockoutEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateOrganizador_WithActiveTorneo_ThrowsInvalidOperation()
    {
        await using var db = BuildDb(nameof(DeactivateOrganizador_WithActiveTorneo_ThrowsInvalidOperation));
        db.Torneos.Add(BuildTorneo(1, "org1", EstadoTorneo.Activo));
        await db.SaveChangesAsync();

        var sut = new AdminService(db, BuildUserManagerMock().Object);

        await sut.Invoking(s => s.DeactivateOrganizadorAsync("org1"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeactivateOrganizador_WithPendienteTorneo_ThrowsInvalidOperation()
    {
        await using var db = BuildDb(nameof(DeactivateOrganizador_WithPendienteTorneo_ThrowsInvalidOperation));
        db.Torneos.Add(BuildTorneo(1, "org1", EstadoTorneo.Pendiente));
        await db.SaveChangesAsync();

        var sut = new AdminService(db, BuildUserManagerMock().Object);

        await sut.Invoking(s => s.DeactivateOrganizadorAsync("org1"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // ─── ReactivateUser ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReactivateUser_ClearsLockoutEnd()
    {
        await using var db = BuildDb(nameof(ReactivateUser_ClearsLockoutEnd));
        var um = BuildUserManagerMock();
        var user = BuildUser("user1", lockoutEnd: DateTimeOffset.MaxValue);
        um.Setup(m => m.FindByIdAsync("user1")).ReturnsAsync(user);
        um.Setup(m => m.UpdateAsync(It.IsAny<AppUser>())).ReturnsAsync(IdentityResult.Success);

        var sut = new AdminService(db, um.Object);
        await sut.ReactivateUserAsync("user1");

        user.LockoutEnd.Should().BeNull();
        um.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    // ─── GetJugadores ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetJugadores_ExcludesAdminRoleUsers()
    {
        await using var db = BuildDb(nameof(GetJugadores_ExcludesAdminRoleUsers));
        var adminUser = BuildUser("admin1", "5550000000");
        var jugadorUser = BuildUser("jug1", "5551111111");
        db.Users.AddRange(adminUser, jugadorUser);
        await db.SaveChangesAsync();

        var um = BuildUserManagerMock();
        um.Setup(m => m.GetUsersInRoleAsync(AppRoles.Admin))
            .ReturnsAsync(new List<AppUser> { adminUser });

        var sut = new AdminService(db, um.Object);
        var result = await sut.GetJugadoresAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("jug1");
    }

    [Fact]
    public async Task GetJugadores_SearchByPhone_FiltersCorrectly()
    {
        await using var db = BuildDb(nameof(GetJugadores_SearchByPhone_FiltersCorrectly));
        var user1 = BuildUser("jug1", "5551234567");
        var user2 = BuildUser("jug2", "5559876543");
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        var um = BuildUserManagerMock();
        um.Setup(m => m.GetUsersInRoleAsync(AppRoles.Admin))
            .ReturnsAsync(new List<AppUser>());

        var sut = new AdminService(db, um.Object);
        var result = await sut.GetJugadoresAsync("1234");

        result.Should().HaveCount(1);
        result[0].Phone.Should().Be("5551234567");
    }

    [Fact]
    public async Task GetJugadores_SearchByNombreDisplay_CaseInsensitive()
    {
        await using var db = BuildDb(nameof(GetJugadores_SearchByNombreDisplay_CaseInsensitive));
        var user1 = BuildUser("jug1", "5551111111", "Carlos Perez");
        var user2 = BuildUser("jug2", "5552222222", "Ana Lopez");
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        var um = BuildUserManagerMock();
        um.Setup(m => m.GetUsersInRoleAsync(AppRoles.Admin))
            .ReturnsAsync(new List<AppUser>());

        var sut = new AdminService(db, um.Object);
        var result = await sut.GetJugadoresAsync("carlos");

        result.Should().HaveCount(1);
        result[0].NombreDisplay.Should().Be("Carlos Perez");
    }

    // ─── DeactivateJugador ───────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateJugador_SetsLockoutMax()
    {
        await using var db = BuildDb(nameof(DeactivateJugador_SetsLockoutMax));
        var um = BuildUserManagerMock();
        var user = BuildUser("jug1");
        um.Setup(m => m.FindByIdAsync("jug1")).ReturnsAsync(user);
        um.Setup(m => m.UpdateAsync(It.IsAny<AppUser>())).ReturnsAsync(IdentityResult.Success);

        var sut = new AdminService(db, um.Object);
        await sut.DeactivateJugadorAsync("jug1");

        user.LockoutEnd.Should().Be(DateTimeOffset.MaxValue);
        user.LockoutEnabled.Should().BeTrue();
    }
}
