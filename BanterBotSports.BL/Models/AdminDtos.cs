namespace BanterBotSports.BL.Models;

public record AdminUserDto(
    string Id,
    string? Phone,
    string? Email,
    string? NombreDisplay,
    bool IsActive,
    int TorneosOrganizados,
    int TorneosParticipados,
    DateTimeOffset? LockoutEnd);

public record AdminUserEditDto(string? NombreDisplay, string? Email);
