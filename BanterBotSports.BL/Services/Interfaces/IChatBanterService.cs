namespace BanterBotSports.BL.Services.Interfaces;

/// <summary>
/// Generates and persists BanterBot commentary when live match scores change.
/// Called from ResultSyncService after each successful score update.
/// </summary>
public interface IChatBanterService
{
    /// <summary>
    /// Generates a banter comment about the score update and saves it to chat.
    /// If AI generation fails, logs the error and does NOT throw.
    /// </summary>
    Task OnScoreUpdatedAsync(
        int torneoId,
        int partidoId,
        int goles1,
        int goles2,
        string equipo1,
        string equipo2);
}
