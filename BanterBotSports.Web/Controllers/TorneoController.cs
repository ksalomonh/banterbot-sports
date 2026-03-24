using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BanterBotSports.Web.Controllers;

[Authorize]
public class TorneoController : Controller
{
    private const string InviteProtectorPurpose = "TorneoInvite";

    private readonly ITorneoService _torneoService;
    private readonly IJornadaService _jornadaService;
    private readonly IDataProtector _protector;
    private readonly UserManager<DAL.AppUser> _userManager;
    private readonly ILogger<TorneoController> _logger;

    public TorneoController(
        ITorneoService torneoService,
        IJornadaService jornadaService,
        IDataProtectionProvider dataProtectionProvider,
        UserManager<DAL.AppUser> userManager,
        ILogger<TorneoController> logger)
    {
        ArgumentNullException.ThrowIfNull(torneoService);
        ArgumentNullException.ThrowIfNull(jornadaService);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(logger);

        _torneoService = torneoService;
        _jornadaService = jornadaService;
        _protector = dataProtectionProvider.CreateProtector(InviteProtectorPurpose);
        _userManager = userManager;
        _logger = logger;
    }

    // GET /torneo
    [HttpGet("/torneo")]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var torneos = await _torneoService.GetTorneosPorUsuarioAsync(userId);
        return View(torneos);
    }

    // GET /torneo/nuevo
    [HttpGet("/torneo/nuevo")]
    public IActionResult Nuevo()
    {
        return View(new TorneoCreateViewModel());
    }

    // POST /torneo/nuevo
    [HttpPost("/torneo/nuevo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(TorneoCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = _userManager.GetUserId(User)!;
        var torneoCreado = await _torneoService.CrearTorneoAsync(model, userId);

        return RedirectToAction(nameof(Dashboard), new { id = torneoCreado.Id });
    }

    // GET /torneo/{id}
    [HttpGet("/torneo/{id:int}")]
    public async Task<IActionResult> Dashboard(int id)
    {
        var torneo = await _torneoService.GetByIdWithDetailsAsync(id);
        if (torneo is null)
            return NotFound();

        var userId = _userManager.GetUserId(User)!;
        var esParticipante = torneo.Participantes.Any(p => p.UserId == userId);
        if (!esParticipante)
            return Forbid();

        var ranking = await _torneoService.BuildRankingAsync(torneo);
        var jornadas = await _jornadaService.GetByTorneoIdAsync(id);

        ViewBag.EsOrganizador = torneo.OrganizadorId == userId;
        ViewBag.Ranking = ranking;
        ViewBag.Jornadas = jornadas.OrderBy(j => j.Numero).ToList();

        return View(torneo);
    }

    // GET /torneo/{id}/invitar
    [HttpGet("/torneo/{id:int}/invitar")]
    public async Task<IActionResult> Invitar(int id)
    {
        var torneo = await _torneoService.GetByIdAsync(id);
        if (torneo is null)
            return NotFound();

        var userId = _userManager.GetUserId(User)!;
        if (torneo.OrganizadorId != userId)
            return Forbid();

        // Protected (signed + encrypted) payload — cannot be forged by clients
        var payload = $"{id}:{DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds()}";
        var inviteToken = _protector.Protect(payload);
        var inviteUrl = Url.Action(nameof(Unirse), "Torneo", new { id, token = inviteToken }, Request.Scheme);

        ViewBag.InviteUrl = inviteUrl;
        return View(torneo);
    }

    // POST /torneo/{id}/unirse?token=X
    [HttpPost("/torneo/{id:int}/unirse")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unirse(int id, [FromQuery] string token)
    {
        var torneo = await _torneoService.GetByIdAsync(id);
        if (torneo is null)
            return NotFound();

        try
        {
            var payload = _protector.Unprotect(token);
            var parts = payload.Split(':');
            if (parts.Length < 2
                || !int.TryParse(parts[0], out var tokenTorneoId)
                || tokenTorneoId != id
                || !long.TryParse(parts[1], out var expiresUnix)
                || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix)
            {
                TempData["Error"] = "Link de invitación inválido o expirado.";
                return RedirectToAction(nameof(Index));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate invite token for torneo {TorneoId}", id);
            TempData["Error"] = "Link de invitación inválido.";
            return RedirectToAction(nameof(Index));
        }

        var userId = _userManager.GetUserId(User)!;
        await _torneoService.UnirseConTokenAsync(id, userId);

        TempData["Success"] = $"Te uniste al torneo {torneo.Nombre}.";
        return RedirectToAction(nameof(Dashboard), new { id });
    }
}
