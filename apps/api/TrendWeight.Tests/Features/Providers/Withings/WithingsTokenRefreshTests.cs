using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrendWeight.Features.Profile.Services;
using TrendWeight.Features.ProviderLinks.Services;
using TrendWeight.Features.Providers;
using TrendWeight.Features.Providers.Models;
using TrendWeight.Features.Providers.Withings;
using TrendWeight.Infrastructure.Configuration;
using TrendWeight.Infrastructure.DataAccess.Models;
using TrendWeight.Tests.Fixtures;
using Xunit;

namespace TrendWeight.Tests.Features.Providers.Withings;

/// <summary>
/// The token-rotation path of WithingsService: an expired stored token is refreshed,
/// the refreshed token is persisted, and the data fetch uses the new bearer.
/// </summary>
public class WithingsTokenRefreshTests
{
    private const string TokenUrl = "/v2/oauth2";
    private const string MeasureUrl = "wbsapi.withings.net/measure";
    private const string EmptyMeasures = "{\"status\":0,\"body\":{\"updatetime\":0,\"timezone\":\"UTC\",\"measuregrps\":[],\"more\":0,\"offset\":0}}";

    private readonly RecordingHttpHandler _http = new();
    private readonly Mock<IProviderLinkService> _providerLinkServiceMock = new();
    private readonly WithingsService _sut;

    public WithingsTokenRefreshTests()
    {
        var options = Options.Create(new AppOptions
        {
            Withings = new WithingsConfig { ClientId = "client-id", ClientSecret = "client-secret" }
        });

        _sut = new WithingsService(
            new HttpClient(_http) { BaseAddress = new Uri("https://wbsapi.withings.net") },
            options,
            _providerLinkServiceMock.Object,
            Mock.Of<IProfileService>(),
            null,
            Mock.Of<ILogger<WithingsService>>());
    }

