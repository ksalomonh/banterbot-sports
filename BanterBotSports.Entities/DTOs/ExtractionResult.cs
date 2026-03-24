namespace BanterBotSports.Entities.DTOs;

public record ExtractionResult(
    bool Success,
    string? Error,
    IReadOnlyList<PrediccionPartidoDto> Predicciones,
    double Confidence
);
