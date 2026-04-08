using BanterBotSports.BL.Models;
using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services.Interfaces;

/// <summary>
/// Provides administrative operations for platform configuration and user management.
/// </summary>
public interface IAdminService
{
    /// <summary>Gets the global platform configuration (Id=1). Returns defaults if not seeded.</summary>
    Task<ConfiguracionGlobal> GetConfiguracionAsync();

    /// <summary>Validates and persists the global platform configuration.</summary>
    Task UpdateConfiguracionAsync(ConfiguracionGlobal config);

    /// <summary>Returns all users who have organised at least one torneo.</summary>
    Task<IReadOnlyList<AdminUserDto>> GetOrganizadoresAsync();

    /// <summary>Returns a single organiser by user ID, or null if not found.</summary>
    Task<AdminUserDto?> GetOrganizadorAsync(string userId);

    /// <summary>Updates NombreDisplay and Email for an organiser.</summary>
    Task UpdateOrganizadorAsync(string userId, AdminUserEditDto dto);

    /// <summary>Locks an organiser account. Throws <see cref="InvalidOperationException"/> if they have active/pending torneos.</summary>
    Task DeactivateOrganizadorAsync(string userId);

    /// <summary>Clears lockout on any user (organiser or player).</summary>
    Task ReactivateUserAsync(string userId);

    /// <summary>Returns all non-admin users, optionally filtered by phone or display name.</summary>
    Task<IReadOnlyList<AdminUserDto>> GetJugadoresAsync(string? search = null);

    /// <summary>Returns a single player by user ID, or null if not found.</summary>
    Task<AdminUserDto?> GetJugadorAsync(string userId);

    /// <summary>Updates NombreDisplay and Email for a player.</summary>
    Task UpdateJugadorAsync(string userId, AdminUserEditDto dto);

    /// <summary>Locks a player account.</summary>
    Task DeactivateJugadorAsync(string userId);
}
