using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities.Enums;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.BanterAI;

/// <summary>
/// Generates and persists BanterBot score commentary when live match scores change.
/// On AI failure: logs the error and silently skips — does NOT propagate exceptions.
/// </summary>
public class ChatBanterService : IChatBanterService
{
    private readonly IBanterEngine _banterEngine;
    private readonly IChatService _chatService;
    private readonly ITorneoRepository _torneoRepository;
    private readonly ILogger<ChatBanterService> _logger;

    public ChatBanterService(
        IBanterEngine banterEngine,
        IChatService chatService,
        ITorneoRepository torneoRepository,
        ILogger<ChatBanterService> logger)
    {
        ArgumentNullException.ThrowIfNull(banterEngine);
        ArgumentNullException.ThrowIfNull(chatService);
        ArgumentNullException.ThrowIfNull(torneoRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _banterEngine = banterEngine;
        _chatService = chatService;
        _torneoRepository = torneoRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnScoreUpdatedAsync(
        int torneoId, int partidoId, int goles1, int goles2, string equipo1, string equipo2)
    {
        var torneo = await _torneoRepository.GetByIdAsync(torneoId);

        if (torneo is null)
        {
            _logger.LogWarning(
                "ChatBanterService: torneo {TorneoId} not found — skipping banter for partido {PartidoId}.",
                torneoId, partidoId);
            return;
        }

        try
        {
            var prompt = $"{equipo1} {goles1} - {goles2} {equipo2}";
            var banter = await _banterEngine.GenerateChatReplyAsync(prompt, "el partido", torneo);

            if (string.IsNullOrWhiteSpace(banter))
            {
                _logger.LogDebug(
                    "ChatBanterService: empty banter returned for partido {PartidoId} — skipping.",
                    partidoId);
                return;
            }

            await _chatService.SaveBanterBotMessageAsync(torneoId, banter, TipoMensajeChat.ResultadoBanter);

            _logger.LogInformation(
                "ChatBanterService: banter posted for partido {PartidoId} ({Equipo1} {G1}-{G2} {Equipo2}).",
                partidoId, equipo1, goles1, goles2, equipo2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ChatBanterService: error generating banter for partido {PartidoId} in torneo {TorneoId}.",
                partidoId, torneoId);
            // Do NOT re-throw — AI failure must not disrupt live score processing
        }
    }
}
