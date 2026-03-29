using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.Integrations.ApiFootball;

/// <summary>
/// No-op implementation used when ApiFootball:ApiKey is not configured.
/// </summary>
public sealed class NullApiFootballClient : IApiFootballClient
{
    public Task<IReadOnlyList<PartidoDto>> GetMatchesAsync(int competitionId, DateOnly from, DateOnly to)
        => Task.FromResult<IReadOnlyList<PartidoDto>>(Array.Empty<PartidoDto>());

    public Task<PartidoDto?> GetLiveScoreAsync(int externalId)
        => Task.FromResult<PartidoDto?>(null);

    public Task<PartidoDto?> GetFixtureByIdAsync(int externalId, CancellationToken ct = default)
        => Task.FromResult<PartidoDto?>(null);
}
