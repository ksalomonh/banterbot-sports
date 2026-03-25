using BanterBotSports.BanterAI;
using BanterBotSports.Entities.DTOs;
using BanterBotSports.Entities.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for PrediccionExtractionService.
///
/// The service wraps AnthropicClient directly (not injectable).  Tests verify
/// observable contract behaviours:
///
///   - When the Anthropic API call raises any exception (invalid key, network, etc.)
///     the service catches it and returns CannotParse (Success=false, empty list).
///
///   - The IPrediccionExtractionService interface contract is exercised through
///     a real instance configured with a fake API key.
///
/// ParseResponse branch coverage (happy path, low confidence, malformed JSON)
/// is achieved via integration tests where we control the HTTP response,
/// or via the ParseResponseTests helper class below that uses reflection to
/// invoke the private method directly for pure unit-test coverage.
/// </summary>
public class PrediccionExtractionServiceTests
{
    private static IPrediccionExtractionService BuildSut(string? apiKey = "fake-key-unit-test")
    {
        var dict = new Dictionary<string, string?> { ["Anthropic:ApiKey"] = apiKey };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();

        return new PrediccionExtractionService(config, NullLogger<PrediccionExtractionService>.Instance);
    }

    private static IReadOnlyList<PartidoDto> BuildPartidos(params (int id, string eq1, string eq2)[] partidos)
        => partidos
            .Select(p => new PartidoDto(
                p.id, null, p.eq1, p.eq2,
                DateTimeOffset.UtcNow.AddDays(1),
                null, null,
                EstadoPartido.Programado))
            .ToList();

    // ---------------------------------------------------------------------------
    // 1. Contract: invalid/unusable API key → network/auth exception caught →
    //    service returns CannotParse instead of bubbling the exception.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_AnthropicApiUnavailable_ReturnsCannotParse()
    {
        // Arrange: real service with fake key — the Anthropic call will fail
        var sut = BuildSut("fake-key-that-will-fail");
        var partidos = BuildPartidos((1, "River", "Boca"));

        // Act — must not throw; service must swallow and return CannotParse
        var result = await sut.ExtractAsync("3-1 para el local", partidos);

        // Assert
        result.Success.Should().BeFalse("any exception during the API call returns CannotParse");
        result.Predicciones.Should().BeEmpty();
        result.Confidence.Should().Be(0.0);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractAsync_MissingApiKey_ThrowsAtConstruction()
    {
        // Arrange: no Anthropic:ApiKey in config
        var dict = new Dictionary<string, string?>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        // Act & Assert: constructor must throw because ApiKey is required
        var act = () => new PrediccionExtractionService(config, NullLogger<PrediccionExtractionService>.Instance);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Anthropic:ApiKey*");
    }

    // ---------------------------------------------------------------------------
    // 2. ParseResponse branch coverage via reflection.
    //    We bypass the HTTP layer and inject synthetic Claude response text
    //    directly into the private ParseResponse method.
    // ---------------------------------------------------------------------------

    private static BanterBotSports.Entities.DTOs.ExtractionResult InvokeParseResponse(
        IPrediccionExtractionService sut,
        string responseText,
        IReadOnlyList<PartidoDto> partidos)
    {
        // Use reflection to call the private ParseResponse method
        var method = sut.GetType().GetMethod(
            "ParseResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull("PrediccionExtractionService must have a private ParseResponse method");

        var result = method!.Invoke(sut, new object[] { responseText, partidos });
        return (BanterBotSports.Entities.DTOs.ExtractionResult)result!;
    }

    [Fact]
    public void ParseResponse_ValidJson_HighConfidence_ReturnsSuccess()
    {
        // Arrange
        const string claudeJson = """
            {
              "predictions": [
                { "matchId": 10, "localGoals": 3, "visitanteGoals": 1 }
              ],
              "confidence": 0.95
            }
            """;
        var sut = BuildSut();
        var partidos = BuildPartidos((10, "River", "Boca"));

        // Act
        var result = InvokeParseResponse(sut, claudeJson, partidos);

        // Assert
        result.Success.Should().BeTrue();
        result.Predicciones.Should().HaveCount(1);
        result.Predicciones[0].PartidoId.Should().Be(10);
        result.Predicciones[0].GolesEquipo1.Should().Be(3, "localGoals maps to GolesEquipo1");
        result.Predicciones[0].GolesEquipo2.Should().Be(1, "visitanteGoals maps to GolesEquipo2");
        result.Confidence.Should().BeApproximately(0.95, 0.001);
    }

    [Fact]
    public void ParseResponse_LowConfidence_AmbiguousTranscription_ReturnsCannotParse()
    {
        // Simulate ambiguous voice transcription → Claude returns low confidence
        const string claudeJson = """
            {
              "predictions": [
                { "matchId": 10, "localGoals": 2, "visitanteGoals": 0 }
              ],
              "confidence": 0.3
            }
            """;
        var sut = BuildSut();
        var partidos = BuildPartidos((10, "River", "Boca"));

        var result = InvokeParseResponse(sut, claudeJson, partidos);

        result.Success.Should().BeFalse("confidence 0.3 is below the 0.7 threshold");
        result.Predicciones.Should().BeEmpty();
        result.Confidence.Should().Be(0.0, "CannotParse constant uses 0.0");
    }

    [Fact]
    public void ParseResponse_MalformedJson_ReturnsCannotParse()
    {
        const string malformedResponse = "I cannot extract any predictions from this text.";
        var sut = BuildSut();
        var partidos = BuildPartidos((10, "River", "Boca"));

        var result = InvokeParseResponse(sut, malformedResponse, partidos);

        result.Success.Should().BeFalse("no JSON braces present → CannotParse");
        result.Predicciones.Should().BeEmpty();
    }

    [Fact]
    public void ParseResponse_ValidJsonWithMatchIdNotInList_FiltersOut()
    {
        const string claudeJson = """
            {
              "predictions": [
                { "matchId": 99, "localGoals": 2, "visitanteGoals": 1 }
              ],
              "confidence": 0.96
            }
            """;
        var sut = BuildSut();
        var partidos = BuildPartidos((10, "River", "Boca")); // ID 99 not provided

        var result = InvokeParseResponse(sut, claudeJson, partidos);

        result.Success.Should().BeTrue("JSON is valid and confidence is high");
        result.Predicciones.Should().BeEmpty("matchId 99 is not in the provided partidos list");
    }

    [Fact]
    public void ParseResponse_JsonWithExtraTextAround_ExtractsJsonBlock()
    {
        // Claude sometimes wraps JSON in prose
        const string responseWithProse = """
            Here are the extracted predictions:
            {
              "predictions": [
                { "matchId": 5, "localGoals": 0, "visitanteGoals": 2 }
              ],
              "confidence": 0.96
            }
            Hope this helps!
            """;
        var sut = BuildSut();
        var partidos = BuildPartidos((5, "Atletico", "Barcelona"));

        var result = InvokeParseResponse(sut, responseWithProse, partidos);

        result.Success.Should().BeTrue();
        result.Predicciones.Should().HaveCount(1);
        result.Predicciones[0].GolesEquipo2.Should().Be(2);
    }
}
