using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Integrations.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.BanterAI;

/// <summary>
/// Dispatches AI-generated banter to each participante after a jornada is finalized.
/// Invoked directly by the JornadaFinalizada callback — NOT a hosted service.
/// </summary>
public class BanterDispatchService
{
    private readonly IBanterEngine _banterEngine;
    private readonly ITelegramBotService _telegramBotService;
    private readonly AppDbContext _context;
    private readonly ILogger<BanterDispatchService> _logger;

    private const int MaxBanterLength = 280;

    public BanterDispatchService(
        IBanterEngine banterEngine,
        ITelegramBotService telegramBotService,
        AppDbContext context,
        ILogger<BanterDispatchService> logger)
    {
        ArgumentNullException.ThrowIfNull(banterEngine);
        ArgumentNullException.ThrowIfNull(telegramBotService);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        _banterEngine = banterEngine;
        _telegramBotService = telegramBotService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Called when a jornada is finalized. Sends banter to all participantes.
    /// </summary>
    public async Task OnJornadaFinalizadaAsync(Jornada jornada)
    {
        _logger.LogInformation("BanterDispatchService: dispatching banter for jornada {JornadaId} (#{Numero}).",
            jornada.Id, jornada.Numero);

        // Load torneo with participantes
        var torneo = await _context.Torneos
            .Include(t => t.Participantes)
            .FirstOrDefaultAsync(t => t.Id == jornada.TorneoId);

        if (torneo is null)
        {
            _logger.LogWarning("Torneo {TorneoId} not found for jornada {JornadaId}.", jornada.TorneoId, jornada.Id);
            return;
        }

        // Load partidos with their predictions for this jornada using explicit loading
        var partidos = await _context.Partidos
            .Where(p => p.JornadaId == jornada.Id)
            .Include(p => p.PrediccionesPartido)
            .ToListAsync();

        // Build ranking: sum of PuntosObtenidos per participante
        var puntosPorParticipante = partidos
            .SelectMany(p => p.PrediccionesPartido)
            .GroupBy(pp => pp.ParticipanteId)
            .ToDictionary(g => g.Key, g => g.Sum(pp => pp.PuntosObtenidos ?? 0));

        var ranking = puntosPorParticipante
            .OrderByDescending(kv => kv.Value)
            .Select((kv, index) => (ParticipanteId: kv.Key, Puntos: kv.Value, Posicion: index + 1))
            .ToList();

        // Load all Telegram users for this torneo in a single query
        var userIds = torneo.Participantes.Select(p => p.UserId).ToList();
        var telegramUsers = await _context.UsuariosTelegram
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.TelegramUserId);

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

                var stats = new ParticipanteStats(
                    NombreParticipante: participante.UserId,
                    NombreTorneo: torneo.Nombre,
                    NumeroJornada: jornada.Numero,
                    PosicionRanking: posicion,
                    PuntosTotal: puntosTotal,
                    Predicciones: predicciones
                );

                var banter = await _banterEngine.GenerateBanterAsync(stats, torneo);

                // Validate AI output before displaying
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
