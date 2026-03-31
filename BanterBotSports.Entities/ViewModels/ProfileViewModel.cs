namespace BanterBotSports.Entities.ViewModels;

/// <summary>
/// View model for the Account/Profile page.
/// Projects only the display fields needed by the view — keeps DAL types out of the Web layer.
/// </summary>
public record ProfileViewModel
{
    public required string? NombreDisplay { get; init; }
    public required string? Email { get; init; }
    /// <summary>User's phone number (login identifier), mapped from AppUser.PhoneNumber.</summary>
    public string? Telefono { get; init; }
    /// <summary>
    /// Telegram display name (username or user ID fallback) from the UsuarioTelegram table.
    /// Null when the user has not linked a Telegram account.
    /// </summary>
    public string? TelegramUsername { get; init; }
    /// <summary>
    /// Pre-built Telegram deep link: https://t.me/{BotUsername}?start={userId}.
    /// Always populated so the view can use it directly in the CTA button.
    /// </summary>
    public required string TelegramDeepLink { get; init; }
}
