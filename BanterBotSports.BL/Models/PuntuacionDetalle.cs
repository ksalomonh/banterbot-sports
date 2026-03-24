namespace BanterBotSports.BL.Models;

public record PuntuacionDetalle(
    int PuntosResultado,
    int PuntosExacto,
    int PuntosGolesJornada)
{
    public int Total => PuntosResultado + PuntosExacto + PuntosGolesJornada;
}
