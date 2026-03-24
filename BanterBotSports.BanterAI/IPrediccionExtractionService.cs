using BanterBotSports.Entities.DTOs;

namespace BanterBotSports.BanterAI;

public interface IPrediccionExtractionService
{
    Task<ExtractionResult> ExtractAsync(string text, IReadOnlyList<PartidoDto> partidos);
}
