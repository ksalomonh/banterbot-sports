using BanterBotSports.Entities.Enums;

namespace BanterBotSports.Entities;

public class Participante
{
    public int Id { get; set; }
    public int TorneoId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public RolParticipante Rol { get; set; }
    public bool Pago { get; set; }

    public Torneo Torneo { get; set; } = null!;
    public ICollection<PrediccionPartido> PrediccionesPartido { get; set; } = new List<PrediccionPartido>();
    public ICollection<PrediccionJornada> PrediccionesJornada { get; set; } = new List<PrediccionJornada>();
}
