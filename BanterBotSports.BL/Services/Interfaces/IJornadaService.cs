using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services.Interfaces;

public interface IJornadaService
{
    // ─── Queries ─────────────────────────────────────────────────────────────

    /// <summary>Returns a jornada by ID including partidos and predicciones.</summary>
    Task<Jornada?> GetDetalleAsync(int jornadaId);

    /// <summary>Returns all jornadas for a torneo ordered by number.</summary>
    Task<IReadOnlyList<Jornada>> GetByTorneoIdAsync(int torneoId);

    // ─── State transitions ────────────────────────────────────────────────────

    Task AbrirJornadaAsync(int jornadaId);
    Task CerrarJornadaAsync(int jornadaId);
    Task FinalizarJornadaAsync(int jornadaId);

    // ─── Events ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when a jornada transitions to Abierta.
    /// Consumers subscribe to send match lists to participants via Telegram.
    /// </summary>
    event Func<Jornada, Task>? JornadaAbierta;

    /// <summary>
    /// Raised when a jornada transitions to Finalizada.
    /// Consumers subscribe to trigger banter dispatch or score settlement.
    /// </summary>
    event Func<Jornada, Task>? JornadaFinalizada;
}
