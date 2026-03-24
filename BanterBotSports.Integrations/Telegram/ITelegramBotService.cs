namespace BanterBotSports.Integrations.Telegram;

public interface ITelegramBotService
{
    Task SendMessageAsync(long chatId, string text);
    Task SendConfirmationListAsync(long chatId, IReadOnlyList<string> predictions);
    Task SetWebhookAsync(string webhookUrl);
}
