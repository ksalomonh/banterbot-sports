using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;

namespace BanterBotSports.BL.Services;

/// <summary>
/// Business logic for chat messages.
/// Enforces deadline visibility rules: before a jornada deadline players only see
/// their own messages and BanterBot messages; after deadline (or when no jornada is
/// active) all messages are visible.
/// </summary>
public class ChatService : IChatService
{
    private const int MaxPlayerMessageLength = 500;
    private const int MaxBanterBotMessageLength = 280;
    private const string BanterBotName = "BanterBot";

    private readonly IChatRepository _chatRepository;
    private readonly IJornadaRepository _jornadaRepository;
    private readonly IParticipanteRepository _participanteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ChatService(
        IChatRepository chatRepository,
        IJornadaRepository jornadaRepository,
        IParticipanteRepository participanteRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(chatRepository);
        ArgumentNullException.ThrowIfNull(jornadaRepository);
        ArgumentNullException.ThrowIfNull(participanteRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _chatRepository = chatRepository;
        _jornadaRepository = jornadaRepository;
        _participanteRepository = participanteRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<MensajeChat> SaveMessageAsync(int torneoId, string userId, string contenido)
    {
        var participante = await _participanteRepository.GetByTorneoAndUserAsync(torneoId, userId);

        if (participante is null)
            throw new UnauthorizedAccessException($"User {userId} is not a participant of torneo {torneoId}.");

        if (contenido.Length > MaxPlayerMessageLength)
            contenido = contenido[..MaxPlayerMessageLength];

        // Resolve display name for the sending user
        var displayNames = await _participanteRepository.GetDisplayNamesByIdsAsync(new List<string> { userId });
        var displayName = displayNames.TryGetValue(userId, out var name) ? name : userId;

        var mensaje = new MensajeChat
        {
            TorneoId = torneoId,
            UserId = userId,
            Contenido = contenido,
            FechaUtc = DateTimeOffset.UtcNow,
            TipoMensaje = TipoMensajeChat.Normal,
            NombreDisplay = displayName
        };

        await _chatRepository.AddAsync(mensaje);
        await _unitOfWork.SaveAsync();
        return mensaje;
    }

    /// <inheritdoc />
    public async Task<MensajeChat> SaveBanterBotMessageAsync(int torneoId, string contenido, TipoMensajeChat tipo)
    {
        // Enforce max BanterBot message length — AI output must be validated before storing
        if (contenido.Length > MaxBanterBotMessageLength)
            contenido = contenido[..MaxBanterBotMessageLength];

        var mensaje = new MensajeChat
        {
            TorneoId = torneoId,
            UserId = null,
            Contenido = contenido,
            FechaUtc = DateTimeOffset.UtcNow,
            TipoMensaje = tipo,
            NombreDisplay = BanterBotName
        };

        await _chatRepository.AddAsync(mensaje);
        await _unitOfWork.SaveAsync();
        return mensaje;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MensajeChat>> GetHistoryAsync(
        int torneoId, string userId, int limit = 50, long? beforeId = null)
    {
        var allMessages = await _chatRepository.GetByTorneoAsync(torneoId, limit, beforeId);

        // Check if there is an active (open) jornada — that means deadline has not passed
        var jornadaAbierta = await _jornadaRepository.GetByTorneoAndEstadoAsync(torneoId, EstadoJornada.Abierta);

        if (jornadaAbierta is not null)
        {
            // Before deadline: show only calling player's messages + BanterBot messages
            return allMessages
                .Where(m => m.UserId == userId || m.UserId == null)
                .ToList();
        }

        // After deadline or no active jornada: show all messages
        return allMessages;
    }
}
