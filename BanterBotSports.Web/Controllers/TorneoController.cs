using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
    private readonly IPartidoService _partidoService;
    private readonly IAdminService _adminService;
    private readonly IDataProtector _protector;
    private readonly UserManager<DAL.AppUser> _userManager;
    private readonly ILogger<TorneoController> _logger;

    public TorneoController(
        ITorneoService torneoService,
        IJornadaService jornadaService,
        IPartidoService partidoService,
        IAdminService adminService,
        IDataProtectionProvider dataProtectionProvider,
        UserManager<DAL.AppUser> userManager,
        ILogger<TorneoController> logger)
    {
        ArgumentNullException.ThrowIfNull(torneoService);
        ArgumentNullException.ThrowIfNull(jornadaService);
        ArgumentNullException.ThrowIfNull(partidoService);
        ArgumentNullException.ThrowIfNull(adminService);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(logger);

        _torneoService = torneoService;
        _jornadaService = jornadaService;
        _partidoService = partidoService;
        _adminService = adminService;
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
    public async Task<IActionResult> Nuevo()
    {
        var config = await _adminService.GetConfiguracionAsync();
        var userId = _userManager.GetUserId(User)!;
        var user = await _userManager.FindByIdAsync(userId);

        ViewBag.Ligas = _partidoService.GetLigas();
        ViewBag.PorcentajePlataforma = config.PorcentajePlataforma;
        ViewBag.PorcentajeOrganizadorMin = config.PorcentajeOrganizadorMin;
        ViewBag.PorcentajeOrganizadorMax = config.PorcentajeOrganizadorMax;
        ViewBag.PorcentajeOrganizadorDefault = user?.PorcentajeOrganizadorGlobal ?? config.PorcentajeOrganizadorMin;

        return View(new TorneoCreateViewModel());
    }

    // POST /torneo/nuevo
    [HttpPost("/torneo/nuevo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuevo(TorneoCreateViewModel model)
    {
        const string PorcentajeOrganizadorField = nameof(TorneoCreateViewModel.PorcentajeOrganizador);

        var config = await _adminService.GetConfiguracionAsync();
        var userId = _userManager.GetUserId(User)!;
        var user = await _userManager.FindByIdAsync(userId);

        if (!ModelState.IsValid)
        {
            ViewBag.InitialStep = ResolveStepFromErrors(ModelState);
            ViewBag.Ligas = _partidoService.GetLigas();
            ViewBag.PorcentajePlataforma = config.PorcentajePlataforma;
            ViewBag.PorcentajeOrganizadorMin = config.PorcentajeOrganizadorMin;
            ViewBag.PorcentajeOrganizadorMax = config.PorcentajeOrganizadorMax;
            ViewBag.PorcentajeOrganizadorDefault = user?.PorcentajeOrganizadorGlobal ?? config.PorcentajeOrganizadorMin;
            return View(model);
        }

        // Validate prize sum against dynamic pool (100% − platform% − organizer%)
        decimal resolvedOrgPct = model.PorcentajeOrganizador
            ?? user?.PorcentajeOrganizadorGlobal
            ?? config.PorcentajeOrganizadorMin;
        decimal expectedPool = 100m - config.PorcentajePlataforma - resolvedOrgPct;
        decimal prizeSum = model.ConfiguracionPremios?.Sum(p => p.Porcentaje) ?? 0m;

        if (resolvedOrgPct < config.PorcentajeOrganizadorMin)
        {
            ModelState.AddModelError(PorcentajeOrganizadorField,
                $"El porcentaje debe ser al menos el mínimo permitido ({config.PorcentajeOrganizadorMin}%)");
            ViewBag.InitialStep = ResolveStepFromErrors(ModelState);
            ViewBag.Ligas = _partidoService.GetLigas();
            ViewBag.PorcentajePlataforma = config.PorcentajePlataforma;
            ViewBag.PorcentajeOrganizadorMin = config.PorcentajeOrganizadorMin;
            ViewBag.PorcentajeOrganizadorMax = config.PorcentajeOrganizadorMax;
            ViewBag.PorcentajeOrganizadorDefault = user?.PorcentajeOrganizadorGlobal ?? config.PorcentajeOrganizadorMin;
            return View(model);
        }

        if (resolvedOrgPct > config.PorcentajeOrganizadorMax)
        {
            ModelState.AddModelError(PorcentajeOrganizadorField,
                $"El porcentaje no puede superar el máximo permitido ({config.PorcentajeOrganizadorMax}%)");
            ViewBag.InitialStep = ResolveStepFromErrors(ModelState);
            ViewBag.Ligas = _partidoService.GetLigas();
            ViewBag.PorcentajePlataforma = config.PorcentajePlataforma;
            ViewBag.PorcentajeOrganizadorMin = config.PorcentajeOrganizadorMin;
            ViewBag.PorcentajeOrganizadorMax = config.PorcentajeOrganizadorMax;
            ViewBag.PorcentajeOrganizadorDefault = user?.PorcentajeOrganizadorGlobal ?? config.PorcentajeOrganizadorMin;
            return View(model);
        }

        if (Math.Abs(prizeSum - expectedPool) > 0.01m)
        {
            ModelState.AddModelError(string.Empty,
                $"Los premios deben sumar exactamente {expectedPool}% (100% − {config.PorcentajePlataforma}% plataforma − {resolvedOrgPct}% organizador)");
            ViewBag.InitialStep = 2;
            ViewBag.Ligas = _partidoService.GetLigas();
            ViewBag.PorcentajePlataforma = config.PorcentajePlataforma;
            ViewBag.PorcentajeOrganizadorMin = config.PorcentajeOrganizadorMin;
            ViewBag.PorcentajeOrganizadorMax = config.PorcentajeOrganizadorMax;
            ViewBag.PorcentajeOrganizadorDefault = user?.PorcentajeOrganizadorGlobal ?? config.PorcentajeOrganizadorMin;
            return View(model);
        }

        try
        {
            var torneoCreado = await _torneoService.CrearTorneoAsync(model, userId);

            // Assign selected matches to Jornada 1
            if (model.PartidosSeleccionados?.Count > 0)
            {
                var jornadas = await _jornadaService.GetByTorneoIdAsync(torneoCreado.Id);
                var primeraJornada = jornadas.MinBy(j => j.Numero);

                if (primeraJornada is not null)
                {
                    // Fetch fixture DTOs via the BL service (IPartidoService proxies IPartidoCatalogService)
                    var partidoDtos = new List<Entities.DTOs.PartidoDto>();
                    foreach (var externalId in model.PartidosSeleccionados)
                    {
                        if (!int.TryParse(externalId, out var extId)) continue;
                        var dto = await _partidoService.GetFixturePorExternalIdAsync(extId);
                        if (dto is not null)
                            partidoDtos.Add(dto);
                    }

                    var failures = await _torneoService.AsignarPartidosInicialesAsync(primeraJornada.Id, partidoDtos);
                    if (failures.Count > 0)
                        TempData[TempDataKeys.Info] = $"No se pudieron asignar {failures.Count} partido(s). Podés agregarlos manualmente.";
                }
            }

            return RedirectToAction(nameof(Dashboard), new { id = torneoCreado.Id });
        }
        catch (InvalidOperationException ex) when (IsOrganizerPercentageValidation(ex.Message))
        {
            _logger.LogWarning(ex, "Organizer percentage validation failed while creating torneo for user {UserId}", userId);
            ModelState.AddModelError(PorcentajeOrganizadorField, ex.Message);
            ViewBag.InitialStep = ResolveStepFromErrors(ModelState);
            ViewBag.Ligas = _partidoService.GetLigas();
            ViewBag.PorcentajePlataforma = config.PorcentajePlataforma;
            ViewBag.PorcentajeOrganizadorMin = config.PorcentajeOrganizadorMin;
            ViewBag.PorcentajeOrganizadorMax = config.PorcentajeOrganizadorMax;
            ViewBag.PorcentajeOrganizadorDefault = user?.PorcentajeOrganizadorGlobal ?? config.PorcentajeOrganizadorMin;
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating torneo for user {UserId}", userId);
            ModelState.AddModelError(string.Empty, "Ocurrió un error al crear el torneo. Intenta de nuevo.");
            ViewBag.InitialStep = ResolveStepFromErrors(ModelState);
            ViewBag.Ligas = _partidoService.GetLigas();
            ViewBag.PorcentajePlataforma = config.PorcentajePlataforma;
            ViewBag.PorcentajeOrganizadorMin = config.PorcentajeOrganizadorMin;
            ViewBag.PorcentajeOrganizadorMax = config.PorcentajeOrganizadorMax;
            ViewBag.PorcentajeOrganizadorDefault = user?.PorcentajeOrganizadorGlobal ?? config.PorcentajeOrganizadorMin;
            return View(model);
        }
    }

    private static bool IsOrganizerPercentageValidation(string message)
        => message.Contains("mínimo permitido", StringComparison.OrdinalIgnoreCase)
           || message.Contains("máximo permitido", StringComparison.OrdinalIgnoreCase);

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

    // GET /torneo/{id}/clonar-jugadores
    [HttpGet("/torneo/{id:int}/clonar-jugadores")]
    public async Task<IActionResult> ClonarJugadores(int id)
    {
        var torneo = await _torneoService.GetByIdAsync(id);
        if (torneo is null) return NotFound();

        var userId = _userManager.GetUserId(User)!;
        if (torneo.OrganizadorId != userId) return Forbid();

        var torneosClonables = await _torneoService.GetTorneosClonablesAsync(id, userId);
        ViewBag.TorneosClonables = torneosClonables;
        return View(torneo);
    }

    // POST /torneo/{id}/clonar-jugadores
    [HttpPost("/torneo/{id:int}/clonar-jugadores")]
    [ValidateAntiForgeryToken]
    [ActionName("ClonarJugadores")]
    public async Task<IActionResult> ClonarJugadoresPost(int id, [FromForm] int torneoOrigenId)
    {
        var torneo = await _torneoService.GetByIdAsync(id);
        if (torneo is null) return NotFound();

        var userId = _userManager.GetUserId(User)!;
        if (torneo.OrganizadorId != userId) return Forbid();

        var result = await _torneoService.ClonarJugadoresAsync(id, torneoOrigenId, userId);

        if (result.Clonados > 0 && result.Omitidos == 0)
            TempData[TempDataKeys.Success] = $"Se clonaron {result.Clonados} jugadores correctamente.";
        else if (result.Clonados > 0 && result.Omitidos > 0)
            TempData[TempDataKeys.Info] = $"Se clonaron {result.Clonados} jugadores. {result.Omitidos} ya estaba(n) inscripto(s) y fue(ron) omitido(s).";
        else if (result.Omitidos > 0)
            TempData[TempDataKeys.Info] = "Todos los jugadores ya estaban inscritos.";
        else
            TempData[TempDataKeys.Info] = "El torneo origen no tiene jugadores para clonar.";

        return RedirectToAction(nameof(Dashboard), new { id });
    }

    // GET /torneo/buscar-partidos?liga={ligaId}
    [HttpGet("/torneo/buscar-partidos")]
    public async Task<IActionResult> BuscarPartidos(int liga)
    {
        if (!_partidoService.EsLigaValida(liga))
            return BadRequest("Liga no válida.");

        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = from.AddDays(35);
        var partidos = await _partidoService.GetProximosPartidosAsync(liga, from, to);
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
        if (keys.Any(k => k.StartsWith("Nombre") || k.StartsWith("NumJornadas") || k.StartsWith("MontoInscripcion") || k.StartsWith("PorcentajeOrganizador")))
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
