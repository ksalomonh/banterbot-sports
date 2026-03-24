using BanterBotSports.Entities.Enums;

namespace BanterBotSports.Entities.DTOs;

public record PrediccionPartidoDto(
    int PartidoId,
    int GolesEquipo1,
    int GolesEquipo2,
    FuentePrediccion Fuente
);
