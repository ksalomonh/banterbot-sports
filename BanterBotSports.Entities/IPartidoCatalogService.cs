using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.Entities;

/// <summary>
/// Abstraction for fetching match catalog data from an external source.
/// Decouples the BL and Web layers from the concrete Integrations implementation.
/// </summary>
public interface IPartidoCatalogService
{
    /// <summary>Returns upcoming matches for the given competition and date range.</summary>
    Task<IReadOnlyList<PartidoDto>> GetProximosPartidosAsync(int competitionId, DateOnly from, DateOnly to);

    /// <summary>Returns fixture data for the given external ID, or null if not found.</summary>
    Task<PartidoDto?> GetFixturePorExternalIdAsync(int externalId, CancellationToken ct = default);

    /// <summary>Returns whether the given competition ID is valid in this catalog.</summary>
    bool EsLigaValida(int competitionId);

    /// <summary>Returns all available leagues.</summary>
    IReadOnlyList<LigaDto> GetLigas();
}
