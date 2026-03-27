using System.Text;
using BanterBotSports.Entities.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BanterBotSports.Integrations.Telegram;

public class TelegramBotService : ITelegramBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotService> _logger;

    private const string ClientName = "TelegramBot";

    public TelegramBotService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TelegramBotService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        var botToken = configuration["Telegram:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
            throw new InvalidOperationException("Telegram:BotToken configuration is required and must not be empty.");

        var httpClient = httpClientFactory.CreateClient(ClientName);
        _botClient = new TelegramBotClient(botToken, httpClient);
    }

    public async Task SendMessageAsync(long chatId, string text)
    {
        try
        {
            await _botClient.SendMessage(
                chatId: new ChatId(chatId),
                text: text,
                parseMode: ParseMode.None
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Telegram message to chatId {ChatId}", chatId);
            throw;
        }
    }

    public async Task SendConfirmationListAsync(long chatId, IReadOnlyList<string> predictions)
    {
        if (predictions.Count == 0)
        {
            await SendMessageAsync(chatId, "No se recibieron predicciones.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Tus predicciones registradas:");
        sb.AppendLine();

        for (int i = 0; i < predictions.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {predictions[i]}");
        }

        await SendMessageAsync(chatId, sb.ToString().TrimEnd());
    }

    public async Task SendMatchesListAsync(long chatId, IReadOnlyList<PartidoDto> partidos)
    {
        if (partidos.Count == 0)
        {
            await SendMessageAsync(chatId, "No hay partidos programados para esta jornada.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("⚽ Partidos de la próxima jornada:");
        sb.AppendLine();

        foreach (var partido in partidos)
        {
            // Format kick-off in a human-readable local-ish string (UTC explicit)
            var kickOff = partido.KickOffUtc.ToString("ddd dd/MM HH:mm");
            sb.AppendLine($"🏟 {partido.Equipo1} vs {partido.Equipo2}");
            sb.AppendLine($"   🕐 {kickOff} UTC");
        }

        await SendMessageAsync(chatId, sb.ToString().TrimEnd());
    }

    public async Task SetWebhookAsync(string webhookUrl)
    {
        try
        {
            await _botClient.SetWebhook(url: webhookUrl);
            _logger.LogInformation("Telegram webhook configured: {WebhookUrl}", webhookUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Telegram webhook to {WebhookUrl}", webhookUrl);
            throw;
        }
    }
}
