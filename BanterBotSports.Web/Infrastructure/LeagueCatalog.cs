namespace BanterBotSports.Web.Infrastructure;

public record LeagueEntry(int Id, string Name, string LogoUrl);

public static class LeagueCatalog
{
    public static readonly IReadOnlyList<LeagueEntry> Leagues = new[]
    {
        new LeagueEntry(39,  "Premier League",    "https://media.api-sports.io/football/leagues/39.png"),
        new LeagueEntry(140, "La Liga",            "https://media.api-sports.io/football/leagues/140.png"),
        new LeagueEntry(135, "Serie A",            "https://media.api-sports.io/football/leagues/135.png"),
        new LeagueEntry(78,  "Bundesliga",         "https://media.api-sports.io/football/leagues/78.png"),
        new LeagueEntry(61,  "Ligue 1",            "https://media.api-sports.io/football/leagues/61.png"),
        new LeagueEntry(2,   "Champions League",   "https://media.api-sports.io/football/leagues/2.png"),
        new LeagueEntry(262, "Liga MX",            "https://media.api-sports.io/football/leagues/262.png"),
        new LeagueEntry(128, "Liga Argentina",     "https://media.api-sports.io/football/leagues/128.png"),
        new LeagueEntry(13,  "Copa Libertadores",  "https://media.api-sports.io/football/leagues/13.png"),
        new LeagueEntry(253, "MLS",                "https://media.api-sports.io/football/leagues/253.png"),
        new LeagueEntry(88,  "Eredivisie",         "https://media.api-sports.io/football/leagues/88.png"),
        new LeagueEntry(71,  "Brasileirão",        "https://media.api-sports.io/football/leagues/71.png"),
    };

    public static readonly IReadOnlySet<int> ValidIds =
        new HashSet<int>(Leagues.Select(l => l.Id));
}
