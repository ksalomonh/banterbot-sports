using BanterBotSports.BanterAI;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Integrations.Telegram;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BanterBotSports.Web.Telegram;

/// <summary>
/// Processes Telegram Update objects consumed from the background queue.
/// All data access goes through BL services — never through DAL directly.
/// </summary>
public class TelegramUpdateHandler : ITelegramUpdateHandler
{
    private readonly ITelegramVinculacionService _vinculacionService;
    private readonly IWhisperService _whisperService;
    private readonly IPrediccionExtractionService _extractionService;
    private readonly IPrediccionService _prediccionService;
    private readonly ITelegramBotService _telegramBotService;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(
        ITelegramVinculacionService vinculacionService,
        IWhisperService whisperService,
        IPrediccionExtractionService extractionService,
        IPrediccionService prediccionService,
        ITelegramBotService telegramBotService,
        ILogger<TelegramUpdateHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(vinculacionService);
        ArgumentNullException.ThrowIfNull(whisperService);
        ArgumentNullException.ThrowIfNull(extractionService);
        ArgumentNullException.ThrowIfNull(prediccionService);
        ArgumentNullException.ThrowIfNull(telegramBotService);
        ArgumentNullException.ThrowIfNull(logger);

        _vinculacionService = vinculacionService;
        _whisperService = whisperService;
        _extractionService = extractionService;
        _prediccionService = prediccionService;
        _telegramBotService = telegramBotService;
        _logger = logger;
    }

