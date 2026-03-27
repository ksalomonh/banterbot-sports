using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Integrations.Telegram;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.BanterAI;

/// <summary>
/// Dispatches AI-generated banter to each participante after a jornada is finalized.
/// Invoked directly by the JornadaFinalizada callback — NOT a hosted service.
/// </summary>
public class BanterDispatchService : IBanterDispatchService
{
    private readonly IBanterEngine _banterEngine;
    private readonly ITelegramBotService _telegramBotService;
    private readonly ITorneoRepository _torneoRepository;
    private readonly IPartidoRepository _partidoRepository;
    private readonly IParticipanteRepository _participanteRepository;
    private readonly IUsuarioTelegramRepository _usuarioTelegramRepository;
    private readonly ILogger<BanterDispatchService> _logger;

    private const int MaxBanterLength = 280;

    public BanterDispatchService(
        IBanterEngine banterEngine,
        ITelegramBotService telegramBotService,
        ITorneoRepository torneoRepository,
        IPartidoRepository partidoRepository,
        IParticipanteRepository participanteRepository,
        IUsuarioTelegramRepository usuarioTelegramRepository,
        ILogger<BanterDispatchService> logger)
    {
        ArgumentNullException.ThrowIfNull(banterEngine);
        ArgumentNullException.ThrowIfNull(telegramBotService);
        ArgumentNullException.ThrowIfNull(torneoRepository);
        ArgumentNullException.ThrowIfNull(partidoRepository);
        ArgumentNullException.ThrowIfNull(participanteRepository);
        ArgumentNullException.ThrowIfNull(usuarioTelegramRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _banterEngine = banterEngine;
        _telegramBotService = telegramBotService;
        _torneoRepository = torneoRepository;
        _partidoRepository = partidoRepository;
        _participanteRepository = participanteRepository;
        _usuarioTelegramRepository = usuarioTelegramRepository;
        _logger = logger;
    }

    /// <summary>
    /// Called when a jornada is finalized. Sends banter to all participantes.
    /// </summary>
    public async Task OnJornadaFinalizadaAsync(Jornada jornada)
    {
        _logger.LogInformation("BanterDispatchService: dispatching banter for jornada {JornadaId} (#{Numero}).",
            jornada.Id, jornada.Numero);

        // Load torneo with participantes via repository
        var torneo = await _torneoRepository.GetByIdWithDetailsAsync(jornada.TorneoId);

        if (torneo is null)
        {
            _logger.LogWarning("Torneo {TorneoId} not found for jornada {JornadaId}.", jornada.TorneoId, jornada.Id);
            return;
        }

        // Load partidos with their predictions for this jornada — single query via repository
        var partidos = await _partidoRepository.GetByJornadaWithPrediccionesAsync(jornada.Id);

        // Build ranking: sum of PuntosObtenidos per participante
        var puntosPorParticipante = partidos
            .SelectMany(p => p.PrediccionesPartido)
            .GroupBy(pp => pp.ParticipanteId)
            .ToDictionary(g => g.Key, g => g.Sum(pp => pp.PuntosObtenidos ?? 0));

        var ranking = puntosPorParticipante
            .OrderByDescending(kv => kv.Value)
            .Select((kv, index) => (ParticipanteId: kv.Key, Puntos: kv.Value, Posicion: index + 1))
            .ToList();

        // Batch-load display names for all participants in a single query
        var userIds = torneo.Participantes.Select(p => p.UserId).ToList();
        var displayNames = await _participanteRepository.GetDisplayNamesByIdsAsync(userIds);

        // Load all Telegram users for this torneo in a single batch query
        var telegramUsers = await _usuarioTelegramRepository.GetTelegramIdsByUserIdsAsync(userIds);

        foreach (var participante in torneo.Participantes)
        {
            try
            {
                var puntosTotal = puntosPorParticipante.GetValueOrDefault(participante.Id, 0);
                var posicion = ranking.FirstOrDefault(r => r.ParticipanteId == participante.Id).Posicion;
                if (posicion == 0) posicion = torneo.Participantes.Count;

                var predicciones = partidos
                    .SelectMany(p => p.PrediccionesPartido
                        .Where(pp => pp.ParticipanteId == participante.Id)
                        .Select(pp => new PrediccionConResultado(
                            Equipo1: p.Equipo1,
                            Equipo2: p.Equipo2,
                            GolesPredichos1: pp.GolesEquipo1,
                            GolesPredichos2: pp.GolesEquipo2,
                            GolesOficiales1: p.GolesEquipo1Oficial,
                            GolesOficiales2: p.GolesEquipo2Oficial,
                            PuntosObtenidos: pp.PuntosObtenidos
                        )))
                    .ToList();

                // Use resolved display name — never send raw UserId to banter generation
                var nombreDisplay = displayNames.GetValueOrDefault(participante.UserId, participante.UserId);

                var stats = new ParticipanteStats(
                    NombreParticipante: nombreDisplay,
                    NombreTorneo: torneo.Nombre,
                    NumeroJornada: jornada.Numero,
                    PosicionRanking: posicion,
                    PuntosTotal: puntosTotal,
                    Predicciones: predicciones
                );

                var banter = await _banterEngine.GenerateBanterAsync(stats, torneo);

                // Validate AI output before displaying (max 280 chars)
                if (string.IsNullOrWhiteSpace(banter) || banter.Length > MaxBanterLength)
                {
                    _logger.LogWarning(
                        "Invalid banter for participante {UserId}: length={Length}. Skipping send.",
                        participante.UserId, banter?.Length ?? 0);
                    continue;
                }

                if (telegramUsers.TryGetValue(participante.UserId, out var chatId))
                {
                    await _telegramBotService.SendMessageAsync(chatId, banter);
                    _logger.LogInformation(
                        "Banter sent to participante {UserId} (chatId: {ChatId}).", participante.UserId, chatId);
                }
                else
                {
                    _logger.LogInformation(
                        "Participante {UserId} has no Telegram linked — banter logged only: {Banter}",
                        participante.UserId, banter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching banter to participante {UserId}.", participante.UserId);
            }
        }
    }
}
