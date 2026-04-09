using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;

namespace BanterBotSports.BL.Services.Interfaces;

public interface IPartidoService
{
    Task AsignarPartidoAsync(int jornadaId, Partido partido);

    /// <summary>Assigns multiple partidos to a jornada in a single database transaction.</summary>
    Task AsignarPartidosAsync(int jornadaId, IReadOnlyList<Partido> partidos);
    Task ActualizarResultadoAsync(int partidoId, int golesEquipo1, int golesEquipo2, EstadoPartido nuevoEstado, bool esOrganizador = false);
    int ComputarGolesReglamento(int golesEquipo1, int golesEquipo2);

    /// <summary>Returns upcoming matches for the given competition and date range from the external catalog.</summary>
    Task<IReadOnlyList<PartidoDto>> GetProximosPartidosAsync(int ligaId, DateOnly desde, DateOnly hasta);

    /// <summary>Returns a single fixture by its external ID, or null if not found.</summary>
    Task<PartidoDto?> GetFixturePorExternalIdAsync(int externalId);

    /// <summary>Returns whether the given competition ID is valid in the catalog.</summary>
    bool EsLigaValida(int ligaId);

    /// <summary>Returns all available leagues from the catalog.</summary>
    IReadOnlyList<LigaDto> GetLigas();
}
