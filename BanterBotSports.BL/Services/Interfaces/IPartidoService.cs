using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;

namespace BanterBotSports.BL.Services.Interfaces;

public interface IPartidoService
{
    Task AsignarPartidoAsync(int jornadaId, Partido partido);
    Task ActualizarResultadoAsync(int partidoId, int golesEquipo1, int golesEquipo2, EstadoPartido nuevoEstado, bool esOrganizador = false);
    int ComputarGolesReglamento(int golesEquipo1, int golesEquipo2);
}
