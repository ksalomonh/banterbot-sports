using BanterBotSports.Entities.Enums;

namespace BanterBotSports.Entities;

public class Jornada
{
    public int Id { get; set; }
    public int TorneoId { get; set; }
    public int Numero { get; set; }
    public EstadoJornada Estado { get; set; }
    public DateTimeOffset? DeadlineUtc { get; set; }

    public Torneo Torneo { get; set; } = null!;
    public ICollection<Partido> Partidos { get; set; } = new List<Partido>();
    public ICollection<PrediccionJornada> PrediccionesJornada { get; set; } = new List<PrediccionJornada>();
}
