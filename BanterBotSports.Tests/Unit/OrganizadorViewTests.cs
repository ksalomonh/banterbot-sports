using BanterBotSports.Entities.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;

namespace BanterBotSports.Tests.Unit;

public class OrganizadorViewTests
{
    [Fact]
    public async Task ConfiguracionView_RendersFallbackPlaceholderAndRangeLabels_WhenGlobalIsNull()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var webRoot = Path.Combine(root, "BanterBotSports.Web");

        var html = await RenderConfiguracionAsync(
            webRoot,
            new ConfiguracionOrganizadorViewModel
            {
                PorcentajeOrganizadorGlobal = null,
                PorcentajeMinimo = 5m,
                PorcentajeMaximo = 30m,
                PorcentajePlataforma = 10m
            });

        html.Should().Contain("placeholder=\"5\"");
        html.Should().Contain("Mínimo organizador");
        html.Should().Contain("Máximo organizador");
        html.Should().Contain("Rango permitido: 5% a 30%");
    }

    [Fact]
    public async Task ConfiguracionView_RendersGlobalPlaceholder_WhenGlobalIsConfigured()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var webRoot = Path.Combine(root, "BanterBotSports.Web");

        var html = await RenderConfiguracionAsync(
            webRoot,
            new ConfiguracionOrganizadorViewModel
            {
                PorcentajeOrganizadorGlobal = 12.5m,
                PorcentajeMinimo = 5m,
                PorcentajeMaximo = 30m,
                PorcentajePlataforma = 10m
            });

        html.Should().Contain("placeholder=\"12.5\"");
    }

    private static async Task<string> RenderConfiguracionAsync(string webRootPath, ConfiguracionOrganizadorViewModel model)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("BanterBotSports.Tests.OrganizadorView"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment(webRootPath));
        services.AddControllersWithViews();

        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var scopedProvider = scope.ServiceProvider;
        var httpContext = new DefaultHttpContext { RequestServices = scopedProvider };
        var routeData = new RouteData();
        routeData.Routers.Add(new RouteCollection());
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

        var viewEngine = scopedProvider.GetRequiredService<IRazorViewEngine>();
        var tempDataProvider = scopedProvider.GetRequiredService<ITempDataProvider>();

        var viewResult = viewEngine.GetView(executingFilePath: null, viewPath: "/Views/Organizador/Configuracion.cshtml", isMainPage: true);
        viewResult.Success.Should().BeTrue("la vista de configuración del organizador debe resolverse por el motor Razor");

        var viewData = new ViewDataDictionary<ConfiguracionOrganizadorViewModel>(
            metadataProvider: new EmptyModelMetadataProvider(),
            modelState: new ModelStateDictionary())
        {
            Model = model
        };

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
