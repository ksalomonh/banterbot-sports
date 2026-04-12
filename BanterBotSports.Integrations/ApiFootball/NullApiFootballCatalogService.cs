using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.Integrations.ApiFootball;

/// <summary>
/// Null-object implementation of <see cref="IPartidoCatalogService"/> used when API-Football is not configured.
/// Returns empty collections and null values without throwing.
/// </summary>
public sealed class NullApiFootballCatalogService : IPartidoCatalogService
{
    public Task<IReadOnlyList<PartidoDto>> GetProximosPartidosAsync(int competitionId, DateOnly from, DateOnly to)
        => Task.FromResult<IReadOnlyList<PartidoDto>>(Array.Empty<PartidoDto>());

    public Task<PartidoDto?> GetFixturePorExternalIdAsync(int externalId, CancellationToken ct = default)
        => Task.FromResult<PartidoDto?>(null);

    public bool EsLigaValida(int competitionId) => false;

    public IReadOnlyList<LigaDto> GetLigas() => Array.Empty<LigaDto>();
}
