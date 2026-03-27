using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.DAL.Repositories;

public class JornadaRepository : IJornadaRepository
{
    private readonly AppDbContext _context;

    public JornadaRepository(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<Jornada?> GetByIdAsync(int id)
        => await _context.Jornadas.FindAsync(id);

    public async Task<Jornada?> GetByIdWithDetailsAsync(int id)
        => await _context.Jornadas
            .Include(j => j.Torneo)
                .ThenInclude(t => t.Participantes)
            .Include(j => j.Partidos)
            .Include(j => j.PrediccionesJornada)
            .FirstOrDefaultAsync(j => j.Id == id);

    public async Task<IReadOnlyList<Jornada>> GetAllAsync()
        => await _context.Jornadas.ToListAsync();

    public async Task<IReadOnlyList<Jornada>> GetByTorneoIdAsync(int torneoId)
        => await _context.Jornadas
            .Where(j => j.TorneoId == torneoId)
            .OrderBy(j => j.Numero)
            .ToListAsync();

    public async Task<IReadOnlyList<Jornada>> GetByEstadoAsync(EstadoJornada estado)
        => await _context.Jornadas
            .Where(j => j.Estado == estado)
            .ToListAsync();

    public Task<Jornada?> GetByTorneoAndEstadoAsync(int torneoId, EstadoJornada estado)
        => _context.Jornadas
            .FirstOrDefaultAsync(j => j.TorneoId == torneoId && j.Estado == estado);

    public Task<Jornada> AddAsync(Jornada jornada)
    {
        _context.Jornadas.Add(jornada);
        return Task.FromResult(jornada);
    }

    public Task UpdateAsync(Jornada jornada)
    {
        _context.Jornadas.Update(jornada);
        return Task.CompletedTask;
    }
}
