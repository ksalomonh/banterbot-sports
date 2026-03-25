using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.DAL.Repositories;

public class ParticipanteRepository : IParticipanteRepository
{
    private readonly AppDbContext _context;

    public ParticipanteRepository(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesByIdsAsync(IReadOnlyList<string> userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        return await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => u.NombreDisplay ?? u.UserName ?? u.Id);
    }

    public async Task<Participante?> GetByIdAsync(int id)
        => await _context.Participantes.FindAsync(id);

    public async Task<IReadOnlyList<Participante>> GetAllAsync()
        => await _context.Participantes.ToListAsync();

    public async Task<IReadOnlyList<Participante>> GetByTorneoIdAsync(int torneoId)
        => await _context.Participantes
            .Where(p => p.TorneoId == torneoId)
            .ToListAsync();

    public async Task<Participante?> GetByTorneoAndUserAsync(int torneoId, string userId)
        => await _context.Participantes
            .FirstOrDefaultAsync(p => p.TorneoId == torneoId && p.UserId == userId);

    public async Task<IReadOnlyList<Participante>> GetByUserIdAsync(string userId)
        => await _context.Participantes
            .Where(p => p.UserId == userId)
            .ToListAsync();

    public Task<Participante> AddAsync(Participante participante)
    {
        _context.Participantes.Add(participante);
        return Task.FromResult(participante);
    }

    public Task UpdateAsync(Participante participante)
    {
        _context.Participantes.Update(participante);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Participante participante)
    {
        _context.Participantes.Remove(participante);
        return Task.CompletedTask;
    }
}
