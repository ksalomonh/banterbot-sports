using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BanterBotSports.BL.Services;

public class TelegramVinculacionService : ITelegramVinculacionService
{
    private readonly IUsuarioTelegramRepository _usuarioTelegramRepository;
    private readonly IParticipanteRepository _participanteRepository;
    private readonly IJornadaRepository _jornadaRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TelegramVinculacionService> _logger;

    public TelegramVinculacionService(
        IUsuarioTelegramRepository usuarioTelegramRepository,
        IParticipanteRepository participanteRepository,
        IJornadaRepository jornadaRepository,
        UserManager<AppUser> userManager,
        IUnitOfWork unitOfWork,
        ILogger<TelegramVinculacionService> logger)
    {
        ArgumentNullException.ThrowIfNull(usuarioTelegramRepository);
        ArgumentNullException.ThrowIfNull(participanteRepository);
        ArgumentNullException.ThrowIfNull(jornadaRepository);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _usuarioTelegramRepository = usuarioTelegramRepository;
        _participanteRepository = participanteRepository;
        _jornadaRepository = jornadaRepository;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> VincularAsync(string appUserId, long telegramUserId, string? telegramUsername)
    {
        var existing = await _usuarioTelegramRepository.GetByUserIdAsync(appUserId);
        if (existing is not null)
        {
            existing.TelegramUserId = telegramUserId;
            existing.TelegramUsername = telegramUsername;
            existing.FechaVinculacion = DateTimeOffset.UtcNow;
            await _usuarioTelegramRepository.UpdateAsync(existing);
        }
        else
        {
            await _usuarioTelegramRepository.AddAsync(new UsuarioTelegram
            {
                UserId = appUserId,
                TelegramUserId = telegramUserId,
                TelegramUsername = telegramUsername,
                FechaVinculacion = DateTimeOffset.UtcNow
            });
        }

        await _unitOfWork.SaveAsync();

        _logger.LogInformation("Telegram account {TelegramUserId} linked to user {UserId}.", telegramUserId, appUserId);
        return true;
    }

    /// <inheritdoc />
    public Task<UsuarioTelegram?> GetByTelegramIdAsync(long telegramUserId)
        => _usuarioTelegramRepository.GetByTelegramUserIdAsync(telegramUserId);

    /// <inheritdoc />
    public async Task<string?> GetDisplayNameAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user?.NombreDisplay ?? user?.UserName;
    }

    /// <inheritdoc />
    public async Task<(Jornada jornada, Participante participante)?> GetJornadaAbiertaParaUsuarioAsync(string userId)
    {
        // Find ALL participations for this user across all torneos
        var participaciones = await _participanteRepository.GetByUserIdAsync(userId);

        if (participaciones.Count == 0)
            return null;

        // Query open jornada for each torneo and pick the most recent (highest Id)
        Jornada? bestJornada = null;
        Participante? bestParticipante = null;

        foreach (var participacion in participaciones)
        {
            var jornada = await _jornadaRepository.GetByTorneoAndEstadoAsync(participacion.TorneoId, EstadoJornada.Abierta);
            if (jornada is null)
                continue;

            if (bestJornada is null || jornada.Id > bestJornada.Id)
            {
                bestJornada = jornada;
                bestParticipante = participacion;
            }
        }

        if (bestJornada is null || bestParticipante is null)
            return null;

        // Load with partidos included
        var jornadaDetallada = await _jornadaRepository.GetByIdWithDetailsAsync(bestJornada.Id);
        if (jornadaDetallada is null)
            return null;

        return (jornadaDetallada, bestParticipante);
    }
}
