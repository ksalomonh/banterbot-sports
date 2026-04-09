using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.BL.Services.Interfaces;

/// <summary>
/// Abstraction for broadcasting ranking updates to connected clients.
/// Implemented in the Web layer via SignalR; consumed by Integrations layer.
/// This interface lives in BL so Integrations can reference it without depending on Web.
/// </summary>
public interface IRankingBroadcaster
{
    /// <summary>
    /// Broadcasts the updated ranking for a torneo to all connected clients.
    /// </summary>
    Task BroadcastRankingAsync(int torneoId, IReadOnlyList<RankingParticipante> ranking);
}
