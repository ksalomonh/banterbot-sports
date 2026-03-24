using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;

namespace BanterBotSports.DAL.Repositories.Interfaces;

public interface IPartidoRepository
{
    Task<Partido?> GetByIdAsync(int id);
    Task<IReadOnlyList<Partido>> GetAllAsync();
    Task<IReadOnlyList<Partido>> GetByJornadaIdAsync(int jornadaId);
    Task<IReadOnlyList<Partido>> GetByEstadoAsync(EstadoPartido estado);
    Task<IReadOnlyList<Partido>> GetByKickOffRangeAsync(DateTimeOffset from, DateTimeOffset to);
    Task<Partido?> GetByExternalIdAsync(string externalId);
    Task<Partido> AddAsync(Partido partido);
    Task UpdateAsync(Partido partido);
}
