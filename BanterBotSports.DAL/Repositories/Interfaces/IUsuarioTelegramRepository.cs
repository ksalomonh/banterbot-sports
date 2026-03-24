using BanterBotSports.Entities;

namespace BanterBotSports.DAL.Repositories.Interfaces;

public interface IUsuarioTelegramRepository
{
    Task<UsuarioTelegram?> GetByTelegramUserIdAsync(long telegramUserId);
    Task<UsuarioTelegram?> GetByUserIdAsync(string userId);
    Task<IReadOnlyDictionary<string, long>> GetTelegramIdsByUserIdsAsync(IEnumerable<string> userIds);
    Task<UsuarioTelegram> AddAsync(UsuarioTelegram usuarioTelegram);
    Task UpdateAsync(UsuarioTelegram usuarioTelegram);
}
