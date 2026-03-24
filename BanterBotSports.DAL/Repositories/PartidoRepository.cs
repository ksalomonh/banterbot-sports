using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.DAL.Repositories;

public class PartidoRepository : IPartidoRepository
{
    private readonly AppDbContext _context;

    public PartidoRepository(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<Partido?> GetByIdAsync(int id)
        => await _context.Partidos.FindAsync(id);

    public async Task<IReadOnlyList<Partido>> GetAllAsync()
        => await _context.Partidos.ToListAsync();

    public async Task<IReadOnlyList<Partido>> GetByJornadaIdAsync(int jornadaId)
        => await _context.Partidos
            .Where(p => p.JornadaId == jornadaId)
            .OrderBy(p => p.KickOffUtc)
            .ToListAsync();

    public async Task<IReadOnlyList<Partido>> GetByEstadoAsync(EstadoPartido estado)
        => await _context.Partidos
            .Where(p => p.Estado == estado)
            .ToListAsync();

    public async Task<IReadOnlyList<Partido>> GetByKickOffRangeAsync(DateTimeOffset from, DateTimeOffset to)
        => await _context.Partidos
            .Where(p => p.ExternalId != null && p.KickOffUtc >= from && p.KickOffUtc <= to)
            .OrderBy(p => p.KickOffUtc)
            .ToListAsync();

    public async Task<Partido?> GetByExternalIdAsync(string externalId)
        => await _context.Partidos
            .FirstOrDefaultAsync(p => p.ExternalId == externalId);

    public Task<Partido> AddAsync(Partido partido)
    {
        _context.Partidos.Add(partido);
        return Task.FromResult(partido);
    }

    public Task UpdateAsync(Partido partido)
    {
        _context.Partidos.Update(partido);
        return Task.CompletedTask;
    }
}
