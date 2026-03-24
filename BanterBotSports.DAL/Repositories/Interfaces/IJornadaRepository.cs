using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;

namespace BanterBotSports.DAL.Repositories.Interfaces;

public interface IJornadaRepository
{
    Task<Jornada?> GetByIdAsync(int id);
    Task<Jornada?> GetByIdWithDetailsAsync(int id);
    Task<IReadOnlyList<Jornada>> GetAllAsync();
    Task<IReadOnlyList<Jornada>> GetByTorneoIdAsync(int torneoId);
    Task<IReadOnlyList<Jornada>> GetByEstadoAsync(EstadoJornada estado);
    Task<Jornada> AddAsync(Jornada jornada);
    Task UpdateAsync(Jornada jornada);
}
