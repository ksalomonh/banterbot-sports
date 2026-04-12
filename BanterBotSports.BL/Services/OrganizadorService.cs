using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Implements organizer-specific configuration: reading and updating the per-user organizer percentage.
/// </summary>
public class OrganizadorService : IOrganizadorService
{
    private readonly IAdminService _adminService;
    private readonly UserManager<AppUser> _userManager;

    public OrganizadorService(IAdminService adminService, UserManager<AppUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(adminService);
        ArgumentNullException.ThrowIfNull(userManager);
        _adminService = adminService;
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<ConfiguracionOrganizadorViewModel> GetConfiguracionAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Usuario '{userId}' no encontrado.");

        var config = await _adminService.GetConfiguracionAsync();

        return new ConfiguracionOrganizadorViewModel
        {
            PorcentajeOrganizadorGlobal = user.PorcentajeOrganizadorGlobal,
            PorcentajeMinimo = config.PorcentajeOrganizadorMin,
            PorcentajeMaximo = config.PorcentajeOrganizadorMax,
            PorcentajePlataforma = config.PorcentajePlataforma
        };
    }

    /// <inheritdoc />
    public async Task UpdateConfiguracionAsync(string userId, decimal porcentaje)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var config = await _adminService.GetConfiguracionAsync();

        if (porcentaje < config.PorcentajeOrganizadorMin)
            throw new ArgumentException($"El porcentaje debe ser al menos el mínimo permitido ({config.PorcentajeOrganizadorMin}%)");

        if (porcentaje > config.PorcentajeOrganizadorMax)
            throw new ArgumentException($"El porcentaje no puede superar el máximo permitido ({config.PorcentajeOrganizadorMax}%)");

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Usuario '{userId}' no encontrado.");

        user.PorcentajeOrganizadorGlobal = porcentaje;
        await _userManager.UpdateAsync(user);
    }
}
