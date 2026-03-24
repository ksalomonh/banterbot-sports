using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.Integrations.ApiFootball;

/// <summary>
/// Wraps IApiFootballClient with PostgreSQL caching.
/// Reads from DB when cache is available; calls the API otherwise.
/// Callers should use this service instead of IApiFootballClient directly.
/// </summary>
public interface IApiFootballSyncService
{
    /// <summary>
    /// Returns matches for the given competition and date range.
    /// Serves from PostgreSQL cache when data exists; fetches from API-Football otherwise.
    /// </summary>
    Task<IReadOnlyList<PartidoDto>> GetMatchesAsync(int competitionId, DateOnly from, DateOnly to);

    /// <summary>
    /// Fetches live score for a match from API-Football and updates the PostgreSQL cache.
    /// </summary>
    Task<PartidoDto?> GetLiveScoreAsync(int externalId);
}
