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

/// <summary>
/// A single match prediction plus its official result.
/// <see cref="Resultado"/> is computed and injected by the BL service layer — no classification logic lives here.
/// </summary>
public record PrediccionConResultado(
    int PartidoId,
    string Equipo1,
    string Equipo2,
    int? GolesEquipo1Oficial,
    int? GolesEquipo2Oficial,
    int? GolesPredichos1,
    int? GolesPredichos2,
    int? PuntosObtenidos,
    ResultadoPrediccion Resultado,
    string? LogoUrlLocal = null,
    string? LogoUrlVisitante = null);
