using Microsoft.AspNetCore.Identity;

namespace BanterBotSports.DAL;

public class AppUser : IdentityUser
{
    public string? NombreDisplay { get; set; }
    public string? TelegramChatId { get; set; }
    public decimal? PorcentajeOrganizadorGlobal { get; set; }
}
