using BanterBotSports.Entities;

namespace BanterBotSports.DAL.Repositories.Interfaces;

public interface IParticipanteRepository
{
    Task<Participante?> GetByIdAsync(int id);
    Task<IReadOnlyList<Participante>> GetAllAsync();
    Task<IReadOnlyList<Participante>> GetByTorneoIdAsync(int torneoId);
    Task<Participante?> GetByTorneoAndUserAsync(int torneoId, string userId);
    Task<IReadOnlyList<Participante>> GetByUserIdAsync(string userId);
    Task<Participante> AddAsync(Participante participante);
    Task UpdateAsync(Participante participante);
    Task DeleteAsync(Participante participante);
}
