using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Calculates per-match points based on configurable tournament settings.
/// GolesReglamento (FT + AET, excludes penalties) is used for result comparison.
/// PuntosGolesJornada is NOT awarded here — it is applied at jornada level by IPrediccionService.
/// </summary>
public class PuntuacionService : IPuntuacionService
{
    public PuntuacionDetalle CalcularPuntos(PrediccionPartido prediccion, Partido partido, Torneo torneo)
    {
        ArgumentNullException.ThrowIfNull(prediccion);
        ArgumentNullException.ThrowIfNull(partido);
        ArgumentNullException.ThrowIfNull(torneo);

        // Only compute points when the match has an official result (regulation goals set)
        if (partido.GolesReglamento is null
            || partido.GolesEquipo1Oficial is null
            || partido.GolesEquipo2Oficial is null)
        {
            return new PuntuacionDetalle(0, 0, 0);
        }

        int oficialEquipo1 = partido.GolesEquipo1Oficial.Value;
        int oficialEquipo2 = partido.GolesEquipo2Oficial.Value;

        // Derive 1X2 result from GolesEquipo1Oficial/GolesEquipo2Oficial (FT+AET, already excludes penalties)
        int signoOficial = Math.Sign(oficialEquipo1 - oficialEquipo2);
        int signoPrediccion = Math.Sign(prediccion.GolesEquipo1 - prediccion.GolesEquipo2);

        bool resultadoCorrecto = signoOficial == signoPrediccion;
        bool marcadorExacto = prediccion.GolesEquipo1 == oficialEquipo1
                              && prediccion.GolesEquipo2 == oficialEquipo2;

        // Exact score implies result is correct — award only PtosMarcador to avoid double-counting
        if (marcadorExacto)
            return new PuntuacionDetalle(0, torneo.PtosMarcador, 0);

        if (resultadoCorrecto)
            return new PuntuacionDetalle(torneo.PtosResultado, 0, 0);

        return new PuntuacionDetalle(0, 0, 0);
    }
}
