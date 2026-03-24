using BanterBotSports.BanterAI;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Integrations.ApiFootball;
using BanterBotSports.Integrations.Hosted;
using BanterBotSports.Integrations.Telegram;
using BanterBotSports.Web.Hubs;
using BanterBotSports.Web.Telegram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Import concrete implementations
using BanterBotSports.BL.Services;
using BanterBotSports.DAL.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Identity ────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ─── Repositories (Scoped) ───────────────────────────────────────────────────
builder.Services.AddScoped<ITorneoRepository, TorneoRepository>();
builder.Services.AddScoped<IJornadaRepository, JornadaRepository>();
builder.Services.AddScoped<IPartidoRepository, PartidoRepository>();
builder.Services.AddScoped<IParticipanteRepository, ParticipanteRepository>();
builder.Services.AddScoped<IPrediccionRepository, PrediccionRepository>();

// ─── BL Services (Scoped) ────────────────────────────────────────────────────
builder.Services.AddScoped<IPuntuacionService, PuntuacionService>();
builder.Services.AddScoped<IPremioService, PremioService>();
builder.Services.AddScoped<IPrediccionService, PrediccionService>();
builder.Services.AddScoped<IPartidoService, PartidoService>();
builder.Services.AddScoped<IJornadaService, JornadaService>();

// ─── Named HttpClients ───────────────────────────────────────────────────────
builder.Services.AddHttpClient("ApiFootball");
builder.Services.AddHttpClient("Whisper");
builder.Services.AddHttpClient("TelegramBot");

// ─── Integration Services ────────────────────────────────────────────────────
builder.Services.AddScoped<IApiFootballClient, ApiFootballClient>();
builder.Services.AddScoped<IWhisperService, WhisperService>();
builder.Services.AddSingleton<ITelegramBotService, TelegramBotService>();

// ─── BanterAI Services (Scoped) ──────────────────────────────────────────────
builder.Services.AddScoped<IPrediccionExtractionService, PrediccionExtractionService>();
builder.Services.AddScoped<IBanterEngine, BanterEngine>();
builder.Services.AddScoped<BanterDispatchService>();

// ─── Telegram Update Handler ─────────────────────────────────────────────────
builder.Services.AddScoped<ITelegramUpdateHandler, TelegramUpdateHandler>();

// ─── Background Services ─────────────────────────────────────────────────────
builder.Services.AddHostedService<DeadlineEnforcerService>();
builder.Services.AddHostedService<ResultSyncService>();

// ─── MVC + SignalR ───────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

// ─── Middleware pipeline ─────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

// ─── Routes ──────────────────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<TorneoHub>("/torneoHub");

// ─── Telegram webhook setup ──────────────────────────────────────────────────
var webhookUrl = builder.Configuration["Telegram:WebhookUrl"];
if (!string.IsNullOrWhiteSpace(webhookUrl))
{
    // Production: register webhook with Telegram on startup
    using var scope = app.Services.CreateScope();
    var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();
    await telegramService.SetWebhookAsync(webhookUrl);
    app.Logger.LogInformation("Telegram webhook registered: {Url}", webhookUrl);
}
else
{
    app.Logger.LogInformation("Telegram:WebhookUrl not set — running in long-polling (dev) mode.");
}

app.Run();
