using BanterBotSports.Entities.DTOs;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.Integrations.Telegram;

/// <summary>
/// No-op implementation of <see cref="ITelegramBotService"/> used when
/// <c>Telegram:BotToken</c> is not configured (e.g. local dev / QA environments).
/// All operations are silently skipped and logged at Warning level.
/// </summary>
public sealed class NullTelegramBotService : ITelegramBotService
{
    private readonly ILogger<NullTelegramBotService> _logger;

    public NullTelegramBotService(ILogger<NullTelegramBotService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task SendMessageAsync(long chatId, string text)
    {
        _logger.LogWarning(
            "NullTelegramBotService: SendMessageAsync skipped — Telegram:BotToken not configured. chatId={ChatId}",
            chatId);
        return Task.CompletedTask;
    }

    public Task SendConfirmationListAsync(long chatId, IReadOnlyList<string> predictions)
    {
        _logger.LogWarning(
            "NullTelegramBotService: SendConfirmationListAsync skipped — Telegram:BotToken not configured. chatId={ChatId}",
            chatId);
        return Task.CompletedTask;
    }

    public Task SendMatchesListAsync(long chatId, IReadOnlyList<PartidoDto> partidos)
    {
        _logger.LogWarning(
            "NullTelegramBotService: SendMatchesListAsync skipped — Telegram:BotToken not configured. chatId={ChatId}",
            chatId);
        return Task.CompletedTask;
    }

    public Task SetWebhookAsync(string webhookUrl)
    {
        _logger.LogWarning(
            "NullTelegramBotService: SetWebhookAsync skipped — Telegram:BotToken not configured. url={Url}",
            webhookUrl);
        return Task.CompletedTask;
    }
}
