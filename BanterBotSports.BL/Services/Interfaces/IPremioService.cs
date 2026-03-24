using BanterBotSports.BL.Models;
using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services.Interfaces;

public interface IPremioService
{
    IReadOnlyList<PremioDistribucion> CalcularDistribucion(IReadOnlyList<RankingParticipante> rankings, Torneo torneo);
}
