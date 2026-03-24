using BanterBotSports.Web.Telegram;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace BanterBotSports.Web.Controllers;

[ApiController]
[Route("telegram/webhook")]
public class TelegramWebhookController : ControllerBase
{
    private const string TelegramSecretTokenHeader = "X-Telegram-Bot-Api-Secret-Token";

    private readonly TelegramUpdateQueue _queue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        TelegramUpdateQueue queue,
        IConfiguration configuration,
        ILogger<TelegramWebhookController> logger)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _queue = queue;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Receives a Telegram Update, validates the secret token, enqueues it for background
    /// processing, and immediately returns 200.
    /// Heavy work (Whisper, Claude) runs in TelegramUpdateWorker to stay within Telegram's 5s limit.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] Update update)
    {
        var expectedSecret = _configuration["Telegram:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(expectedSecret))
        {
            var headerValue = Request.Headers[TelegramSecretTokenHeader].FirstOrDefault();
            if (headerValue != expectedSecret)
            {
                _logger.LogWarning("Telegram webhook request rejected: invalid or missing secret token.");
                return Unauthorized();
            }
        }

        _logger.LogDebug("Received Telegram update {UpdateId}, type: {UpdateType}", update.Id, update.Type);

        // Enqueue for background processing. Pass the request cancellation token so that
        // if the queue is full and the request is cancelled (e.g. Telegram's 5s timeout),
        // we do not block indefinitely.
        await _queue.Writer.WriteAsync(update, HttpContext.RequestAborted);

        return Ok();
    }
}
