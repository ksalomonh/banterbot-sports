using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.BL.Services.Hosted;

/// <summary>
/// Background service that periodically checks for jornadas past their deadline
/// and automatically closes them.
/// </summary>
public class DeadlineEnforcerService : IHostedService, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeadlineEnforcerService> _logger;

    private readonly TimeSpan _interval = TimeSpan.FromSeconds(60);
    private PeriodicTimer? _timer;
    private Task? _executingTask;
    private CancellationTokenSource? _cts;

    public DeadlineEnforcerService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeadlineEnforcerService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(_interval);
        _executingTask = ExecuteAsync(_cts.Token);

        _logger.LogInformation("DeadlineEnforcerService started. Checking every {Interval}s.", _interval.TotalSeconds);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DeadlineEnforcerService stopping.");

        if (_cts is not null)
            await _cts.CancelAsync();

        if (_executingTask is not null)
        {
            try { await _executingTask.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { /* expected */ }
        }
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (_timer is null) return;

        while (await _timer.WaitForNextTickAsync(cancellationToken))
        {
            await CheckAndCloseDeadlineJornadasAsync(cancellationToken);
        }
    }

    private async Task CheckAndCloseDeadlineJornadasAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var jornadaRepository = scope.ServiceProvider.GetRequiredService<IJornadaRepository>();
            var jornadaService = scope.ServiceProvider.GetRequiredService<IJornadaService>();

            var now = DateTimeOffset.UtcNow;
            var jornadasAbiertas = await jornadaRepository.GetByEstadoAsync(EstadoJornada.Abierta);

            var jornadasVencidas = jornadasAbiertas
                .Where(j => j.DeadlineUtc.HasValue && j.DeadlineUtc.Value <= now)
                .ToList();

            foreach (var jornada in jornadasVencidas)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _logger.LogInformation(
                        "Closing jornada {JornadaId} (#{Numero}) — deadline {Deadline} has passed.",
                        jornada.Id, jornada.Numero, jornada.DeadlineUtc);

                    await jornadaService.CerrarJornadaAsync(jornada.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing jornada {JornadaId} after deadline.", jornada.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Service is stopping — stop gracefully
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in DeadlineEnforcerService tick.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }
    }
}
