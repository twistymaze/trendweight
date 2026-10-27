using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TrendWeight.Features.Measurements;
using TrendWeight.Features.Measurements.Models;
using TrendWeight.Features.Profile.Services;
using TrendWeight.Features.Providers;
using TrendWeight.Features.Providers.Exceptions;
using TrendWeight.Features.Providers.Fitbit;
using TrendWeight.Features.Providers.Fitbit.Models;
using TrendWeight.Features.Providers.Models;
using TrendWeight.Features.ProviderLinks.Services;
using TrendWeight.Infrastructure.Configuration;
using TrendWeight.Infrastructure.DataAccess.Models;
using Xunit;

namespace TrendWeight.Tests.Features.Providers.Fitbit;

public class FitbitServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<IOptions<AppOptions>> _appOptionsMock;
    private readonly Mock<IProviderLinkService> _providerLinkServiceMock;
    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<ILogger<FitbitService>> _loggerMock;
    private readonly FitbitService _sut;
    private readonly FitbitConfig _fitbitConfig;

    public FitbitServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.fitbit.com")
        };

        _fitbitConfig = new FitbitConfig
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret"
        };

        var appOptions = new AppOptions
        {
            Fitbit = _fitbitConfig
        };

        _appOptionsMock = new Mock<IOptions<AppOptions>>();
        _appOptionsMock.Setup(x => x.Value).Returns(appOptions);

        _providerLinkServiceMock = new Mock<IProviderLinkService>();
        _profileServiceMock = new Mock<IProfileService>();
        _loggerMock = new Mock<ILogger<FitbitService>>();

        _sut = new FitbitService(
            _httpClient,
            _appOptionsMock.Object,
            _providerLinkServiceMock.Object,
            _profileServiceMock.Object,
            null, // ISyncProgressReporter
            _loggerMock.Object);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SyncMeasurementsAsync_WhenLaterPageFails_DiscardsPartialResults(HttpStatusCode failureStatus)
    {
        var userId = Guid.NewGuid();
        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(new DbProviderLink { Uid = userId, Provider = "fitbit", Token = CreateValidToken("fitbit-user") });
        _httpMessageHandlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"weight\":[{\"date\":\"2026-01-01\",\"time\":\"08:00:00\",\"weight\":180}]}")
            })
            .ReturnsAsync(new HttpResponseMessage(failureStatus) { Content = new StringContent("Unavailable") });

        var result = await _sut.SyncMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-40));

        result.Success.Should().BeFalse();
        result.Error.Should().Be(ProviderSyncError.NetworkError);
        result.Measurements.Should().BeNull();
        _httpMessageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WhenQuotaIsExhausted_WaitsForTheResetAndContinues()
    {
        var userId = Guid.NewGuid();
        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(new DbProviderLink { Uid = userId, Provider = "fitbit", Token = CreateValidToken("fitbit-user") });

        // Every chunk reports the quota as exhausted with the reset one second away. A full
        // sync needs more requests than one quota window allows, so the sync must pause for
        // the reset and carry on rather than fail (see SendRateLimitedRequestAsync).
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"weight\":[]}", Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "2");
                response.Headers.Add("fitbit-rate-limit-reset", "1");
                return response;
            });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _sut.SyncMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-40));
        stopwatch.Stop();

        result.Success.Should().BeTrue();
        result.Measurements.Should().NotBeNull();
        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900), "the second chunk must wait for the quota reset");
        _httpMessageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_WithIncompleteToken_DoesNotStoreCredentials()
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });

        var act = () => _sut.ExchangeAuthorizationCodeAsync("code", "https://example.com/callback", Guid.NewGuid());

        await act.Should().ThrowAsync<JsonException>();
        _providerLinkServiceMock.Verify(x => x.StoreProviderLinkAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WhenDisabled_ReturnsDisabledWithoutNetworkOrStorage()
    {
        // Arrange - flip the kill-switch
        _fitbitConfig.Enabled = false;

        // Act
        var result = await _sut.SyncMeasurementsAsync(Guid.NewGuid(), metric: true);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be(ProviderSyncError.Disabled);
        result.Measurements.Should().BeNull();
        // No token lookup or network call happens
        _providerLinkServiceMock.Verify(x => x.GetProviderLinkAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    #region Date Range Handling Tests - Critical for Fitbit

    [Fact]
    public async Task GetMeasurementsAsync_WithLargeDateRange_SplitsInto32DayChunks()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        // Set up a date range of 100 days
        var startDate = DateTime.UtcNow.AddDays(-100);
        var endDate = DateTime.UtcNow;

        // Track API calls to verify chunking
        var apiCalls = new List<(DateTime start, DateTime end)>();

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                // Extract dates from URL
                var url = request.RequestUri!.ToString();
                var match = System.Text.RegularExpressions.Regex.Match(url, @"/date/(\d{4}-\d{2}-\d{2})/(\d{4}-\d{2}-\d{2})\.json");
                if (match.Success)
                {
                    var start = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    var end = DateTime.ParseExact(match.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    apiCalls.Add((start, end));
                }

                // Return empty response for simplicity
                var weightData = new FitbitWeightLog { Weight = new List<FitbitWeightLogEntry>() };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Act
        await _sut.GetMeasurementsAsync(userId, true, startDate);

        // Assert
        // With 100 days and 32-day chunks, we expect 4 API calls
        apiCalls.Should().HaveCountGreaterThan(2);

        // Verify each chunk is at most 32 days
        foreach (var (chunkStart, chunkEnd) in apiCalls)
        {
            var daysDiff = (chunkEnd - chunkStart).Days + 1; // +1 to include both start and end
            daysDiff.Should().BeLessThanOrEqualTo(32);
        }

        // Verify chunks cover the entire range
        apiCalls.First().start.Date.Should().Be(startDate.Date);
        apiCalls.Last().end.Date.Should().BeOnOrAfter(DateTime.UtcNow.Date.AddDays(-1)); // Allow for timing
    }

    [Fact]
    public async Task GetMeasurementsAsync_ForInitialSync_StartsFrom2009()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        DateTime? capturedStartDate = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                var url = request.RequestUri!.ToString();
                var match = System.Text.RegularExpressions.Regex.Match(url, @"/date/(\d{4}-\d{2}-\d{2})/");
                if (match.Success && capturedStartDate == null)
                {
                    capturedStartDate = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                var weightData = new FitbitWeightLog { Weight = new List<FitbitWeightLogEntry>() };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Act - null startDate means initial sync
        await _sut.GetMeasurementsAsync(userId, true, null);

        // Assert
        capturedStartDate.Should().NotBeNull();
        capturedStartDate!.Value.Should().Be(new DateTime(2009, 1, 1));
    }

    [Fact]
    public async Task GetMeasurementsAsync_WithMultipleChunks_CombinesMeasurementsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        // Set up 65 days range to ensure multiple chunks
        var startDate = DateTime.UtcNow.AddDays(-65);

        var chunkIndex = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                var weights = new List<FitbitWeightLogEntry>();

                // First chunk has 2 measurements
                if (chunkIndex == 0)
                {
                    weights.Add(new FitbitWeightLogEntry
                    {
                        Date = DateTime.UtcNow.AddDays(-65).ToString("yyyy-MM-dd"),
                        Time = "08:00:00",
                        Weight = 165.0m, // pounds
                        Fat = 25.0m,
                        LogId = 1000
                    });
                    weights.Add(new FitbitWeightLogEntry
                    {
                        Date = DateTime.UtcNow.AddDays(-50).ToString("yyyy-MM-dd"),
                        Time = "09:00:00",
                        Weight = 164.0m,
                        LogId = 1001
                    });
                }
                // Second chunk has 1 measurement
                else if (chunkIndex == 1)
                {
                    weights.Add(new FitbitWeightLogEntry
                    {
                        Date = DateTime.UtcNow.AddDays(-20).ToString("yyyy-MM-dd"),
                        Time = "07:30:00",
                        Weight = 163.0m,
                        LogId = 1002
                    });
                }
                // Third chunk has 2 measurements
                else
                {
                    weights.Add(new FitbitWeightLogEntry
                    {
                        Date = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd"),
                        Time = "08:15:00",
                        Weight = 162.0m,
                        LogId = 1003
                    });
                    weights.Add(new FitbitWeightLogEntry
                    {
                        Date = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
                        Time = "07:45:00",
                        Weight = 161.5m,
                        Fat = 24.5m,
                        LogId = 1004
                    });
                }

                chunkIndex++;

                var weightData = new FitbitWeightLog { Weight = weights };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                response.Headers.Add("Accept-Language", "en-US");
                return response;
            });

        // Act
        var result = await _sut.GetMeasurementsAsync(userId, true, startDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5); // Total measurements from all chunks

        // Verify weights are converted from pounds to kg correctly
        result![0].Weight.Should().BeApproximately(74.843m, 0.001m); // 165 lbs
        result[1].Weight.Should().BeApproximately(74.389m, 0.001m); // 164 lbs
        result[2].Weight.Should().BeApproximately(73.936m, 0.001m); // 163 lbs
        result[3].Weight.Should().BeApproximately(73.482m, 0.001m); // 162 lbs
        result[4].Weight.Should().BeApproximately(73.256m, 0.001m); // 161.5 lbs

        // Verify fat percentages are converted correctly
        result[0].FatRatio.Should().BeApproximately(0.25m, 0.001m); // 25%
        result[4].FatRatio.Should().BeApproximately(0.245m, 0.001m); // 24.5%

        // Verify at least 3 API calls were made (for 65 days)
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeast(3),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString().Contains("/body/log/weight/date")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetMeasurementsAsync_VerifiesCompleteRangeCoverageWithNoGaps()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        // Set up a specific date range to test complete coverage
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 4, 15, 0, 0, 0, DateTimeKind.Utc); // 105 days

        // Track all date ranges requested
        var requestedRanges = new List<(DateTime start, DateTime end)>();

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                var url = request.RequestUri!.ToString();
                var match = System.Text.RegularExpressions.Regex.Match(url, @"/date/(\d{4}-\d{2}-\d{2})/(\d{4}-\d{2}-\d{2})\.json");
                if (match.Success)
                {
                    var start = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    var end = DateTime.ParseExact(match.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    requestedRanges.Add((start, end));
                }

                var weightData = new FitbitWeightLog { Weight = new List<FitbitWeightLogEntry>() };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Act
        await _sut.GetMeasurementsAsync(userId, true, startDate);

        // Assert - Verify complete coverage
        requestedRanges.Should().NotBeEmpty();

        // Sort ranges by start date
        var sortedRanges = requestedRanges.OrderBy(r => r.start).ToList();

        // First chunk should start at our start date
        sortedRanges.First().start.Should().Be(startDate);

        // Last chunk should include today (or close to it)
        sortedRanges.Last().end.Should().BeOnOrAfter(DateTime.UtcNow.Date.AddDays(-1));

        // Verify no gaps between chunks
        for (int i = 0; i < sortedRanges.Count - 1; i++)
        {
            var currentEnd = sortedRanges[i].end;
            var nextStart = sortedRanges[i + 1].start;

            // Next chunk should start exactly one day after current chunk ends
            nextStart.Should().Be(currentEnd.AddDays(1));
        }

        // Verify all chunks are within the 32-day limit
        foreach (var range in sortedRanges)
        {
            var days = (range.end - range.start).Days + 1;
            days.Should().BeLessThanOrEqualTo(32);
        }
    }

    [Fact]
    public async Task GetMeasurementsAsync_WithExactly32DayRange_MakesSingleRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        // Adjust for 2-day timezone buffer: 29 days ago + today + 2 future days = 32 days total
        var startDate = DateTime.UtcNow.AddDays(-29);
        var apiCallCount = 0;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                apiCallCount++;
                var weightData = new FitbitWeightLog { Weight = new List<FitbitWeightLogEntry>() };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Act
        await _sut.GetMeasurementsAsync(userId, true, startDate);

        // Assert
        apiCallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMeasurementsAsync_RequestsEndDateTwoDaysAheadOfUtcToday()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        var todayUtc = DateTime.UtcNow.Date;
        var expectedEnd = todayUtc.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string? capturedEnd = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken ct) =>
            {
                // Capture end date from the URL: /date/{start}/{end}.json
                var url = request.RequestUri!.ToString();
                var match = System.Text.RegularExpressions.Regex.Match(url, @"/date/(\d{4}-\d{2}-\d{2})/(\d{4}-\d{2}-\d{2})\.json");
                if (match.Success && capturedEnd == null)
                {
                    capturedEnd = match.Groups[2].Value;
                }

                var weightData = new FitbitWeightLog { Weight = new List<FitbitWeightLogEntry>() };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Choose a recent start date so the service will cap the range by its computed endDate
        var startDate = todayUtc.AddDays(-1);

        // Act
        await _sut.GetMeasurementsAsync(userId, true, startDate);

        // Assert: The requested end date should be today (UTC) + 2 days
        capturedEnd.Should().NotBeNull();
        capturedEnd!.Should().Be(expectedEnd);
    }

    [Fact]
    public async Task GetMeasurementsAsync_With30DayRange_MakesTwoRequestsDueToTimezoneBuffer()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        // 32 days ago to today = 33 days total, which exceeds 32-day limit and requires 2 API calls
        var startDate = DateTime.UtcNow.AddDays(-32);
        var apiCallCount = 0;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                apiCallCount++;
                var weightData = new FitbitWeightLog { Weight = new List<FitbitWeightLogEntry>() };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Act
        await _sut.GetMeasurementsAsync(userId, true, startDate);

        // Assert - Expects 2 API calls because 33 days exceeds the 32-day limit
        apiCallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetMeasurementsAsync_WithRateLimitHeaders_ProcessesHeadersCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        var callCount = 0;

        _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    callCount++;
                    var weightData = new FitbitWeightLog { Weight = new List<FitbitWeightLogEntry>() };
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                    };

                    // Add rate limit headers - the service processes these after each request
                    response.Headers.Add("fitbit-rate-limit-limit", "150");
                    response.Headers.Add("fitbit-rate-limit-remaining", callCount == 1 ? "3" : "149"); // Low on first call
                    response.Headers.Add("fitbit-rate-limit-reset", "1"); // Reset in 1 second to avoid long wait

                    return response;
                });

        // Act - Use a date range that requires multiple chunks
        await _sut.GetMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-40));

        // Assert
        // Verify we got the headers by checking that debug logging occurred
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Fitbit rate limit:")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetMeasurementsAsync_WithYearSpanningRange_HandlesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        // Start in 2009 (minimum date) and request a full year
        var startDate = new DateTime(2009, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var requestCount = 0;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                requestCount++;
                var weightData = new FitbitWeightLog { Weight = new List<FitbitWeightLogEntry>() };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Act
        await _sut.GetMeasurementsAsync(userId, true, startDate);

        // Assert
        // From 2009-01-01 to today is many years, should be many chunks
        requestCount.Should().BeGreaterThan(100); // Rough estimate: 15+ years * 12 chunks/year
    }

    [Fact]
    public async Task GetMeasurementsAsync_WhenChunkFailsPartway_ThrowsProviderAuthException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        var startDate = DateTime.UtcNow.AddDays(-100); // Multiple chunks needed
        var callCount = 0;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 2)
                {
                    // Fail on second chunk with 401
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        Content = new StringContent("{\"errors\":[{\"errorType\":\"oauth\",\"message\":\"Access token invalid\"}]}")
                    };
                }

                var weightData = new FitbitWeightLog { Weight = new List<FitbitWeightLogEntry>() };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Act & Assert
        await _sut.Invoking(x => x.GetMeasurementsAsync(userId, true, startDate))
            .Should().ThrowAsync<ProviderAuthException>();

        // Verify we made exactly 2 calls (succeeded on first, failed on second)
        callCount.Should().Be(2);
    }

    #endregion

    #region Other Important Tests

    [Fact]
    public void ProviderName_ReturnsFitbit()
    {
        // Assert
        _sut.ProviderName.Should().Be("fitbit");
    }

    [Fact]
    public void GetAuthorizationUrl_ReturnsCorrectUrl()
    {
        // Arrange
        var state = "test-state";
        var callbackUrl = "https://example.com/callback";

        // Act
        var result = _sut.GetAuthorizationUrl(state, callbackUrl);

        // Assert
        result.Should().StartWith("https://www.fitbit.com/oauth2/authorize");
        result.Should().Contain($"client_id={_fitbitConfig.ClientId}");
        result.Should().Contain("response_type=code");
        result.Should().Contain("scope=weight");
        result.Should().Contain($"state={state}");
        result.Should().Contain("redirect_uri=");
        // Verify the callback URL is encoded (case-insensitive check)
        result.ToLower().Should().Contain(Uri.EscapeDataString(callbackUrl).ToLower());
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_WithValidCode_ReturnsTrue()
    {
        // Arrange
        var code = "test-code";
        var callbackUrl = "https://example.com/callback";
        var userId = Guid.NewGuid();

        var tokenResponse = new FitbitTokenResponse
        {
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token",
            TokenType = "Bearer",
            ExpiresIn = 28800,
            Scope = "weight",
            // UserId is returned in token response but not stored in our model
        };

        SetupHttpResponse("https://api.fitbit.com/oauth2/token", tokenResponse);

        // Act
        var result = await _sut.ExchangeAuthorizationCodeAsync(code, callbackUrl, userId);

        // Assert
        result.Should().BeTrue();

        _providerLinkServiceMock.Verify(x => x.StoreProviderLinkAsync(
            userId,
            "fitbit",
            It.Is<Dictionary<string, object>>(d =>
                d["access_token"].ToString() == "test-access-token" &&
                d["refresh_token"].ToString() == "test-refresh-token" &&
                d["token_type"].ToString() == "Bearer" &&
                d["scope"].ToString() == "weight" &&
                d.ContainsKey("received_at") &&
                d["expires_in"].ToString() == "28800"),
            null),
            Times.Once);

        // Verify Basic Auth header was used
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.Authorization!.Scheme == "Basic" &&
                req.Headers.Authorization.Parameter != null),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetMeasurementsAsync_WithImperialUnits_ConvertsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        var weightData = new FitbitWeightLog
        {
            Weight = new List<FitbitWeightLogEntry>
            {
                new FitbitWeightLogEntry
                {
                    Date = "2024-01-01",
                    Time = "08:00:00",
                    Weight = 200.0m, // pounds
                    Fat = 30.0m, // percentage
                    LogId = 2000
                }
            }
        };

        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Act - metric = false means user prefers imperial but we always store in kg
        // Use a very recent date to ensure only one API call
        var result = await _sut.GetMeasurementsAsync(userId, false, DateTime.UtcNow.AddDays(-1));

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        // All results should have the same converted weight
        // 200 lbs = 90.7185 kg
        foreach (var measurement in result!)
        {
            measurement.Weight.Should().BeApproximately(90.7185m, 0.001m);
            measurement.FatRatio.Should().Be(0.30m); // 30% as ratio
        }
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithLargeDateRange_HandlesAllChunks()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var fitbitUserId = "ABCDEF";
        // Profile service is not used in sync

        var token = CreateValidToken(fitbitUserId);
        var providerLink = new DbProviderLink
        {
            Provider = "fitbit",
            Token = token
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        var totalMeasurements = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                // Return 3 measurements per chunk
                var weights = new List<FitbitWeightLogEntry>();
                for (int i = 0; i < 3; i++)
                {
                    weights.Add(new FitbitWeightLogEntry
                    {
                        Date = DateTime.UtcNow.AddDays(-(totalMeasurements + i)).ToString("yyyy-MM-dd"),
                        Time = "08:00:00",
                        Weight = 150.0m + i,
                        LogId = 3000 + totalMeasurements + i
                    });
                }
                totalMeasurements += 3;

                var weightData = new FitbitWeightLog { Weight = weights };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(weightData), Encoding.UTF8, "application/json")
                };
                response.Headers.Add("fitbit-rate-limit-limit", "150");
                response.Headers.Add("fitbit-rate-limit-remaining", "149");
                response.Headers.Add("fitbit-rate-limit-reset", "3600");
                return response;
            });

        // Source data update is now handled by the caller

        // Act
        var result = await _sut.SyncMeasurementsAsync(userId, true);

        // Assert
        result.Success.Should().BeTrue();
        result.Provider.Should().Be("fitbit");
        result.Measurements.Should().NotBeNull();

        // Verify multiple chunks were processed
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeast(2), // Should be multiple chunks for a full sync
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.ToString().Contains("/body/log/weight/date")),
            ItExpr.IsAny<CancellationToken>());
    }

    #endregion

    #region Helper Methods

    private Dictionary<string, object> CreateValidToken(string fitbitUserId)
    {
        return new Dictionary<string, object>
        {
            ["access_token"] = "test-access-token",
            ["refresh_token"] = "test-refresh-token",
            ["received_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["expires_in"] = 28800,
            ["user_id"] = fitbitUserId
        };
    }

    private void SetupHttpResponse<T>(string urlContains, T responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(responseContent);
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Add default Fitbit headers
        response.Headers.Add("fitbit-rate-limit-limit", "150");
        response.Headers.Add("fitbit-rate-limit-remaining", "149");
        response.Headers.Add("fitbit-rate-limit-reset", "3600");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains(urlContains)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    #endregion

    #region ProviderServiceBase Method Tests

    [Fact]
    public async Task HasActiveProviderLinkAsync_WithExistingLink_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var providerLink = new DbProviderLink
        {
            Uid = userId,
            Provider = "fitbit",
            Token = CreateValidToken("fitbit-user-123"),
            UpdatedAt = DateTime.UtcNow.AddDays(-1).ToString("o")
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        // Act
        var result = await _sut.HasActiveProviderLinkAsync(userId);

        // Assert
        result.Should().BeTrue();
        _providerLinkServiceMock.Verify(x => x.GetProviderLinkAsync(userId, "fitbit"), Times.Once);
    }

    [Fact]
    public async Task HasActiveProviderLinkAsync_WithNoLink_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync((DbProviderLink?)null);

        // Act
        var result = await _sut.HasActiveProviderLinkAsync(userId);

        // Assert
        result.Should().BeFalse();
        _providerLinkServiceMock.Verify(x => x.GetProviderLinkAsync(userId, "fitbit"), Times.Once);
    }

    [Fact]
    public async Task RemoveProviderLinkAsync_Successfully_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _providerLinkServiceMock.Setup(x => x.RemoveProviderLinkAsync(userId, "fitbit"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.RemoveProviderLinkAsync(userId);

        // Assert
        result.Should().BeTrue();
        _providerLinkServiceMock.Verify(x => x.RemoveProviderLinkAsync(userId, "fitbit"), Times.Once);
    }

    [Fact]
    public async Task RemoveProviderLinkAsync_WithException_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _providerLinkServiceMock.Setup(x => x.RemoveProviderLinkAsync(userId, "fitbit"))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _sut.RemoveProviderLinkAsync(userId);

        // Assert
        result.Should().BeFalse();
        _providerLinkServiceMock.Verify(x => x.RemoveProviderLinkAsync(userId, "fitbit"), Times.Once);
    }



    [Fact]
    public async Task GetMeasurementsAsync_WithExpiredTokenAndFailedRefresh_ThrowsProviderAuthException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiredToken = CreateExpiredToken();
        var providerLink = new DbProviderLink
        {
            Uid = userId,
            Provider = "fitbit",
            Token = expiredToken,
            UpdatedAt = DateTime.UtcNow.AddDays(-1).ToString("o")
        };

        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(providerLink);

        // Setup refresh token call to fail
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString().Contains("/oauth2/token")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        // Act & Assert
        await _sut.Invoking(x => x.GetMeasurementsAsync(userId, true))
            .Should().ThrowAsync<ProviderAuthException>();

        // The per-link refresh lock must not outlive the refresh, even when it fails
        ProviderServiceBase.HasRefreshLock(userId, "fitbit").Should().BeFalse();
    }

    [Fact]
    public async Task GetMeasurementsAsync_WhenConcurrentRequestAlreadyRefreshed_UsesStoredTokenWithoutRefreshing()
    {
        // Arrange - the first read sees an expired token; by the time the refresh lock is held,
        // another request has stored a fresh token (both providers rotate refresh tokens, so
        // spending the stale one would fail with invalid_grant)
        var userId = Guid.NewGuid();
        var freshToken = CreateValidToken("fitbit-user");
        freshToken["access_token"] = "fresh-access-token";

        _providerLinkServiceMock.SetupSequence(x => x.GetProviderLinkAsync(userId, "fitbit"))
            .ReturnsAsync(new DbProviderLink { Uid = userId, Provider = "fitbit", Token = CreateExpiredToken() })
            .ReturnsAsync(new DbProviderLink { Uid = userId, Provider = "fitbit", Token = freshToken });

        string? bearerUsed = null;
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/body/log/weight/date")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                bearerUsed = req.Headers.Authorization?.Parameter;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"weight\":[]}", Encoding.UTF8, "application/json")
                };
            });

        // Act
        var result = await _sut.GetMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-5));

        // Assert
        result.Should().NotBeNull();
        bearerUsed.Should().Be("fresh-access-token");
        _httpMessageHandlerMock.Protected().Verify("SendAsync", Times.Never(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/oauth2/token")),
            ItExpr.IsAny<CancellationToken>());
        _providerLinkServiceMock.Verify(x => x.StoreProviderLinkAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>()), Times.Never);
        ProviderServiceBase.HasRefreshLock(userId, "fitbit").Should().BeFalse("the refresh lock entry is released after the refresh completes");
    }

    private Dictionary<string, object> CreateExpiredToken()
    {
        return new Dictionary<string, object>
        {
            ["access_token"] = "expired-token",
            ["refresh_token"] = "test-refresh-token",
            ["token_type"] = "Bearer",
            ["expires_in"] = 3600,
            ["scope"] = "weight",
            ["user_id"] = "fitbit-user-id",
            ["received_at"] = ((DateTimeOffset)DateTime.UtcNow.AddHours(-2)).ToUnixTimeSeconds() // Expired 2 hours ago
        };
    }

    #endregion
}
