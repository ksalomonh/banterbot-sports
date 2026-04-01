using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BanterBotSports.Web.Controllers;

/// <summary>
/// Handles chat-related HTTP requests.
/// GET /Chat/History?torneoId=X&beforeId=Y — returns paginated message history.
/// GET /Chat/Index?torneoId=X — renders the chat UI view.
/// </summary>
[Authorize]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly UserManager<AppUser> _userManager;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        UserManager<AppUser> userManager,
        ILogger<ChatController> logger)
    {
        ArgumentNullException.ThrowIfNull(chatService);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(logger);

        _chatService = chatService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Renders the chat UI for the given torneo.
    /// </summary>
    public IActionResult Index(int torneoId)
    {
        ViewBag.TorneoId = torneoId;
        return View();
    }

    /// <summary>
    /// Returns paginated message history as JSON.
    /// Deadline visibility is enforced by IChatService.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> History(int torneoId, long? beforeId = null, int limit = 50)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            var messages = await _chatService.GetHistoryAsync(torneoId, userId, limit, beforeId);

            var result = messages.Select(m => new
            {
                m.Id,
                m.TorneoId,
                m.UserId,
                m.NombreDisplay,
                m.Contenido,
                FechaUtc = m.FechaUtc.ToString("o"),
                TipoMensaje = m.TipoMensaje.ToString()
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching chat history for torneo {TorneoId}.", torneoId);
            return StatusCode(500, "Error loading chat history.");
        }
    }
}
