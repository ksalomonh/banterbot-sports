using System.Diagnostics;
using System.Security.Claims;
using BanterBotSports.BL;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace BanterBotSports.Tests.Unit;

public class TorneoDashboardViewTests
{
    [Fact]
    public async Task DashboardView_ShowsOrganizerConfigLink_WhenUserIsOrganizador()
    {
        var html = await RenderDashboardAsync(
            BuildPrincipal(AppRoles.Organizador),
            esOrganizador: false);

        html.Should().Contain("Panel Organizador");
    }

    [Fact]
    public async Task DashboardView_HidesOrganizerConfigLink_WhenUserIsNotOrganizador()
    {
        var html = await RenderDashboardAsync(
            BuildPrincipal(AppRoles.Jugador),
            esOrganizador: false);

        html.Should().NotContain("Panel Organizador");
    }

    private static ClaimsPrincipal BuildPrincipal(string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, role)
        ],
        authenticationType: "Test"));

    private static async Task<string> RenderDashboardAsync(ClaimsPrincipal user, bool esOrganizador)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var webRoot = Path.Combine(root, "BanterBotSports.Web");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("BanterBotSports.Tests.TorneoDashboardView"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(webRoot));
        services.AddControllersWithViews();

        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var scopedProvider = scope.ServiceProvider;
        var httpContext = new DefaultHttpContext { RequestServices = scopedProvider, User = user };
        var routeData = new RouteData();
        routeData.Routers.Add(new RouteCollection());
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

        var viewEngine = scopedProvider.GetRequiredService<IRazorViewEngine>();
        var tempDataProvider = scopedProvider.GetRequiredService<ITempDataProvider>();

        var viewResult = viewEngine.GetView(executingFilePath: null, viewPath: "/Views/Torneo/Dashboard.cshtml", isMainPage: true);
        viewResult.Success.Should().BeTrue("la vista de dashboard debe resolverse por Razor");

        var model = new Torneo
        {
            Id = 1,
            Nombre = "Demo",
            Estado = EstadoTorneo.Pendiente,
            MontoInscripcion = 100m,
            OrganizadorId = "org-1",
            NumJornadas = 1,
            PtosResultado = 3,
            PtosMarcador = 1,
            PtosGolesJornada = 2,
            PorcentajeOrganizador = 5m,
            Participantes = []
        };

        var viewData = new ViewDataDictionary<Torneo>(
            metadataProvider: new EmptyModelMetadataProvider(),
            modelState: new ModelStateDictionary())
        {
            Model = model
        };

        viewData["EsOrganizador"] = esOrganizador;
        viewData["Ranking"] = Array.Empty<object>();
        viewData["Jornadas"] = Array.Empty<object>();

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View!,
            viewData,
            new TempDataDictionary(httpContext, tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private sealed class TestWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "BanterBotSports.Web";
        public string WebRootPath { get; set; } = Path.Combine(webRootPath, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(Path.Combine(webRootPath, "wwwroot"));
        public string ContentRootPath { get; set; } = webRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(webRootPath);
    }
}
