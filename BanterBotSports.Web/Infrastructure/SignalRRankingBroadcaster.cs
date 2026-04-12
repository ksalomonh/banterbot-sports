using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BanterBotSports.Web.Infrastructure;

/// <summary>
/// Web layer implementation of IRankingBroadcaster.
/// Delegates broadcasting to TorneoHub via IHubContext.
/// </summary>
public sealed class SignalRRankingBroadcaster : IRankingBroadcaster
{
    private readonly IHubContext<TorneoHub> _hubContext;

    public SignalRRankingBroadcaster(IHubContext<TorneoHub> hubContext)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public Task BroadcastRankingAsync(int torneoId, IReadOnlyList<RankingParticipante> ranking)
        => TorneoHub.BroadcastAsync(_hubContext, torneoId, ranking);
}
