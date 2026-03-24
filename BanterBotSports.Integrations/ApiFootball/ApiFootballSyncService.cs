using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.Integrations.ApiFootball;

/// <summary>
/// Caching wrapper for IApiFootballClient.
/// Reads from PostgreSQL when cached data is available for the requested range.
/// Calls the API otherwise and persists results to PostgreSQL before returning.
/// </summary>
public class ApiFootballSyncService : IApiFootballSyncService
{
    private readonly IApiFootballClient _apiClient;
    private readonly IPartidoRepository _partidoRepository;
    private readonly AppDbContext _context;
    private readonly ILogger<ApiFootballSyncService> _logger;

    public ApiFootballSyncService(
        IApiFootballClient apiClient,
        IPartidoRepository partidoRepository,
        AppDbContext context,
        ILogger<ApiFootballSyncService> logger)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(partidoRepository);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _apiClient = apiClient;
        _partidoRepository = partidoRepository;
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PartidoDto>> GetMatchesAsync(int competitionId, DateOnly from, DateOnly to)
    {
        var fromOffset = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        // 1. Check PostgreSQL cache for matches in this date range
        var cached = await _partidoRepository.GetByKickOffRangeAsync(fromOffset, toOffset);

        if (cached.Count > 0)
        {
            _logger.LogDebug("Returning {Count} matches from PostgreSQL cache for range {From} to {To}.",
                cached.Count, from, to);

            return cached.Select(MapToDto).ToList().AsReadOnly();
        }

        // 2. Cache miss — fetch from API
        _logger.LogDebug("Cache miss for range {From}–{To} — fetching from API-Football.", from, to);
        var apiResults = await _apiClient.GetMatchesAsync(competitionId, from, to);

        // 3. Persist to PostgreSQL — update existing records by externalId
        await PersistMatchResultsAsync(apiResults);

        return apiResults;
    }

    public async Task<PartidoDto?> GetLiveScoreAsync(int externalId)
    {
        var liveScore = await _apiClient.GetLiveScoreAsync(externalId);

        if (liveScore is not null)
            await PersistLiveScoreAsync(liveScore);

        return liveScore;
    }

    private async Task PersistMatchResultsAsync(IReadOnlyList<PartidoDto> partidos)
    {
        foreach (var dto in partidos)
        {
            if (dto.ExternalId is null) continue;

            var existing = await _partidoRepository.GetByExternalIdAsync(dto.ExternalId);

            if (existing is null)
            {
                _logger.LogDebug("Fixture {ExternalId} not found in DB — skipping insert (jornada not assigned yet).", dto.ExternalId);
                continue;
            }

            // Update cached match data
            existing.GolesEquipo1Oficial = dto.GolesEquipo1;
            existing.GolesEquipo2Oficial = dto.GolesEquipo2;
            existing.Estado = dto.Estado;

            await _partidoRepository.UpdateAsync(existing);
        }

        await _context.SaveChangesAsync();
    }

    private async Task PersistLiveScoreAsync(PartidoDto dto)
    {
        if (dto.ExternalId is null) return;

        var existing = await _partidoRepository.GetByExternalIdAsync(dto.ExternalId);

        if (existing is null)
        {
            _logger.LogDebug("Fixture {ExternalId} not in DB — cannot cache live score.", dto.ExternalId);
            return;
        }

        existing.GolesEquipo1Oficial = dto.GolesEquipo1;
        existing.GolesEquipo2Oficial = dto.GolesEquipo2;
        existing.Estado = dto.Estado;

        await _partidoRepository.UpdateAsync(existing);
        await _context.SaveChangesAsync();
    }

    private static PartidoDto MapToDto(Partido p) => new(
        Id: p.Id,
        ExternalId: p.ExternalId,
        Equipo1: p.Equipo1,
        Equipo2: p.Equipo2,
        KickOffUtc: p.KickOffUtc,
        GolesEquipo1: p.GolesEquipo1Oficial,
        GolesEquipo2: p.GolesEquipo2Oficial,
        Estado: p.Estado
    );
}
