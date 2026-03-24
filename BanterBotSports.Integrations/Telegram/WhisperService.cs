using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.Integrations.Telegram;

public class WhisperService : IWhisperService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _openAiApiKey;
    private readonly string _telegramBotToken;
    private readonly ILogger<WhisperService> _logger;

    private const string TelegramClientName = "TelegramApi";
    private const string WhisperClientName = "OpenAIWhisper";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WhisperService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WhisperService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _openAiApiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey configuration is required.");
        _telegramBotToken = configuration["Telegram:BotToken"]
            ?? throw new InvalidOperationException("Telegram:BotToken configuration is required.");
    }

    public async Task<string> TranscribeAsync(string telegramFileId)
    {
        // Step 1: Get file path from Telegram
        var filePath = await GetTelegramFilePathAsync(telegramFileId);

        // Step 2: Download the OGG audio from Telegram
        var audioBytes = await DownloadTelegramFileAsync(filePath);

        // Step 3: Upload to Whisper and return transcript
        // Note: callers MUST pass the transcript through IPrediccionExtractionService — never parse raw
        return await TranscribeWithWhisperAsync(audioBytes);
    }

    private async Task<string> GetTelegramFilePathAsync(string fileId)
    {
        var client = _httpClientFactory.CreateClient(TelegramClientName);
        var url = $"https://api.telegram.org/bot{_telegramBotToken}/getFile?file_id={fileId}";

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TelegramGetFileResponse>(json, JsonOptions);

        if (result?.Ok != true || result.Result?.FilePath is null)
            throw new InvalidOperationException($"Failed to get Telegram file path for fileId '{fileId}'.");

        return result.Result.FilePath;
    }

    private async Task<byte[]> DownloadTelegramFileAsync(string filePath)
    {
        var client = _httpClientFactory.CreateClient(TelegramClientName);
        var url = $"https://api.telegram.org/file/bot{_telegramBotToken}/{filePath}";

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task<string> TranscribeWithWhisperAsync(byte[] audioBytes)
    {
        var client = _httpClientFactory.CreateClient(WhisperClientName);

        using var requestMessage = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.openai.com/v1/audio/transcriptions");

        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);

        using var content = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/ogg");
        content.Add(audioContent, "file", "audio.ogg");
        content.Add(new StringContent("whisper-1"), "model");

        requestMessage.Content = content;

        var response = await client.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<WhisperTranscriptionResponse>(json, JsonOptions);

        return result?.Text ?? string.Empty;
    }

    // --- Response shapes ---

    private sealed class TelegramGetFileResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public TelegramFileResult? Result { get; set; }
    }

    private sealed class TelegramFileResult
    {
        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; }
    }

    private sealed class WhisperTranscriptionResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
