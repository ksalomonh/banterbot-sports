using BanterBotSports.BL.Exceptions;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Entities.ViewModels;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Manages Jornada state transitions:
///   PendientePartidos → Abierta → Cerrada → Finalizada
/// On finalization, raises JornadaFinalizada event so consumers (e.g., BanterAI) can react.
/// </summary>
public class JornadaService : IJornadaService
{
    private readonly IJornadaRepository _jornadaRepository;
    private readonly IPartidoRepository _partidoRepository;
    private readonly IParticipanteRepository _participanteRepository;
    private readonly ITorneoService _torneoService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JornadaService> _logger;

    /// <summary>
    /// Raised when a jornada transitions to Abierta.
    /// Consumers subscribe to send match lists to participants via Telegram.
    /// </summary>
    public event Func<Jornada, Task>? JornadaAbierta;

    /// <summary>
    /// Raised when a jornada transitions to Finalizada.
    /// Consumers subscribe to trigger banter dispatch or score settlement.
    /// </summary>
    public event Func<Jornada, Task>? JornadaFinalizada;

    public JornadaService(
        IJornadaRepository jornadaRepository,
        IPartidoRepository partidoRepository,
        IParticipanteRepository participanteRepository,
        ITorneoService torneoService,
        IUnitOfWork unitOfWork,
        ILogger<JornadaService> logger)
    {
        ArgumentNullException.ThrowIfNull(jornadaRepository);
        ArgumentNullException.ThrowIfNull(partidoRepository);
        ArgumentNullException.ThrowIfNull(participanteRepository);
        ArgumentNullException.ThrowIfNull(torneoService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _jornadaRepository = jornadaRepository;
        _partidoRepository = partidoRepository;
        _participanteRepository = participanteRepository;
        _torneoService = torneoService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ─── Queries ─────────────────────────────────────────────────────────────

    public Task<Jornada?> GetDetalleAsync(int jornadaId)
        => _jornadaRepository.GetByIdWithDetailsAsync(jornadaId);

    public Task<IReadOnlyList<Jornada>> GetByTorneoIdAsync(int torneoId)
        => _jornadaRepository.GetByTorneoIdAsync(torneoId);

    // ─── State transitions ────────────────────────────────────────────────────

    public async Task AbrirJornadaAsync(int jornadaId)
    {
        var jornada = await GetJornadaOrThrowAsync(jornadaId);

        if (jornada.Estado != EstadoJornada.PendientePartidos)
        {
            throw new InvalidOperationException(
                $"La jornada {jornada.Numero} no puede abrirse desde el estado '{jornada.Estado}'.");
        }

        // Auto-baja: remove unpaid participants BEFORE opening predictions
        var removidos = await _torneoService.DarDeBajaImpagosAsync(jornada.TorneoId);
        if (removidos > 0)
        {
            _logger.LogInformation(
                "Auto-baja: {Removidos} unpaid participant(s) removed from torneo {TorneoId} before opening jornada {JornadaId}.",
                removidos, jornada.TorneoId, jornada.Id);
        }

        // Load partidos to set DeadlineUtc and validate that at least one exists
        var partidos = await _partidoRepository.GetByJornadaIdAsync(jornadaId);

        if (partidos.Count == 0)
            throw new JornadaSinPartidosException(jornada.Id, jornada.Numero);

        // DeadlineUtc = earliest kick-off so predictions lock at the first match start
        var earliestKickOff = partidos.Min(p => p.KickOffUtc);
        jornada.DeadlineUtc = earliestKickOff;

        jornada.Estado = EstadoJornada.Abierta;
        await _jornadaRepository.UpdateAsync(jornada);
        await _unitOfWork.SaveAsync();

        _logger.LogInformation(
            "Jornada {JornadaId} (#{Numero}) abierta. DeadlineUtc={DeadlineUtc}.",
            jornada.Id, jornada.Numero, jornada.DeadlineUtc);

        // Notify subscribers (e.g. Telegram match list notification)
        if (JornadaAbierta is not null)
            await JornadaAbierta.Invoke(jornada);
    }

    public async Task CerrarJornadaAsync(int jornadaId)
    {
        var jornada = await GetJornadaOrThrowAsync(jornadaId);

        if (jornada.Estado != EstadoJornada.Abierta)
        {
            throw new InvalidOperationException(
                $"La jornada {jornada.Numero} no puede cerrarse desde el estado '{jornada.Estado}'.");
        }

        jornada.Estado = EstadoJornada.Cerrada;
        await _jornadaRepository.UpdateAsync(jornada);
        await _unitOfWork.SaveAsync();
    }

    public async Task FinalizarJornadaAsync(int jornadaId)
    {
        var jornada = await GetJornadaOrThrowAsync(jornadaId);

        if (jornada.Estado != EstadoJornada.Cerrada)
        {
            throw new InvalidOperationException(
                $"La jornada {jornada.Numero} no puede finalizarse desde el estado '{jornada.Estado}'.");
        }

        jornada.Estado = EstadoJornada.Finalizada;
        await _jornadaRepository.UpdateAsync(jornada);
        await _unitOfWork.SaveAsync();

        // Notify subscribers (e.g. BanterAI dispatch, score settlement)
        if (JornadaFinalizada is not null)
            await JornadaFinalizada.Invoke(jornada);
    }

    // ─── Resumen ──────────────────────────────────────────────────────────────

    public async Task<ResumenViewModel?> GetResumenJornadaAsync(int jornadaId)
    {
        // Single query: jornada + torneo + participantes + partidos + predicciones por partido
        var jornada = await _jornadaRepository.GetByIdWithResumenAsync(jornadaId);
        if (jornada is null)
            return null;

        var torneo = jornada.Torneo;
        var partidos = jornada.Partidos.OrderBy(p => p.KickOffUtc).ToList();

        // Resolve display names for all participants in one query
        var userIds = torneo.Participantes.Select(p => p.UserId).ToList();
        var displayNames = await _participanteRepository.GetDisplayNamesByIdsAsync(userIds);

        // Build per-participant rows
        var filas = torneo.Participantes
            .Select(participante =>
            {
                // Points for this jornada from PrediccionesJornada
                var pjornada = jornada.PrediccionesJornada
                    .FirstOrDefault(pj => pj.ParticipanteId == participante.Id);
                int puntosJornada = pjornada?.PuntosObtenidos ?? 0;

                // Per-match prediction vs result
                var predicciones = partidos.Select(partido =>
                {
                    var pred = partido.PrediccionesPartido
                        .FirstOrDefault(pp => pp.ParticipanteId == participante.Id);

                    var resultado = PrediccionClassifier.Clasificar(
                        pred?.GolesEquipo1, pred?.GolesEquipo2,
                        partido.GolesEquipo1Oficial, partido.GolesEquipo2Oficial);

                    return new PrediccionConResultado(
                        PartidoId: partido.Id,
                        Equipo1: partido.Equipo1,
                        Equipo2: partido.Equipo2,
                        GolesEquipo1Oficial: partido.GolesEquipo1Oficial,
                        GolesEquipo2Oficial: partido.GolesEquipo2Oficial,
                        GolesPredichos1: pred?.GolesEquipo1,
                        GolesPredichos2: pred?.GolesEquipo2,
                        PuntosObtenidos: pred?.PuntosObtenidos,
                        Resultado: resultado,
                        LogoUrlLocal: partido.LogoUrlLocal,
                        LogoUrlVisitante: partido.LogoUrlVisitante);
                }).ToList();

                var nombreDisplay = displayNames.GetValueOrDefault(participante.UserId, participante.UserId);

                return new ResumenParticipanteRow(
                    NombreDisplay: nombreDisplay,
                    PuntosJornada: puntosJornada,
                    Predicciones: predicciones);
            })
            .OrderByDescending(r => r.PuntosJornada)
            .ToList();

        return new ResumenViewModel(
            JornadaId: jornada.Id,
            JornadaNumero: jornada.Numero,
            TorneoNombre: torneo.Nombre,
            TorneoId: torneo.Id,
            Participantes: filas);
    }

    private async Task<Jornada> GetJornadaOrThrowAsync(int jornadaId)
    {
        return await _jornadaRepository.GetByIdAsync(jornadaId)
            ?? throw new InvalidOperationException($"Jornada {jornadaId} no encontrada.");
    }

}
