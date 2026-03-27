namespace BanterBotSports.BL.Exceptions;

/// <summary>
/// Thrown when an operation requires a <see cref="Entities.Jornada"/> to have at least
/// one <see cref="Entities.Partido"/> assigned and none are found.
/// </summary>
public sealed class JornadaSinPartidosException : InvalidOperationException
{
    public int JornadaId { get; }
    public int JornadaNumero { get; }

    public JornadaSinPartidosException(int jornadaId, int jornadaNumero)
        : base($"La jornada {jornadaNumero} no tiene partidos asignados y no puede abrirse.")
    {
        JornadaId = jornadaId;
        JornadaNumero = jornadaNumero;
    }
}
