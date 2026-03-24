using BanterBotSports.BL.Models;
using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services.Interfaces;

public interface IPuntuacionService
{
    PuntuacionDetalle CalcularPuntos(PrediccionPartido prediccion, Partido partido, Torneo torneo);
}
