using BanterBotSports.Entities.Enums;

namespace BanterBotSports.Entities.DTOs;

public record PartidoDto(
    int Id,
    string? ExternalId,
    string Equipo1,
    string Equipo2,
    DateTimeOffset KickOffUtc,
    int? GolesEquipo1,
    int? GolesEquipo2,
    EstadoPartido Estado
);
