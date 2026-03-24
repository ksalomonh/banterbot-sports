using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Distributes the prize pool according to organizer-defined percentage configuration.
/// Tie-breaking: players tied at position N share the combined prize for positions N..N+k (split evenly).
/// Percentages are read from torneo.ConfiguracionPremios — never hardcoded.
/// </summary>
public class PremioService : IPremioService
{
    public IReadOnlyList<PremioDistribucion> CalcularDistribucion(
        IReadOnlyList<RankingParticipante> rankings,
        Torneo torneo)
    {
        ArgumentNullException.ThrowIfNull(rankings);
        ArgumentNullException.ThrowIfNull(torneo);

        if (rankings.Count == 0)
            return Array.Empty<PremioDistribucion>();

        // Total pool = sum of inscriptions
        decimal totalPool = torneo.MontoInscripcion * torneo.Participantes.Count;

        // Build lookup: posicion -> porcentaje from organizer configuration
        var configMap = torneo.ConfiguracionPremios
            .ToDictionary(c => c.Posicion, c => c.Porcentaje);

        var result = new List<PremioDistribucion>();
        int currentPosition = 1;

        // Group by PuntosTotal descending to detect ties
        var groupedByPoints = rankings
            .OrderByDescending(r => r.PuntosTotal)
            .GroupBy(r => r.PuntosTotal)
            .ToList();

        foreach (var group in groupedByPoints)
        {
            var tied = group.ToList();
            int tiedCount = tied.Count;
            int lastPosition = currentPosition + tiedCount - 1;

            // Sum percentages for all positions covered by this tie group
            decimal combinedPorcentaje = 0m;
            for (int pos = currentPosition; pos <= lastPosition; pos++)
            {
                if (configMap.TryGetValue(pos, out decimal pct))
                    combinedPorcentaje += pct;
            }

            decimal totalPrizeTied = totalPool * combinedPorcentaje / 100m;
            decimal prizePerParticipant = tiedCount > 0
                ? Math.Round(totalPrizeTied / tiedCount, 2)
                : 0m;

            foreach (var participant in tied)
            {
                result.Add(new PremioDistribucion(
                    participant.ParticipanteId,
                    currentPosition,
                    prizePerParticipant));
            }

            currentPosition += tiedCount;
        }

        return result.AsReadOnly();
    }
}
