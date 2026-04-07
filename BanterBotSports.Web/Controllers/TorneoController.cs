using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Integrations.ApiFootball;
using BanterBotSports.Web.Infrastructure;
using BanterBotSports.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BanterBotSports.Web.Controllers;

[Authorize]
public class TorneoController : Controller
{
    /// <summary>
    /// Data protection purpose for invite tokens. Public so tests can reference it without duplicating the string.
    /// </summary>
    public const string InviteProtectorPurpose = "TorneoInvite";

    private readonly ITorneoService _torneoService;
    private readonly IJornadaService _jornadaService;
    private readonly IApiFootballSyncService _apiFootballSyncService;
    private readonly IPartidoService _partidoService;
    private readonly IDataProtector _protector;
    private readonly UserManager<DAL.AppUser> _userManager;
    private readonly ILogger<TorneoController> _logger;

    public TorneoController(
        ITorneoService torneoService,
        IJornadaService jornadaService,
        IApiFootballSyncService apiFootballSyncService,
        IPartidoService partidoService,
        IDataProtectionProvider dataProtectionProvider,
        UserManager<DAL.AppUser> userManager,
        ILogger<TorneoController> logger)
    {
        ArgumentNullException.ThrowIfNull(torneoService);
        ArgumentNullException.ThrowIfNull(jornadaService);
        ArgumentNullException.ThrowIfNull(apiFootballSyncService);
        ArgumentNullException.ThrowIfNull(partidoService);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(logger);

        _torneoService = torneoService;
        _jornadaService = jornadaService;
        _apiFootballSyncService = apiFootballSyncService;
        _partidoService = partidoService;
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
        ViewBag.Ligas = LeagueCatalog.Leagues;
        return View(new TorneoCreateViewModel());
    }

