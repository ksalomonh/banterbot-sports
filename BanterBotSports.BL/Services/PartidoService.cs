using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.DTOs;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPartidoCatalogService _catalogService;

    public PartidoService(
        IPartidoRepository partidoRepository,
        IJornadaRepository jornadaRepository,
        IUnitOfWork unitOfWork,
        IPartidoCatalogService catalogService)
    {
        ArgumentNullException.ThrowIfNull(partidoRepository);
        ArgumentNullException.ThrowIfNull(jornadaRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogService);
        _partidoRepository = partidoRepository;
        _jornadaRepository = jornadaRepository;
        _unitOfWork = unitOfWork;
        _catalogService = catalogService;
    }

    public async Task AsignarPartidoAsync(int jornadaId, Partido partido)
    {
        ArgumentNullException.ThrowIfNull(partido);

        var jornada = await _jornadaRepository.GetByIdAsync(jornadaId)
            ?? throw new InvalidOperationException($"Jornada {jornadaId} no encontrada.");

        partido.JornadaId = jornada.Id;
        await _partidoRepository.AddAsync(partido);
        await _unitOfWork.SaveAsync();
    }

    /// <inheritdoc />
    public async Task AsignarPartidosAsync(int jornadaId, IReadOnlyList<Partido> partidos)
    {
        ArgumentNullException.ThrowIfNull(partidos);
        if (partidos.Count == 0) return;

        var jornada = await _jornadaRepository.GetByIdAsync(jornadaId)
            ?? throw new InvalidOperationException($"Jornada {jornadaId} no encontrada.");

        foreach (var partido in partidos)
        {
            partido.JornadaId = jornada.Id;
            await _partidoRepository.AddAsync(partido);
        }

        await _unitOfWork.SaveAsync();
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
        await _unitOfWork.SaveAsync();
    }

    /// <summary>
    /// Computes regulation-time goals (FT + AET, penalties excluded).
    /// The official scores passed must already exclude penalty shootout goals.
    /// </summary>
    public int ComputarGolesReglamento(int golesEquipo1, int golesEquipo2)
        => golesEquipo1 + golesEquipo2;

    /// <inheritdoc />
    public Task<IReadOnlyList<PartidoDto>> GetProximosPartidosAsync(int ligaId, DateOnly desde, DateOnly hasta)
        => _catalogService.GetProximosPartidosAsync(ligaId, desde, hasta);

    /// <inheritdoc />
    public Task<PartidoDto?> GetFixturePorExternalIdAsync(int externalId)
        => _catalogService.GetFixturePorExternalIdAsync(externalId);

    /// <inheritdoc />
    public bool EsLigaValida(int ligaId)
        => _catalogService.EsLigaValida(ligaId);

    /// <inheritdoc />
    public IReadOnlyList<LigaDto> GetLigas()
        => _catalogService.GetLigas();
}
