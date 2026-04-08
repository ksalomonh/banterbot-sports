using BanterBotSports.BL;
using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BanterBotSports.Web.Controllers;

/// <summary>
/// Administrative panel: platform configuration and user management (organizers + players).
/// Restricted to Admin role.
/// </summary>
[Authorize(Roles = AppRoles.Admin)]
[Route("admin")]
public class AdminController : Controller
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        ArgumentNullException.ThrowIfNull(adminService);
        _adminService = adminService;
    }

    // ─── Index ───────────────────────────────────────────────────────────────

    [HttpGet("")]
    public IActionResult Index() => View();

    // ─── Organizadores ───────────────────────────────────────────────────────

    [HttpGet("organizadores")]
    public async Task<IActionResult> Organizadores()
    {
        var list = await _adminService.GetOrganizadoresAsync();
        return View(list);
    }

    [HttpGet("organizadores/{userId}/edit")]
    public async Task<IActionResult> EditOrganizador(string userId)
    {
        var dto = await _adminService.GetOrganizadorAsync(userId);
        if (dto is null) return NotFound();
        return View(dto);
    }

    [HttpPost("organizadores/{userId}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditOrganizadorPost(string userId, AdminUserEditDto dto)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(EditOrganizador), new { userId });

        await _adminService.UpdateOrganizadorAsync(userId, dto);
        TempData[TempDataKeys.Success] = "Organizador actualizado correctamente.";
        return RedirectToAction(nameof(Organizadores));
    }

    [HttpPost("organizadores/{userId}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateOrganizador(string userId)
    {
        try
        {
            await _adminService.DeactivateOrganizadorAsync(userId);
            TempData[TempDataKeys.Success] = "Organizador desactivado correctamente.";
        }
        catch (InvalidOperationException ex)
        {
            TempData[TempDataKeys.Error] = ex.Message;
        }
        return RedirectToAction(nameof(Organizadores));
    }

    [HttpPost("organizadores/{userId}/reactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateOrganizador(string userId)
    {
        await _adminService.ReactivateUserAsync(userId);
        TempData[TempDataKeys.Success] = "Organizador reactivado correctamente.";
        return RedirectToAction(nameof(Organizadores));
    }

    // ─── Jugadores ───────────────────────────────────────────────────────────

    [HttpGet("jugadores")]
    public async Task<IActionResult> Jugadores(string? search)
    {
        var list = await _adminService.GetJugadoresAsync(search);
        ViewData["Search"] = search;
        return View(list);
    }

    [HttpGet("jugadores/{userId}/edit")]
    public async Task<IActionResult> EditJugador(string userId)
    {
        var dto = await _adminService.GetJugadorAsync(userId);
        if (dto is null) return NotFound();
        return View(dto);
    }

    [HttpPost("jugadores/{userId}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditJugadorPost(string userId, AdminUserEditDto dto)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(EditJugador), new { userId });

        await _adminService.UpdateJugadorAsync(userId, dto);
        TempData[TempDataKeys.Success] = "Jugador actualizado correctamente.";
        return RedirectToAction(nameof(Jugadores));
    }

    [HttpPost("jugadores/{userId}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateJugador(string userId)
    {
        await _adminService.DeactivateJugadorAsync(userId);
        TempData[TempDataKeys.Success] = "Jugador desactivado correctamente.";
        return RedirectToAction(nameof(Jugadores));
    }

    [HttpPost("jugadores/{userId}/reactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateJugador(string userId)
    {
        await _adminService.ReactivateUserAsync(userId);
        TempData[TempDataKeys.Success] = "Jugador reactivado correctamente.";
        return RedirectToAction(nameof(Jugadores));
    }

    // ─── Configuracion ───────────────────────────────────────────────────────

    [HttpGet("configuracion")]
    public async Task<IActionResult> Configuracion()
    {
        var config = await _adminService.GetConfiguracionAsync();
        return View(config);
    }

    [HttpPost("configuracion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfiguracionPost(ConfiguracionGlobal config)
    {
        if (!ModelState.IsValid)
            return View(config);

        try
        {
            await _adminService.UpdateConfiguracionAsync(config);
            TempData[TempDataKeys.Success] = "Configuración guardada correctamente.";
            return RedirectToAction(nameof(Configuracion));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(config);
        }
    }
}
