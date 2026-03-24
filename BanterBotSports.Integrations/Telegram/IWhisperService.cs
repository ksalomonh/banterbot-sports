namespace BanterBotSports.Integrations.Telegram;

public interface IWhisperService
{
    Task<string> TranscribeAsync(string telegramFileId);
}