    private void StoreLink(Guid userId, Dictionary<string, object> token)
    {
        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "withings"))
            .ReturnsAsync(new DbProviderLink { Uid = userId, Provider = "withings", Token = token });
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithExpiredToken_RefreshesStoresAndUsesNewBearer()
    {
        var userId = Guid.NewGuid();
        StoreLink(userId, ProviderTokens.Expired("old-access"));
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK,
                "{\"status\":0,\"body\":{\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":10800,\"token_type\":\"Bearer\",\"scope\":\"user.metrics\"}}")
            .WhenUrlContains(MeasureUrl, HttpStatusCode.OK, EmptyMeasures);
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await _sut.SyncMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-5));

        result.Success.Should().BeTrue();
        result.Measurements.Should().NotBeNull();

        var refresh = _http.Requests.Should().ContainSingle(r => r.Uri.ToString().Contains(TokenUrl)).Subject;
        refresh.Method.Should().Be(HttpMethod.Post);
        refresh.Body.Should().Contain("action=requesttoken")
            .And.Contain("grant_type=refresh_token")
            .And.Contain("refresh_token=test-refresh-token");

        _providerLinkServiceMock.Verify(x => x.StoreProviderLinkAsync(userId, "withings",
            It.Is<Dictionary<string, object>>(t =>
                (string)t["access_token"] == "new-access" &&
                (string)t["refresh_token"] == "new-refresh" &&
                (int)t["expires_in"] == 10800 &&
                (long)t["received_at"] >= before),
            It.IsAny<string?>()), Times.Once);

        var fetches = _http.Requests.Where(r => r.Uri.ToString().Contains(MeasureUrl)).ToList();
        fetches.Should().NotBeEmpty();
        fetches.Should().OnlyContain(r => r.AuthScheme == "Bearer" && r.AuthParameter == "new-access");
        ProviderServiceBase.HasRefreshLock(userId, "withings").Should().BeFalse();
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithValidToken_DoesNotRefresh()
    {
        var userId = Guid.NewGuid();
        StoreLink(userId, ProviderTokens.Valid("still-good"));
        _http.WhenUrlContains(MeasureUrl, HttpStatusCode.OK, EmptyMeasures);

        var result = await _sut.SyncMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-5));

        result.Success.Should().BeTrue();
        _http.Requests.Should().NotContain(r => r.Uri.ToString().Contains(TokenUrl));
        _http.Requests.Should().OnlyContain(r => r.AuthParameter == "still-good");
        _providerLinkServiceMock.Verify(x => x.StoreProviderLinkAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithExpiredTokenAndNoRefreshToken_ReportsUnknownWithoutCalling()
    {
        var userId = Guid.NewGuid();
        StoreLink(userId, ProviderTokens.ExpiredWithoutRefreshToken());

        var result = await _sut.SyncMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-5));

        result.Success.Should().BeFalse();
        result.Error.Should().Be(ProviderSyncError.Unknown);
        result.Measurements.Should().BeNull();
        _http.Requests.Should().BeEmpty("nothing can be fetched or refreshed without a refresh token");
        _providerLinkServiceMock.Verify(x => x.StoreProviderLinkAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>()), Times.Never);
        ProviderServiceBase.HasRefreshLock(userId, "withings").Should().BeFalse();
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WhenRefreshIsRejected_ReportsAuthFailureAndKeepsStoredToken()
    {
        // Withings reports failures as HTTP 200 with a non-zero status
        var userId = Guid.NewGuid();
        StoreLink(userId, ProviderTokens.Expired());
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK, "{\"status\":503,\"error\":\"Invalid Params: invalid refresh_token\"}");

        var result = await _sut.SyncMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-5));

        result.Success.Should().BeFalse();
        result.Error.Should().Be(ProviderSyncError.AuthFailed);
        _http.Requests.Should().NotContain(r => r.Uri.ToString().Contains(MeasureUrl));
        _providerLinkServiceMock.Verify(x => x.StoreProviderLinkAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>()), Times.Never);
    }
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderSyncError.NetworkError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ProviderSyncError.NetworkError)]
    [InlineData(HttpStatusCode.InternalServerError, ProviderSyncError.Unknown)]
    public async Task SyncMeasurementsAsync_WhenRefreshHasHttpFailure_MapsStatusAndKeepsLink(HttpStatusCode status, ProviderSyncError expected)
    {
        var userId = Guid.NewGuid();
        StoreLink(userId, ProviderTokens.Expired());
        _http.WhenUrlContains(TokenUrl, status, $"{{\"status\":{(int)status},\"error\":\"{status}\"}}");

        var result = await _sut.SyncMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-5));

        result.Success.Should().BeFalse();
        result.Error.Should().Be(expected);
        _http.Requests.Should().NotContain(r => r.Uri.ToString().Contains(MeasureUrl));
        VerifyLinkUnchanged();
        ProviderServiceBase.HasRefreshLock(userId, "withings").Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"status\":0,\"body\":{\"access_tok")]
    [InlineData("<html><body>Bad Gateway</body></html>")]
    public async Task SyncMeasurementsAsync_WhenRefreshBodyIsMalformed_ReportsUnknownAndKeepsLink(string body)
    {
        var userId = Guid.NewGuid();
        StoreLink(userId, ProviderTokens.Expired());
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK, body);

        var result = await _sut.SyncMeasurementsAsync(userId, true, DateTime.UtcNow.AddDays(-5));

        result.Success.Should().BeFalse();
        result.Error.Should().Be(ProviderSyncError.Unknown);
        _http.Requests.Should().NotContain(r => r.Uri.ToString().Contains(MeasureUrl));
        VerifyLinkUnchanged();
    }

    private void VerifyLinkUnchanged()
    {
        _providerLinkServiceMock.Verify(x => x.StoreProviderLinkAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>()), Times.Never);
        _providerLinkServiceMock.Verify(x => x.RemoveProviderLinkAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }
}
