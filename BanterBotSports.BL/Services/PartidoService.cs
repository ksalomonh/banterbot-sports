using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Manages match assignment and result updates.
/// GolesReglamento = sum of FT + AET goals (penalties excluded).
/// After the jornada deadline, only organizers may update results.
/// </summary>
public class PartidoService : IPartidoService
{
    private readonly IPartidoRepository _partidoRepository;
    private readonly IJornadaRepository _jornadaRepository;
    private readonly AppDbContext _context;

    public PartidoService(
        IPartidoRepository partidoRepository,
        IJornadaRepository jornadaRepository,
        AppDbContext context)
    {
        _partidoRepository = partidoRepository;
        _jornadaRepository = jornadaRepository;
        _context = context;
    }

    public async Task AsignarPartidoAsync(int jornadaId, Partido partido)
    {
        ArgumentNullException.ThrowIfNull(partido);

        var jornada = await _jornadaRepository.GetByIdAsync(jornadaId)
            ?? throw new InvalidOperationException($"Jornada {jornadaId} no encontrada.");

        partido.JornadaId = jornada.Id;
        await _partidoRepository.AddAsync(partido);
        await _context.SaveChangesAsync();
    }

    public async Task ActualizarResultadoAsync(
        int partidoId,
        int golesEquipo1,
        int golesEquipo2,
        EstadoPartido nuevoEstado,
        bool esOrganizador = false)
    {
        var partido = await _partidoRepository.GetByIdAsync(partidoId)
            ?? throw new InvalidOperationException($"Partido {partidoId} no encontrado.");

        var jornada = await _jornadaRepository.GetByIdAsync(partido.JornadaId)
            ?? throw new InvalidOperationException($"Jornada {partido.JornadaId} no encontrada.");

        // Enforce: after deadline, only organizer can update results
        if (jornada.DeadlineUtc.HasValue
            && DateTimeOffset.UtcNow >= jornada.DeadlineUtc.Value
            && !esOrganizador)
        {
            throw new UnauthorizedAccessException(
                "Solo el organizador puede modificar resultados después del cierre de la jornada.");
        }

        partido.GolesEquipo1Oficial = golesEquipo1;
        partido.GolesEquipo2Oficial = golesEquipo2;
        partido.GolesReglamento = ComputarGolesReglamento(golesEquipo1, golesEquipo2);
        partido.Estado = nuevoEstado;

        await _partidoRepository.UpdateAsync(partido);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Computes regulation-time goals (FT + AET, penalties excluded).
    /// The official scores passed must already exclude penalty shootout goals.
    /// </summary>
    public int ComputarGolesReglamento(int golesEquipo1, int golesEquipo2)
        => golesEquipo1 + golesEquipo2;
}
