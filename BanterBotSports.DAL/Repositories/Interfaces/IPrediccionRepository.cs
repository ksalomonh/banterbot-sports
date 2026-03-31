using BanterBotSports.Entities;

namespace BanterBotSports.DAL.Repositories.Interfaces;

public interface IPrediccionRepository
{
    Task<PrediccionPartido?> GetPrediccionPartidoByIdAsync(int id);
    Task<PrediccionPartido?> GetPrediccionPartidoAsync(int partidoId, int participanteId);
    Task<IReadOnlyList<PrediccionPartido>> GetPrediccionesByPartidoAsync(int partidoId);
    Task<IReadOnlyList<PrediccionPartido>> GetPrediccionesByParticipanteAsync(int participanteId);
    Task<IReadOnlyList<PrediccionPartido>> GetPrediccionesByJornadaIdAsync(int jornadaId);
    Task<IReadOnlyList<PrediccionPartido>> GetPrediccionesByJornadaAndParticipanteAsync(int jornadaId, int participanteId);
    Task<IReadOnlyDictionary<int, int>> GetPuntosTotalesPorParticipanteAsync(IEnumerable<int> participanteIds);
    Task<PrediccionPartido> AddPrediccionPartidoAsync(PrediccionPartido prediccion);
    Task UpdatePrediccionPartidoAsync(PrediccionPartido prediccion);

    Task<PrediccionJornada?> GetPrediccionJornadaByIdAsync(int id);
    Task<PrediccionJornada?> GetPrediccionJornadaAsync(int jornadaId, int participanteId);
    Task<IReadOnlyList<PrediccionJornada>> GetPrediccionesJornadaByJornadaAsync(int jornadaId);
    Task<PrediccionJornada> AddPrediccionJornadaAsync(PrediccionJornada prediccion);
    Task UpdatePrediccionJornadaAsync(PrediccionJornada prediccion);

    /// <summary>
    /// Deletes all PrediccionPartido and PrediccionJornada records for the given participant.
    /// Used during auto-baja of unpaid players.
    /// </summary>
    Task DeleteByParticipanteIdAsync(int participanteId);
}
