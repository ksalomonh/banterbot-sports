using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Integrations.ApiFootball;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.Integrations.Hosted;

/// <summary>
/// Background service that polls live scores every 5 minutes for active matches
/// and updates results and points in the database.
/// </summary>
public class ResultSyncService : IHostedService, IAsyncDisposable
{
    private readonly IPartidoRepository _partidoRepository;
    private readonly IApiFootballClient _apiFootballClient;
    private readonly IPartidoService _partidoService;
    private readonly IPuntuacionService _puntuacionService;
    private readonly ILogger<ResultSyncService> _logger;

    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private PeriodicTimer? _timer;
    private Task? _executingTask;
    private CancellationTokenSource? _cts;

    public ResultSyncService(
        IPartidoRepository partidoRepository,
        IApiFootballClient apiFootballClient,
        IPartidoService partidoService,
        IPuntuacionService puntuacionService,
        ILogger<ResultSyncService> logger)
    {
        ArgumentNullException.ThrowIfNull(partidoRepository);
        ArgumentNullException.ThrowIfNull(apiFootballClient);
        ArgumentNullException.ThrowIfNull(partidoService);
        ArgumentNullException.ThrowIfNull(puntuacionService);
        ArgumentNullException.ThrowIfNull(logger);

        _partidoRepository = partidoRepository;
        _apiFootballClient = apiFootballClient;
        _partidoService = partidoService;
        _puntuacionService = puntuacionService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(_interval);
        _executingTask = ExecuteAsync(_cts.Token);

        _logger.LogInformation("ResultSyncService started. Polling every {Interval} minutes.", _interval.TotalMinutes);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ResultSyncService stopping.");

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
            await SyncLiveMatchResultsAsync(cancellationToken);
        }
    }

    private async Task SyncLiveMatchResultsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var activeMatches = await _partidoRepository.GetByEstadoAsync(EstadoPartido.EnCurso);

            if (activeMatches.Count == 0)
                return;

            _logger.LogDebug("Syncing {Count} active match(es).", activeMatches.Count);

            foreach (var partido in activeMatches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (partido.ExternalId is null || !int.TryParse(partido.ExternalId, out var externalId))
                    continue;

                try
                {
                    var liveScore = await _apiFootballClient.GetLiveScoreAsync(externalId);

                    if (liveScore is null)
                        continue;

                    // Only update if scores have changed or state changed
                    bool scoresChanged = liveScore.GolesEquipo1 != partido.GolesEquipo1Oficial
                                     || liveScore.GolesEquipo2 != partido.GolesEquipo2Oficial;
                    bool stateChanged = liveScore.Estado != partido.Estado;

                    if (!scoresChanged && !stateChanged)
                        continue;

                    var goles1 = liveScore.GolesEquipo1 ?? partido.GolesEquipo1Oficial ?? 0;
                    var goles2 = liveScore.GolesEquipo2 ?? partido.GolesEquipo2Oficial ?? 0;

                    await _partidoService.ActualizarResultadoAsync(
                        partidoId: partido.Id,
                        golesEquipo1: goles1,
                        golesEquipo2: goles2,
                        nuevoEstado: liveScore.Estado,
                        esOrganizador: true // automated sync is treated as authoritative
                    );

                    _logger.LogInformation(
                        "Updated partido {PartidoId} ({Equipo1} vs {Equipo2}): {G1}-{G2} [{Estado}]",
                        partido.Id, partido.Equipo1, partido.Equipo2, goles1, goles2, liveScore.Estado);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing live score for partido {PartidoId} (externalId: {ExternalId}).",
                        partido.Id, partido.ExternalId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Service is stopping
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ResultSyncService tick.");
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
