using BanterBotSports.BanterAI;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.Web.Hubs;

/// <summary>
/// SignalR hub for bidirectional quiniela chat.
/// Players connect with a torneoId query param. Non-participants are rejected on connect.
/// Messages are persisted, broadcast to the torneo group, and optionally trigger BanterBot reply.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IChatBroadcaster _chatBroadcaster;
    private readonly IBanterEngine _banterEngine;
    private readonly IParticipanteRepository _participanteRepository;
    private readonly ITorneoRepository _torneoRepository;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IChatService chatService,
        IChatBroadcaster chatBroadcaster,
        IBanterEngine banterEngine,
        IParticipanteRepository participanteRepository,
        ITorneoRepository torneoRepository,
        ILogger<ChatHub> logger)
    {
        ArgumentNullException.ThrowIfNull(chatService);
        ArgumentNullException.ThrowIfNull(chatBroadcaster);
        ArgumentNullException.ThrowIfNull(banterEngine);
        ArgumentNullException.ThrowIfNull(participanteRepository);
        ArgumentNullException.ThrowIfNull(torneoRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _chatService = chatService;
        _chatBroadcaster = chatBroadcaster;
        _banterEngine = banterEngine;
        _participanteRepository = participanteRepository;
        _torneoRepository = torneoRepository;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var torneoIdStr = Context.GetHttpContext()?.Request.Query["torneoId"].ToString();

        if (!int.TryParse(torneoIdStr, out var torneoId))
        {
            _logger.LogWarning("ChatHub: connection refused — missing or invalid torneoId.");
            Context.Abort();
            return;
        }

        var userId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("ChatHub: connection refused — unauthenticated user.");
            Context.Abort();
            return;
        }

        var participante = await _participanteRepository.GetByTorneoAndUserAsync(torneoId, userId);
        if (participante is null)
        {
            _logger.LogWarning(
                "ChatHub: connection refused — user {UserId} is not a participant of torneo {TorneoId}.",
                userId, torneoId);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(torneoId));
        _logger.LogDebug(
            "ChatHub: user {UserId} joined group {Group}.", userId, GroupName(torneoId));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var torneoIdStr = Context.GetHttpContext()?.Request.Query["torneoId"].ToString();

        if (int.TryParse(torneoIdStr, out var torneoId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(torneoId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by clients to send a chat message.
    /// Persists the message, broadcasts it to the group, and optionally generates a
    /// BanterBot reply when the message contains "@banterbot".
    /// </summary>
    public async Task SendMessage(int torneoId, string contenido)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(userId))
            return;

        MensajeChat mensaje;
        try
        {
            mensaje = await _chatService.SaveMessageAsync(torneoId, userId, contenido);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "ChatHub.SendMessage: user {UserId} is not a participant.", userId);
            return;
        }

        await _chatBroadcaster.BroadcastMessageAsync(torneoId, mensaje);

        // @banterbot detection — case-insensitive
        if (contenido.Contains("@banterbot", StringComparison.OrdinalIgnoreCase))
        {
            await GenerateBanterBotReplyAsync(torneoId, userId, contenido, mensaje.NombreDisplay);
        }
    }

    private async Task GenerateBanterBotReplyAsync(
        int torneoId, string userId, string playerMessage, string playerName)
    {
        try
        {
            var torneo = await _torneoRepository.GetByIdAsync(torneoId);
            if (torneo is null) return;

            var reply = await _banterEngine.GenerateChatReplyAsync(playerMessage, playerName, torneo);

            if (string.IsNullOrWhiteSpace(reply)) return;

            var banterMessage = await _chatService.SaveBanterBotMessageAsync(
                torneoId, reply, TipoMensajeChat.RespuestaMencion);

            await _chatBroadcaster.BroadcastMessageAsync(torneoId, banterMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ChatHub: error generating BanterBot reply for user {UserId} in torneo {TorneoId}.",
                userId, torneoId);
            // Do NOT propagate — bot failure must not disrupt player messaging
        }
    }

    /// <summary>
    /// Broadcasts a message to all clients in a torneo chat group.
    /// Called from server-side services (e.g. ChatJornadaNotifier, ResultSyncService).
    /// </summary>
    public static Task BroadcastAsync(
        IHubContext<ChatHub> hubContext,
        int torneoId,
        MensajeChat mensaje)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(mensaje);

        return hubContext.Clients
            .Group(GroupName(torneoId))
            .SendAsync("NuevoMensaje", new
            {
                mensaje.Id,
                mensaje.TorneoId,
                mensaje.UserId,
                mensaje.NombreDisplay,
                mensaje.Contenido,
                FechaUtc = mensaje.FechaUtc.ToString("o"),
                TipoMensaje = mensaje.TipoMensaje.ToString()
            });
    }

    private static string GroupName(int torneoId) => $"chat-{torneoId}";
}
