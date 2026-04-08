using BanterBotSports.BL.Models;
using BanterBotSports.BL.Services.Interfaces;
using BanterBotSports.Entities;
using BanterBotSports.Web.Controllers;
using BanterBotSports.Web.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for AdminController.
/// Follows the BuildSut + TempDataDictionary pattern from TorneoControllerClonarTests.
/// </summary>
public class AdminControllerTests
{
    // ─── Factory ─────────────────────────────────────────────────────────────

    private static (AdminController Controller, Mock<IAdminService> AdminSvc) BuildSut()
    {
        var adminSvc = new Mock<IAdminService>();
        var controller = new AdminController(adminSvc.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<ITempDataProvider>());

        return (controller, adminSvc);
    }

    private static AdminUserDto BuildDto(string id = "user1", bool isActive = true)
        => new(id, "5551234567", "user@email.com", "Test User", isActive, 2, 3, null);

    // ─── Index ───────────────────────────────────────────────────────────────

    [Fact]
    public void GET_Index_ReturnsView()
    {
        var (controller, _) = BuildSut();
        var result = controller.Index();
        result.Should().BeOfType<ViewResult>();
    }

    // ─── Organizadores ───────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Organizadores_ReturnsViewWithList()
    {
        var (controller, adminSvc) = BuildSut();
        var list = new List<AdminUserDto> { BuildDto() };
        adminSvc.Setup(s => s.GetOrganizadoresAsync()).ReturnsAsync(list);

        var result = await controller.Organizadores();

        result.Should().BeOfType<ViewResult>();
        var view = (ViewResult)result;
        view.Model.Should().Be(list);
    }

