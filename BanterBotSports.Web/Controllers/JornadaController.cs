using BanterBotSports.BanterAI;
using BanterBotSports.BL.Exceptions;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Integrations.ApiFootball;
using BanterBotSports.Integrations.Telegram;
using BanterBotSports.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BanterBotSports.Web.Controllers;

[Authorize]
public class JornadaController : Controller
{
    private readonly IJornadaService _jornadaService;
    private readonly IPartidoService _partidoService;
    private readonly ITorneoService _torneoService;
    private readonly IPrediccionService _prediccionService;
    private readonly IApiFootballClient _apiFootballClient;
    private readonly UserManager<DAL.AppUser> _userManager;
    private readonly ILogger<JornadaController> _logger;

    public JornadaController(
        IJornadaService jornadaService,
        IPartidoService partidoService,
        ITorneoService torneoService,
        IPrediccionService prediccionService,
        IApiFootballClient apiFootballClient,
        UserManager<DAL.AppUser> userManager,
        ILogger<JornadaController> logger,
        JornadaAbiertaNotifier jornadaAbiertaNotifier,
        IBanterDispatchService banterDispatchService)
    {
        ArgumentNullException.ThrowIfNull(jornadaService);
        ArgumentNullException.ThrowIfNull(partidoService);
        ArgumentNullException.ThrowIfNull(torneoService);
        ArgumentNullException.ThrowIfNull(prediccionService);
        ArgumentNullException.ThrowIfNull(apiFootballClient);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(jornadaAbiertaNotifier);
        ArgumentNullException.ThrowIfNull(banterDispatchService);

        _jornadaService = jornadaService;
        _partidoService = partidoService;
        _torneoService = torneoService;
        _prediccionService = prediccionService;
        _apiFootballClient = apiFootballClient;
        _userManager = userManager;
        _logger = logger;

        // Wire event subscribers in the composition root (per-scope, per-request)
        jornadaService.JornadaAbierta += jornadaAbiertaNotifier.OnJornadaAbiertaAsync;
        jornadaService.JornadaFinalizada += banterDispatchService.OnJornadaFinalizadaAsync;
    }

    // GET /jornada/{id}
    [HttpGet("/jornada/{id:int}")]
    public async Task<IActionResult> Detalle(int id)
    {
        var jornada = await _jornadaService.GetDetalleAsync(id);
        if (jornada is null)
            return NotFound();

        var torneo = await _torneoService.GetByIdAsync(jornada.TorneoId);
        if (torneo is null)
            return NotFound();

        var userId = _userManager.GetUserId(User)!;
        ViewBag.EsOrganizador = torneo.OrganizadorId == userId;
        ViewBag.Torneo = torneo;

        return View(jornada);
    }

