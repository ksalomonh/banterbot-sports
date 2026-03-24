using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.DAL.Repositories;

public class TorneoRepository : ITorneoRepository
{
    private readonly AppDbContext _context;

    public TorneoRepository(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<Torneo?> GetByIdAsync(int id)
        => await _context.Torneos.FindAsync(id);

    public async Task<Torneo?> GetByIdWithDetailsAsync(int id)
        => await _context.Torneos
            .Include(t => t.ConfiguracionPremios)
            .Include(t => t.Jornadas)
            .Include(t => t.Participantes)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IReadOnlyList<Torneo>> GetAllAsync()
        => await _context.Torneos.ToListAsync();

    public async Task<IReadOnlyList<Torneo>> GetByOrganizadorIdAsync(string organizadorId)
        => await _context.Torneos
            .Where(t => t.OrganizadorId == organizadorId)
            .ToListAsync();

    public async Task<IReadOnlyList<Torneo>> GetTorneosByParticipanteAsync(string userId)
        => await _context.Participantes
            .Where(p => p.UserId == userId)
            .Select(p => p.Torneo!)
            .ToListAsync();

    public Task<Torneo> AddAsync(Torneo torneo)
    {
        _context.Torneos.Add(torneo);
        return Task.FromResult(torneo);
    }

    public Task UpdateAsync(Torneo torneo)
    {
        _context.Torneos.Update(torneo);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Torneo torneo)
    {
        _context.Torneos.Remove(torneo);
        return Task.CompletedTask;
    }
}
