using BanterBotSports.Entities.Enums;

namespace BanterBotSports.Entities;

public class PrediccionPartido
{
    public int Id { get; set; }
    public int PartidoId { get; set; }
    public int ParticipanteId { get; set; }
    public int GolesEquipo1 { get; set; }
    public int GolesEquipo2 { get; set; }
    public FuentePrediccion Fuente { get; set; }
    public int? PuntosObtenidos { get; set; }

    public Partido Partido { get; set; } = null!;
    public Participante Participante { get; set; } = null!;
}