    // POST /jornada/{id}/partidos
    [HttpPost("/jornada/{id:int}/partidos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarPartido(int id, [FromForm] string externalId)
    {
        var jornada = await _jornadaService.GetDetalleAsync(id);
        if (jornada is null)
            return NotFound();

        if (!await EsOrganizadorAsync(jornada.TorneoId))
            return Forbid();

        if (!int.TryParse(externalId, out var extId))
        {
            TempData[TempDataKeys.Error] = "ID externo inválido.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        // IApiFootballClient caches responses in PostgreSQL per integrations layer rule.
        var matchDto = await _apiFootballClient.GetLiveScoreAsync(extId);
        if (matchDto is null)
        {
            TempData[TempDataKeys.Error] = "No se encontró el partido con ese ID en API-Football.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var partido = new Entities.Partido
        {
            JornadaId = id,
            ExternalId = matchDto.ExternalId,
            Equipo1 = matchDto.Equipo1,
            Equipo2 = matchDto.Equipo2,
            KickOffUtc = matchDto.KickOffUtc,
            Estado = matchDto.Estado
        };

        await _partidoService.AsignarPartidoAsync(id, partido);

        TempData[TempDataKeys.Success] = $"Partido {partido.Equipo1} vs {partido.Equipo2} asignado.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    // POST /jornada/{id}/abrir
    [HttpPost("/jornada/{id:int}/abrir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Abrir(int id)
    {
        if (!await EsOrganizadorByIdAsync(id))
            return Forbid();

        try
        {
            await _jornadaService.AbrirJornadaAsync(id);
            TempData[TempDataKeys.Success] = "Jornada abierta.";
        }
        catch (JornadaSinPartidosException ex)
        {
            _logger.LogWarning(ex, "Failed to abrir jornada {JornadaId}: no partidos", id);
            TempData[TempDataKeys.Error] = "No se puede abrir una jornada sin partidos asignados.";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to abrir jornada {JornadaId}", id);
            TempData[TempDataKeys.Error] = "No se pudo abrir la jornada. Verificá que esté en estado válido.";
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    // POST /jornada/{id}/cerrar
    [HttpPost("/jornada/{id:int}/cerrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id)
    {
        if (!await EsOrganizadorByIdAsync(id))
            return Forbid();

        try
        {
            await _jornadaService.CerrarJornadaAsync(id);

            // Aggregate each participant's total predicted goals from their match predictions.
            // Must run after the jornada closes (predictions are now locked).
            await _prediccionService.ActualizarGolesJornadaAsync(id);

            TempData[TempDataKeys.Success] = "Jornada cerrada.";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to cerrar jornada {JornadaId}", id);
            TempData[TempDataKeys.Error] = "No se pudo cerrar la jornada. Verificá que esté en estado válido.";
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    // POST /jornada/{id}/finalizar
    [HttpPost("/jornada/{id:int}/finalizar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalizar(int id)
    {
        if (!await EsOrganizadorByIdAsync(id))
            return Forbid();

        try
        {
            await _jornadaService.FinalizarJornadaAsync(id);

            // Compute and persist jornada-level goal points for every participant.
            // Must run after official results are entered (GolesEquipo1Oficial / GolesEquipo2Oficial set).
            await _prediccionService.CalcularPuntosGolesJornadaAsync(id);

            TempData[TempDataKeys.Success] = "Jornada finalizada.";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to finalizar jornada {JornadaId}", id);
            TempData[TempDataKeys.Error] = "No se pudo finalizar la jornada. Verificá que esté en estado válido.";
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    // GET /jornada/{id}/resumen
    [HttpGet("/jornada/{id:int}/resumen")]
    public async Task<IActionResult> Resumen(int id)
    {
        var resumen = await _jornadaService.GetResumenJornadaAsync(id);
        if (resumen is null)
            return NotFound();

        return View(resumen);
    }

    // GET /jornada/{id}/buscar-partidos?q=...
    [HttpGet("/jornada/{id:int}/buscar-partidos")]
    public async Task<IActionResult> BuscarPartidos(int id, [FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Json(Array.Empty<PartidoDto>());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-30);
        var to = today.AddDays(30);

        // IApiFootballClient caches responses in PostgreSQL per integrations layer rule.
        // Results are returned from the local cache when available; the API is only hit on misses.
        //
        // Search modes:
        //  - Numeric q → treated as a competition/league ID; returns all matches for that competition.
        //  - Text q    → requires a prior competition search; returns empty (API-Football does not support
        //                free-text team name search without a competition context).
        if (!int.TryParse(q, out var competitionId))
            return Json(Array.Empty<PartidoDto>());

        var results = await _apiFootballClient.GetMatchesAsync(competitionId, from, to);

        var filtered = results.Take(20).ToList();

        return Json(filtered);
    }

    private async Task<bool> EsOrganizadorAsync(int torneoId)
    {
        var torneo = await _torneoService.GetByIdAsync(torneoId);
        return torneo is not null && torneo.OrganizadorId == _userManager.GetUserId(User);
    }

    private async Task<bool> EsOrganizadorByIdAsync(int jornadaId)
    {
        var jornada = await _jornadaService.GetDetalleAsync(jornadaId);
        if (jornada is null) return false;
        return await EsOrganizadorAsync(jornada.TorneoId);
    }
}
