using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.BanterAI;

public interface IBanterEngine
{
    Task<string> GenerateBanterAsync(ParticipanteStats stats, Torneo torneo);
}