    // POST /torneo/nuevo
    [HttpPost("/torneo/nuevo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(TorneoCreateViewModel model)
    {
        var sumaPremios = model.ConfiguracionPremios?.Sum(p => p.Porcentaje) ?? 0;
        if (Math.Abs(sumaPremios - 100m) > 0.01m)
        {
            ModelState.AddModelError(string.Empty,
                "Los premios deben sumar exactamente 100%. Volvé al paso Premios.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.InitialStep = ResolveStepFromErrors(ModelState);
            ViewBag.Ligas = LeagueCatalog.Leagues;
            return View(model);
        }

        try
        {
            var userId = _userManager.GetUserId(User)!;
            var torneoCreado = await _torneoService.CrearTorneoAsync(model, userId);

            // Assign selected matches to Jornada 1
            if (model.PartidosSeleccionados?.Count > 0)
            {
                var jornadas = await _jornadaService.GetByTorneoIdAsync(torneoCreado.Id);
                var primeraJornada = jornadas.MinBy(j => j.Numero);

                if (primeraJornada is not null)
                {
                    var matchFailures = new List<string>();
                    foreach (var externalId in model.PartidosSeleccionados)
                    {
                        if (!int.TryParse(externalId, out var extId)) continue;
                        try
                        {
                            var partidoDto = await _apiFootballSyncService.GetFixtureByIdAsync(extId);
                            if (partidoDto is not null)
                            {
                                var partido = new Partido
                                {
                                    JornadaId = primeraJornada.Id,
                                    ExternalId = partidoDto.ExternalId,
                                    Equipo1 = partidoDto.Equipo1,
                                    Equipo2 = partidoDto.Equipo2,
                                    KickOffUtc = partidoDto.KickOffUtc,
                                    Estado = partidoDto.Estado
                                };
                                await _partidoService.AsignarPartidoAsync(primeraJornada.Id, partido);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to assign fixture {ExternalId} to jornada {JornadaId}", externalId, primeraJornada.Id);
                            matchFailures.Add(externalId);
                        }
                    }
                    if (matchFailures.Count > 0)
                        TempData[TempDataKeys.Info] = $"No se pudieron asignar {matchFailures.Count} partido(s). Podés agregarlos manualmente.";
                }
            }

            return RedirectToAction(nameof(Dashboard), new { id = torneoCreado.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating torneo for user {UserId}", _userManager.GetUserId(User));
            ModelState.AddModelError(string.Empty, "Ocurrió un error al crear el torneo. Intenta de nuevo.");
            ViewBag.Ligas = LeagueCatalog.Leagues;
            return View(model);
        }
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

    // GET /torneo/{id}/leaderboard
    [HttpGet("/torneo/{id:int}/leaderboard")]
    public async Task<IActionResult> Leaderboard(int id)
    {
        var torneo = await _torneoService.GetByIdWithDetailsAsync(id);
        if (torneo is null)
            return NotFound();

        var userId = _userManager.GetUserId(User)!;
        var esParticipante = torneo.Participantes.Any(p => p.UserId == userId);
        if (!esParticipante)
            return Forbid();

        var ranking = await _torneoService.BuildRankingAsync(torneo);
        var vm = new LeaderboardViewModel(torneo.Nombre, torneo.Id, ranking);

        return View(vm);
    }

    // POST /torneo/{id}/confirmar-pago
    [HttpPost("/torneo/{id:int}/confirmar-pago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarPago(int id, [FromForm] int participanteId)
    {
        var torneo = await _torneoService.GetByIdAsync(id);
        if (torneo is null)
            return NotFound();

        var userId = _userManager.GetUserId(User)!;
        if (torneo.OrganizadorId != userId)
            return Forbid();

        try
        {
            await _torneoService.ConfirmarPagoAsync(id, participanteId, userId);
            TempData[TempDataKeys.Success] = "Pago confirmado.";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to confirm payment for participante {ParticipanteId} in torneo {TorneoId}", participanteId, id);
            TempData[TempDataKeys.Error] = "No se pudo confirmar el pago.";
        }

        return RedirectToAction(nameof(Dashboard), new { id });
    }

    // POST /torneo/{id}/revocar-pago
    [HttpPost("/torneo/{id:int}/revocar-pago")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevocarPago(int id, [FromForm] int participanteId)
    {
        var torneo = await _torneoService.GetByIdAsync(id);
        if (torneo is null)
            return NotFound();

        var userId = _userManager.GetUserId(User)!;
        if (torneo.OrganizadorId != userId)
            return Forbid();

        try
        {
            await _torneoService.RevocarPagoAsync(id, participanteId, userId);
            TempData[TempDataKeys.Success] = "Pago revocado.";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to revoke payment for participante {ParticipanteId} in torneo {TorneoId}", participanteId, id);
            TempData[TempDataKeys.Error] = ex.Message.Contains("organizador")
                ? "No se puede revocar el pago del organizador."
                : "No se pudo revocar el pago.";
        }

        return RedirectToAction(nameof(Dashboard), new { id });
    }

    // GET /torneo/{id}/unirse?token=X
    // AllowAnonymous so unauthenticated users can land here and be redirected to Login
    // with the full returnUrl (including token) preserved — fixing the broken invite flow.
    [AllowAnonymous]
    [HttpGet("/torneo/{id:int}/unirse")]
    public async Task<IActionResult> Unirse(int id, [FromQuery] string? token)
    {
        var torneo = await _torneoService.GetByIdAsync(id);
        if (torneo is null)
            return NotFound();

        if (!TryValidateInviteToken(token, id))
        {
            TempData[TempDataKeys.Error] = "Link de invitación inválido o expirado.";
            return RedirectToAction(nameof(Index));
        }

        // Unauthenticated user: redirect to Login preserving the full URL (token included).
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            var returnUrl = Url.Action(nameof(Unirse), "Torneo", new { id, token });
            return RedirectToAction("Login", "Account", new { returnUrl });
        }

        return View(torneo);
    }

    // POST /torneo/{id}/unirse?token=X
    // Renamed to UnirsePost with [ActionName("Unirse")] so GET and POST can coexist without
    // C# method name collision while still routing to /torneo/{id}/unirse.
    [HttpPost("/torneo/{id:int}/unirse")]
    [ValidateAntiForgeryToken]
    [ActionName("Unirse")]
    public async Task<IActionResult> UnirsePost(int id, [FromQuery] string? token)
    {
        var torneo = await _torneoService.GetByIdAsync(id);
        if (torneo is null)
            return NotFound();

        if (!TryValidateInviteToken(token, id))
        {
            TempData[TempDataKeys.Error] = "Link de invitación inválido o expirado.";
            return RedirectToAction(nameof(Index));
        }

        var userId = _userManager.GetUserId(User)!;
        await _torneoService.UnirseConTokenAsync(id, userId);

        TempData[TempDataKeys.Success] = $"Te uniste al torneo {torneo.Nombre}.";
        return RedirectToAction(nameof(Dashboard), new { id });
    }

    // GET /torneo/buscar-partidos?liga={ligaId}
    [HttpGet("/torneo/buscar-partidos")]
    public async Task<IActionResult> BuscarPartidos(int liga)
    {
        if (!LeagueCatalog.ValidIds.Contains(liga))
            return BadRequest("Liga no válida.");

        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = from.AddDays(35);
        var partidos = await _apiFootballSyncService.GetMatchesAsync(liga, from, to);
        return Json(partidos);
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Determines which wizard step should be shown when returning to the form after a POST error,
    /// based on which model fields have validation errors.
    /// </summary>
    private static int ResolveStepFromErrors(ModelStateDictionary modelState)
    {
        var keys = modelState.Keys.ToHashSet();
        if (keys.Any(k => k.StartsWith("Nombre") || k.StartsWith("NumJornadas") || k.StartsWith("MontoInscripcion")))
            return 0;
        if (keys.Any(k => k.StartsWith("PtosResultado") || k.StartsWith("PtosMarcador") || k.StartsWith("PtosGolesJornada")))
            return 1;
        if (keys.Any(k => k.StartsWith("ConfiguracionPremios") || k == string.Empty))
            return 2;
        return 0;
    }

    /// <summary>
    /// Validates the invite token: decrypts the payload, verifies the torneo ID matches,
    /// and checks that the token has not expired.
    /// </summary>
    private bool TryValidateInviteToken(string? token, int torneoId)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            var payload = _protector.Unprotect(token);
            var parts = payload.Split(':');
            if (parts.Length < 2
                || !int.TryParse(parts[0], out var tokenTorneoId)
                || tokenTorneoId != torneoId
                || !long.TryParse(parts[1], out var expiresUnix)
                || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate invite token for torneo {TorneoId}", torneoId);
            return false;
        }
    }
}
