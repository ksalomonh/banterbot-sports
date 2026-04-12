using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.Integrations.ApiFootball;

/// <summary>
/// Adapter that exposes the API-Football sync service as the domain-level <see cref="IPartidoCatalogService"/>.
/// Bridges the Integrations layer to the BL/Web layers without leaking Integrations-specific types.
/// </summary>
public sealed class ApiFootballCatalogService : IPartidoCatalogService
{
    private static readonly IReadOnlyList<LigaDto> _ligas = new LigaDto[]
    {
        new(39,  "Premier League",    "https://media.api-sports.io/football/leagues/39.png"),
        new(140, "La Liga",            "https://media.api-sports.io/football/leagues/140.png"),
        new(135, "Serie A",            "https://media.api-sports.io/football/leagues/135.png"),
        new(78,  "Bundesliga",         "https://media.api-sports.io/football/leagues/78.png"),
        new(61,  "Ligue 1",            "https://media.api-sports.io/football/leagues/61.png"),
        new(2,   "Champions League",   "https://media.api-sports.io/football/leagues/2.png"),
        new(262, "Liga MX",            "https://media.api-sports.io/football/leagues/262.png"),
        new(128, "Liga Argentina",     "https://media.api-sports.io/football/leagues/128.png"),
        new(13,  "Copa Libertadores",  "https://media.api-sports.io/football/leagues/13.png"),
        new(253, "MLS",                "https://media.api-sports.io/football/leagues/253.png"),
        new(88,  "Eredivisie",         "https://media.api-sports.io/football/leagues/88.png"),
        new(71,  "Brasileirão",        "https://media.api-sports.io/football/leagues/71.png"),
    };

    private static readonly IReadOnlySet<int> _validIds =
        new HashSet<int>(_ligas.Select(l => l.Id));

    private readonly IApiFootballSyncService _apiFootballSyncService;

    public ApiFootballCatalogService(IApiFootballSyncService apiFootballSyncService)
    {
        ArgumentNullException.ThrowIfNull(apiFootballSyncService);
        _apiFootballSyncService = apiFootballSyncService;
    }

    public Task<IReadOnlyList<PartidoDto>> GetProximosPartidosAsync(int competitionId, DateOnly from, DateOnly to)
        => _apiFootballSyncService.GetMatchesAsync(competitionId, from, to);

    public Task<PartidoDto?> GetFixturePorExternalIdAsync(int externalId, CancellationToken ct = default)
        => _apiFootballSyncService.GetFixtureByIdAsync(externalId, ct);

    public bool EsLigaValida(int competitionId)
        => _validIds.Contains(competitionId);

    public IReadOnlyList<LigaDto> GetLigas()
        => _ligas;
}
