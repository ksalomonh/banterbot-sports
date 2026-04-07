using BanterBotSports.BL.Services.Interfaces;
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
    private readonly IUnitOfWork _unitOfWork;

    public PrediccionService(
        IPrediccionRepository prediccionRepository,
        IJornadaRepository jornadaRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(prediccionRepository);
        ArgumentNullException.ThrowIfNull(jornadaRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _prediccionRepository = prediccionRepository;
        _jornadaRepository = jornadaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyDictionary<int, PrediccionPartido>> GetPorJornadaYParticipanteAsync(int jornadaId, int participanteId)
    {
        var predicciones = await _prediccionRepository
            .GetPrediccionesByJornadaAndParticipanteAsync(jornadaId, participanteId);
        return predicciones.ToDictionary(pp => pp.PartidoId);
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

        await _unitOfWork.SaveAsync();
    }

    public async Task<IReadOnlyList<PrediccionJornada>> GetByJornadaAsync(int jornadaId)
    {
        return await _prediccionRepository.GetPrediccionesJornadaByJornadaAsync(jornadaId);
    }

    /// <summary>
    /// Computes jornada-level goal points for each participant.
    /// Compares each participant's GolesPronosticados with the sum of official goals across
    /// all partidos in the jornada. Awards Torneo.PtosGolesJornada on an exact match,
    /// persists zero otherwise. Creates a PrediccionJornada with PuntosObtenidos = 0
    /// for participants who have no prior record.
    /// Must be called after official results are entered (GolesEquipo1Oficial is set).
    /// </summary>
    public async Task CalcularPuntosGolesJornadaAsync(int jornadaId)
    {
        var jornada = await _jornadaRepository.GetByIdWithDetailsAsync(jornadaId)
            ?? throw new InvalidOperationException($"Jornada {jornadaId} no encontrada.");

        // Sum official goals across all partidos in the jornada
        int totalGolesOficiales = jornada.Partidos
            .Sum(p => (p.GolesEquipo1Oficial ?? 0) + (p.GolesEquipo2Oficial ?? 0));

        // Load all PrediccionJornada records for this jornada
        var prediccionesJornada = await _prediccionRepository.GetPrediccionesJornadaByJornadaAsync(jornadaId);

        int ptosGolesJornada = jornada.Torneo.PtosGolesJornada;

        // Build a lookup of existing PrediccionJornada rows by participanteId
        var existingByParticipante = prediccionesJornada.ToDictionary(pj => pj.ParticipanteId);

        // Update existing rows and create zero-point rows for participants with no record
        foreach (var participante in jornada.Torneo.Participantes)
        {
            if (existingByParticipante.TryGetValue(participante.Id, out var prediccionJornada))
            {
                bool acierto = prediccionJornada.GolesPronosticados == totalGolesOficiales;
                prediccionJornada.PuntosObtenidos = acierto ? ptosGolesJornada : 0;
                await _prediccionRepository.UpdatePrediccionJornadaAsync(prediccionJornada);
            }
            else
            {
                // Participant had no predictions at all — persist a zero-point row
                await _prediccionRepository.AddPrediccionJornadaAsync(new PrediccionJornada
                {
                    JornadaId = jornadaId,
                    ParticipanteId = participante.Id,
                    GolesPronosticados = 0,
                    PuntosObtenidos = 0
                });
            }
        }

        await _unitOfWork.SaveAsync();
    }

    /// <summary>
    /// Upserts the participant's manual total-goals prediction for the jornada.
    /// Mirrors GuardarPrediccionAsync deadline enforcement pattern.
    /// </summary>
    public async Task GuardarPrediccionJornadaAsync(
        int jornadaId,
        int participanteId,
        int golesPronosticados,
        bool esOrganizador = false)
    {
        var jornada = await _jornadaRepository.GetByIdWithDetailsAsync(jornadaId)
            ?? throw new InvalidOperationException($"Jornada {jornadaId} no encontrada.");

        // Enforce deadline: same pattern as GuardarPrediccionAsync
        if (jornada.DeadlineUtc.HasValue
            && DateTimeOffset.UtcNow >= jornada.DeadlineUtc.Value
            && !esOrganizador)
        {
            throw new InvalidOperationException(
                $"El plazo para la jornada {jornada.Numero} ya cerró. " +
                $"Deadline: {jornada.DeadlineUtc:u}");
        }

        // Upsert via existing repository method
        var existing = await _prediccionRepository.GetPrediccionJornadaAsync(jornadaId, participanteId);

        if (existing is not null)
        {
            existing.GolesPronosticados = golesPronosticados;
            await _prediccionRepository.UpdatePrediccionJornadaAsync(existing);
        }
        else
        {
            await _prediccionRepository.AddPrediccionJornadaAsync(new PrediccionJornada
            {
                JornadaId = jornadaId,
                ParticipanteId = participanteId,
                GolesPronosticados = golesPronosticados,
                PuntosObtenidos = 0
            });
        }

        await _unitOfWork.SaveAsync();
    }

    /// <summary>
    /// Aggregates total goals predicted by each participant across all match predictions
    /// in the jornada and persists the sum in PrediccionJornada.GolesPronosticados.
    /// </summary>
    public async Task ActualizarGolesJornadaAsync(int jornadaId)
    {
        var jornada = await _jornadaRepository.GetByIdWithDetailsAsync(jornadaId)
            ?? throw new InvalidOperationException($"Jornada {jornadaId} no encontrada.");

        // Single query to fetch all predicciones for every partido in the jornada
        var prediccionesPorPartido = await _prediccionRepository.GetPrediccionesByJornadaIdAsync(jornadaId);

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

        await _unitOfWork.SaveAsync();
    }
}
