using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.Integrations.ApiFootball;

/// <summary>
/// Pure HTTP client for the API-Football v3 API.
/// Caching in PostgreSQL is handled by the service layer (e.g., ResultSyncService).
/// </summary>
public class ApiFootballClient : IApiFootballClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;
    private readonly ILogger<ApiFootballClient> _logger;

    private const string ClientName = "ApiFootball";
    private const string BaseAddress = "https://v3.football.api-sports.io";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiFootballClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ApiFootballClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = configuration["ApiFootball:ApiKey"]
            ?? throw new InvalidOperationException("ApiFootball:ApiKey configuration is required.");
    }

    public async Task<IReadOnlyList<PartidoDto>> GetMatchesAsync(int competitionId, DateOnly from, DateOnly to)
    {
        var year = from.Year;
        var url = $"{BaseAddress}/fixtures?league={competitionId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&season={year}";

        try
        {
            var apiResponse = await FetchAsync<ApiFootballResponse>(url);

            if (apiResponse?.Response is null || apiResponse.Response.Count == 0)
                return Array.Empty<PartidoDto>();

            return apiResponse.Response
                .Select(MapFixtureToDto)
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching matches for competition {CompetitionId} from {From} to {To}",
                competitionId, from, to);
            return Array.Empty<PartidoDto>();
        }
    }

    public async Task<PartidoDto?> GetLiveScoreAsync(int externalId)
    {
        var url = $"{BaseAddress}/fixtures?id={externalId}&live=all";

        try
        {
            var apiResponse = await FetchAsync<ApiFootballResponse>(url);
            var fixture = apiResponse?.Response?.FirstOrDefault();
            return fixture is null ? null : MapFixtureToDto(fixture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching live score for fixture {ExternalId}", externalId);
            return null;
        }
    }

    public async Task<PartidoDto?> GetFixtureByIdAsync(int externalId, CancellationToken ct = default)
    {
        var url = $"{BaseAddress}/fixtures?id={externalId}";

        try
        {
            var apiResponse = await FetchAsync<ApiFootballResponse>(url, ct);
            var fixture = apiResponse?.Response?.FirstOrDefault();
            return fixture is null ? null : MapFixtureToDto(fixture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fixture by id {ExternalId}", externalId);
            return null;
        }
    }

    private async Task<T?> FetchAsync<T>(string url, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-apisports-key", _apiKey);

        var client = _httpClientFactory.CreateClient(ClientName);
        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static PartidoDto MapFixtureToDto(ApiFixture fixture)
    {
        var kickOff = DateTimeOffset.FromUnixTimeSeconds(fixture.Fixture.Timestamp);
        var estado = MapEstado(fixture.Fixture.Status?.Short);

        return new PartidoDto(
            Id: fixture.Fixture.Id,
            ExternalId: fixture.Fixture.Id.ToString(),
            Equipo1: fixture.Teams?.Home?.Name ?? string.Empty,
            Equipo2: fixture.Teams?.Away?.Name ?? string.Empty,
            KickOffUtc: kickOff,
            GolesEquipo1: fixture.Goals?.Home,
            GolesEquipo2: fixture.Goals?.Away,
            Estado: estado,
            LogoUrlEquipo1: fixture.Teams?.Home?.Logo,
            LogoUrlEquipo2: fixture.Teams?.Away?.Logo
        );
    }

    private static EstadoPartido MapEstado(string? shortStatus) => shortStatus switch
    {
        "NS" => EstadoPartido.Programado,
        "1H" or "HT" or "2H" or "ET" or "P" or "LIVE" => EstadoPartido.EnCurso,
        "FT" or "AET" or "PEN" => EstadoPartido.Finalizado,
        "SUSP" => EstadoPartido.Suspendido,
        "PST" => EstadoPartido.Aplazado,
        _ => EstadoPartido.Programado
    };

    // --- API response shape ---

    private sealed class ApiFootballResponse
    {
        [JsonPropertyName("response")]
        public List<ApiFixture>? Response { get; set; }
    }

    private sealed class ApiFixture
    {
        [JsonPropertyName("fixture")]
        public ApiFixtureInfo Fixture { get; set; } = null!;

        [JsonPropertyName("teams")]
        public ApiTeams? Teams { get; set; }

        [JsonPropertyName("goals")]
        public ApiGoals? Goals { get; set; }
    }

    private sealed class ApiFixtureInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("status")]
        public ApiStatus? Status { get; set; }
    }

    private sealed class ApiStatus
    {
        [JsonPropertyName("short")]
        public string? Short { get; set; }
    }

    private sealed class ApiTeams
    {
        [JsonPropertyName("home")]
        public ApiTeam? Home { get; set; }

        [JsonPropertyName("away")]
        public ApiTeam? Away { get; set; }
    }

    private sealed class ApiTeam
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("logo")]
        public string? Logo { get; set; }
    }

    private sealed class ApiGoals
    {
        [JsonPropertyName("home")]
        public int? Home { get; set; }

        [JsonPropertyName("away")]
        public int? Away { get; set; }
    }
}
