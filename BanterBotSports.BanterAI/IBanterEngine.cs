using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.BanterAI;

public interface IBanterEngine
{
    Task<string> GenerateBanterAsync(ParticipanteStats stats, Torneo torneo);

    /// <summary>
    /// Generates a chat reply from BanterBot to a player @mention.
    /// Max 280 characters. Returns empty string on failure (caller decides to skip).
    /// </summary>
    Task<string> GenerateChatReplyAsync(string playerMessage, string playerName, Torneo torneo);
}
