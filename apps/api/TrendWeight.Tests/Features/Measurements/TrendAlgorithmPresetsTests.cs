using FluentAssertions;
using TrendWeight.Features.Measurements;
using Xunit;

namespace TrendWeight.Tests.Features.Measurements;

public class TrendAlgorithmPresetsTests
{
    [Fact]
    public void Resolve_WithNull_ReturnsDefaultPreset()
    {
        var preset = TrendAlgorithmPresets.Resolve(null);

        preset.Id.Should().Be(TrendAlgorithmPresets.DefaultId);
        preset.Alpha.Should().Be(0.1m);
        preset.Beta.Should().Be(0m);
    }

    [Fact]
    public void Resolve_WithUnknownId_FallsBackToDefault()
    {
        var preset = TrendAlgorithmPresets.Resolve("some-future-algorithm");

        preset.Id.Should().Be(TrendAlgorithmPresets.DefaultId);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("holt-gentle")]
    [InlineData("holt")]
    [InlineData("holt-responsive")]
    public void Resolve_WithKnownId_ReturnsThatPreset(string id)
    {
        var preset = TrendAlgorithmPresets.Resolve(id);

        preset.Id.Should().Be(id);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("default", true)]
    [InlineData("holt-gentle", true)]
    [InlineData("holt", true)]
    [InlineData("holt-responsive", true)]
    [InlineData("", false)]
    [InlineData("bogus", false)]
    public void IsValid_ReturnsExpectedResult(string? id, bool expected)
    {
        TrendAlgorithmPresets.IsValid(id).Should().Be(expected);
    }
}
