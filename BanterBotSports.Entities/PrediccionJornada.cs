namespace BanterBotSports.Entities;

public class PrediccionJornada
{
    public int Id { get; set; }
    public int JornadaId { get; set; }
    public int ParticipanteId { get; set; }
    public int GolesPronosticados { get; set; }
    public int? PuntosObtenidos { get; set; }

    public Jornada Jornada { get; set; } = null!;
    public Participante Participante { get; set; } = null!;
}
