using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Handles prediction storage with deadline enforcement and jornada goal aggregation.
/// Deadline = DeadlineUtc on Jornada (set to first kick-off of the jornada).
/// Only organizers (or "Ambos") can submit/update predictions after the deadline.
/// </summary>
public class PrediccionService : IPrediccionService
{
    private readonly IPrediccionRepository _prediccionRepository;
    private readonly IJornadaRepository _jornadaRepository;
    private readonly AppDbContext _context;

    public PrediccionService(
        IPrediccionRepository prediccionRepository,
        IJornadaRepository jornadaRepository,
        AppDbContext context)
    {
        _prediccionRepository = prediccionRepository;
        _jornadaRepository = jornadaRepository;
        _context = context;
    }

    public async Task GuardarPrediccionAsync(
        PrediccionPartido prediccion,
        Jornada jornada,
        bool esOrganizador = false)
    {
        ArgumentNullException.ThrowIfNull(prediccion);
        ArgumentNullException.ThrowIfNull(jornada);

        // Enforce deadline: if deadline has passed and caller is not organizer, reject
        if (jornada.DeadlineUtc.HasValue
            && DateTimeOffset.UtcNow >= jornada.DeadlineUtc.Value
            && !esOrganizador)
        {
            throw new InvalidOperationException(
                $"El plazo para la jornada {jornada.Numero} ya cerró. " +
                $"Deadline: {jornada.DeadlineUtc:u}");
        }

        // Upsert: update if already exists, otherwise add
        var existing = await _prediccionRepository.GetPrediccionPartidoAsync(
            prediccion.PartidoId,
            prediccion.ParticipanteId);

        if (existing is not null)
        {
            existing.GolesEquipo1 = prediccion.GolesEquipo1;
            existing.GolesEquipo2 = prediccion.GolesEquipo2;
            existing.Fuente = prediccion.Fuente;
            await _prediccionRepository.UpdatePrediccionPartidoAsync(existing);
        }
        else
        {
            await _prediccionRepository.AddPrediccionPartidoAsync(prediccion);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<PrediccionJornada>> GetByJornadaAsync(int jornadaId)
    {
        return await _prediccionRepository.GetPrediccionesJornadaByJornadaAsync(jornadaId);
    }

    /// <summary>
    /// Aggregates total goals predicted by each participant across all match predictions
    /// in the jornada and persists the sum in PrediccionJornada.GolesPronosticados.
    /// </summary>
    public async Task ActualizarGolesJornadaAsync(int jornadaId)
    {
        var jornada = await _jornadaRepository.GetByIdWithDetailsAsync(jornadaId)
            ?? throw new InvalidOperationException($"Jornada {jornadaId} no encontrada.");

        // Fetch all predicciones for every partido in the jornada grouped by participant
        var prediccionesPorPartido = new List<PrediccionPartido>();
        foreach (var partido in jornada.Partidos)
        {
            var preds = await _prediccionRepository.GetPrediccionesByPartidoAsync(partido.Id);
            prediccionesPorPartido.AddRange(preds);
        }

        // Group by participante and sum goals
        var goalsByParticipante = prediccionesPorPartido
            .GroupBy(p => p.ParticipanteId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(p => p.GolesEquipo1 + p.GolesEquipo2));

        // Upsert PrediccionJornada records
        foreach (var (participanteId, totalGoles) in goalsByParticipante)
        {
            var existing = await _prediccionRepository.GetPrediccionJornadaAsync(jornadaId, participanteId);
            if (existing is not null)
            {
                existing.GolesPronosticados = totalGoles;
                await _prediccionRepository.UpdatePrediccionJornadaAsync(existing);
            }
            else
            {
                await _prediccionRepository.AddPrediccionJornadaAsync(new PrediccionJornada
                {
                    JornadaId = jornadaId,
                    ParticipanteId = participanteId,
                    GolesPronosticados = totalGoles
                });
            }
        }

        await _context.SaveChangesAsync();
    }
}
