using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.Integrations.Telegram;

/// <summary>
/// Subscribes to <see cref="IJornadaService.JornadaAbierta"/> and sends the upcoming
/// match list to every participant that has a linked Telegram account.
/// Registered as a singleton at the composition root and wired to the event in Program.cs.
/// Uses <see cref="IServiceScopeFactory"/> because the underlying repositories are scoped.
/// </summary>
public sealed class JornadaAbiertaNotifier
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotService _telegramBotService;
    private readonly ILogger<JornadaAbiertaNotifier> _logger;

    public JornadaAbiertaNotifier(
        IServiceScopeFactory scopeFactory,
        ITelegramBotService telegramBotService,
        ILogger<JornadaAbiertaNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(telegramBotService);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _telegramBotService = telegramBotService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the <see cref="IJornadaService.JornadaAbierta"/> event.
    /// Loads matches and participants from a fresh scope, then sends the match list to
    /// each participant that has a linked Telegram account.
    /// </summary>
    public async Task OnJornadaAbiertaAsync(Jornada jornada)
    {
        _logger.LogInformation(
            "JornadaAbiertaNotifier: sending match list for jornada {JornadaId} (#{Numero}).",
            jornada.Id, jornada.Numero);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var partidoRepository = scope.ServiceProvider.GetRequiredService<IPartidoRepository>();
        var torneoRepository = scope.ServiceProvider.GetRequiredService<ITorneoRepository>();
        var usuarioTelegramRepository = scope.ServiceProvider.GetRequiredService<IUsuarioTelegramRepository>();

        // Load the partidos for this jornada
        var partidos = await partidoRepository.GetByJornadaIdAsync(jornada.Id);

        if (partidos.Count == 0)
        {
            _logger.LogWarning(
                "JornadaAbiertaNotifier: no partidos found for jornada {JornadaId}. Skipping notification.",
                jornada.Id);
            return;
        }

        // Map to DTOs for the send operation
        var partidoDtos = partidos
            .Select(p => new PartidoDto(
                Id: p.Id,
                ExternalId: p.ExternalId,
                Equipo1: p.Equipo1,
                Equipo2: p.Equipo2,
                KickOffUtc: p.KickOffUtc,
                GolesEquipo1: p.GolesEquipo1Oficial,
                GolesEquipo2: p.GolesEquipo2Oficial,
                Estado: p.Estado))
            .ToList();

        // Load torneo with participantes to get user IDs
        var torneo = await torneoRepository.GetByIdWithDetailsAsync(jornada.TorneoId);

        if (torneo is null)
        {
            _logger.LogWarning(
                "JornadaAbiertaNotifier: torneo {TorneoId} not found for jornada {JornadaId}.",
                jornada.TorneoId, jornada.Id);
            return;
        }

        // Batch-load all Telegram chat IDs for torneo participants
        var userIds = torneo.Participantes.Select(p => p.UserId).ToList();
        var telegramIds = await usuarioTelegramRepository.GetTelegramIdsByUserIdsAsync(userIds);

        foreach (var participante in torneo.Participantes)
        {
            if (!telegramIds.TryGetValue(participante.UserId, out var chatId))
            {
                _logger.LogDebug(
                    "JornadaAbiertaNotifier: participante {UserId} has no linked Telegram — skipping.",
                    participante.UserId);
                continue;
            }

            try
            {
                await _telegramBotService.SendMatchesListAsync(chatId, partidoDtos);
                _logger.LogInformation(
                    "JornadaAbiertaNotifier: match list sent to participante {UserId} (chatId: {ChatId}).",
                    participante.UserId, chatId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "JornadaAbiertaNotifier: error sending match list to participante {UserId}.",
                    participante.UserId);
            }
        }
    }
}
