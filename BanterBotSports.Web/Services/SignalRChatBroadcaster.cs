using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BanterBotSports.Web.Services;

/// <summary>
/// Web layer implementation of IChatBroadcaster.
/// Delegates broadcasting to ChatHub via IHubContext.
/// </summary>
public sealed class SignalRChatBroadcaster : IChatBroadcaster
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRChatBroadcaster(IHubContext<ChatHub> hubContext)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task BroadcastMessageAsync(int torneoId, MensajeChat mensaje)
        => ChatHub.BroadcastAsync(_hubContext, torneoId, mensaje);
}
