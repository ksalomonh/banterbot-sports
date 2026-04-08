using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.BL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Implements administrative operations: platform configuration and user management.
/// </summary>
public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public AdminService(AppDbContext db, UserManager<AppUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(userManager);
        _db = db;
        _userManager = userManager;
    }

    // ─── Configuracion ───────────────────────────────────────────────────────

    public async Task<ConfiguracionGlobal> GetConfiguracionAsync()
    {
        var config = await _db.ConfiguracionGlobal.FirstOrDefaultAsync(c => c.Id == 1);
        if (config is null)
        {
            config = new ConfiguracionGlobal
            {
                Id = 1,
                PorcentajePlataforma = 10,
                PorcentajeOrganizadorMin = 5,
                PorcentajeOrganizadorMax = 30,
                MontoInscripcionMinimo = 500
            };
        }
        return config;
    }

    public async Task UpdateConfiguracionAsync(ConfiguracionGlobal config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.PorcentajePlataforma <= 0 ||
            config.PorcentajeOrganizadorMin <= 0 ||
            config.PorcentajeOrganizadorMax <= 0 ||
            config.MontoInscripcionMinimo <= 0)
            throw new ArgumentException("Todos los valores deben ser mayores a cero.");

        if (config.PorcentajePlataforma > 50)
            throw new ArgumentException("El porcentaje de plataforma no puede superar el 50%.");

        if (config.PorcentajeOrganizadorMin > config.PorcentajeOrganizadorMax)
            throw new ArgumentException("El porcentaje mínimo del organizador no puede ser mayor al máximo.");

        var existing = await _db.ConfiguracionGlobal.FindAsync(config.Id);
        if (existing is not null)
        {
            existing.PorcentajePlataforma = config.PorcentajePlataforma;
            existing.PorcentajeOrganizadorMin = config.PorcentajeOrganizadorMin;
            existing.PorcentajeOrganizadorMax = config.PorcentajeOrganizadorMax;
            existing.MontoInscripcionMinimo = config.MontoInscripcionMinimo;
        }
        else
        {
            _db.ConfiguracionGlobal.Add(config);
        }
        await _db.SaveChangesAsync();
    }

    // ─── Organizadores ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AdminUserDto>> GetOrganizadoresAsync()
    {
        var organizadorIds = await _db.Torneos
            .Select(t => t.OrganizadorId)
            .Distinct()
            .ToListAsync();

        var users = await _db.Users
            .Where(u => organizadorIds.Contains(u.Id))
            .ToListAsync();

        var result = new List<AdminUserDto>();
        foreach (var user in users)
        {
            var torneosOrg = await _db.Torneos.CountAsync(t => t.OrganizadorId == user.Id);
            var torneosParticipados = await _db.Set<Participante>().CountAsync(p => p.UserId == user.Id);
            var isActive = user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow;
            result.Add(new AdminUserDto(user.Id, user.PhoneNumber, user.Email, user.NombreDisplay, isActive, torneosOrg, torneosParticipados, user.LockoutEnd));
        }
        return result;
    }

    public async Task<AdminUserDto?> GetOrganizadorAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var torneosOrg = await _db.Torneos.CountAsync(t => t.OrganizadorId == userId);
        var torneosParticipados = await _db.Set<Participante>().CountAsync(p => p.UserId == userId);
        var isActive = user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow;
        return new AdminUserDto(user.Id, user.PhoneNumber, user.Email, user.NombreDisplay, isActive, torneosOrg, torneosParticipados, user.LockoutEnd);
    }

    public async Task UpdateOrganizadorAsync(string userId, AdminUserEditDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Usuario '{userId}' no encontrado.");
        user.NombreDisplay = dto.NombreDisplay;
        user.Email = dto.Email;
        user.UserName = dto.Email;
        await _userManager.UpdateAsync(user);
    }

    public async Task DeactivateOrganizadorAsync(string userId)
    {
        var hasActiveTorneos = await _db.Torneos.AnyAsync(
            t => t.OrganizadorId == userId &&
                 (t.Estado == EstadoTorneo.Activo || t.Estado == EstadoTorneo.Pendiente));

        if (hasActiveTorneos)
            throw new InvalidOperationException(
                "No se puede desactivar al organizador mientras tiene torneos activos o pendientes.");

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Usuario '{userId}' no encontrado.");

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);
    }

    public async Task ReactivateUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Usuario '{userId}' no encontrado.");

        user.LockoutEnd = null;
        await _userManager.UpdateAsync(user);
    }

    // ─── Jugadores ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AdminUserDto>> GetJugadoresAsync(string? search = null)
    {
        var adminUsers = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
        var adminIds = adminUsers.Select(u => u.Id).ToHashSet();

        IQueryable<AppUser> query = _db.Users.Where(u => !adminIds.Contains(u.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var dbName = _db.Database.ProviderName ?? string.Empty;
            if (dbName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u =>
                    EF.Functions.ILike(u.PhoneNumber!, $"%{search}%") ||
                    EF.Functions.ILike(u.NombreDisplay!, $"%{search}%"));
            }
            else
            {
                var lowerSearch = search.ToLower();
                query = query.Where(u =>
                    (u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(lowerSearch)) ||
                    (u.NombreDisplay != null && u.NombreDisplay.ToLower().Contains(lowerSearch)));
            }
        }

        var users = await query.ToListAsync();
        var result = new List<AdminUserDto>();
        foreach (var user in users)
        {
            var torneosOrg = await _db.Torneos.CountAsync(t => t.OrganizadorId == user.Id);
            var torneosParticipados = await _db.Set<Participante>().CountAsync(p => p.UserId == user.Id);
            var isActive = user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow;
            result.Add(new AdminUserDto(user.Id, user.PhoneNumber, user.Email, user.NombreDisplay, isActive, torneosOrg, torneosParticipados, user.LockoutEnd));
        }
        return result;
    }

    public async Task<AdminUserDto?> GetJugadorAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var torneosOrg = await _db.Torneos.CountAsync(t => t.OrganizadorId == userId);
        var torneosParticipados = await _db.Set<Participante>().CountAsync(p => p.UserId == userId);
        var isActive = user.LockoutEnd == null || user.LockoutEnd < DateTimeOffset.UtcNow;
        return new AdminUserDto(user.Id, user.PhoneNumber, user.Email, user.NombreDisplay, isActive, torneosOrg, torneosParticipados, user.LockoutEnd);
    }

    public async Task UpdateJugadorAsync(string userId, AdminUserEditDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Usuario '{userId}' no encontrado.");
        user.NombreDisplay = dto.NombreDisplay;
        user.Email = dto.Email;
        user.UserName = dto.Email;
        await _userManager.UpdateAsync(user);
    }

    public async Task DeactivateJugadorAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Usuario '{userId}' no encontrado.");

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);
    }
}
