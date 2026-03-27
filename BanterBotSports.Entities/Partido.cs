using BanterBotSports.Entities.Enums;

namespace BanterBotSports.Entities;

public class Partido
{
    public int Id { get; set; }
    public int JornadaId { get; set; }
    public string? ExternalId { get; set; }
    public string Equipo1 { get; set; } = string.Empty;
    public string Equipo2 { get; set; } = string.Empty;
    public string? LogoUrlLocal { get; set; }
    public string? LogoUrlVisitante { get; set; }
    public DateTimeOffset KickOffUtc { get; set; }

    /// <summary>Goals in regulation time + extra time (excludes penalties).</summary>
    public int? GolesEquipo1Oficial { get; set; }

    /// <summary>Goals in regulation time + extra time (excludes penalties).</summary>
    public int? GolesEquipo2Oficial { get; set; }

    /// <summary>Goals scored in regulation time only (90 minutes).</summary>
    public int? GolesReglamento { get; set; }

    public EstadoPartido Estado { get; set; }

    public Jornada Jornada { get; set; } = null!;
    public ICollection<PrediccionPartido> PrediccionesPartido { get; set; } = new List<PrediccionPartido>();
}
