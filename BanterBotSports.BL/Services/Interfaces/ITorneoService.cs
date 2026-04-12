using BanterBotSports.BL.Models;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.ViewModels;

namespace BanterBotSports.BL.Services.Interfaces;

/// <summary>
/// Handles torneo creation, participant management, and ranking calculation.
/// All write operations and data queries go through this service;
/// controllers never call repositories directly.
/// </summary>
public interface ITorneoService
{
    /// <summary>
    /// Creates a new torneo, auto-enrolls the organizer as Ambos participant,
    /// and pre-creates the configured number of jornadas.
    /// </summary>
    Task<Torneo> CrearTorneoAsync(TorneoCreateViewModel model, string organizadorId);

    /// <summary>Returns a torneo by ID (without navigation properties).</summary>
    Task<Torneo?> GetByIdAsync(int torneoId);

    /// <summary>Returns a torneo by ID including participants, jornadas, and prize config.</summary>
    Task<Torneo?> GetByIdWithDetailsAsync(int torneoId);

    /// <summary>
    /// Returns the torneos the given user participates in (as player or organizer).
    /// Filtered at the DB level — no full table scan.
    /// </summary>
    Task<IReadOnlyList<Torneo>> GetTorneosPorUsuarioAsync(string userId);

    /// <summary>Returns the participante record for a given torneo and user, or null if not found.</summary>
    Task<Entities.Participante?> GetParticipanteAsync(int torneoId, string userId);

    /// <summary>
    /// Adds the user as a Jugador participant using a validated invite token payload.
    /// Idempotent: does nothing if already a participant.
    /// </summary>
    Task UnirseConTokenAsync(int torneoId, string userId);

    /// <summary>
    /// Builds the ranking for a torneo — points aggregated at the DB level, not in memory.
    /// Requires torneo.Participantes to be loaded.
    /// </summary>
    Task<IReadOnlyList<RankingParticipante>> BuildRankingAsync(Torneo torneo);

    /// <summary>
    /// Confirms payment for a participant. Only the organizer can call this.
    /// Idempotent: if already paid, does nothing.
    /// </summary>
    Task ConfirmarPagoAsync(int torneoId, int participanteId, string organizadorId);

    /// <summary>
    /// Revokes payment for a participant. Only the organizer can call this.
    /// Cannot revoke the organizer's own payment (Rol=Ambos).
    /// </summary>
    Task RevocarPagoAsync(int torneoId, int participanteId, string organizadorId);

    /// <summary>
    /// Removes all unpaid participants from the torneo and deletes their predictions.
    /// The organizer (Rol=Ambos) is never removed.
    /// Returns the number of participants removed.
    /// </summary>
    Task<int> DarDeBajaImpagosAsync(int torneoId);

    /// <summary>
    /// Returns torneos owned by the same organizer with Estado Activo or Finalizado,
    /// excluding the specified torneo.
    /// </summary>
    Task<IReadOnlyList<TorneoResumen>> GetTorneosClonablesAsync(int excluirTorneoId, string organizadorId);

    /// <summary>
    /// Clones Jugador-role participants from source torneo into destination torneo.
    /// Both torneos must belong to the same organizer. Sets Pago=false on all cloned rows.
    /// Idempotent: already-enrolled users are skipped (counted as Omitidos).
    /// </summary>
    Task<ClonarJugadoresResult> ClonarJugadoresAsync(int torneoDestinoId, int torneoOrigenId, string organizadorId);

    /// <summary>
    /// Assigns a list of fixtures (from an external catalog) to the given jornada.
    /// Partido entities are created from the provided DTOs.
    /// Returns a list of external IDs that failed to be assigned.
    /// </summary>
    Task<IReadOnlyList<string>> AsignarPartidosInicialesAsync(int jornadaId, IReadOnlyList<PartidoDto> partidos);
}
