using BanterBotSports.BL.Models;

namespace BanterBotSports.Web.Models;

public record LeaderboardViewModel(
    string TorneoNombre,
    int TorneoId,
    IReadOnlyList<RankingParticipante> Ranking);
