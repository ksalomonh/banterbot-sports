using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services.Interfaces;

/// <summary>
/// Abstraction for broadcasting chat messages to connected clients via SignalR.
/// Implemented in the Web layer; consumed by BL services.
/// Lives in BL so that BL services can use it without depending on Web.
/// </summary>
public interface IChatBroadcaster
{
    /// <summary>
    /// Broadcasts a chat message to all clients in the torneo chat group.
    /// </summary>
    Task BroadcastMessageAsync(int torneoId, MensajeChat mensaje);
}
