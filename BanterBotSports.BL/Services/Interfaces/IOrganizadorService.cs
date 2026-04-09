using BanterBotSports.Entities.ViewModels;

namespace BanterBotSports.BL.Services.Interfaces;

/// <summary>
/// Provides organizer-specific configuration operations.
/// </summary>
public interface IOrganizadorService
{
    /// <summary>Returns the current organizer configuration for the given user, merged with global platform config.</summary>
    Task<ConfiguracionOrganizadorViewModel> GetConfiguracionAsync(string userId);

    /// <summary>Validates and persists a new organizer percentage for the given user.</summary>
    Task UpdateConfiguracionAsync(string userId, decimal porcentaje);
}
