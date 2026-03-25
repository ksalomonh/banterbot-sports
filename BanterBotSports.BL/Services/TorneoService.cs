using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Entities.ViewModels;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Encapsulates torneo creation, participant management, and ranking logic.
/// Controllers only call this service — never repositories directly.
/// </summary>
public class TorneoService : ITorneoService
{
    private readonly ITorneoRepository _torneoRepository;
    private readonly IParticipanteRepository _participanteRepository;
    private readonly IJornadaRepository _jornadaRepository;
    private readonly IPrediccionRepository _prediccionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TorneoService(
        ITorneoRepository torneoRepository,
        IParticipanteRepository participanteRepository,
        IJornadaRepository jornadaRepository,
        IPrediccionRepository prediccionRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(torneoRepository);
        ArgumentNullException.ThrowIfNull(participanteRepository);
        ArgumentNullException.ThrowIfNull(jornadaRepository);
        ArgumentNullException.ThrowIfNull(prediccionRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _torneoRepository = torneoRepository;
        _participanteRepository = participanteRepository;
        _jornadaRepository = jornadaRepository;
        _prediccionRepository = prediccionRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Torneo> CrearTorneoAsync(TorneoCreateViewModel model, string organizadorId)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizadorId);

        var totalPorcentaje = model.ConfiguracionPremios.Sum(p => p.Porcentaje);
        if (totalPorcentaje != 100)
            throw new InvalidOperationException("Los porcentajes de premios deben sumar 100%");

        var torneo = new Torneo
        {
            Nombre = model.Nombre,
            OrganizadorId = organizadorId,
            NumJornadas = model.NumJornadas,
            MontoInscripcion = model.MontoInscripcion,
            PtosResultado = model.PtosResultado,
            PtosMarcador = model.PtosMarcador,
            PtosGolesJornada = model.PtosGolesJornada,
            Estado = EstadoTorneo.Pendiente
        };

        foreach (var premioVm in model.ConfiguracionPremios)
        {
            torneo.ConfiguracionPremios.Add(new ConfiguracionPremio
            {
                Posicion = premioVm.Posicion,
                Porcentaje = premioVm.Porcentaje
            });
        }

        var torneoCreado = await _torneoRepository.AddAsync(torneo);
        await _unitOfWork.SaveAsync();

        // Auto-enroll organizer as Ambos (organizer + player)
        await _participanteRepository.AddAsync(new Participante
        {
            TorneoId = torneoCreado.Id,
            UserId = organizadorId,
            Rol = RolParticipante.Ambos,
            Pago = true
        });

        // Pre-create the configured number of jornadas
        for (int i = 1; i <= torneoCreado.NumJornadas; i++)
        {
            await _jornadaRepository.AddAsync(new Jornada
            {
                TorneoId = torneoCreado.Id,
                Numero = i,
                Estado = EstadoJornada.PendientePartidos
            });
        }

        await _unitOfWork.SaveAsync();
        return torneoCreado;
    }

    /// <inheritdoc />
    public Task<Torneo?> GetByIdAsync(int torneoId)
        => _torneoRepository.GetByIdAsync(torneoId);

    /// <inheritdoc />
    public Task<Torneo?> GetByIdWithDetailsAsync(int torneoId)
        => _torneoRepository.GetByIdWithDetailsAsync(torneoId);

    /// <inheritdoc />
    public Task<IReadOnlyList<Torneo>> GetTorneosPorUsuarioAsync(string userId)
        => _torneoRepository.GetTorneosByParticipanteAsync(userId);

    /// <inheritdoc />
    public Task<Participante?> GetParticipanteAsync(int torneoId, string userId)
        => _participanteRepository.GetByTorneoAndUserAsync(torneoId, userId);

    /// <inheritdoc />
    public async Task UnirseConTokenAsync(int torneoId, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var existing = await _participanteRepository.GetByTorneoAndUserAsync(torneoId, userId);
        if (existing is not null)
            return; // Already a participant — idempotent

        await _participanteRepository.AddAsync(new Participante
        {
            TorneoId = torneoId,
            UserId = userId,
            Rol = RolParticipante.Jugador,
            Pago = false
        });

        await _unitOfWork.SaveAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RankingParticipante>> BuildRankingAsync(Torneo torneo)
    {
        ArgumentNullException.ThrowIfNull(torneo);

        var participanteIds = torneo.Participantes.Select(p => p.Id).ToList();

        // Single DB query to get total points per participant
        var puntosPorParticipante = await _prediccionRepository
            .GetPuntosTotalesPorParticipanteAsync(participanteIds);

        // Resolve display names in one query — delegated to DAL to avoid EF Core in BL
        var userIds = torneo.Participantes.Select(p => p.UserId).ToList();
        var users = await _participanteRepository.GetDisplayNamesByIdsAsync(userIds);

        return torneo.Participantes
            .Select(p => new
            {
                p.Id,
                Display = users.GetValueOrDefault(p.UserId, p.UserId),
                Pts = puntosPorParticipante.GetValueOrDefault(p.Id, 0)
            })
            .OrderByDescending(x => x.Pts)
            .Select((x, i) => new RankingParticipante(x.Id, x.Display, x.Pts, i + 1))
            .ToList();
    }
}
