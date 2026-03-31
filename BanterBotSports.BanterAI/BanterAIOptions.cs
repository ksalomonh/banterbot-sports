namespace BanterBotSports.BanterAI;

/// <summary>
/// Configuration options for the BanterAI module.
/// Bind via "BanterAI" configuration section.
/// </summary>
public class BanterAIOptions
{
    /// <summary>
    /// Minimum confidence threshold for prediction extraction.
    /// Extractions below this value are rejected.
    /// </summary>
    public double MinConfidence { get; set; } = 0.75;
}
