using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;

namespace BanterBotSports.DAL.Repositories.Interfaces;

public interface IJornadaRepository
{
    Task<Jornada?> GetByIdAsync(int id);
    Task<Jornada?> GetByIdWithDetailsAsync(int id);

    /// <summary>
    /// Returns a jornada with Torneo (including Participantes), Partidos,
    /// and each Partido's PrediccionesPartido (with Participante) for the Resumen view.
    /// Avoids N+1 by using ThenInclude to load all predictions in a single query.
    /// </summary>
    Task<Jornada?> GetByIdWithResumenAsync(int id);
    Task<IReadOnlyList<Jornada>> GetAllAsync();
    Task<IReadOnlyList<Jornada>> GetByTorneoIdAsync(int torneoId);
    Task<IReadOnlyList<Jornada>> GetByEstadoAsync(EstadoJornada estado);
    Task<Jornada?> GetByTorneoAndEstadoAsync(int torneoId, EstadoJornada estado);
    Task<Jornada> AddAsync(Jornada jornada);
    Task UpdateAsync(Jornada jornada);
}
