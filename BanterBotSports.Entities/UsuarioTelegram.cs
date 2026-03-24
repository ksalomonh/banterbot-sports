namespace BanterBotSports.Entities;

public class UsuarioTelegram
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long TelegramUserId { get; set; }
    public string? TelegramUsername { get; set; }
    public DateTimeOffset FechaVinculacion { get; set; }
}
