using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.Web.Telegram;

/// <summary>
/// Background worker that drains the <see cref="TelegramUpdateQueue"/> and
/// processes each Telegram update via <see cref="ITelegramUpdateHandler"/>.
/// Scoped services are resolved per update to avoid captive dependency issues.
/// </summary>
public sealed class TelegramUpdateWorker : BackgroundService
{
    private readonly TelegramUpdateQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramUpdateWorker> _logger;

    public TelegramUpdateWorker(
        TelegramUpdateQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramUpdateWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelegramUpdateWorker started.");

        await foreach (var update in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ITelegramUpdateHandler>();
                await handler.HandleAsync(update, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error processing Telegram update {UpdateId}.", update.Id);
            }
        }

        _logger.LogInformation("TelegramUpdateWorker stopped.");
    }
}
