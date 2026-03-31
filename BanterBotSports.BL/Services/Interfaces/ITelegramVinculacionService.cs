using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services.Interfaces;

/// <summary>
/// Handles Telegram account linking and data lookups for the Telegram update handler.
/// Encapsulates all DAL access so the Web layer does not touch repositories directly.
/// </summary>
public interface ITelegramVinculacionService
{
    /// <summary>
    /// Links (or re-links) a Telegram account to an existing app user identified by <paramref name="appUserId"/>.
    /// Returns true on success.
    /// </summary>
    Task<bool> VincularAsync(string appUserId, long telegramUserId, string? telegramUsername);

    /// <summary>
    /// Finds the UsuarioTelegram record for a given Telegram chat/user ID.
    /// </summary>
    Task<UsuarioTelegram?> GetByTelegramIdAsync(long telegramUserId);

    /// <summary>
    /// Finds the UsuarioTelegram record for a given app user ID.
    /// Returns null if the user has not linked a Telegram account.
    /// </summary>
    Task<UsuarioTelegram?> GetByUserIdAsync(string userId);

    /// <summary>
    /// Returns the display name for an app user by their ID.
    /// Falls back to username if no display name is set. Returns null if user not found.
    /// </summary>
    Task<string?> GetDisplayNameAsync(string userId);

    /// <summary>
    /// Returns the active open jornada (with partidos loaded) and the participante
    /// for the torneo the user belongs to. Null if no open jornada exists.
    /// </summary>
    Task<(Jornada jornada, Participante participante)?> GetJornadaAbiertaParaUsuarioAsync(string userId);
}
