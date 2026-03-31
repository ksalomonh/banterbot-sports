using System.Text.Json;
using BanterBotSports.BanterAI;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Integrations.Telegram;
using BanterBotSports.Web.Telegram;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for TelegramUpdateHandler.
/// Covers: /start, text predictions, voice, /mis_predicciones, null message guard.
/// All 5 dependencies are mocked: ITelegramVinculacionService, IWhisperService,
/// IPrediccionExtractionService, IPrediccionService, ITelegramBotService.
/// </summary>
public class TelegramUpdateHandlerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static (
        TelegramUpdateHandler sut,
        Mock<ITelegramVinculacionService> vinculacion,
        Mock<IWhisperService> whisper,
        Mock<IPrediccionExtractionService> extraction,
        Mock<IPrediccionService> prediccion,
        Mock<ITelegramBotService> bot
    ) BuildSut()
    {
        var vinculacion = new Mock<ITelegramVinculacionService>();
        var whisper = new Mock<IWhisperService>();
        var extraction = new Mock<IPrediccionExtractionService>();
        var prediccion = new Mock<IPrediccionService>();
        var bot = new Mock<ITelegramBotService>();

        var sut = new TelegramUpdateHandler(
            vinculacion.Object,
            whisper.Object,
            extraction.Object,
            prediccion.Object,
            bot.Object,
            NullLogger<TelegramUpdateHandler>.Instance);

        return (sut, vinculacion, whisper, extraction, prediccion, bot);
    }

    /// <summary>
    /// Telegram.Bot's Message type uses read-only computed properties (Type, MessageId).
    /// We use JSON deserialization to create properly populated instances,
    /// since that's how Telegram.Bot itself populates them from the API.
    /// </summary>
    private static readonly JsonSerializerOptions TelegramJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static Update DeserializeUpdate(string json)
        => JsonSerializer.Deserialize<Update>(json, TelegramJsonOptions)
           ?? throw new InvalidOperationException("Failed to deserialize Update");

    private static Update BuildTextUpdate(string text, long userId = 100, long chatId = 200)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var json = $$"""
        {
          "update_id": 1,
          "message": {
            "message_id": 1,
            "text": {{JsonSerializer.Serialize(text)}},
            "from": { "id": {{userId}}, "is_bot": false, "first_name": "TestUser" },
            "chat": { "id": {{chatId}}, "type": "private" },
            "date": {{now}}
          }
        }
        """;
        return DeserializeUpdate(json);
    }

    private static Update BuildVoiceUpdate(string fileId = "voice-file-1", long userId = 100, long chatId = 200)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var json = $$"""
        {
          "update_id": 2,
          "message": {
            "message_id": 2,
            "voice": { "file_id": {{JsonSerializer.Serialize(fileId)}}, "duration": 5, "file_unique_id": "unique1" },
            "from": { "id": {{userId}}, "is_bot": false, "first_name": "VoiceUser" },
            "chat": { "id": {{chatId}}, "type": "private" },
            "date": {{now}}
          }
        }
        """;
        return DeserializeUpdate(json);
    }

    private static UsuarioTelegram MakeUsuarioTelegram(long telegramId = 100, string userId = "app-user-1")
        => new() { TelegramUserId = telegramId, UserId = userId };

    private static (Jornada jornada, Participante participante) MakeJornadaContext(int jornadaId = 1, int participanteId = 10)
    {
        var jornada = new Jornada
        {
            Id = jornadaId,
            TorneoId = 1,
            Numero = 5,
            Estado = EstadoJornada.Abierta,
            Partidos = new List<Partido>
            {
                new() { Id = 1, Equipo1 = "River", Equipo2 = "Boca", Estado = EstadoPartido.Programado, KickOffUtc = DateTimeOffset.UtcNow.AddDays(1) }
            }
        };
        var participante = new Participante { Id = participanteId, TorneoId = 1, UserId = "app-user-1" };
        return (jornada, participante);
    }

    // ---------------------------------------------------------------------------
    // /start tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Start_ValidUserId_LinksAccountAndReplies()
    {
        // Arrange
        var (sut, vinculacion, _, _, _, bot) = BuildSut();
        vinculacion.Setup(v => v.GetDisplayNameAsync("app-user-1")).ReturnsAsync("Kevin");
        vinculacion.Setup(v => v.VincularAsync("app-user-1", 100L, null)).ReturnsAsync(true);
        var update = BuildTextUpdate("/start app-user-1");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("Kevin"))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Start_NoUserId_SendsInvitePrompt()
    {
        // Arrange
        var (sut, _, _, _, _, bot) = BuildSut();
        var update = BuildTextUpdate("/start");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("link de invitación"))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Start_InvalidUserId_SendsInvalidLinkMessage()
    {
        // Arrange
        var (sut, vinculacion, _, _, _, bot) = BuildSut();
        vinculacion.Setup(v => v.GetDisplayNameAsync("nonexistent")).ReturnsAsync((string?)null);
        var update = BuildTextUpdate("/start nonexistent");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("inválido"))), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Text prediction tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_TextPrediction_HappyPath_SavesAndConfirms()
    {
        // Arrange
        var (sut, vinculacion, _, extraction, prediccion, bot) = BuildSut();
        var usuarioTelegram = MakeUsuarioTelegram();
        var (jornada, participante) = MakeJornadaContext();

        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync(usuarioTelegram);
        vinculacion.Setup(v => v.GetJornadaAbiertaParaUsuarioAsync("app-user-1"))
            .ReturnsAsync((jornada, participante));

        var extractedPredictions = new List<PrediccionPartidoDto>
        {
            new(PartidoId: 1, GolesEquipo1: 2, GolesEquipo2: 1, Fuente: FuentePrediccion.Telegram)
        };
        extraction.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PartidoDto>>()))
            .ReturnsAsync(new ExtractionResult(
                Success: true, Error: null, Predicciones: extractedPredictions, Confidence: 0.95));

        prediccion.Setup(p => p.GuardarPrediccionAsync(It.IsAny<PrediccionPartido>(), It.IsAny<Jornada>(), false))
            .Returns(Task.CompletedTask);

        var update = BuildTextUpdate("River 2-1 Boca");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendConfirmationListAsync(200L, It.IsAny<IReadOnlyList<string>>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TextPrediction_LowConfidence_SendsErrorMessage()
    {
        // Arrange
        var (sut, vinculacion, _, extraction, _, bot) = BuildSut();
        var usuarioTelegram = MakeUsuarioTelegram();
        var (jornada, participante) = MakeJornadaContext();

        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync(usuarioTelegram);
        vinculacion.Setup(v => v.GetJornadaAbiertaParaUsuarioAsync("app-user-1"))
            .ReturnsAsync((jornada, participante));

        extraction.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PartidoDto>>()))
            .ReturnsAsync(new ExtractionResult(
                Success: false,
                Error: "No estoy seguro de haber entendido bien.",
                Predicciones: Array.Empty<PrediccionPartidoDto>(),
                Confidence: 0.3));

        var update = BuildTextUpdate("algo ambiguo");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("seguro") || s.Contains("entendido"))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TextPrediction_SaveError_SendsErrorMessage()
    {
        // Arrange
        var (sut, vinculacion, _, extraction, prediccion, bot) = BuildSut();
        var usuarioTelegram = MakeUsuarioTelegram();
        var (jornada, participante) = MakeJornadaContext();

        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync(usuarioTelegram);
        vinculacion.Setup(v => v.GetJornadaAbiertaParaUsuarioAsync("app-user-1"))
            .ReturnsAsync((jornada, participante));

        var extractedPredictions = new List<PrediccionPartidoDto>
        {
            new(PartidoId: 1, GolesEquipo1: 2, GolesEquipo2: 1, Fuente: FuentePrediccion.Telegram)
        };
        extraction.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PartidoDto>>()))
            .ReturnsAsync(new ExtractionResult(
                Success: true, Error: null, Predicciones: extractedPredictions, Confidence: 0.95));

        prediccion.Setup(p => p.GuardarPrediccionAsync(It.IsAny<PrediccionPartido>(), It.IsAny<Jornada>(), false))
            .ThrowsAsync(new InvalidOperationException("Deadline exceeded"));

        var update = BuildTextUpdate("River 2-1 Boca");

        // Act
        await sut.HandleAsync(update);

        // Assert — must send error list (not crash)
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("no pudieron guardarse") || s.Contains("Deadline"))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TextPrediction_UnlinkedUser_SendsLinkPrompt()
    {
        // Arrange
        var (sut, vinculacion, _, _, _, bot) = BuildSut();
        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync((UsuarioTelegram?)null);
        var update = BuildTextUpdate("River 2-1 Boca");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("vincular"))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TextPrediction_NoOpenJornada_SendsNoJornadaMessage()
    {
        // Arrange
        var (sut, vinculacion, _, _, _, bot) = BuildSut();
        var usuarioTelegram = MakeUsuarioTelegram();

        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync(usuarioTelegram);
        vinculacion.Setup(v => v.GetJornadaAbiertaParaUsuarioAsync("app-user-1"))
            .ReturnsAsync(((Jornada jornada, Participante participante)?)null);

        var update = BuildTextUpdate("River 2-1 Boca");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("No hay jornada"))), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Voice tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Voice_HappyPath_TranscribesAndProcesses()
    {
        // Arrange
        var (sut, vinculacion, whisper, extraction, prediccion, bot) = BuildSut();
        var usuarioTelegram = MakeUsuarioTelegram();
        var (jornada, participante) = MakeJornadaContext();

        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync(usuarioTelegram);
        vinculacion.Setup(v => v.GetJornadaAbiertaParaUsuarioAsync("app-user-1"))
            .ReturnsAsync((jornada, participante));
        whisper.Setup(w => w.TranscribeAsync("voice-file-1")).ReturnsAsync("River 2-1 Boca");

        var extractedPredictions = new List<PrediccionPartidoDto>
        {
            new(PartidoId: 1, GolesEquipo1: 2, GolesEquipo2: 1, Fuente: FuentePrediccion.Telegram)
        };
        extraction.Setup(e => e.ExtractAsync("River 2-1 Boca", It.IsAny<IReadOnlyList<PartidoDto>>()))
            .ReturnsAsync(new ExtractionResult(
                Success: true, Error: null, Predicciones: extractedPredictions, Confidence: 0.95));
        prediccion.Setup(p => p.GuardarPrediccionAsync(It.IsAny<PrediccionPartido>(), It.IsAny<Jornada>(), false))
            .Returns(Task.CompletedTask);

        var update = BuildVoiceUpdate();

        // Act
        await sut.HandleAsync(update);

        // Assert
        whisper.Verify(w => w.TranscribeAsync("voice-file-1"), Times.Once);
        bot.Verify(b => b.SendConfirmationListAsync(200L, It.IsAny<IReadOnlyList<string>>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Voice_TranscriptionError_SendsErrorMessage()
    {
        // Arrange
        var (sut, vinculacion, whisper, _, _, bot) = BuildSut();
        var usuarioTelegram = MakeUsuarioTelegram();

        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync(usuarioTelegram);
        whisper.Setup(w => w.TranscribeAsync(It.IsAny<string>())).ThrowsAsync(new Exception("API error"));

        var update = BuildVoiceUpdate();

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("transcribir"))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Voice_UnlinkedUser_SendsLinkPrompt()
    {
        // Arrange
        var (sut, vinculacion, _, _, _, bot) = BuildSut();
        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync((UsuarioTelegram?)null);
        var update = BuildVoiceUpdate();

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("vincular"))), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // /mis_predicciones tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_MisPredicciones_WithPredictions_FormatsAndReplies()
    {
        // Arrange
        var (sut, vinculacion, _, _, prediccion, bot) = BuildSut();
        var usuarioTelegram = MakeUsuarioTelegram();
        var (jornada, participante) = MakeJornadaContext();

        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync(usuarioTelegram);
        vinculacion.Setup(v => v.GetJornadaAbiertaParaUsuarioAsync("app-user-1"))
            .ReturnsAsync((jornada, participante));

        var predictions = new Dictionary<int, PrediccionPartido>
        {
            [1] = new() { PartidoId = 1, ParticipanteId = participante.Id, GolesEquipo1 = 2, GolesEquipo2 = 0, Fuente = FuentePrediccion.Telegram }
        };
        prediccion.Setup(p => p.GetPorJornadaYParticipanteAsync(jornada.Id, participante.Id))
            .ReturnsAsync(predictions);

        var update = BuildTextUpdate("/mis_predicciones");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s =>
            s.Contains("River") && s.Contains("2") && s.Contains("0") && s.Contains("Boca"))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MisPredicciones_NoPredictions_SendsEmptyMessage()
    {
        // Arrange
        var (sut, vinculacion, _, _, prediccion, bot) = BuildSut();
        var usuarioTelegram = MakeUsuarioTelegram();
        var (jornada, participante) = MakeJornadaContext();

        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync(usuarioTelegram);
        vinculacion.Setup(v => v.GetJornadaAbiertaParaUsuarioAsync("app-user-1"))
            .ReturnsAsync((jornada, participante));
        prediccion.Setup(p => p.GetPorJornadaYParticipanteAsync(jornada.Id, participante.Id))
            .ReturnsAsync(new Dictionary<int, PrediccionPartido>());

        var update = BuildTextUpdate("/mis_predicciones");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("No tenés predicciones"))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MisPredicciones_UnlinkedUser_SendsLinkPrompt()
    {
        // Arrange
        var (sut, vinculacion, _, _, _, bot) = BuildSut();
        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync((UsuarioTelegram?)null);
        var update = BuildTextUpdate("/mis_predicciones");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("vincular"))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MisPredicciones_NoOpenJornada_SendsNoJornadaMessage()
    {
        // Arrange
        var (sut, vinculacion, _, _, _, bot) = BuildSut();
        var usuarioTelegram = MakeUsuarioTelegram();
        vinculacion.Setup(v => v.GetByTelegramIdAsync(100L)).ReturnsAsync(usuarioTelegram);
        vinculacion.Setup(v => v.GetJornadaAbiertaParaUsuarioAsync("app-user-1"))
            .ReturnsAsync(((Jornada jornada, Participante participante)?)null);

        var update = BuildTextUpdate("/mis_predicciones");

        // Act
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(200L, It.Is<string>(s => s.Contains("No hay jornada"))), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Null message guard
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_NullMessage_DoesNothing()
    {
        // Arrange
        var (sut, _, _, _, _, bot) = BuildSut();
        var update = new Update { Id = 99, Message = null };

        // Act — must not throw
        await sut.HandleAsync(update);

        // Assert
        bot.Verify(b => b.SendMessageAsync(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }
}
