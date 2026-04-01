using BanterBotSports.Entities;

namespace BanterBotSports.DAL.Repositories.Interfaces;

public interface IChatRepository
{
    /// <summary>
    /// Returns messages for a torneo ordered by FechaUtc descending.
    /// If beforeId is provided, only messages with Id &lt; beforeId are returned (cursor-based pagination).
    /// </summary>
    Task<IReadOnlyList<MensajeChat>> GetByTorneoAsync(int torneoId, int limit, long? beforeId = null);

    Task<MensajeChat> AddAsync(MensajeChat mensaje);
}
