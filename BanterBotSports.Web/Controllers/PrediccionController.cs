using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BanterBotSports.Web.Controllers;

[Authorize]
public class PrediccionController : Controller
{
    private readonly IJornadaService _jornadaService;
    private readonly IPrediccionService _prediccionService;
    private readonly ITorneoService _torneoService;
    private readonly UserManager<DAL.AppUser> _userManager;

    public PrediccionController(
        IJornadaService jornadaService,
        IPrediccionService prediccionService,
        ITorneoService torneoService,
        UserManager<DAL.AppUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(jornadaService);
        ArgumentNullException.ThrowIfNull(prediccionService);
        ArgumentNullException.ThrowIfNull(torneoService);
        ArgumentNullException.ThrowIfNull(userManager);

        _jornadaService = jornadaService;
        _prediccionService = prediccionService;
        _torneoService = torneoService;
        _userManager = userManager;
    }

    // GET /prediccion/{jornadaId}
    [HttpGet("/prediccion/{jornadaId:int}")]
    public async Task<IActionResult> Form(int jornadaId)
    {
        var jornada = await _jornadaService.GetDetalleAsync(jornadaId);
        if (jornada is null)
            return NotFound();

        var userId = _userManager.GetUserId(User)!;
        var participante = await _torneoService.GetParticipanteAsync(jornada.TorneoId, userId);
        if (participante is null)
            return Forbid();

        var esCerrada = jornada.Estado == EstadoJornada.Cerrada ||
                        jornada.Estado == EstadoJornada.Finalizada;
        var pastDeadline = jornada.DeadlineUtc.HasValue &&
                           DateTimeOffset.UtcNow > jornada.DeadlineUtc.Value;

        var existingPredictions = await _prediccionService
            .GetPorJornadaYParticipanteAsync(jornadaId, participante.Id);

        var torneo = await _torneoService.GetByIdAsync(jornada.TorneoId);

        ViewBag.Participante = participante;
        ViewBag.EsCerrada = esCerrada;
        ViewBag.ExistingPredictions = existingPredictions;
        ViewBag.Torneo = torneo;
        ViewBag.PastDeadline = pastDeadline;

        return View(jornada);
    }

    // GET /prediccion/{jornadaId}/publicas
    [AllowAnonymous]
    [HttpGet("/prediccion/{jornadaId:int}/publicas")]
    public async Task<IActionResult> Publicas(int jornadaId)
    {
        var jornada = await _jornadaService.GetDetalleAsync(jornadaId);
        if (jornada is null)
            return NotFound();

        // Only reveal predictions after the deadline has passed
        var pastDeadline = jornada.DeadlineUtc.HasValue &&
                           DateTimeOffset.UtcNow > jornada.DeadlineUtc.Value;
        if (!pastDeadline)
            return StatusCode(403);

        var resumen = await _jornadaService.GetResumenJornadaAsync(jornadaId);
        if (resumen is null)
            return NotFound();

        return View(resumen);
    }

    // POST /prediccion/{jornadaId}
    [HttpPost("/prediccion/{jornadaId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Form(int jornadaId, [FromForm] Dictionary<int, int[]> predicciones)
    {
        var jornada = await _jornadaService.GetDetalleAsync(jornadaId);
        if (jornada is null)
            return NotFound();

        var userId = _userManager.GetUserId(User)!;
        var participante = await _torneoService.GetParticipanteAsync(jornada.TorneoId, userId);
        if (participante is null)
            return Forbid();

        var torneo = await _torneoService.GetByIdAsync(jornada.TorneoId);
        var esOrganizador = torneo?.OrganizadorId == userId;

        if (jornada.DeadlineUtc.HasValue &&
            DateTimeOffset.UtcNow > jornada.DeadlineUtc.Value &&
            !esOrganizador)
        {
            ModelState.AddModelError(string.Empty, "El plazo para predicciones ya venció.");
            await PopulateViewBagAsync(jornadaId, participante, jornada, torneo, esCerrada: true, pastDeadline: true);
            return View(jornada);
        }

        var errores = new List<string>();

        foreach (var (partidoId, goles) in predicciones)
        {
            if (goles.Length < 2) continue;

            var partido = jornada.Partidos.FirstOrDefault(p => p.Id == partidoId);
            if (partido is null) continue;

            try
            {
                var prediccion = new PrediccionPartido
                {
                    PartidoId = partidoId,
                    ParticipanteId = participante.Id,
                    GolesEquipo1 = goles[0],
                    GolesEquipo2 = goles[1],
                    Fuente = FuentePrediccion.Web
                };

                await _prediccionService.GuardarPrediccionAsync(prediccion, jornada, esOrganizador);
            }
            catch (InvalidOperationException ex)
            {
                errores.Add($"{partido.Equipo1} vs {partido.Equipo2}: {ex.Message}");
            }
        }

        if (errores.Count > 0)
        {
            foreach (var error in errores)
                ModelState.AddModelError(string.Empty, error);

            await PopulateViewBagAsync(jornadaId, participante, jornada, torneo, esCerrada: false, pastDeadline: false);
            return View(jornada);
        }

        TempData[TempDataKeys.Success] = "Predicciones guardadas correctamente.";
        return RedirectToAction(nameof(Form), new { jornadaId });
    }

    private async Task PopulateViewBagAsync(
        int jornadaId,
        Participante participante,
        Jornada jornada,
        Entities.Torneo? torneo,
        bool esCerrada,
        bool pastDeadline)
    {
        var existingPredictions = await _prediccionService
            .GetPorJornadaYParticipanteAsync(jornadaId, participante.Id);

        ViewBag.Participante = participante;
        ViewBag.EsCerrada = esCerrada;
        ViewBag.ExistingPredictions = existingPredictions;
        ViewBag.Torneo = torneo;
        ViewBag.PastDeadline = pastDeadline;
    }
}
