using BanterBotSports.Entities;

namespace BanterBotSports.DAL.Repositories.Interfaces;

public interface IUsuarioTelegramRepository
{
    Task<UsuarioTelegram?> GetByTelegramUserIdAsync(long telegramUserId);
    Task<UsuarioTelegram?> GetByUserIdAsync(string userId);
    Task<UsuarioTelegram> AddAsync(UsuarioTelegram usuarioTelegram);
    Task UpdateAsync(UsuarioTelegram usuarioTelegram);
}
