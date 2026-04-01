using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;

namespace BanterBotSports.BL.Services.Interfaces;

public interface IChatService
{
    /// <summary>
    /// Saves a player message to the database.
    /// Truncates content to 500 characters.
    /// Throws <see cref="UnauthorizedAccessException"/> if the user is not a participant.
    /// </summary>
    Task<MensajeChat> SaveMessageAsync(int torneoId, string userId, string contenido);

    /// <summary>
    /// Saves a BanterBot message (UserId = null).
    /// </summary>
    Task<MensajeChat> SaveBanterBotMessageAsync(int torneoId, string contenido, TipoMensajeChat tipo);

    /// <summary>
    /// Returns chat history for a torneo.
    /// Before deadline (jornada Abierta): only the calling player's messages + BanterBot messages.
    /// After deadline or no active jornada: all messages.
    /// </summary>
    Task<IReadOnlyList<MensajeChat>> GetHistoryAsync(int torneoId, string userId, int limit = 50, long? beforeId = null);
}
