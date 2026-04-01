namespace BanterBotSports.Web.Infrastructure;

/// <summary>
/// Well-known keys for TempData used across controllers and views.
/// Centralised here to prevent magic string literals scattered throughout the web layer.
/// </summary>
public static class TempDataKeys
{
    public const string Success = "Success";
    public const string Error = "Error";
    public const string Info = "Info";
}
