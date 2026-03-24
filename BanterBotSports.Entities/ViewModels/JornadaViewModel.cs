using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;

namespace BanterBotSports.Entities.ViewModels;

public class JornadaViewModel
{
    public int Id { get; set; }
    public int TorneoId { get; set; }
    public int Numero { get; set; }
    public EstadoJornada Estado { get; set; }
    public DateTimeOffset? DeadlineUtc { get; set; }
    public IReadOnlyList<PartidoDto> Partidos { get; set; } = Array.Empty<PartidoDto>();
    public bool EstaAbierta => Estado == EstadoJornada.Abierta;
}
