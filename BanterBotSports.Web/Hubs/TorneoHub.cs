using BanterBotSports.Entities.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace BanterBotSports.Web.Hubs;

/// <summary>
/// SignalR hub for real-time torneo updates.
/// Clients connect providing a torneoId query parameter and join the corresponding group.
/// </summary>
public class TorneoHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var torneoIdStr = Context.GetHttpContext()?.Request.Query["torneoId"].ToString();

        if (!string.IsNullOrWhiteSpace(torneoIdStr) && int.TryParse(torneoIdStr, out var torneoId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(torneoId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var torneoIdStr = Context.GetHttpContext()?.Request.Query["torneoId"].ToString();

        if (!string.IsNullOrWhiteSpace(torneoIdStr) && int.TryParse(torneoIdStr, out var torneoId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(torneoId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Broadcasts updated ranking to all clients watching a torneo.
    /// Called from ResultSyncService (or any server-side service) after score update.
    /// </summary>
    public static Task BroadcastAsync(
        IHubContext<TorneoHub> hubContext,
        int torneoId,
        IReadOnlyList<RankingParticipante> ranking)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(ranking);

        return hubContext.Clients
            .Group(GroupName(torneoId))
            .SendAsync("RankingActualizado", ranking);
    }

    private static string GroupName(int torneoId) => $"torneo-{torneoId}";
}
