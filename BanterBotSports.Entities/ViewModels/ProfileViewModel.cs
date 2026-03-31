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
    /// <summary>Telegram Chat ID, mapped from AppUser.TelegramChatId (dedicated column).</summary>
    public string? TelegramChatId { get; init; }
}
