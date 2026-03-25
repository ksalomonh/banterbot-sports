using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.BanterAI;

public class PrediccionExtractionService : IPrediccionExtractionService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<PrediccionExtractionService> _logger;

    private const string ModelId = "claude-haiku-4-5-20251001";
    private const double MinConfidence = 0.95;

    private static readonly ExtractionResult CannotParse = new(
        Success: false,
        Error: "Could not parse predictions from the provided text.",
        Predicciones: Array.Empty<PrediccionPartidoDto>(),
        Confidence: 0.0
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string SystemPrompt = """
        You are a football predictions parser. Given natural language text, extract match predictions.
        For each match mentioned, output the predicted score.

        Respond ONLY with valid JSON in this exact format:
        {
          "predictions": [
            { "matchId": <int>, "localGoals": <int>, "visitanteGoals": <int> }
          ],
          "confidence": <number between 0 and 1>
        }

        Rules:
        - Only include matches from the provided list.
        - If the text is ambiguous or unclear, lower the confidence score.
        - If no predictions can be extracted, return an empty predictions array with confidence 0.
        - confidence must reflect how certain you are about the extracted predictions.

        Important: respond only with valid JSON as instructed. Do not include offensive,
        harmful, or inappropriate content under any circumstances.
        """;

    public PrediccionExtractionService(IConfiguration configuration, ILogger<PrediccionExtractionService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        var apiKey = configuration["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey configuration is required.");

        _client = new AnthropicClient(apiKey);
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractAsync(string text, IReadOnlyList<PartidoDto> partidos)
    {
        try
        {
            var userMessage = BuildUserMessage(text, partidos);

            var parameters = new MessageParameters
            {
                Model = ModelId,
                MaxTokens = 1024,
                System = [new SystemMessage { Text = SystemPrompt }],
                Messages =
                [
                    new Message
                    {
                        Role = RoleType.User,
                        Content = [new TextContent { Text = userMessage }]
                    }
                ]
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters);
            var responseText = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;

            return ParseResponse(responseText, partidos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting predictions from text");
            return CannotParse;
        }
    }

    private static string BuildUserMessage(string text, IReadOnlyList<PartidoDto> partidos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Text to parse:");
        sb.AppendLine(text);
        sb.AppendLine();
        sb.AppendLine("Available matches:");

        foreach (var partido in partidos)
        {
            sb.AppendLine($"- ID {partido.Id}: {partido.Equipo1} vs {partido.Equipo2} ({partido.KickOffUtc:yyyy-MM-dd HH:mm} UTC)");
        }

        return sb.ToString();
    }

    private ExtractionResult ParseResponse(string responseText, IReadOnlyList<PartidoDto> partidos)
    {
        try
        {
            // Extract JSON from the response (in case Claude adds extra text)
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd < 0)
                return CannotParse;

            var json = responseText[jsonStart..(jsonEnd + 1)];
            var parsed = JsonSerializer.Deserialize<ExtractionResponseJson>(json, JsonOptions);

            if (parsed is null)
                return CannotParse;

            if (parsed.Confidence < MinConfidence)
                return CannotParse;

            var validMatchIds = partidos.Select(p => p.Id).ToHashSet();

            var predicciones = parsed.Predictions
                .Where(p => validMatchIds.Contains(p.MatchId))
                .Select(p => new PrediccionPartidoDto(
                    PartidoId: p.MatchId,
                    GolesEquipo1: p.LocalGoals,
                    GolesEquipo2: p.VisitanteGoals,
                    Fuente: FuentePrediccion.Texto
                ))
                .ToList();

            return new ExtractionResult(
                Success: true,
                Error: null,
                Predicciones: predicciones,
                Confidence: parsed.Confidence
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing extraction response");
            return CannotParse;
        }
    }

    private sealed record ExtractionResponseJson
    {
        [JsonPropertyName("predictions")]
        public List<PredictionJson> Predictions { get; init; } = new();

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }
    }

    private sealed record PredictionJson
    {
        [JsonPropertyName("matchId")]
        public int MatchId { get; init; }

        [JsonPropertyName("localGoals")]
        public int LocalGoals { get; init; }

        [JsonPropertyName("visitanteGoals")]
        public int VisitanteGoals { get; init; }
    }
}
