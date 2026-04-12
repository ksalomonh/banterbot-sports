using BanterBotSports.Entities.Enums;

namespace BanterBotSports.Entities;

public class Torneo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string OrganizadorId { get; set; } = string.Empty;
    public int NumJornadas { get; set; }
    public decimal MontoInscripcion { get; set; }
    public int PtosResultado { get; set; }
    public int PtosMarcador { get; set; }
    public int PtosGolesJornada { get; set; }
    public decimal PorcentajeOrganizador { get; set; }
    public EstadoTorneo Estado { get; set; }

    public ICollection<ConfiguracionPremio> ConfiguracionPremios { get; set; } = new List<ConfiguracionPremio>();
    public ICollection<Jornada> Jornadas { get; set; } = new List<Jornada>();
    public ICollection<Participante> Participantes { get; set; } = new List<Participante>();
}
