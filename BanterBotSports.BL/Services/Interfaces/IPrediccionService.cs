using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services.Interfaces;

public interface IPrediccionService
{
    // ─── Queries ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns predicciones for a specific jornada and participant, keyed by PartidoId.
    /// Used by the web form to pre-populate existing predictions.
    /// </summary>
    Task<IReadOnlyDictionary<int, PrediccionPartido>> GetPorJornadaYParticipanteAsync(int jornadaId, int participanteId);

    /// <summary>Returns all PrediccionJornada records for the given jornada.</summary>
    Task<IReadOnlyList<PrediccionJornada>> GetByJornadaAsync(int jornadaId);

    // ─── Write operations ─────────────────────────────────────────────────────

    Task GuardarPrediccionAsync(PrediccionPartido prediccion, Jornada jornada, bool esOrganizador = false);
    Task ActualizarGolesJornadaAsync(int jornadaId);
}
