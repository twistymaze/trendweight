using FluentAssertions;
using TrendWeight.Features.Measurements;
using TrendWeight.Features.Measurements.Models;
using TrendWeight.Features.Profile.Models;
using Xunit;

namespace TrendWeight.Tests.Features.Measurements.Services;

public class MeasurementComputationServiceTests
{
    private readonly MeasurementComputationService _sut = new();

    #region Test Data Helpers

    private static ProfileData CreateTestProfile(int dayStartOffset = 0, bool hideDataBeforeStart = false, DateTime? goalStart = null, string? trendAlgorithm = null)
    {
        return new ProfileData
        {
            UseMetric = true,
            DayStartOffset = dayStartOffset,
            HideDataBeforeStart = hideDataBeforeStart,
            GoalStart = goalStart,
            TrendAlgorithm = trendAlgorithm
        };
    }

    private static List<SourceData> CreateTestSourceData(params (string date, string time, decimal weight, decimal? fatRatio)[] measurements)
    {
        return new List<SourceData>
        {
            new()
            {
                Source = "test",
                Measurements = measurements.Select(m => new RawMeasurement
                {
                    Date = m.date,
                    Time = m.time,
                    Weight = m.weight,
                    FatRatio = m.fatRatio
                }).ToList()
            }
        };
    }

    #endregion

    #region Basic Functionality Tests

    [Fact]
    public void ComputeMeasurements_WithEmptyData_ReturnsEmptyList()
    {
        // Arrange
        var profile = CreateTestProfile();
        var sourceData = new List<SourceData>();

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeMeasurements_WithNullData_ReturnsEmptyList()
    {
        // Arrange
        var profile = CreateTestProfile();

        // Act
        var result = _sut.ComputeMeasurements(null!, profile);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeMeasurements_WithSingleMeasurement_ReturnsCorrectTrend()
    {
        // Arrange
        var profile = CreateTestProfile();
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 70.0m, null)
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(1);
        result[0].Date.Should().Be("2024-01-01");
        result[0].ActualWeight.Should().Be(70.0m);
        result[0].TrendWeight.Should().Be(70.0m); // First measurement = trend
        result[0].WeightIsInterpolated.Should().BeFalse();
    }

    #endregion

    #region Weight Trend Calculation Tests

    [Fact]
    public void ComputeMeasurements_WithMultipleWeights_CalculatesCorrectTrend()
    {
        // Arrange
        var profile = CreateTestProfile();
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 70.0m, null),
            ("2024-01-02", "08:00:00", 71.0m, null),
            ("2024-01-03", "08:00:00", 69.0m, null)
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(3);

        // First measurement: trend = actual
        result[0].TrendWeight.Should().Be(70.0m);

        // Second measurement: trend = previous + 0.1 * (actual - previous)
        // 70.0 + 0.1 * (71.0 - 70.0) = 70.1
        result[1].TrendWeight.Should().Be(70.1m);

        // Third measurement: 70.1 + 0.1 * (69.0 - 70.1) = 69.99
        result[2].TrendWeight.Should().Be(69.99m);
    }

    #endregion

    #region Weight Interpolation Tests

    [Fact]
    public void ComputeMeasurements_WithMissingDays_InterpolatesWeight()
    {
        // Arrange
        var profile = CreateTestProfile();
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 70.0m, null),
            ("2024-01-04", "08:00:00", 73.0m, null) // 3-day gap
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(4); // Original 2 + 2 interpolated

        // Check interpolated values
        result[1].Date.Should().Be("2024-01-02");
        result[1].ActualWeight.Should().Be(71.0m); // 70 + 1*1 = 71
        result[1].WeightIsInterpolated.Should().BeTrue();

        result[2].Date.Should().Be("2024-01-03");
        result[2].ActualWeight.Should().Be(72.0m); // 70 + 2*1 = 72
        result[2].WeightIsInterpolated.Should().BeTrue();

