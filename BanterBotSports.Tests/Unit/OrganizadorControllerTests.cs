using System.Security.Claims;
using BanterBotSports.BL;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.DAL;
using BanterBotSports.Entities.ViewModels;
using BanterBotSports.Web.Controllers;
using BanterBotSports.Web.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BanterBotSports.Tests.Unit;

public class OrganizadorControllerTests
{
    private const string UserId = "organizador-1";

    private static Mock<UserManager<AppUser>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<AppUser>>();
        return new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static (OrganizadorController Controller, Mock<IOrganizadorService> OrganizadorSvc)
        BuildSut()
    {
        var organizadorSvc = new Mock<IOrganizadorService>();
        var userManager = BuildUserManagerMock();

        userManager
            .Setup(um => um.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(UserId);

        var controller = new OrganizadorController(
            organizadorSvc.Object,
            userManager.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<ITempDataProvider>());

        return (controller, organizadorSvc);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute_ForOrganizadorRole()
    {
        var attribute = typeof(OrganizadorController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull();
        attribute!.Roles.Should().Be(AppRoles.Organizador);
    }

    [Fact]
    public async Task GetConfiguracion_ReturnsViewWithModel()
    {
        var vm = new ConfiguracionOrganizadorViewModel
        {
            PorcentajeOrganizadorGlobal = 12m,
            PorcentajeMinimo = 5m,
            PorcentajeMaximo = 30m,
            PorcentajePlataforma = 10m
        };

        var (controller, organizadorSvc) = BuildSut();
        organizadorSvc.Setup(s => s.GetConfiguracionAsync(UserId)).ReturnsAsync(vm);

        var result = await controller.Configuracion();

        result.Should().BeOfType<ViewResult>();
        ((ViewResult)result).Model.Should().Be(vm);
    }

    [Fact]
    public async Task GetConfiguracion_WhenUserHasNoGlobal_ReturnsModelWithNullAndRangeContext()
    {
        var vm = new ConfiguracionOrganizadorViewModel
        {
            PorcentajeOrganizadorGlobal = null,
            PorcentajeMinimo = 5m,
            PorcentajeMaximo = 30m,
            PorcentajePlataforma = 10m
        };

        var (controller, organizadorSvc) = BuildSut();
        organizadorSvc.Setup(s => s.GetConfiguracionAsync(UserId)).ReturnsAsync(vm);

        var result = await controller.Configuracion();

        result.Should().BeOfType<ViewResult>();
        var model = ((ViewResult)result).Model.Should().BeOfType<ConfiguracionOrganizadorViewModel>().Subject;
        model.PorcentajeOrganizadorGlobal.Should().BeNull();
        model.PorcentajeMinimo.Should().Be(5m);
        model.PorcentajeMaximo.Should().Be(30m);
    }

    [Fact]
    public async Task MvcAuthorizationPipeline_WithJugadorRole_ForbidsOrganizadorControllerAccess()
    {
        // Arrange: runtime MVC authorization components (policy provider + evaluator)
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "jugador-1"),
                        new Claim(ClaimTypes.Role, AppRoles.Jugador)
                    },
                    authenticationType: "Test"))
        };

        var authorizeData = typeof(OrganizadorController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<IAuthorizeData>()
            .ToArray();

        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData);
        policy.Should().NotBeNull();

        var policyEvaluator = provider.GetRequiredService<IPolicyEvaluator>();

        // Act
        var authenticateResult = await policyEvaluator.AuthenticateAsync(policy!, httpContext);
        var authorizeResult = await policyEvaluator.AuthorizeAsync(policy!, authenticateResult, httpContext, resource: null);

        // Assert: authenticated Jugador is forbidden from Organizador-only policy
        authorizeResult.Succeeded.Should().BeFalse();
        authorizeResult.Forbidden.Should().BeTrue();
    }

    [Fact]
    public async Task PostConfiguracion_Valid_RedirectsWithSuccessTempData()
    {
        var (controller, organizadorSvc) = BuildSut();
        var model = new ConfiguracionOrganizadorViewModel
        {
            PorcentajeOrganizadorGlobal = 20m,
            PorcentajeMinimo = 5m,
            PorcentajeMaximo = 30m,
            PorcentajePlataforma = 10m
        };

        var result = await controller.Configuracion(model);

        organizadorSvc.Verify(s => s.UpdateConfiguracionAsync(UserId, 20m), Times.Once);
        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be(nameof(OrganizadorController.Configuracion));
        controller.TempData[TempDataKeys.Success].Should().NotBeNull();
    }

    [Fact]
    public async Task PostConfiguracion_OutOfRange_ReturnsViewWithFieldError()
    {
        var (controller, organizadorSvc) = BuildSut();
        var model = new ConfiguracionOrganizadorViewModel
        {
            PorcentajeOrganizadorGlobal = 35m
        };

        organizadorSvc
            .Setup(s => s.UpdateConfiguracionAsync(UserId, 35m))
            .ThrowsAsync(new ArgumentException("El porcentaje no puede superar el máximo permitido (30%)."));

        organizadorSvc
            .Setup(s => s.GetConfiguracionAsync(UserId))
            .ReturnsAsync(new ConfiguracionOrganizadorViewModel
            {
                PorcentajeMinimo = 5m,
                PorcentajeMaximo = 30m,
                PorcentajePlataforma = 10m
            });

        var result = await controller.Configuracion(model);

        result.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState.ContainsKey(nameof(ConfiguracionOrganizadorViewModel.PorcentajeOrganizadorGlobal)).Should().BeTrue();
        model.PorcentajeMinimo.Should().Be(5m);
        model.PorcentajeMaximo.Should().Be(30m);
        model.PorcentajePlataforma.Should().Be(10m);
    }

    [Fact]
    public async Task PostConfiguracion_BelowMin_ReturnsViewWithFieldErrorAndExpectedMessage()
    {
        var (controller, organizadorSvc) = BuildSut();
        var model = new ConfiguracionOrganizadorViewModel
        {
            PorcentajeOrganizadorGlobal = 3m
        };

        const string expectedMessage = "El porcentaje debe ser al menos el mínimo permitido (5%).";

        organizadorSvc
            .Setup(s => s.UpdateConfiguracionAsync(UserId, 3m))
            .ThrowsAsync(new ArgumentException(expectedMessage));

        organizadorSvc
            .Setup(s => s.GetConfiguracionAsync(UserId))
            .ReturnsAsync(new ConfiguracionOrganizadorViewModel
            {
                PorcentajeMinimo = 5m,
                PorcentajeMaximo = 30m,
                PorcentajePlataforma = 10m
            });

        var result = await controller.Configuracion(model);

        result.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();

        var fieldKey = nameof(ConfiguracionOrganizadorViewModel.PorcentajeOrganizadorGlobal);
        controller.ModelState.ContainsKey(fieldKey).Should().BeTrue();
        controller.ModelState[fieldKey]!.Errors.Should().ContainSingle();
        controller.ModelState[fieldKey]!.Errors[0].ErrorMessage.Should().Be(expectedMessage);

        model.PorcentajeMinimo.Should().Be(5m);
        model.PorcentajeMaximo.Should().Be(30m);
        model.PorcentajePlataforma.Should().Be(10m);
    }

    [Fact]
    public async Task PostConfiguracion_ModelStateInvalid_ReturnsViewAndRepopulatesReadOnlyFields()
    {
        var (controller, organizadorSvc) = BuildSut();
        var model = new ConfiguracionOrganizadorViewModel
        {
            PorcentajeOrganizadorGlobal = 4m
        };

        controller.ModelState.AddModelError("PorcentajeOrganizadorGlobal", "Valor inválido");

        organizadorSvc
            .Setup(s => s.GetConfiguracionAsync(UserId))
            .ReturnsAsync(new ConfiguracionOrganizadorViewModel
            {
                PorcentajeMinimo = 5m,
                PorcentajeMaximo = 30m,
                PorcentajePlataforma = 10m
            });

        var result = await controller.Configuracion(model);

        result.Should().BeOfType<ViewResult>();
        organizadorSvc.Verify(s => s.UpdateConfiguracionAsync(It.IsAny<string>(), It.IsAny<decimal>()), Times.Never);
        model.PorcentajeMinimo.Should().Be(5m);
        model.PorcentajeMaximo.Should().Be(30m);
        model.PorcentajePlataforma.Should().Be(10m);
    }

}
