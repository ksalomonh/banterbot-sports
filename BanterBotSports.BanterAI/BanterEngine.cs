using System.Text;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.BanterAI;

public class BanterEngine : IBanterEngine
{
    private readonly AnthropicClient _client;
    private readonly ILogger<BanterEngine> _logger;

    private const string ModelId = "claude-haiku-4-5-20251001";
    private const int MaxBanterLength = 280;

    private const string SystemPrompt = """
        Sos un comentarista deportivo argentino con mucha personalidad, humor y picardía.
        Generás mensajes de banter futbolero en español rioplatense para una quiniela de fútbol.
        Tus mensajes son divertidos, irónicos, picantes pero nunca hirientes.
        Usás expresiones como "loco", "hermano", "dale", "qué partidazo", "la rompiste", "se te fue al mazo".
        El mensaje debe tener MÁXIMO 280 caracteres. Si es más corto, mejor.
        Respondé ÚNICAMENTE con el mensaje de banter, sin explicaciones ni formato extra.
        """;

    private const string HttpClientName = "Anthropic";

    public BanterEngine(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<BanterEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        var apiKey = configuration["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey configuration is required.");

        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        _client = new AnthropicClient(new APIAuthentication(apiKey), httpClient);
        _logger = logger;
    }

    public async Task<string> GenerateBanterAsync(ParticipanteStats stats, Torneo torneo)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(torneo);

        try
        {
            var userMessage = BuildUserMessage(stats, torneo);

            var parameters = new MessageParameters
            {
                Model = ModelId,
                MaxTokens = 300,
                System = new List<SystemMessage>
                {
                    new SystemMessage(SystemPrompt)
                },
                Messages = new List<Message>
                {
                    new Message
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase>
                        {
                            new TextContent { Text = userMessage }
                        }
                    }
                }
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters);
            var banter = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;

            // Validate AI output — fall back to safe default if empty or whitespace
            if (string.IsNullOrWhiteSpace(banter))
                return $"¡Dale {stats.NombreParticipante}, seguí jugando en {torneo.Nombre}!";

            // Enforce max length
            if (banter.Length > MaxBanterLength)
                banter = banter[..MaxBanterLength].TrimEnd();

            return banter;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating banter for participant {Participante}", stats.NombreParticipante);
            return $"¡Dale {stats.NombreParticipante}, seguí jugando en {torneo.Nombre}!";
        }
    }

    private static string BuildUserMessage(ParticipanteStats stats, Torneo torneo)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Torneo: {torneo.Nombre}");
        sb.AppendLine($"Jugador: {stats.NombreParticipante}");
        sb.AppendLine($"Jornada: {stats.NumeroJornada}");
        sb.AppendLine($"Posición en el ranking: {stats.PosicionRanking}");
        sb.AppendLine($"Puntos totales: {stats.PuntosTotal}");
        sb.AppendLine();
        sb.AppendLine("Predicciones de la jornada:");

        foreach (var pred in stats.Predicciones)
        {
            var resultado = pred.GolesOficiales1.HasValue
                ? $"{pred.GolesOficiales1}-{pred.GolesOficiales2} (predijo {pred.GolesPredichos1}-{pred.GolesPredichos2}, {pred.PuntosObtenidos ?? 0} pts)"
                : $"predijo {pred.GolesPredichos1}-{pred.GolesPredichos2} (sin resultado aún)";

            sb.AppendLine($"  {pred.Equipo1} vs {pred.Equipo2}: {resultado}");
        }

        sb.AppendLine();
        sb.AppendLine("Generá un mensaje de banter divertido para este jugador en Rioplatense. Máximo 280 caracteres.");

        return sb.ToString();
    }
}