        result[3].Date.Should().Be("2024-01-04");
        result[3].ActualWeight.Should().Be(73.0m);
        result[3].WeightIsInterpolated.Should().BeFalse();
    }

    [Fact]
    public void ComputeMeasurements_WithFractionalDailyStep_KeepsThreeDecimalsOfInterpolatedWeight()
    {
        var profile = CreateTestProfile();
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 70.0m, null),
            ("2024-01-04", "08:00:00", 70.5m, null) // 0.1667 kg per day
        );

        var result = _sut.ComputeMeasurements(sourceData, profile);

        result.Select(r => r.ActualWeight).Should().Equal(70.0m, 70.167m, 70.333m, 70.5m);
        result.Select(r => r.WeightIsInterpolated).Should().Equal(false, true, true, false);
    }

    #endregion

    #region Fat Data Tests

    [Fact]
    public void ComputeMeasurements_WithFatData_CalculatesIndependentTrendMasses()
    {
        // Arrange
        var profile = CreateTestProfile();
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 100.0m, 0.2m), // 100kg, 20% fat
            ("2024-01-02", "08:00:00", 101.0m, 0.21m) // 101kg, 21% fat
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(2);

        // First day: fat mass = 100 * 0.2 = 20kg, lean mass = 80kg
        result[0].ActualFatPercent.Should().Be(0.2m);
        result[0].TrendFatPercent.Should().Be(0.2m);
        result[0].TrendFatMass.Should().Be(20.0m); // First measurement = trend
        result[0].TrendLeanMass.Should().Be(80.0m);

        // Second day: independent moving averages
        // Fat mass trend: 20 + 0.1 * (21.21 - 20) = 20.121
        // Lean mass trend: 80 + 0.1 * (79.79 - 80) = 79.979
        var expectedFatMass = 20.0m + 0.1m * (21.21m - 20.0m);
        var expectedLeanMass = 80.0m + 0.1m * (79.79m - 80.0m);

        result[1].TrendFatMass.Should().BeApproximately(expectedFatMass, 0.001m);
        result[1].TrendLeanMass.Should().BeApproximately(expectedLeanMass, 0.001m);
    }

    [Fact]
    public void ComputeMeasurements_WithFatDataGaps_InterpolatesFatMeasurements()
    {
        // Arrange
        var profile = CreateTestProfile();
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 100.0m, 0.2m), // Day 1: 100kg, 20% fat
            ("2024-01-04", "08:00:00", 103.0m, 0.23m) // Day 4: 103kg, 23% fat (3-day gap)
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(4);

        // Check that fat data was interpolated for missing days
        result[1].Date.Should().Be("2024-01-02");
        result[1].FatIsInterpolated.Should().BeTrue();
        result[1].ActualFatPercent.Should().Be(0.21m); // Linear interpolation: 0.20 + 0.01/day

        result[2].Date.Should().Be("2024-01-03");
        result[2].FatIsInterpolated.Should().BeTrue();
        result[2].ActualFatPercent.Should().Be(0.22m);

        // The fat series interpolates its own weights (100 -> 103 over three days gives
        // 101 and 102), and fat/lean mass are seeded from those, not from the weight series:
        //   fat mass  = weight * ratio      : 20.00, 21.21, 22.44, 23.69
        //   lean mass = weight * (1 - ratio): 80.00, 79.79, 79.56, 79.31
        // Each mass gets its own EMA with alpha 0.1 (trend + 0.1 * (value - trend)):
        //   fat  : 20 -> 20.121 -> 20.3529 -> 20.68661  (rounded to 3 places below)
        //   lean : 80 -> 79.979 -> 79.9371 -> 79.87439
        // and the ratio itself: 0.2 -> 0.201 -> 0.2029 -> 0.20561.
        result.Select(r => r.ActualWeight).Should().Equal(100.0m, 101.0m, 102.0m, 103.0m);
        result.Select(r => r.TrendFatMass).Should().Equal(20.0m, 20.121m, 20.353m, 20.687m);
        result.Select(r => r.TrendLeanMass).Should().Equal(80.0m, 79.979m, 79.937m, 79.874m);
        result.Select(r => r.TrendFatPercent).Should().Equal(0.2m, 0.201m, 0.2029m, 0.2056m);
        result.Select(r => r.WeightIsInterpolated).Should().Equal(false, true, true, false);
        result.Select(r => r.FatIsInterpolated).Should().Equal(false, true, true, false);
    }

    [Fact]
    public void ComputeMeasurements_WithDescendingInput_MatchesAscendingInput()
    {
        // Arrange - the same readings, once oldest-first and once newest-first (the order
        // providers store them), with a gap and same-day duplicates so grouping and
        // interpolation both depend on the sort rather than the input order.
        var profile = CreateTestProfile();
        var readings = new[]
        {
            ("2024-01-01", "07:30:00", 80.0m, (decimal?)0.25m),
            ("2024-01-01", "21:00:00", 81.0m, (decimal?)null),
            ("2024-01-02", "07:30:00", 79.6m, (decimal?)0.248m),
            ("2024-01-05", "07:30:00", 79.0m, (decimal?)null),
            ("2024-01-06", "07:30:00", 78.8m, (decimal?)0.244m)
        };

        // Act
        var ascending = _sut.ComputeMeasurements(CreateTestSourceData(readings), profile);
        var descending = _sut.ComputeMeasurements(CreateTestSourceData(Enumerable.Reverse(readings).ToArray()), profile);

        // Assert
        ascending.Should().HaveCount(6);
        ascending[0].Date.Should().Be("2024-01-01");
        ascending[^1].Date.Should().Be("2024-01-06");
        descending.Should().BeEquivalentTo(ascending, options => options.WithStrictOrdering());
        descending[0].Date.Should().Be("2024-01-01");
    }

    #endregion

    #region Day Start Offset Tests

    [Fact]
    public void ComputeMeasurements_WithDayStartOffset_AdjustsDateProperly()
    {
        // Arrange
        var profile = CreateTestProfile(dayStartOffset: 4); // 4-hour offset
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "02:00:00", 70.0m, null) // 2 AM measurement
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(1);
        // 2 AM - 4 hours = 10 PM previous day, so should be assigned to Dec 31
        result[0].Date.Should().Be("2023-12-31");
    }

    #endregion

    #region Goal Start Filter Tests

    [Fact]
    public void ComputeMeasurements_WithHideDataBeforeStart_FiltersEarlyMeasurements()
    {
        // Arrange
        var goalStart = new DateTime(2024, 1, 2);
        var profile = CreateTestProfile(hideDataBeforeStart: true, goalStart: goalStart);
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 70.0m, null), // Before goal start
            ("2024-01-02", "08:00:00", 71.0m, null), // On goal start
            ("2024-01-03", "08:00:00", 72.0m, null)  // After goal start
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(2);
        result[0].Date.Should().Be("2024-01-02");
        result[1].Date.Should().Be("2024-01-03");
    }

    [Fact]
    public void ComputeMeasurements_WithHideDataBeforeStart_AppliesDayStartOffsetBeforeComparing()
    {
        // With a 4 AM day start, a 03:59 reading on the goal-start date still belongs to
        // the previous day and must be hidden; 04:00 is the first reading of the new day
        var goalStart = new DateTime(2024, 1, 2);
        var profile = CreateTestProfile(dayStartOffset: 4, hideDataBeforeStart: true, goalStart: goalStart);
        var sourceData = CreateTestSourceData(
            ("2024-01-02", "03:59:00", 70.0m, null),
            ("2024-01-02", "04:00:00", 71.0m, null),
            ("2024-01-03", "08:00:00", 72.0m, null)
        );

        var result = _sut.ComputeMeasurements(sourceData, profile);

        result.Select(m => m.Date).Should().Equal("2024-01-02", "2024-01-03");
        result[0].ActualWeight.Should().Be(71.0m, "the 03:59 reading shifted to 2024-01-01 and was hidden");
    }

    [Fact]
    public void ComputeMeasurements_WithHideDataBeforeStartButNoGoalStart_KeepsEverything()
    {
        var profile = CreateTestProfile(hideDataBeforeStart: true, goalStart: null);
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 70.0m, null),
            ("2024-01-02", "08:00:00", 71.0m, null)
        );

        var result = _sut.ComputeMeasurements(sourceData, profile);

        result.Select(m => m.Date).Should().Equal("2024-01-01", "2024-01-02");
    }

    #endregion

    #region Grouping Tests

    [Fact]
    public void ComputeMeasurements_WithMultipleMeasurementsPerDay_SelectsFirstByTimestamp()
    {
        // Arrange
        var profile = CreateTestProfile();
        var sourceData = new List<SourceData>
        {
            new()
            {
                Source = "test",
                Measurements = new List<RawMeasurement>
                {
                    new() { Date = "2024-01-01", Time = "10:00:00", Weight = 71.0m },
                    new() { Date = "2024-01-01", Time = "08:00:00", Weight = 70.0m }, // Earlier time
                    new() { Date = "2024-01-01", Time = "12:00:00", Weight = 72.0m }
                }
            }
        };

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(1);
        result[0].ActualWeight.Should().Be(70.0m); // Should select 8:00 AM measurement
    }

    [Fact]
    public void ComputeMeasurements_WithManualAndScaleSameDay_ManualWins()
    {
        // Arrange: manual readings are stored at end of day, so without explicit
        // preference the earlier scale reading would win
        var profile = CreateTestProfile();
        var sourceData = new List<SourceData>
        {
            new()
            {
                Source = "withings",
                Measurements = new List<RawMeasurement>
                {
                    new() { Date = "2024-01-01", Time = "07:00:00", Weight = 70.0m, FatRatio = 0.25m },
                    new() { Date = "2024-01-02", Time = "07:00:00", Weight = 71.0m }
                }
            },
            new()
            {
                Source = "manual",
                Measurements = new List<RawMeasurement>
                {
                    new() { Date = "2024-01-01", Time = "23:59:59", Weight = 68.5m, FatRatio = 0.22m }
                }
            }
        };

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(2);
        result[0].ActualWeight.Should().Be(68.5m); // Manual entry overrides same-day scale reading
        result[0].ActualFatPercent.Should().Be(0.22m); // Fat selection prefers manual too
        result[1].ActualWeight.Should().Be(71.0m); // Scale-only day unaffected
    }

    #endregion

    #region Edge Cases and Performance Tests

    [Fact]
    public void ComputeMeasurements_WithLargeDataset_HandlesEfficently()
    {
        // Arrange: Create a large dataset with gaps that will be interpolated
        var profile = CreateTestProfile();
        var measurements = new List<RawMeasurement>();
        var baseDate = new DateTime(2024, 1, 1);

        // Create measurements with gaps - but ensure continuous coverage so interpolation works
        var actualMeasurementCount = 0;
        for (int i = 0; i < 100; i++) // Create 100 days with some gaps
        {
            // Create measurement every 2-3 days to test interpolation
            if (i % 3 != 1) // Skip middle day of every 3-day group
            {
                var currentDate = baseDate.AddDays(i);
                var weight = 70.0m + (decimal)Math.Sin(i / 30.0) * 2; // Sinusoidal pattern
                measurements.Add(new RawMeasurement
                {
                    Date = currentDate.ToString("yyyy-MM-dd"),
                    Time = "08:00:00",
                    Weight = weight,
                    FatRatio = i % 30 == 0 ? 0.2m + (decimal)Math.Sin(i / 60.0) * 0.05m : null
                });
                actualMeasurementCount++;
            }
        }

        var sourceData = new List<SourceData>
        {
            new() { Source = "test", Measurements = measurements }
        };

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert - every day from the first reading to the last is present exactly once
        var firstDate = DateTime.Parse(measurements[0].Date, System.Globalization.CultureInfo.InvariantCulture);
        var lastDate = DateTime.Parse(measurements[^1].Date, System.Globalization.CultureInfo.InvariantCulture);
        var daySpan = (lastDate - firstDate).Days + 1;
        result.Should().HaveCount(daySpan);
        result.Select(r => r.Date).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();

        // The skipped days (and only those) are interpolated
        var measuredDates = measurements.Select(m => m.Date).ToHashSet();
        result.Where(r => r.WeightIsInterpolated).Select(r => r.Date).Should().NotIntersectWith(measuredDates);
        result.Where(r => !r.WeightIsInterpolated).Select(r => r.Date).Should().BeEquivalentTo(measuredDates);
        result.Count(r => r.WeightIsInterpolated).Should().Be(daySpan - actualMeasurementCount);

        // Verify trend is smoother than actual values
        var actualWeights = result.Where(r => !r.WeightIsInterpolated).Select(r => (double)r.ActualWeight).ToList();
        var trendWeights = result.Select(r => (double)r.TrendWeight).ToList();

        actualWeights.Should().HaveCount(actualMeasurementCount);
        CalculateVariance(trendWeights).Should().BeLessThan(CalculateVariance(actualWeights), "trend should be smoother than actual measurements");
    }

    [Fact]
    public void ComputeMeasurements_WithWeightFluctuations_SmoothsTrend()
    {
        // Arrange: Create measurements with a spike
        var profile = CreateTestProfile();
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 80.0m, null),
            ("2024-01-02", "08:00:00", 85.0m, null), // +5kg spike
            ("2024-01-03", "08:00:00", 80.0m, null)  // back to normal
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(3);

        // Trend should smooth out the spike
        result[0].TrendWeight.Should().Be(80.0m);
        result[1].TrendWeight.Should().Be(80.5m); // 80 + 0.1 * (85 - 80)
        result[2].TrendWeight.Should().Be(80.45m); // 80.5 + 0.1 * (80 - 80.5)
    }

    [Fact]
    public void ComputeMeasurements_WithFatMassTrendsVerification_CalculatesIndependently()
    {
        // Arrange: Test that fat/lean mass trends are calculated independently
        var profile = CreateTestProfile();
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 100.0m, 0.3m),  // Day 1: 100kg, 30% fat (30kg fat, 70kg lean)
            ("2024-01-02", "08:00:00", 100.0m, 0.25m), // Day 2: 100kg, 25% fat (25kg fat, 75kg lean)
            ("2024-01-03", "08:00:00", 100.0m, 0.2m)   // Day 3: 100kg, 20% fat (20kg fat, 80kg lean)
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(3);

        // Verify independent fat mass trending: 30 -> 25 -> 20
        result[0].TrendFatMass.Should().Be(30.0m); // First = actual
        result[1].TrendFatMass.Should().Be(29.5m); // 30 + 0.1 * (25 - 30) = 29.5
        result[2].TrendFatMass.Should().Be(28.55m); // 29.5 + 0.1 * (20 - 29.5) = 28.55

        // Verify independent lean mass trending: 70 -> 75 -> 80
        result[0].TrendLeanMass.Should().Be(70.0m); // First = actual
        result[1].TrendLeanMass.Should().Be(70.5m); // 70 + 0.1 * (75 - 70) = 70.5
        result[2].TrendLeanMass.Should().Be(71.45m); // 70.5 + 0.1 * (80 - 70.5) = 71.45
    }

    [Fact]
    public void ComputeMeasurements_CompareWithProductOfTrends_ShowsDifference()
    {
        // Arrange: This test demonstrates why independent calculation is better
        var profile = CreateTestProfile();
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 100.0m, 0.25m),
            ("2024-01-02", "08:00:00", 90.0m, 0.30m)  // Weight drops, fat % increases
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(2);

        // Fat mass is trended from daily fat masses (25 -> 27), not derived from the
        // weight and fat-percent trends (99 * 0.255 = 25.245).
        result[1].TrendWeight.Should().Be(99.0m);
        result[1].TrendFatPercent.Should().Be(0.255m);
        result[1].TrendFatMass.Should().Be(25.2m); // 25 + 0.1 * (27 - 25)
        result[1].TrendLeanMass.Should().Be(73.8m); // 75 + 0.1 * (63 - 75)
    }

    [Theory]
    [InlineData(0)] // No offset
    [InlineData(4)] // 4-hour offset
    [InlineData(-2)] // Negative offset
    public void ComputeMeasurements_WithVariousDayStartOffsets_AdjustsDatesCorrectly(int offsetHours)
    {
        // Arrange
        var profile = CreateTestProfile(dayStartOffset: offsetHours);
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "02:00:00", 70.0m, null) // 2 AM measurement
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(1);

        // Calculate expected date
        var originalDateTime = DateTime.Parse("2024-01-01 02:00:00");
        var adjustedDateTime = originalDateTime.AddHours(-offsetHours);
        var expectedDate = adjustedDateTime.Date.ToString("yyyy-MM-dd");

        result[0].Date.Should().Be(expectedDate);
    }

    [Fact]
    public void ComputeMeasurements_WhenDayWinnerHasNoFat_TakesFatFromSameDayScaleReading()
    {
        // Arrange: a manual entry (weight only) wins the day, but a Withings reading later
        // that morning measured body fat. Weight comes from the manual entry; the fat series
        // is intentionally independent, so fat data is not lost for the day.
        var profile = CreateTestProfile();
        var sourceData = new List<SourceData>
        {
            new()
            {
                Source = "manual",
                Measurements = new List<RawMeasurement>
                {
                    new() { Date = "2024-01-01", Time = "07:00:00", Weight = 80.0m }
                }
            },
            new()
            {
                Source = "withings",
                Measurements = new List<RawMeasurement>
                {
                    new() { Date = "2024-01-01", Time = "08:00:00", Weight = 81.0m, FatRatio = 0.25m }
                }
            }
        };

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(1);
        result[0].ActualWeight.Should().Be(80.0m, "the manual entry wins the day's weight");
        result[0].ActualFatPercent.Should().Be(0.25m, "fat comes from the scale reading that measured it");
        result[0].TrendFatMass.Should().Be(20.25m, "fat mass is seeded from that reading's own weight (81 * 0.25)");
        result[0].FatIsInterpolated.Should().BeFalse();
    }

    [Fact]
    public void ComputeMeasurements_WithMultipleSources_MergesIntoOneDailySeries()
    {
        // Arrange - one reading from each source on consecutive days
        var profile = CreateTestProfile();
        var sourceData = new List<SourceData>
        {
            new()
            {
                Source = "withings",
                Measurements = new List<RawMeasurement>
                {
                    new() { Date = "2024-01-01", Time = "08:00:00", Weight = 70.0m }
                }
            },
            new()
            {
                Source = "legacy",
                Measurements = new List<RawMeasurement>
                {
                    new() { Date = "2024-01-02", Time = "08:00:00", Weight = 71.0m }
                }
            }
        };

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert - both sources feed a single chronological series with one trend
        result.Select(r => r.Date).Should().Equal("2024-01-01", "2024-01-02");
        result.Select(r => r.ActualWeight).Should().Equal(70.0m, 71.0m);
        result.Select(r => r.TrendWeight).Should().Equal(70.0m, 70.1m); // 70 + 0.1 * (71 - 70)
        result.Should().AllSatisfy(r => r.WeightIsInterpolated.Should().BeFalse());
    }

    #endregion

    #region Trend Algorithm Preset Tests

    [Fact]
    public void ComputeMeasurements_WithHoltPreset_CalculatesCorrectTrend()
    {
        // Arrange
        var profile = CreateTestProfile(trendAlgorithm: "holt"); // alpha = 0.1, beta = 0.1
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 70.0m, null),
            ("2024-01-02", "08:00:00", 71.0m, null),
            ("2024-01-03", "08:00:00", 69.0m, null),
            ("2024-01-04", "08:00:00", 70.0m, null)
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(4);

        // First measurement: level = actual, slope = 0
        result[0].TrendWeight.Should().Be(70.0m);

        // Second: forecast = 70.0 + 0 = 70.0
        //         level = 70.0 + 0.1 * (71.0 - 70.0) = 70.1
        //         slope = 0 + 0.1 * (70.1 - 70.0 - 0) = 0.01
        result[1].TrendWeight.Should().Be(70.1m);

        // Third:  forecast = 70.1 + 0.01 = 70.11
        //         level = 70.11 + 0.1 * (69.0 - 70.11) = 69.999
        //         slope = 0.01 + 0.1 * (69.999 - 70.1 - 0.01) = -0.0011
        result[2].TrendWeight.Should().Be(69.999m);

        // Fourth: forecast = 69.999 - 0.0011 = 69.9979
        //         level = 69.9979 + 0.1 * (70.0 - 69.9979) = 69.99811 -> rounds to 69.998
        result[3].TrendWeight.Should().Be(69.998m);
    }

    [Fact]
    public void ComputeMeasurements_WithHoltPreset_CalculatesCorrectFatTrends()
    {
        // Arrange
        var profile = CreateTestProfile(trendAlgorithm: "holt"); // alpha = 0.1, beta = 0.1
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 70.0m, 0.25m),
            ("2024-01-02", "08:00:00", 71.0m, 0.26m),
            ("2024-01-03", "08:00:00", 69.0m, 0.24m)
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert
        result.Should().HaveCount(3);

        // Fat ratio Holt series: 0.25; then 0.25 + 0.1 * (0.26 - 0.25) = 0.251, slope = 0.0001;
        // then forecast = 0.2511, level = 0.2511 + 0.1 * (0.24 - 0.2511) = 0.24999 -> rounds to 0.25
        result[0].TrendFatPercent.Should().Be(0.25m);
        result[1].TrendFatPercent.Should().Be(0.251m);
        result[2].TrendFatPercent.Should().Be(0.25m);

        // Fat mass series (weight * fatRatio): 17.5, 18.46, 16.56
        // Holt: 17.5; then 17.5 + 0.1 * (18.46 - 17.5) = 17.596, slope = 0.0096;
        // then forecast = 17.6056, level = 17.6056 + 0.1 * (16.56 - 17.6056) = 17.50104 -> 17.501
        result[0].TrendFatMass.Should().Be(17.5m);
        result[1].TrendFatMass.Should().Be(17.596m);
        result[2].TrendFatMass.Should().Be(17.501m);

        // Lean mass series (weight * (1 - fatRatio)): 52.5, 52.54, 52.44
        // Holt: 52.5; then 52.5 + 0.1 * (52.54 - 52.5) = 52.504, slope = 0.0004;
        // then forecast = 52.5044, level = 52.5044 + 0.1 * (52.44 - 52.5044) = 52.49796 -> 52.498
        result[0].TrendLeanMass.Should().Be(52.5m);
        result[1].TrendLeanMass.Should().Be(52.504m);
        result[2].TrendLeanMass.Should().Be(52.498m);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("bogus-future-algorithm")]
    public void ComputeMeasurements_WithDefaultOrUnknownPreset_MatchesDefaultExactly(string trendAlgorithm)
    {
        // Arrange: a deterministic pseudo-random series
        var random = new Random(396);
        var measurements = new List<(string date, string time, decimal weight, decimal? fatRatio)>();
        for (int i = 0; i < 100; i++)
        {
            var date = new DateTime(2024, 1, 1).AddDays(i).ToString("yyyy-MM-dd");
            var weight = 80.0m - 0.05m * i + (decimal)random.NextDouble();
            measurements.Add((date, "08:00:00", Math.Round(weight, 2), 0.2m + 0.001m * (i % 10)));
        }
        var sourceData = CreateTestSourceData(measurements.ToArray());

        // Act
        var defaultResult = _sut.ComputeMeasurements(sourceData, CreateTestProfile());
        var presetResult = _sut.ComputeMeasurements(sourceData, CreateTestProfile(trendAlgorithm: trendAlgorithm));

        // Assert: explicit "default" and unknown ids are exactly the default algorithm
        presetResult.Should().HaveCount(defaultResult.Count);
        for (int i = 0; i < defaultResult.Count; i++)
        {
            presetResult[i].TrendWeight.Should().Be(defaultResult[i].TrendWeight);
            presetResult[i].TrendFatPercent.Should().Be(defaultResult[i].TrendFatPercent);
            presetResult[i].TrendFatMass.Should().Be(defaultResult[i].TrendFatMass);
            presetResult[i].TrendLeanMass.Should().Be(defaultResult[i].TrendLeanMass);
        }
    }

    [Fact]
    public void ComputeMeasurements_DefaultProfile_MatchesReferenceEmaLoop()
    {
        // Arrange: deterministic pseudo-random weights
        var random = new Random(42);
        var weights = new List<decimal>();
        for (int i = 0; i < 50; i++)
        {
            weights.Add(Math.Round(75.0m + (decimal)(random.NextDouble() * 2 - 1), 2));
        }
        var measurements = weights
            .Select((w, i) => (new DateTime(2024, 1, 1).AddDays(i).ToString("yyyy-MM-dd"), "08:00:00", w, (decimal?)null))
            .ToArray();
        var sourceData = CreateTestSourceData(measurements);

        // Act
        var result = _sut.ComputeMeasurements(sourceData, CreateTestProfile());

        // Assert: matches the default Hacker's Diet EMA computed independently
        decimal trend = 0m;
        for (int i = 0; i < weights.Count; i++)
        {
            trend = i == 0 ? weights[i] : trend + 0.1m * (weights[i] - trend);
            result[i].TrendWeight.Should().Be(Math.Round(trend, 3));
        }
    }

    [Fact]
    public void ComputeMeasurements_WithHoltPresetAndSteadyLoss_TracksCloserThanDefault()
    {
        // Arrange: a perfectly steady loss of 0.2 kg/day for 60 days
        var measurements = Enumerable.Range(0, 60)
            .Select(i => (new DateTime(2024, 1, 1).AddDays(i).ToString("yyyy-MM-dd"), "08:00:00", 90.0m - 0.2m * i, (decimal?)null))
            .ToArray();
        var sourceData = CreateTestSourceData(measurements);

        // Act
        var defaultResult = _sut.ComputeMeasurements(sourceData, CreateTestProfile());
        var holtResult = _sut.ComputeMeasurements(sourceData, CreateTestProfile(trendAlgorithm: "holt"));

        // Assert: the default EMA settles into a steady-state lag of slope * (1 - alpha) / alpha
        // (~1.8 kg here); Holt's slope state largely eliminates it (it can slightly overshoot,
        // so compare lag magnitudes)
        var actual = defaultResult[^1].ActualWeight;
        var defaultLag = defaultResult[^1].TrendWeight - actual;
        var holtLag = Math.Abs(holtResult[^1].TrendWeight - actual);

        defaultLag.Should().BeGreaterThan(1.5m);
        holtLag.Should().BeLessThan(0.5m);
    }

    [Fact]
    public void ComputeMeasurements_WithHoltPresetAndGaps_SmoothsInterpolatedSeries()
    {
        // Arrange: a 3-day gap between readings; interpolation fills days 2 and 3
        var profile = CreateTestProfile(trendAlgorithm: "holt");
        var sourceData = CreateTestSourceData(
            ("2024-01-01", "08:00:00", 70.0m, null),
            ("2024-01-04", "08:00:00", 73.0m, null)
        );

        // Act
        var result = _sut.ComputeMeasurements(sourceData, profile);

        // Assert: interpolated weights are 71.0 and 72.0; Holt runs over the contiguous series
        result.Should().HaveCount(4);
        result[1].WeightIsInterpolated.Should().BeTrue();
        result[2].WeightIsInterpolated.Should().BeTrue();

        // level0 = 70.0, slope = 0
        // day 2: level = 70.0 + 0.1 * (71.0 - 70.0) = 70.1, slope = 0.01
        // day 3: forecast = 70.11, level = 70.11 + 0.1 * (72.0 - 70.11) = 70.299, slope = 0.01 + 0.1 * (70.299 - 70.1 - 0.01) = 0.0289
        // day 4: forecast = 70.3279, level = 70.3279 + 0.1 * (73.0 - 70.3279) = 70.59511 -> 70.595
        result[0].TrendWeight.Should().Be(70.0m);
        result[1].TrendWeight.Should().Be(70.1m);
        result[2].TrendWeight.Should().Be(70.299m);
        result[3].TrendWeight.Should().Be(70.595m);
    }

    #endregion

    #region Helper Methods

    private static double CalculateVariance(IList<double> values)
    {
        if (values.Count == 0) return 0;

        var mean = values.Sum() / values.Count;
        var squaredDiffs = values.Select(val => Math.Pow(val - mean, 2));
        return squaredDiffs.Sum() / values.Count;
    }

    #endregion
}
