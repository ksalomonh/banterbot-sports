using BanterBotSports.Entities.Enums;

namespace BanterBotSports.Entities;

public class MensajeChat
{
    public long Id { get; set; }
    public int TorneoId { get; set; }

    /// <summary>
    /// Null for BanterBot messages.
    /// </summary>
    public string? UserId { get; set; }

    public string Contenido { get; set; } = string.Empty;
    public DateTimeOffset FechaUtc { get; set; }
    public TipoMensajeChat TipoMensaje { get; set; }

    /// <summary>
    /// Cached sender name (participant display name or "BanterBot").
    /// </summary>
    public string NombreDisplay { get; set; } = string.Empty;

    public Torneo Torneo { get; set; } = null!;
}
