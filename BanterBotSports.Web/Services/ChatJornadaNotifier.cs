using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.Web.Services;

/// <summary>
/// Subscribes to <see cref="IJornadaService.JornadaAbierta"/> and posts a BanterBot
/// announcement message to the torneo chat when a jornada opens.
/// Registered as a singleton at the composition root.
/// Uses <see cref="IServiceScopeFactory"/> because the underlying services are scoped.
/// </summary>
public sealed class ChatJornadaNotifier
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatJornadaNotifier> _logger;

    public ChatJornadaNotifier(
        IServiceScopeFactory scopeFactory,
        ILogger<ChatJornadaNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the <see cref="IJornadaService.JornadaAbierta"/> event.
    /// Posts an announcement to the torneo chat via IChatService + IChatBroadcaster.
    /// </summary>
    public async Task OnJornadaAbiertaAsync(Jornada jornada)
    {
        _logger.LogInformation(
            "ChatJornadaNotifier: posting jornada announcement for jornada {JornadaId} (#{Numero}).",
            jornada.Id, jornada.Numero);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
            var chatBroadcaster = scope.ServiceProvider.GetRequiredService<IChatBroadcaster>();

            var announcement = $"🎯 ¡Jornada {jornada.Numero} abierta! Ya podés hacer tus predicciones. " +
                               $"El plazo cierra antes del primer partido. ¡Dale, que arranca!";

            var mensaje = await chatService.SaveBanterBotMessageAsync(
                jornada.TorneoId, announcement, TipoMensajeChat.AnuncioJornada);

            await chatBroadcaster.BroadcastMessageAsync(jornada.TorneoId, mensaje);

            _logger.LogInformation(
                "ChatJornadaNotifier: announcement posted to torneo {TorneoId} chat.",
                jornada.TorneoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ChatJornadaNotifier: error posting announcement for jornada {JornadaId}.",
                jornada.Id);
        }
    }
}
