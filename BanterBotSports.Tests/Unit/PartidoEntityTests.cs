using BanterBotSports.Entities;
using FluentAssertions;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="Partido"/> entity — logo URL fields (REQ-6).
/// Verifies that <see cref="Partido.LogoUrlLocal"/> and
/// <see cref="Partido.LogoUrlVisitante"/> are exposed as nullable strings.
/// </summary>
public class PartidoEntityTests
{
    [Fact]
    public void LogoUrlLocal_DefaultsToNull()
    {
        var partido = new Partido();

        partido.LogoUrlLocal.Should().BeNull();
    }

    [Fact]
    public void LogoUrlVisitante_DefaultsToNull()
    {
        var partido = new Partido();

        partido.LogoUrlVisitante.Should().BeNull();
    }

    [Fact]
    public void LogoUrlLocal_AcceptsNonNullValue()
    {
        const string url = "https://cdn.example.com/logos/river.png";
        var partido = new Partido { LogoUrlLocal = url };

        partido.LogoUrlLocal.Should().Be(url);
    }

    [Fact]
    public void LogoUrlVisitante_AcceptsNonNullValue()
    {
        const string url = "https://cdn.example.com/logos/boca.png";
        var partido = new Partido { LogoUrlVisitante = url };

        partido.LogoUrlVisitante.Should().Be(url);
    }

    [Fact]
    public void LogoUrlLocal_AcceptsExplicitNull()
    {
        var partido = new Partido { LogoUrlLocal = "https://cdn.example.com/logos/river.png" };
        partido.LogoUrlLocal = null;

        partido.LogoUrlLocal.Should().BeNull();
    }

    [Fact]
    public void LogoUrlVisitante_AcceptsExplicitNull()
    {
        var partido = new Partido { LogoUrlVisitante = "https://cdn.example.com/logos/boca.png" };
        partido.LogoUrlVisitante = null;

        partido.LogoUrlVisitante.Should().BeNull();
    }
}
