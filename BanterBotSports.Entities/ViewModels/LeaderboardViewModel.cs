using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.Entities.ViewModels;

public record LeaderboardViewModel(
    string TorneoNombre,
    int TorneoId,
    IReadOnlyList<RankingParticipante> Ranking);
