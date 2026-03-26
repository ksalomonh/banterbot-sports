using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
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
        IUnitOfWork unitOfWork,
        ILogger<JornadaService> logger)
    {
        ArgumentNullException.ThrowIfNull(jornadaRepository);
        ArgumentNullException.ThrowIfNull(partidoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _jornadaRepository = jornadaRepository;
        _partidoRepository = partidoRepository;
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

        // Load partidos to set DeadlineUtc and validate that at least one exists
        var partidos = await _partidoRepository.GetByJornadaIdAsync(jornadaId);

        if (partidos.Count == 0)
        {
            throw new InvalidOperationException(
                $"La jornada {jornada.Numero} no tiene partidos asignados y no puede abrirse.");
        }

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

    private async Task<Jornada> GetJornadaOrThrowAsync(int jornadaId)
    {
        return await _jornadaRepository.GetByIdAsync(jornadaId)
            ?? throw new InvalidOperationException($"Jornada {jornadaId} no encontrada.");
    }
}
