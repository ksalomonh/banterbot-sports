using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.DAL.Repositories;

public class PrediccionRepository : IPrediccionRepository
{
    private readonly AppDbContext _context;

    public PrediccionRepository(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    // --- PrediccionPartido ---

    public async Task<PrediccionPartido?> GetPrediccionPartidoByIdAsync(int id)
        => await _context.PrediccionesPartido.FindAsync(id);

    public async Task<PrediccionPartido?> GetPrediccionPartidoAsync(int partidoId, int participanteId)
        => await _context.PrediccionesPartido
            .FirstOrDefaultAsync(pp => pp.PartidoId == partidoId && pp.ParticipanteId == participanteId);

    public async Task<IReadOnlyList<PrediccionPartido>> GetPrediccionesByPartidoAsync(int partidoId)
        => await _context.PrediccionesPartido
            .Where(pp => pp.PartidoId == partidoId)
            .ToListAsync();

    public async Task<IReadOnlyList<PrediccionPartido>> GetPrediccionesByParticipanteAsync(int participanteId)
        => await _context.PrediccionesPartido
            .Where(pp => pp.ParticipanteId == participanteId)
            .ToListAsync();

    public async Task<IReadOnlyList<PrediccionPartido>> GetPrediccionesByJornadaAndParticipanteAsync(int jornadaId, int participanteId)
        => await _context.PrediccionesPartido
            .Include(pp => pp.Partido)
            .Where(pp => pp.Partido.JornadaId == jornadaId && pp.ParticipanteId == participanteId)
            .ToListAsync();

    public Task<PrediccionPartido> AddPrediccionPartidoAsync(PrediccionPartido prediccion)
    {
        _context.PrediccionesPartido.Add(prediccion);
        return Task.FromResult(prediccion);
    }

    public Task UpdatePrediccionPartidoAsync(PrediccionPartido prediccion)
    {
        _context.PrediccionesPartido.Update(prediccion);
        return Task.CompletedTask;
    }

    // --- PrediccionJornada ---

    public async Task<PrediccionJornada?> GetPrediccionJornadaByIdAsync(int id)
        => await _context.PrediccionesJornada.FindAsync(id);

    public async Task<PrediccionJornada?> GetPrediccionJornadaAsync(int jornadaId, int participanteId)
        => await _context.PrediccionesJornada
            .FirstOrDefaultAsync(pj => pj.JornadaId == jornadaId && pj.ParticipanteId == participanteId);

    public async Task<IReadOnlyList<PrediccionJornada>> GetPrediccionesJornadaByJornadaAsync(int jornadaId)
        => await _context.PrediccionesJornada
            .Where(pj => pj.JornadaId == jornadaId)
            .ToListAsync();

    public Task<PrediccionJornada> AddPrediccionJornadaAsync(PrediccionJornada prediccion)
    {
        _context.PrediccionesJornada.Add(prediccion);
        return Task.FromResult(prediccion);
    }

    public Task UpdatePrediccionJornadaAsync(PrediccionJornada prediccion)
    {
        _context.PrediccionesJornada.Update(prediccion);
        return Task.CompletedTask;
    }
}
