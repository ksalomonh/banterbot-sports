namespace BanterBotSports.Entities.ViewModels;

/// <summary>Classification of a single match prediction accuracy.</summary>
public enum ResultadoPrediccion
{
    /// <summary>No prediction was submitted for this match.</summary>
    SinPrediccion,

    /// <summary>Exact score predicted correctly.</summary>
    Exacto,

    /// <summary>Match outcome (win/draw/loss) predicted correctly, but not the exact score.</summary>
    ResultadoCorrecto,

    /// <summary>Wrong outcome predicted.</summary>
    Fallido
}

/// <summary>Summary of a closed/finalized jornada: per-participant predictions vs official results.</summary>
public record ResumenViewModel(
    int JornadaId,
    int JornadaNumero,
    string TorneoNombre,
    int TorneoId,
    IReadOnlyList<ResumenParticipanteRow> Participantes);

/// <summary>One row per participant in the jornada summary.</summary>
public record ResumenParticipanteRow(
    string NombreDisplay,
    int PuntosJornada,
    IReadOnlyList<PrediccionConResultado> Predicciones);

/// <summary>A single match prediction plus its official result, with pre-computed accuracy classification.</summary>
public record PrediccionConResultado(
    int PartidoId,
    string Equipo1,
    string Equipo2,
    int? GolesEquipo1Oficial,
    int? GolesEquipo2Oficial,
    int? GolesPredichos1,
    int? GolesPredichos2,
    int? PuntosObtenidos)
{
    /// <summary>
    /// Pre-computed accuracy classification — avoids business logic in the view layer.
    /// Computed from the raw goals data at construction time in the service layer.
    /// </summary>
    public ResultadoPrediccion Resultado => ComputeResultado();

    private ResultadoPrediccion ComputeResultado()
    {
        // No prediction submitted
        if (GolesPredichos1 is null || GolesPredichos2 is null)
            return ResultadoPrediccion.SinPrediccion;

        // Results not yet official
        if (GolesEquipo1Oficial is null || GolesEquipo2Oficial is null)
            return ResultadoPrediccion.SinPrediccion;

        // Exact score match
        if (GolesEquipo1Oficial == GolesPredichos1 && GolesEquipo2Oficial == GolesPredichos2)
            return ResultadoPrediccion.Exacto;

        // Outcome match (home win / away win / draw)
        var oficialOutcome = Math.Sign(GolesEquipo1Oficial.Value - GolesEquipo2Oficial.Value);
        var predichoOutcome = Math.Sign(GolesPredichos1.Value - GolesPredichos2.Value);
        if (oficialOutcome == predichoOutcome)
            return ResultadoPrediccion.ResultadoCorrecto;

        return ResultadoPrediccion.Fallido;
    }
}
