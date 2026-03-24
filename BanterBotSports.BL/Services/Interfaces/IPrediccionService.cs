using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services.Interfaces;

public interface IPrediccionService
{
    Task GuardarPrediccionAsync(PrediccionPartido prediccion, Jornada jornada, bool esOrganizador = false);
    Task<IReadOnlyList<PrediccionJornada>> GetByJornadaAsync(int jornadaId);
    Task ActualizarGolesJornadaAsync(int jornadaId);
}
