using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.DAL.Repositories;

public class ChatRepository : IChatRepository
{
    private readonly AppDbContext _context;

    public ChatRepository(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MensajeChat>> GetByTorneoAsync(int torneoId, int limit, long? beforeId = null)
    {
        var query = _context.MensajesChat
            .Where(m => m.TorneoId == torneoId);

        if (beforeId.HasValue)
            query = query.Where(m => m.Id < beforeId.Value);

        return await query
            .OrderByDescending(m => m.FechaUtc)
            .Take(limit)
            .ToListAsync();
    }

    /// <inheritdoc />
    public Task<MensajeChat> AddAsync(MensajeChat mensaje)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        _context.MensajesChat.Add(mensaje);
        return Task.FromResult(mensaje);
    }
}
