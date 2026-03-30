using BanterBotSports.Integrations.ApiFootball;
using FluentAssertions;

namespace BanterBotSports.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="NullApiFootballClient"/> — verifies that all
/// methods return safe empty/null values and never throw.
/// </summary>
public class NullApiFootballClientTests
{
    private static readonly NullApiFootballClient Sut = new();

    [Fact]
    public async Task GetMatchesAsync_ReturnsEmptyList()
    {
        var result = await Sut.GetMatchesAsync(1, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow));

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLiveScoreAsync_ReturnsNull()
    {
        var result = await Sut.GetLiveScoreAsync(12345);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFixtureByIdAsync_ReturnsNull()
    {
        var result = await Sut.GetFixtureByIdAsync(12345);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AllMethods_DoNotThrow()
    {
        var act1 = () => Sut.GetMatchesAsync(1, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow));
        var act2 = () => Sut.GetLiveScoreAsync(1);
        var act3 = () => Sut.GetFixtureByIdAsync(1);

        await act1.Should().NotThrowAsync();
        await act2.Should().NotThrowAsync();
        await act3.Should().NotThrowAsync();
    }
}
