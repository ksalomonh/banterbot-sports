using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using BanterBotSports.Entities.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

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
    private readonly IPartidoService _partidoService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminService _adminService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<TorneoService> _logger;

    public TorneoService(
        ITorneoRepository torneoRepository,
        IParticipanteRepository participanteRepository,
        IJornadaRepository jornadaRepository,
        IPrediccionRepository prediccionRepository,
        IUnitOfWork unitOfWork,
        IAdminService adminService,
        UserManager<AppUser> userManager,
        IPartidoService partidoService,
        ILogger<TorneoService> logger)
    {
        ArgumentNullException.ThrowIfNull(torneoRepository);
        ArgumentNullException.ThrowIfNull(participanteRepository);
        ArgumentNullException.ThrowIfNull(jornadaRepository);
        ArgumentNullException.ThrowIfNull(prediccionRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(adminService);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(partidoService);
        ArgumentNullException.ThrowIfNull(logger);

        _torneoRepository = torneoRepository;
        _participanteRepository = participanteRepository;
        _jornadaRepository = jornadaRepository;
        _prediccionRepository = prediccionRepository;
        _unitOfWork = unitOfWork;
        _adminService = adminService;
        _userManager = userManager;
        _partidoService = partidoService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Torneo> CrearTorneoAsync(TorneoCreateViewModel model, string organizadorId)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizadorId);

        var config = await _adminService.GetConfiguracionAsync();
        var user = await _userManager.FindByIdAsync(organizadorId)
            ?? throw new InvalidOperationException($"Usuario '{organizadorId}' no encontrado.");

        decimal resolvedPct = model.PorcentajeOrganizador
            ?? user.PorcentajeOrganizadorGlobal
            ?? config.PorcentajeOrganizadorMin;

        if (resolvedPct < config.PorcentajeOrganizadorMin || resolvedPct > config.PorcentajeOrganizadorMax)
            throw new InvalidOperationException(
                $"El porcentaje del organizador debe estar entre {config.PorcentajeOrganizadorMin}% y {config.PorcentajeOrganizadorMax}%.");

        decimal expectedPrizePool = 100m - config.PorcentajePlataforma - resolvedPct;
        var totalPorcentaje = model.ConfiguracionPremios.Sum(p => p.Porcentaje);
        if (Math.Abs(totalPorcentaje - expectedPrizePool) > 0.01m)
            throw new InvalidOperationException(
                $"Los porcentajes de premios deben sumar exactamente {expectedPrizePool}%.");

        var torneo = new Torneo
        {
            Nombre = model.Nombre,
            OrganizadorId = organizadorId,
            NumJornadas = model.NumJornadas,
            MontoInscripcion = model.MontoInscripcion,
            PtosResultado = model.PtosResultado,
            PtosMarcador = model.PtosMarcador,
            PtosGolesJornada = model.PtosGolesJornada,
            PorcentajeOrganizador = resolvedPct,
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

        // Assign Organizador role to the user if not already assigned
        if (!await _userManager.IsInRoleAsync(user, AppRoles.Organizador))
            await _userManager.AddToRoleAsync(user, AppRoles.Organizador);

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
    public async Task ConfirmarPagoAsync(int torneoId, int participanteId, string organizadorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizadorId);

        var torneo = await _torneoRepository.GetByIdAsync(torneoId)
            ?? throw new InvalidOperationException($"Torneo {torneoId} no encontrado.");

        if (torneo.OrganizadorId != organizadorId)
            throw new UnauthorizedAccessException("Solo el organizador puede confirmar pagos.");

        var participante = await _participanteRepository.GetByIdAsync(participanteId)
            ?? throw new InvalidOperationException($"Participante {participanteId} no encontrado.");

        if (participante.TorneoId != torneoId)
            throw new InvalidOperationException("El participante no pertenece a este torneo.");

        if (participante.Pago)
            return; // already paid — idempotent

        participante.Pago = true;
        await _participanteRepository.UpdateAsync(participante);
        await _unitOfWork.SaveAsync();
    }

    /// <inheritdoc />
    public async Task RevocarPagoAsync(int torneoId, int participanteId, string organizadorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizadorId);

        var torneo = await _torneoRepository.GetByIdAsync(torneoId)
            ?? throw new InvalidOperationException($"Torneo {torneoId} no encontrado.");

        if (torneo.OrganizadorId != organizadorId)
            throw new UnauthorizedAccessException("Solo el organizador puede revocar pagos.");

        var participante = await _participanteRepository.GetByIdAsync(participanteId)
            ?? throw new InvalidOperationException($"Participante {participanteId} no encontrado.");

        if (participante.TorneoId != torneoId)
            throw new InvalidOperationException("El participante no pertenece a este torneo.");

        if (participante.Rol == RolParticipante.Ambos)
            throw new InvalidOperationException("No se puede revocar el pago del organizador.");

        participante.Pago = false;
        await _participanteRepository.UpdateAsync(participante);
        await _unitOfWork.SaveAsync();
    }

    /// <inheritdoc />
    public async Task<int> DarDeBajaImpagosAsync(int torneoId)
    {
        var participantes = await _participanteRepository.GetByTorneoIdAsync(torneoId);
        var impagos = participantes
            .Where(p => !p.Pago && p.Rol != RolParticipante.Ambos)
            .ToList();

        foreach (var impago in impagos)
        {
            await _prediccionRepository.DeleteByParticipanteIdAsync(impago.Id);
            await _participanteRepository.DeleteAsync(impago);
        }

        if (impagos.Count > 0)
            await _unitOfWork.SaveAsync();

        return impagos.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TorneoResumen>> GetTorneosClonablesAsync(int excluirTorneoId, string organizadorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizadorId);

        // GetByOrganizadorIdAsync includes Participantes — no N+1 queries needed.
        var torneos = await _torneoRepository.GetByOrganizadorIdAsync(organizadorId);

        return torneos
            .Where(t => t.Id != excluirTorneoId
                && (t.Estado == EstadoTorneo.Activo || t.Estado == EstadoTorneo.Finalizado))
            .Select(t => new TorneoResumen(
                t.Id,
                t.Nombre,
                t.Participantes.Count(p => p.Rol == RolParticipante.Jugador)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ClonarJugadoresResult> ClonarJugadoresAsync(int torneoDestinoId, int torneoOrigenId, string organizadorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizadorId);

        var torneoDestino = await _torneoRepository.GetByIdAsync(torneoDestinoId)
            ?? throw new InvalidOperationException($"Torneo destino {torneoDestinoId} no encontrado.");
        if (torneoDestino.OrganizadorId != organizadorId)
            throw new UnauthorizedAccessException("Solo el organizador puede clonar jugadores en este torneo.");

        var torneoOrigen = await _torneoRepository.GetByIdAsync(torneoOrigenId)
            ?? throw new InvalidOperationException($"Torneo origen {torneoOrigenId} no encontrado.");
        if (torneoOrigen.OrganizadorId != organizadorId)
            throw new UnauthorizedAccessException("Ambos torneos deben pertenecer al mismo organizador.");

        var participantesOrigen = await _participanteRepository.GetByTorneoIdAsync(torneoOrigenId);
        var jugadores = participantesOrigen.Where(p => p.Rol == RolParticipante.Jugador).ToList();

        var participantesDestino = await _participanteRepository.GetByTorneoIdAsync(torneoDestinoId);
        var enrolledUserIds = participantesDestino.Select(p => p.UserId).ToHashSet();

        int clonados = 0, omitidos = 0;
        foreach (var jugador in jugadores)
        {
            if (enrolledUserIds.Contains(jugador.UserId)) { omitidos++; continue; }
            await _participanteRepository.AddAsync(new Participante
            {
                TorneoId = torneoDestinoId,
                UserId = jugador.UserId,
                Rol = RolParticipante.Jugador,
                Pago = false
            });
            clonados++;
        }

        if (clonados > 0)
            await _unitOfWork.SaveAsync();

        return new ClonarJugadoresResult(clonados, omitidos);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> AsignarPartidosInicialesAsync(int jornadaId, IReadOnlyList<PartidoDto> partidos)
    {
        ArgumentNullException.ThrowIfNull(partidos);
        if (partidos.Count == 0) return Array.Empty<string>();

        var failures = new List<string>();
        var validPartidos = new List<Partido>();

        foreach (var dto in partidos)
        {
            try
            {
                validPartidos.Add(new Partido
                {
                    JornadaId = jornadaId,
                    ExternalId = dto.ExternalId,
                    Equipo1 = dto.Equipo1,
                    Equipo2 = dto.Equipo2,
                    KickOffUtc = dto.KickOffUtc,
                    Estado = dto.Estado,
                    LogoUrlLocal = dto.LogoUrlEquipo1,
                    LogoUrlVisitante = dto.LogoUrlEquipo2
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build fixture {ExternalId} for jornada {JornadaId}", dto.ExternalId, jornadaId);
                if (dto.ExternalId is not null)
                    failures.Add(dto.ExternalId);
            }
        }

        if (validPartidos.Count > 0)
        {
            try
            {
                // Batch-assign all valid partidos in a single DB transaction.
                await _partidoService.AsignarPartidosAsync(jornadaId, validPartidos);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to batch-assign {Count} fixture(s) to jornada {JornadaId}", validPartidos.Count, jornadaId);
                failures.AddRange(validPartidos
                    .Where(p => p.ExternalId is not null)
                    .Select(p => p.ExternalId!));
            }
        }

        return failures;
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
                Display = users.GetValueOrDefault(p.UserId, "Jugador"),
                Pts = puntosPorParticipante.GetValueOrDefault(p.Id, 0)
            })
            .OrderByDescending(x => x.Pts)
            .Select((x, i) => new RankingParticipante(x.Id, x.Display, x.Pts, i + 1))
            .ToList();
    }
}
