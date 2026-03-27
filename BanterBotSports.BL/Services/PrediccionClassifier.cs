using BanterBotSports.Entities.ViewModels;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Classifies a match prediction against the official result.
/// Extracted as a public static helper so it can be unit-tested directly
/// and reused without instantiating JornadaService.
/// </summary>
public static class PrediccionClassifier
{
    /// <summary>
    /// Returns the accuracy classification for a prediction vs. official goals.
    /// Returns <see cref="ResultadoPrediccion.SinPrediccion"/> when either set of goals is null.
    /// </summary>
    public static ResultadoPrediccion Clasificar(
        int? golesPredichos1, int? golesPredichos2,
        int? golesOficiales1, int? golesOficiales2)
    {
        if (golesPredichos1 is null || golesPredichos2 is null)
            return ResultadoPrediccion.SinPrediccion;

        if (golesOficiales1 is null || golesOficiales2 is null)
            return ResultadoPrediccion.SinPrediccion;

        if (golesOficiales1 == golesPredichos1 && golesOficiales2 == golesPredichos2)
            return ResultadoPrediccion.Exacto;

        var oficialOutcome  = Math.Sign(golesOficiales1.Value  - golesOficiales2.Value);
        var predichoOutcome = Math.Sign(golesPredichos1.Value  - golesPredichos2.Value);
        if (oficialOutcome == predichoOutcome)
            return ResultadoPrediccion.ResultadoCorrecto;

        return ResultadoPrediccion.Fallido;
    }
}
