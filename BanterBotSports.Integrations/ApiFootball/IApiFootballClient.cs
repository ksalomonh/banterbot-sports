using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.Integrations.ApiFootball;

public interface IApiFootballClient
{
    Task<IReadOnlyList<PartidoDto>> GetMatchesAsync(int competitionId, DateOnly from, DateOnly to);
    Task<PartidoDto?> GetLiveScoreAsync(int externalId);
}
