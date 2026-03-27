using BanterBotSports.BanterAI;
using BanterBotSports.BL.Services;
using BanterBotSports.BL.Services.Hosted;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.DAL.Repositories;
using BanterBotSports.DAL.Repositories.Interfaces;
using BanterBotSports.Integrations.ApiFootball;
using BanterBotSports.Integrations.Hosted;
using BanterBotSports.Integrations.Telegram;
using BanterBotSports.Web.Hubs;
using BanterBotSports.Web.Telegram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

// ─── Data Protection (signed/encrypted tokens for invites) ──────────────────
builder.Services.AddDataProtection();

// ─── Unit of Work (Scoped) ───────────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ─── Repositories (Scoped) ───────────────────────────────────────────────────
builder.Services.AddScoped<ITorneoRepository, TorneoRepository>();
builder.Services.AddScoped<IJornadaRepository, JornadaRepository>();
builder.Services.AddScoped<IPartidoRepository, PartidoRepository>();
builder.Services.AddScoped<IParticipanteRepository, ParticipanteRepository>();
builder.Services.AddScoped<IPrediccionRepository, PrediccionRepository>();
builder.Services.AddScoped<IUsuarioTelegramRepository, UsuarioTelegramRepository>();

// ─── BL Services (Scoped) ────────────────────────────────────────────────────
builder.Services.AddScoped<ITorneoService, TorneoService>();
builder.Services.AddScoped<IPuntuacionService, PuntuacionService>();
builder.Services.AddScoped<IPremioService, PremioService>();
builder.Services.AddScoped<IPrediccionService, PrediccionService>();
builder.Services.AddScoped<IPartidoService, PartidoService>();
builder.Services.AddScoped<IJornadaService, JornadaService>();
builder.Services.AddScoped<ITelegramVinculacionService, TelegramVinculacionService>();

// ─── Named HttpClients ───────────────────────────────────────────────────────
builder.Services.AddHttpClient("ApiFootball");
builder.Services.AddHttpClient("Anthropic");
builder.Services.AddHttpClient("Whisper");
builder.Services.AddHttpClient("TelegramBot");

// ─── Integration Services ────────────────────────────────────────────────────
builder.Services.AddScoped<IApiFootballClient, ApiFootballClient>();
builder.Services.AddScoped<IWhisperService, WhisperService>();
builder.Services.AddSingleton<ITelegramBotService, TelegramBotService>();
builder.Services.AddSingleton<JornadaAbiertaNotifier>();

// ─── BanterAI Services (Scoped) ──────────────────────────────────────────────
builder.Services.AddScoped<IPrediccionExtractionService, PrediccionExtractionService>();
builder.Services.AddScoped<IBanterEngine, BanterEngine>();
builder.Services.AddScoped<IBanterDispatchService, BanterDispatchService>();

// ─── Telegram Update Handler + Background Queue ──────────────────────────────
builder.Services.AddScoped<ITelegramUpdateHandler, TelegramUpdateHandler>();
builder.Services.AddSingleton<TelegramUpdateQueue>();

// ─── Background Services ─────────────────────────────────────────────────────
builder.Services.AddHostedService<TelegramUpdateWorker>();
builder.Services.AddHostedService<DeadlineEnforcerService>();
builder.Services.AddHostedService<ResultSyncService>();

// ─── MVC + SignalR ───────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

// ─── Auto-migrate on startup (dev) ───────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ─── Middleware pipeline ─────────────────────────────────────────────────────
// UseExceptionHandler is always active so DB/internal errors never expose stack
// traces to the browser — not even in Development when run outside a debugger.
app.UseExceptionHandler("/Home/Error");

if (!app.Environment.IsDevelopment())
{
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
