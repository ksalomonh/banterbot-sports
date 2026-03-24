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
}
