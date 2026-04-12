using BanterBotSports.BL.Models;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.BL.Services.Interfaces;

public interface IPremioService
{
    IReadOnlyList<PremioDistribucion> CalcularDistribucion(IReadOnlyList<RankingParticipante> rankings, Torneo torneo);
}
