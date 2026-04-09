using BanterBotSports.BL;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BanterBotSports.Web.Controllers;

/// <summary>
/// Organizer panel: personal configuration (organizer percentage, etc.).
/// Restricted to Organizador role.
/// </summary>
[Authorize(Roles = AppRoles.Organizador)]
[Route("organizador")]
public class OrganizadorController : Controller
{
    private readonly IOrganizadorService _organizadorService;
    private readonly UserManager<DAL.AppUser> _userManager;

    public OrganizadorController(
        IOrganizadorService organizadorService,
        UserManager<DAL.AppUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(organizadorService);
        ArgumentNullException.ThrowIfNull(userManager);

        _organizadorService = organizadorService;
        _userManager = userManager;
    }

    // GET /organizador/configuracion
    [HttpGet("configuracion")]
    public async Task<IActionResult> Configuracion()
    {
        var userId = _userManager.GetUserId(User)!;
        var vm = await _organizadorService.GetConfiguracionAsync(userId);
        return View(vm);
    }

    // POST /organizador/configuracion
    [HttpPost("configuracion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configuracion(ConfiguracionOrganizadorViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = _userManager.GetUserId(User)!;

        try
        {
            await _organizadorService.UpdateConfiguracionAsync(userId, model.PorcentajeOrganizadorGlobal ?? 0m);
            TempData[TempDataKeys.Success] = "Configuración guardada correctamente.";
            return RedirectToAction(nameof(Configuracion));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
