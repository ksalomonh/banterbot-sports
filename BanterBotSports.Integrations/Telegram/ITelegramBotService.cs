using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.Integrations.Telegram;

public interface ITelegramBotService
{
    Task SendMessageAsync(long chatId, string text);
    Task SendConfirmationListAsync(long chatId, IReadOnlyList<string> predictions);
    Task SetWebhookAsync(string webhookUrl);

    /// <summary>
    /// Sends a formatted match list for the upcoming jornada to the given Telegram chat.
    /// Each partido is rendered with kick-off time and team names.
    /// </summary>
    Task SendMatchesListAsync(long chatId, IReadOnlyList<PartidoDto> partidos);
}
