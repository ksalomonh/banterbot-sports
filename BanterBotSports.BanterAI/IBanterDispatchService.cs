using BanterBotSports.Entities;

namespace BanterBotSports.BanterAI;

/// <summary>
/// Dispatches AI-generated banter to each participante after a jornada is finalized.
/// </summary>
public interface IBanterDispatchService
{
    Task OnJornadaFinalizadaAsync(Jornada jornada);
}
