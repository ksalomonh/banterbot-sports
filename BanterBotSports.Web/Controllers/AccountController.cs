using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace BanterBotSports.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly ITelegramVinculacionService _telegramService;
    private readonly IConfiguration _configuration;

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ILogger<AccountController> logger,
        ITelegramVinculacionService telegramService,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(telegramService);
        ArgumentNullException.ThrowIfNull(configuration);

        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _telegramService = telegramService;
        _configuration = configuration;
    }

    // GET /Account/Login
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signInManager.IsSignedIn(User))
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST /Account/Login
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Telefono, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Telefono} logged in.", model.Telefono);
            return LocalRedirect(returnUrl ?? "/");
        }

        ModelState.AddModelError(string.Empty, "Teléfono o contraseña incorrectos.");
        return View(model);
    }

    // POST /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User signed out.");
        return RedirectToAction("Index", "Home");
    }

    // GET /Account/Register
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (_signInManager.IsSignedIn(User))
            return RedirectToAction("Index", "Home");

        return View();
    }

    // POST /Account/Register
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new AppUser
        {
            UserName = model.Telefono,
            Email = model.Email,
            PhoneNumber = model.Telefono,
            NombreDisplay = model.NombreDisplay
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            _logger.LogInformation("New user registered: {Telefono}", model.Telefono);
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    // GET /Account/ForgotPassword
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View();

    // POST /Account/ForgotPassword
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ForgotPassword(string email)
    {
        // Email delivery is out of scope for this cycle.
        // We show a confirmation regardless of whether the email exists (security best practice).
        TempData[TempDataKeys.Success] = "Si existe una cuenta con ese email, recibirás instrucciones en breve.";
        return RedirectToAction(nameof(ForgotPassword));
    }

    // GET /Account/AccessDenied
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // GET /Account/Profile
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return NotFound();

        var telegramLink = await _telegramService.GetByUserIdAsync(user.Id);
        var vm = BuildProfileViewModel(user, telegramLink);

        ViewData["EditModel"] = new ProfileEditViewModel { NombreDisplay = user.NombreDisplay ?? "" };

        return View(vm);
    }

    // POST /Account/Profile
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction(nameof(Login));

            var telegramLink = await _telegramService.GetByUserIdAsync(currentUser.Id);
            var profileModel = BuildProfileViewModel(currentUser, telegramLink);
            ViewData["EditModel"] = model;
            return View(profileModel);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        user.NombreDisplay = model.NombreDisplay;
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData[TempDataKeys.Success] = "Nombre de jugador actualizado correctamente";
        }
        else
        {
            TempData[TempDataKeys.Error] = "No se pudo actualizar el nombre. Intentá de nuevo.";
        }

        return RedirectToAction(nameof(Profile));
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private string BuildTelegramDeepLink(string userId)
    {
        var botUsername = _configuration["Telegram:BotUsername"];
        return $"https://t.me/{botUsername}?start={userId}";
    }

    private ProfileViewModel BuildProfileViewModel(AppUser user, UsuarioTelegram? telegramLink)
    {
        string? telegramUsername = null;
        if (telegramLink is not null)
        {
            // Scenario 1c fallback: if username is null, show numeric user ID
            telegramUsername = telegramLink.TelegramUsername ?? telegramLink.TelegramUserId.ToString();
        }

        return new ProfileViewModel
        {
            NombreDisplay   = user.NombreDisplay,
            Email           = user.Email,
            Telefono        = user.PhoneNumber,
            TelegramUsername = telegramUsername,
            TelegramDeepLink = BuildTelegramDeepLink(user.Id)
        };
    }
}
