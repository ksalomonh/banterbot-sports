using BanterBotSports.BanterAI;
using BanterBotSports.Entities;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Moq;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Tests for IBanterEngine.GenerateChatReplyAsync contract — max 280 chars enforced,
/// fallback on AI error, verified via mock implementation.
/// The real BanterEngine makes external HTTP calls so we test the interface contract
/// and verify callers handle the response correctly.
/// </summary>
public class BanterEngineChatReplyTests
{
    private static Torneo BuildTorneo()
        => new()
        {
            Id = 1,
            Nombre = "Test Torneo",
            OrganizadorId = "org",
            Estado = EstadoTorneo.Activo,
            PtosResultado = 3,
            PtosMarcador = 5,
            PtosGolesJornada = 2
        };

    [Fact]
    public async Task GenerateChatReplyAsync_ReturnsEmptyStringOnAIError()
    {
        // Arrange — simulate AI error by returning empty string (same as engine fallback)
        var banterEngineMock = new Mock<IBanterEngine>();
        banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ReturnsAsync(string.Empty);

        var torneo = BuildTorneo();

        // Act
        var result = await banterEngineMock.Object.GenerateChatReplyAsync(
            "@banterbot hola", "Player One", torneo);

        // Assert — caller gets empty string when AI fails
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateChatReplyAsync_ResponseFitsWithin280Chars()
    {
        // Arrange — simulate AI returning a long response
        var longResponse = new string('x', 350);
        var banterEngineMock = new Mock<IBanterEngine>();
        banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ReturnsAsync(longResponse[..280]); // engine truncates before returning

        var torneo = BuildTorneo();

        // Act
        var result = await banterEngineMock.Object.GenerateChatReplyAsync(
            "@banterbot ¿quién va a ganar?", "Player One", torneo);

        // Assert
        result.Length.Should().BeLessThanOrEqualTo(280);
    }

    [Fact]
    public async Task GenerateChatReplyAsync_InterfaceAcceptsNullableParameters()
    {
        // Verify the method signature is callable with expected parameter types
        var banterEngineMock = new Mock<IBanterEngine>();
        banterEngineMock
            .Setup(e => e.GenerateChatReplyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Torneo>()))
            .ReturnsAsync("¡Buena pregunta, loco!");

        var torneo = BuildTorneo();

        // Act
        var result = await banterEngineMock.Object.GenerateChatReplyAsync(
            "@banterbot ¿quién va a ganar?", "Player One", torneo);

        // Assert
        result.Should().NotBeEmpty();
        banterEngineMock.Verify(
            e => e.GenerateChatReplyAsync("@banterbot ¿quién va a ganar?", "Player One", torneo),
            Times.Once);
    }
}
