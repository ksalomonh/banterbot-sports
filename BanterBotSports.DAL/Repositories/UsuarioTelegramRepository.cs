using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using Microsoft.EntityFrameworkCore;

namespace BanterBotSports.DAL.Repositories;

public class UsuarioTelegramRepository : IUsuarioTelegramRepository
{
    private readonly AppDbContext _context;

    public UsuarioTelegramRepository(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public Task<UsuarioTelegram?> GetByTelegramUserIdAsync(long telegramUserId)
        => _context.UsuariosTelegram
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId);

    public Task<UsuarioTelegram?> GetByUserIdAsync(string userId)
        => _context.UsuariosTelegram
            .FirstOrDefaultAsync(u => u.UserId == userId);

    /// <summary>Tracks entity for insertion — caller must call SaveChangesAsync.</summary>
    public Task<UsuarioTelegram> AddAsync(UsuarioTelegram usuarioTelegram)
    {
        _context.UsuariosTelegram.Add(usuarioTelegram);
        return Task.FromResult(usuarioTelegram);
    }

    /// <summary>Marks entity as modified — caller must call SaveChangesAsync.</summary>
    public Task UpdateAsync(UsuarioTelegram usuarioTelegram)
    {
        _context.UsuariosTelegram.Update(usuarioTelegram);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyDictionary<string, long>> GetTelegramIdsByUserIdsAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.ToList();
        return await _context.UsuariosTelegram
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.TelegramUserId);
    }
}
