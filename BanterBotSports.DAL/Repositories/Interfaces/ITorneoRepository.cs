using BanterBotSports.Entities;

namespace BanterBotSports.DAL.Repositories.Interfaces;

public interface ITorneoRepository
{
    Task<Torneo?> GetByIdAsync(int id);
    Task<Torneo?> GetByIdWithDetailsAsync(int id);
    Task<IReadOnlyList<Torneo>> GetAllAsync();
    Task<IReadOnlyList<Torneo>> GetByOrganizadorIdAsync(string organizadorId);
    Task<Torneo> AddAsync(Torneo torneo);
    Task UpdateAsync(Torneo torneo);
    Task DeleteAsync(Torneo torneo);
}
