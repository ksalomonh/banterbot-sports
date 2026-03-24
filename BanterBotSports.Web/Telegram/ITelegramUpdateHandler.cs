using Telegram.Bot.Types;

namespace BanterBotSports.Web.Telegram;

public interface ITelegramUpdateHandler
{
    Task HandleAsync(Update update, CancellationToken cancellationToken = default);
}