    [Fact]
    public async Task GET_EditOrganizador_NotFound_ReturnsNotFound()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.GetOrganizadorAsync("missing")).ReturnsAsync((AdminUserDto?)null);

        var result = await controller.EditOrganizador("missing");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GET_EditOrganizador_Found_ReturnsViewWithDto()
    {
        var (controller, adminSvc) = BuildSut();
        var dto = BuildDto();
        adminSvc.Setup(s => s.GetOrganizadorAsync("user1")).ReturnsAsync(dto);

        var result = await controller.EditOrganizador("user1");

        result.Should().BeOfType<ViewResult>();
        ((ViewResult)result).Model.Should().Be(dto);
    }

    [Fact]
    public async Task POST_EditOrganizador_Valid_RedirectsWithSuccess()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.UpdateOrganizadorAsync("user1", It.IsAny<AdminUserEditDto>()))
            .Returns(Task.CompletedTask);

        var result = await controller.EditOrganizadorPost("user1", new AdminUserEditDto("NewName", "new@email.com"));

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be("Organizadores");
        controller.TempData[TempDataKeys.Success].Should().NotBeNull();
    }

    [Fact]
    public async Task POST_DeactivateOrganizador_Success_RedirectsWithSuccess()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.DeactivateOrganizadorAsync("user1")).Returns(Task.CompletedTask);

        var result = await controller.DeactivateOrganizador("user1");

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be("Organizadores");
        controller.TempData[TempDataKeys.Success].Should().NotBeNull();
    }

    [Fact]
    public async Task POST_DeactivateOrganizador_InvalidOperation_SetsErrorTempData_AndRedirects()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.DeactivateOrganizadorAsync("user1"))
            .ThrowsAsync(new InvalidOperationException("Tiene torneos activos."));

        var result = await controller.DeactivateOrganizador("user1");

        result.Should().BeOfType<RedirectToActionResult>();
        controller.TempData[TempDataKeys.Error].Should().NotBeNull();
    }

    [Fact]
    public async Task POST_ReactivateOrganizador_Success_RedirectsWithSuccess()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.ReactivateUserAsync("user1")).Returns(Task.CompletedTask);

        var result = await controller.ReactivateOrganizador("user1");

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be("Organizadores");
        controller.TempData[TempDataKeys.Success].Should().NotBeNull();
    }

    // ─── Jugadores ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Jugadores_ReturnsViewWithList()
    {
        var (controller, adminSvc) = BuildSut();
        var list = new List<AdminUserDto> { BuildDto() };
        adminSvc.Setup(s => s.GetJugadoresAsync(null)).ReturnsAsync(list);

        var result = await controller.Jugadores(null);

        result.Should().BeOfType<ViewResult>();
        ((ViewResult)result).Model.Should().Be(list);
    }

    [Fact]
    public async Task GET_Jugadores_WithSearch_PassesSearchToService()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.GetJugadoresAsync("carlos")).ReturnsAsync(new List<AdminUserDto>());

        var result = await controller.Jugadores("carlos");

        adminSvc.Verify(s => s.GetJugadoresAsync("carlos"), Times.Once);
    }

    [Fact]
    public async Task GET_EditJugador_NotFound_ReturnsNotFound()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.GetJugadorAsync("missing")).ReturnsAsync((AdminUserDto?)null);

        var result = await controller.EditJugador("missing");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GET_EditJugador_Found_ReturnsViewWithDto()
    {
        var (controller, adminSvc) = BuildSut();
        var dto = BuildDto();
        adminSvc.Setup(s => s.GetJugadorAsync("user1")).ReturnsAsync(dto);

        var result = await controller.EditJugador("user1");

        result.Should().BeOfType<ViewResult>();
        ((ViewResult)result).Model.Should().Be(dto);
    }

    [Fact]
    public async Task POST_EditJugador_Valid_RedirectsWithSuccess()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.UpdateJugadorAsync("user1", It.IsAny<AdminUserEditDto>()))
            .Returns(Task.CompletedTask);

        var result = await controller.EditJugadorPost("user1", new AdminUserEditDto("NewName", "new@email.com"));

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be("Jugadores");
        controller.TempData[TempDataKeys.Success].Should().NotBeNull();
    }

    [Fact]
    public async Task POST_DeactivateJugador_Success_RedirectsWithSuccess()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.DeactivateJugadorAsync("user1")).Returns(Task.CompletedTask);

        var result = await controller.DeactivateJugador("user1");

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be("Jugadores");
        controller.TempData[TempDataKeys.Success].Should().NotBeNull();
    }

    [Fact]
    public async Task POST_ReactivateJugador_Success_RedirectsWithSuccess()
    {
        var (controller, adminSvc) = BuildSut();
        adminSvc.Setup(s => s.ReactivateUserAsync("user1")).Returns(Task.CompletedTask);

        var result = await controller.ReactivateJugador("user1");

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be("Jugadores");
        controller.TempData[TempDataKeys.Success].Should().NotBeNull();
    }

    // ─── Configuracion ───────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Configuracion_ReturnsViewWithModel()
    {
        var (controller, adminSvc) = BuildSut();
        var config = new ConfiguracionGlobal { Id = 1, PorcentajePlataforma = 10 };
        adminSvc.Setup(s => s.GetConfiguracionAsync()).ReturnsAsync(config);

        var result = await controller.Configuracion();

        result.Should().BeOfType<ViewResult>();
        ((ViewResult)result).Model.Should().Be(config);
    }

    [Fact]
    public async Task POST_Configuracion_Valid_RedirectsWithSuccess()
    {
        var (controller, adminSvc) = BuildSut();
        var config = new ConfiguracionGlobal { Id = 1, PorcentajePlataforma = 10, PorcentajeOrganizadorMin = 5, PorcentajeOrganizadorMax = 30, MontoInscripcionMinimo = 500 };
        adminSvc.Setup(s => s.UpdateConfiguracionAsync(config)).Returns(Task.CompletedTask);

        var result = await controller.ConfiguracionPost(config);

        result.Should().BeOfType<RedirectToActionResult>();
        ((RedirectToActionResult)result).ActionName.Should().Be("Configuracion");
        controller.TempData[TempDataKeys.Success].Should().NotBeNull();
    }

    [Fact]
    public async Task POST_Configuracion_ArgumentException_AddsModelError_AndReturnsView()
    {
        var (controller, adminSvc) = BuildSut();
        var config = new ConfiguracionGlobal { Id = 1, PorcentajePlataforma = 60 };
        adminSvc.Setup(s => s.UpdateConfiguracionAsync(config))
            .ThrowsAsync(new ArgumentException("Porcentaje inválido."));

        var result = await controller.ConfiguracionPost(config);

        result.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
    }
}
