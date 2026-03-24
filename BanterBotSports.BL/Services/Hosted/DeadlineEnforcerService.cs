using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.BL.Services.Hosted;

/// <summary>
/// Background service that periodically checks for jornadas past their deadline
/// and automatically closes them.
/// </summary>
public class DeadlineEnforcerService : IHostedService, IAsyncDisposable
{
    private readonly IJornadaRepository _jornadaRepository;
    private readonly IJornadaService _jornadaService;
    private readonly ILogger<DeadlineEnforcerService> _logger;

    private readonly TimeSpan _interval = TimeSpan.FromSeconds(60);
    private PeriodicTimer? _timer;
    private Task? _executingTask;
    private CancellationTokenSource? _cts;

    public DeadlineEnforcerService(
        IJornadaRepository jornadaRepository,
        IJornadaService jornadaService,
        ILogger<DeadlineEnforcerService> logger)
    {
        ArgumentNullException.ThrowIfNull(jornadaRepository);
        ArgumentNullException.ThrowIfNull(jornadaService);
        ArgumentNullException.ThrowIfNull(logger);

        _jornadaRepository = jornadaRepository;
        _jornadaService = jornadaService;
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
            var now = DateTimeOffset.UtcNow;
            var jornadasAbiertas = await _jornadaRepository.GetByEstadoAsync(EstadoJornada.Abierta);

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

                    await _jornadaService.CerrarJornadaAsync(jornada.Id);
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
