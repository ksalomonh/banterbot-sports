namespace BanterBotSports.Entities.ViewModels;

/// <summary>
/// View model for the Account/Profile page.
/// Projects only the display fields needed by the view — keeps DAL types out of the Web layer.
/// </summary>
public record ProfileViewModel
{
    public required string? NombreDisplay { get; init; }
    public required string? Email { get; init; }
    /// <summary>Telegram Chat ID stored in PhoneNumber field of AppUser.</summary>
    public string? TelegramChatId { get; init; }
}