    public async Task HandleAsync(Update update, CancellationToken cancellationToken = default)
    {
        if (update.Message is null)
            return;

        var message = update.Message;
        var chatId = message.Chat.Id;

        try
        {
            if (message.Type == MessageType.Text &&
                message.Text is not null &&
                message.Text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                await HandleStartCommandAsync(message, chatId);
                return;
            }

            if (message.Type == MessageType.Voice && message.Voice is not null)
            {
                await HandleVoiceMessageAsync(message, chatId, cancellationToken);
                return;
            }

            if (message.Type == MessageType.Text && message.Text is not null)
            {
                await HandleTextMessageAsync(message, chatId, cancellationToken);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing Telegram update {UpdateId}", update.Id);
            await SafeSendAsync(chatId, "Ocurrió un error procesando tu mensaje. Por favor intentá de nuevo.");
        }
    }

    private async Task HandleStartCommandAsync(Message message, long chatId)
    {
        // /start <appUserId> — token is the app user's ID embedded in the invite link
        var parts = message.Text!.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var appUserId = parts.Length > 1 ? parts[1].Trim() : null;

        if (string.IsNullOrWhiteSpace(appUserId))
        {
            await SafeSendAsync(chatId,
                "Hola! Para vincular tu cuenta, usá el link de invitación desde la app web.");
            return;
        }

        var displayName = await _vinculacionService.GetDisplayNameAsync(appUserId);
        if (displayName is null)
        {
            await SafeSendAsync(chatId, "Link de invitación inválido.");
            return;
        }

        var telegramUserId = message.From?.Id ?? chatId;
        var telegramUsername = message.From?.Username;

        await _vinculacionService.VincularAsync(appUserId, telegramUserId, telegramUsername);

        await SafeSendAsync(chatId,
            $"Cuenta vinculada correctamente, {displayName}! Ya podés enviar tus predicciones por acá.");
    }

    private async Task HandleVoiceMessageAsync(Message message, long chatId, CancellationToken cancellationToken)
    {
        var telegramUserId = message.From?.Id ?? chatId;
        var usuarioTelegram = await _vinculacionService.GetByTelegramIdAsync(telegramUserId);
        if (usuarioTelegram is null)
        {
            await SafeSendAsync(chatId,
                "Primero tenés que vincular tu cuenta. Usá el link de invitación desde la app web.");
            return;
        }

        string transcription;
        try
        {
            transcription = await _whisperService.TranscribeAsync(message.Voice!.FileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transcribing voice message for user {UserId}", usuarioTelegram.UserId);
            await SafeSendAsync(chatId, "No pude transcribir tu mensaje de voz. Intentá enviarlo como texto.");
            return;
        }

        await ProcessPredictionTextAsync(transcription, usuarioTelegram.UserId, chatId, FuentePrediccion.Telegram, cancellationToken);
    }

    private async Task HandleTextMessageAsync(Message message, long chatId, CancellationToken cancellationToken)
    {
        var telegramUserId = message.From?.Id ?? chatId;
        var usuarioTelegram = await _vinculacionService.GetByTelegramIdAsync(telegramUserId);
        if (usuarioTelegram is null)
        {
            await SafeSendAsync(chatId,
                "Primero tenés que vincular tu cuenta. Usá el link de invitación desde la app web.");
            return;
        }

        await ProcessPredictionTextAsync(message.Text!, usuarioTelegram.UserId, chatId, FuentePrediccion.Telegram, cancellationToken);
    }

    private async Task ProcessPredictionTextAsync(
        string text,
        string userId,
        long chatId,
        FuentePrediccion fuente,
        CancellationToken cancellationToken)
    {
        var context = await _vinculacionService.GetJornadaAbiertaParaUsuarioAsync(userId);
        if (context is null)
        {
            await SafeSendAsync(chatId, "No hay jornada abierta en tu torneo en este momento o no estás inscripto.");
            return;
        }

        var (jornada, participante) = context.Value;

        var partidoDtos = jornada.Partidos.Select(p => new PartidoDto(
            p.Id, p.ExternalId, p.Equipo1, p.Equipo2,
            p.KickOffUtc, p.GolesEquipo1Oficial, p.GolesEquipo2Oficial, p.Estado
        )).ToList();

        ExtractionResult extraction;
        try
        {
            extraction = await _extractionService.ExtractAsync(text, partidoDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting predictions for user {UserId}", userId);
            await SafeSendAsync(chatId, "No pude interpretar tus predicciones. Intentá con un formato más claro (ej: 'River 2 - 1 Boca').");
            return;
        }

        if (!extraction.Success || extraction.Predicciones.Count == 0)
        {
            await SafeSendAsync(chatId,
                extraction.Error ?? "No se pudieron interpretar predicciones. Intentá de nuevo con un formato como 'Equipo1 2-1 Equipo2'.");
            return;
        }

        var confirmaciones = new List<string>();
        var errores = new List<string>();

        foreach (var dto in extraction.Predicciones)
        {
            var partido = jornada.Partidos.FirstOrDefault(p => p.Id == dto.PartidoId);
            if (partido is null) continue;

            try
            {
                var prediccion = new PrediccionPartido
                {
                    PartidoId = dto.PartidoId,
                    ParticipanteId = participante.Id,
                    GolesEquipo1 = dto.GolesEquipo1,
                    GolesEquipo2 = dto.GolesEquipo2,
                    Fuente = fuente
                };

                await _prediccionService.GuardarPrediccionAsync(prediccion, jornada, esOrganizador: false);
                confirmaciones.Add($"{partido.Equipo1} {dto.GolesEquipo1} - {dto.GolesEquipo2} {partido.Equipo2}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save prediction for partido {PartidoId}", dto.PartidoId);
                errores.Add($"{partido.Equipo1} vs {partido.Equipo2}: {ex.Message}");
            }
        }

        if (confirmaciones.Count > 0)
            await _telegramBotService.SendConfirmationListAsync(chatId, confirmaciones);

        if (errores.Count > 0)
            await SafeSendAsync(chatId, "Algunos pronósticos no pudieron guardarse:\n" + string.Join("\n", errores));

        if (confirmaciones.Count == 0 && errores.Count == 0)
            await SafeSendAsync(chatId, "No se encontraron predicciones válidas para la jornada actual.");
    }

    private async Task SafeSendAsync(long chatId, string text)
    {
        try { await _telegramBotService.SendMessageAsync(chatId, text); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to send reply to chatId {ChatId}", chatId); }
    }
}
