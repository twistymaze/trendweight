using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Web;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrendWeight.Features.Measurements.Models;
using TrendWeight.Features.Profile.Services;
using TrendWeight.Features.ProviderLinks.Services;
using TrendWeight.Features.Providers.Exceptions;
using TrendWeight.Features.Providers.Models;
using TrendWeight.Features.Providers.Withings;
using TrendWeight.Infrastructure.Configuration;
using TrendWeight.Infrastructure.DataAccess.Models;
using TrendWeight.Tests.Fixtures;
using Xunit;
using static TrendWeight.Tests.Fixtures.WithingsPayloads;

namespace TrendWeight.Tests.Features.Providers.Withings;

/// <summary>
/// WithingsService against the documented wire format: what it sends, how it reads
/// measurement pages, and how it degrades when Withings sends something unexpected.
/// Every provider payload comes from <see cref="WithingsPayloads"/>.
/// </summary>
public class WithingsServiceTests
{
    private const string TokenUrl = "https://wbsapi.withings.net/v2/oauth2";
    private const string MeasureUrl = "wbsapi.withings.net/measure";
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";
    private const string Callback = "https://example.com/callback";

    // 2024-01-01T08:00:00Z: 09:00 in Paris, 03:00 in New York
    private const long Jan1_0800Utc = 1704096000;
    private const long Jan1_1200Utc = 1704110400;
    private const long Jan1_0700Utc = 1704092400;
    private const long Jan2_0700Utc = 1704178800;
    private const long Jan3_0700Utc = 1704265200;

    private readonly RecordingHttpHandler _http = new();
    private readonly CapturingLoggerProvider _logs = new();
    private readonly Mock<IProviderLinkService> _providerLinkServiceMock = new();
    private readonly WithingsService _sut;

    public WithingsServiceTests()
    {
        var options = Options.Create(new AppOptions
        {
            Withings = new WithingsConfig { ClientId = ClientId, ClientSecret = ClientSecret }
        });

        _sut = new WithingsService(
            new HttpClient(_http),
            options,
            _providerLinkServiceMock.Object,
            Mock.Of<IProfileService>(),
            null,
            _logs.CreateLogger<WithingsService>());
    }

    #region Measurement parsing

    [Theory]
    [InlineData(79350, -3, "79.35")]
    [InlineData(7935, -2, "79.35")]
    [InlineData(793500, -4, "79.35")]
    [InlineData(80, 0, "80")]
    public async Task SyncMeasurementsAsync_ScalesWeightByUnitExponent(int value, int unit, string expectedKg)
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas("UTC", [Group(1, Jan1_0800Utc, Measure(value, 1, unit))]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Measurements.Should().ContainSingle().Which.Weight.Should().Be(decimal.Parse(expectedKg, CultureInfo.InvariantCulture));
    }


    [Fact]
    public async Task SyncMeasurementsAsync_AcceptsFractionalMeasureValues()
    {
        var userId = LinkUser();
        // The docs describe value as an integer; a fractional value must not fail the page
        MeasurePage(null, GetMeas("UTC", [Group(1, Jan1_0800Utc, Measure(79350, 1, -3).Replace("79350", "79350.5", StringComparison.Ordinal))]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Measurements.Should().ContainSingle().Which.Weight.Should().Be(79.3505m);
    }
    [Theory]
    [InlineData(2265, -2)]
    [InlineData(22650, -3)]
    public async Task SyncMeasurementsAsync_ConvertsFatPercentToRatio(int value, int unit)
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas("UTC", [Group(1, Jan1_0800Utc, Weight79_35, Measure(value, 6, unit))]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        var measurement = result.Measurements.Should().ContainSingle().Subject;
        measurement.Weight.Should().Be(79.35m);
        measurement.FatRatio.Should().Be(0.2265m);
    }

    [Theory]
    [InlineData("Europe/Paris", Jan1_0800Utc, "2024-01-01", "09:00:00")]
    [InlineData("America/New_York", Jan1_1200Utc, "2024-01-01", "07:00:00")]
    [InlineData("UTC", Jan1_0800Utc, "2024-01-01", "08:00:00")]
    public async Task SyncMeasurementsAsync_ConvertsTimestampToLocalDateAndTime(string timezone, long unixDate, string expectedDate, string expectedTime)
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas(timezone, [Group(1, unixDate, Weight79_35)]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        var measurement = result.Measurements.Should().ContainSingle().Subject;
        measurement.Date.Should().Be(expectedDate);
        measurement.Time.Should().Be(expectedTime);
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithoutFatMeasure_LeavesFatRatioNull()
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas("UTC", [Group(1, Jan1_0800Utc, Weight79_35)]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Measurements.Should().ContainSingle().Which.FatRatio.Should().BeNull();
    }

    #endregion

    #region Request shape

    [Fact]
    public async Task SyncMeasurementsAsync_SendsDocumentedGetMeasQueryWithBearerToken()
    {
        var userId = LinkUser(ProviderTokens.Valid("bearer-abc"));
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        MeasurePage(null, GetMeas("UTC", []));

        await _sut.SyncMeasurementsAsync(userId, true, startDate);

        var request = _http.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri.GetLeftPart(UriPartial.Path).Should().Be("https://wbsapi.withings.net/measure");
        request.AuthScheme.Should().Be("Bearer");
        request.AuthParameter.Should().Be("bearer-abc");

        var query = HttpUtility.ParseQueryString(request.Uri.Query);
        query["action"].Should().Be("getmeas");
        query["category"].Should().Be("1");
        query["meastypes"].Should().Be("1,6");
        query["startdate"].Should().Be(new DateTimeOffset(startDate).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        query.AllKeys.Should().NotContain("offset");
        _logs.ShouldNotMention("bearer-abc");
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithoutStartDate_RequestsAllTime()
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas("UTC", []));

        await _sut.SyncMeasurementsAsync(userId, true);

        var query = HttpUtility.ParseQueryString(_http.Requests.Single().Uri.Query);
        query["startdate"].Should().Be("1");
    }

    #endregion

    #region Pagination

    [Fact]
    public async Task SyncMeasurementsAsync_FollowsOffsetsUntilMoreIsZero_AndReturnsDescending()
    {
        var userId = LinkUser();
        // Page one is deliberately out of order to prove the page is sorted, and both
        // pages are newer than the next so the concatenation is descending overall.
        MeasurePage(null, GetMeas("UTC", [Group(1, Jan2_0700Utc, Measure(80000, 1, -3)), Group(2, Jan3_0700Utc, Measure(81000, 1, -3))], more: 1, offset: 42));
        MeasurePage("42", GetMeas("UTC", [Group(3, Jan1_0700Utc, Measure(79000, 1, -3))], more: 1, offset: 77));
        MeasurePage("77", GetMeas("UTC", []));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Provider.Should().Be("withings");
        result.Measurements.Should().Equal(
            new RawMeasurement { Date = "2024-01-03", Time = "07:00:00", Weight = 81m },
            new RawMeasurement { Date = "2024-01-02", Time = "07:00:00", Weight = 80m },
            new RawMeasurement { Date = "2024-01-01", Time = "07:00:00", Weight = 79m });
        RequestedOffsets().Should().Equal(null, "42", "77");
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithEmptyMiddlePage_ContinuesPagination()
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas("UTC", [Group(1, Jan2_0700Utc, Weight79_35)], more: 1, offset: 42));
        MeasurePage("42", GetMeas("UTC", [], more: 1, offset: 77));
        MeasurePage("77", GetMeas("UTC", [Group(2, Jan1_0700Utc, Weight79_35)]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Measurements.Should().HaveCount(2);
        RequestedOffsets().Should().Equal(null, "42", "77");
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithManyPages_FetchesEveryPageOnce()
    {
        const int pages = 10;
        var userId = LinkUser();
        _http.When(IsMeasureRequest, r =>
        {
            var offset = OffsetOf(r);
            var page = offset == null ? 1 : int.Parse(offset, CultureInfo.InvariantCulture) + 1;
            var group = Group(page, Jan1_0700Utc - (page - 1) * 86400, Weight79_35);
            var json = page < pages
                ? GetMeas("UTC", [group], more: 1, offset: page)
                : GetMeas("UTC", [group]);
            return RecordingHttpHandler.Json(HttpStatusCode.OK, json);
        });

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Measurements.Should().HaveCount(pages);
        RequestedOffsets().Should().Equal(Enumerable.Range(0, pages).Select(i => i == 0 ? null : i.ToString(CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(0, 2)]
    [InlineData(123, 2)]
    public async Task SyncMeasurementsAsync_WithInvalidPagination_FailsInsteadOfLooping(int? offset, int expectedCalls)
    {
        var userId = LinkUser();
        // more:1 with the same cursor forever; a null cursor is the fixture's more:1 page with offset removed
        var page = offset is null
            ? GetMeas("UTC", [], more: 1).Replace(",\"offset\":0", string.Empty, StringComparison.Ordinal)
            : GetMeas("UTC", [], more: 1, offset: offset.Value);
        _http.When(IsMeasureRequest, _ => RecordingHttpHandler.Json(HttpStatusCode.OK, page));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(ProviderSyncError.Unknown);
        result.Measurements.Should().BeNull();
        _http.Requests.Should().HaveCount(expectedCalls);
    }

    [Fact]
    public async Task GetMeasurementsAsync_WhenLaterPageReportsAuthError_ThrowsProviderAuthException()
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas("UTC", [Group(1, Jan2_0700Utc, Weight79_35)], more: 1, offset: 42));
        MeasurePage("42", Error(401, "Invalid access token"));

        var act = () => _sut.GetMeasurementsAsync(userId, true);

        await act.Should().ThrowAsync<ProviderAuthException>();
        _http.Requests.Should().HaveCount(2);
    }

    #endregion

    #region Resilience: tolerated payload shapes

    [Fact]
    public async Task SyncMeasurementsAsync_IgnoresDocumentedFieldsItDoesNotUse()
    {
        var userId = LinkUser();
        // Group() already carries hash_deviceid, modified and comment; measures gain algo and fm
        var group = Group(1, Jan1_0800Utc,
            Measure(79350, 1, -3, ",\"algo\":0,\"fm\":3"),
            Measure(2265, 6, -2, ",\"algo\":0,\"fm\":3"));
        MeasurePage(null, GetMeas("UTC", [group]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Measurements.Should().ContainSingle().Which.Should().Be(
            new RawMeasurement { Date = "2024-01-01", Time = "08:00:00", Weight = 79.35m, FatRatio = 0.2265m });
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithoutTimezone_UsesUtcWithoutWarning()
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas(null, [Group(1, Jan1_0800Utc, Weight79_35)]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        var measurement = result.Measurements.Should().ContainSingle().Subject;
        measurement.Date.Should().Be("2024-01-01");
        measurement.Time.Should().Be("08:00:00");
        _logs.ShouldNotHaveLogged(LogLevel.Warning);
    }

    [Fact]
    public async Task SyncMeasurementsAsync_SkipsGroupsWithoutWeight_AndKeepsTheRest()
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas("UTC",
        [
            Group(1, Jan2_0700Utc, Fat22_65),
            Group(2, Jan1_0700Utc, Weight79_35)
        ]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Measurements.Should().ContainSingle().Which.Should().Be(
            new RawMeasurement { Date = "2024-01-01", Time = "07:00:00", Weight = 79.35m });
    }

    [Fact]
    public async Task SyncMeasurementsAsync_IgnoresUnknownMeasureTypes()
    {
        var userId = LinkUser();
        var group = Group(1, Jan1_0800Utc,
            Measure(50000, 5, -3),   // fat free mass
            Measure(60000, 8, -3),   // fat mass weight
            Measure(40000, 76, -3),  // muscle mass
            Measure(30000, 77, -3),  // hydration
            Measure(2000, 88, -3),   // bone mass
            Weight79_35,
            Fat22_65);
        MeasurePage(null, GetMeas("UTC", [group]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Measurements.Should().ContainSingle().Which.Should().Be(
            new RawMeasurement { Date = "2024-01-01", Time = "08:00:00", Weight = 79.35m, FatRatio = 0.2265m });
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithGroupMissingMeasuresArray_SkipsThatGroup()
    {
        var userId = LinkUser();
        var groupWithoutMeasures = Group(1, Jan2_0700Utc).Replace("\"measures\":[],", string.Empty, StringComparison.Ordinal);
        groupWithoutMeasures.Should().NotContain("measures", "the fixture must have been reshaped");
        MeasurePage(null, GetMeas("UTC", [groupWithoutMeasures, Group(2, Jan1_0700Utc, Weight79_35)]));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Measurements.Should().ContainSingle().Which.Weight.Should().Be(79.35m);
    }

    [Fact]
    public async Task SyncMeasurementsAsync_WithNoGroups_SucceedsWithZeroMeasurements()
    {
        var userId = LinkUser();
        MeasurePage(null, GetMeas("UTC", []));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Measurements.Should().BeEmpty();
    }

    #endregion

    #region Resilience: failures

    [Fact]
    public async Task SyncMeasurementsAsync_WithSuccessStatusButNullBody_ReportsUnknownWithoutCrashing()
    {
        var userId = LinkUser();
        // Not a shape the docs describe, so WithingsPayloads has no builder for it
        MeasurePage(null, "{\"status\":0,\"body\":null}");

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(ProviderSyncError.Unknown);
        result.Measurements.Should().BeNull();
        _logs.ShouldHaveLogged(LogLevel.Error, "without a body");
        _logs.Entries.Should().NotContain(e => e.Exception is NullReferenceException);
        VerifyNothingStored();
    }

    [Theory]
    [InlineData(401, "Unauthorized", ProviderSyncError.AuthFailed)]
    [InlineData(601, "Same arguments in less than 10 seconds", ProviderSyncError.Unknown)]
    [InlineData(2555, "Unknown error", ProviderSyncError.Unknown)]
    public async Task SyncMeasurementsAsync_WithApiStatusInBody_MapsToSyncError(int status, string error, ProviderSyncError expected)
    {
        var userId = LinkUser();
        MeasurePage(null, Error(status, error));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(expected);
        result.Measurements.Should().BeNull();
        VerifyNothingStored();
    }

    [Fact]
    public async Task GetMeasurementsAsync_With503InvalidRefreshToken_ThrowsProviderAuthException()
    {
        var userId = LinkUser();
        MeasurePage(null, Error(503, "Invalid Params: invalid refresh_token"));

        var act = () => _sut.GetMeasurementsAsync(userId, true);

        await act.Should().ThrowAsync<ProviderAuthException>().WithMessage("*invalid refresh_token*");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ProviderSyncError.AuthFailed)]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderSyncError.NetworkError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ProviderSyncError.NetworkError)]
    [InlineData(HttpStatusCode.InternalServerError, ProviderSyncError.Unknown)]
    public async Task SyncMeasurementsAsync_WithHttpFailure_MapsStatusToSyncError(HttpStatusCode status, ProviderSyncError expected)
    {
        var userId = LinkUser();
        _http.WhenUrlContains(MeasureUrl, status, Error((int)status, status.ToString()));

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(expected);
        result.Measurements.Should().BeNull();
        _http.Requests.Should().ContainSingle("a failed page must not be retried in a loop");
        VerifyNothingStored();
    }

    [Theory]
    [MemberData(nameof(UnparseableBodies))]
    public async Task SyncMeasurementsAsync_WithUnparseableBody_ReportsUnknown(string caseName, string body)
    {
        var userId = LinkUser();
        MeasurePage(null, body);

        var result = await _sut.SyncMeasurementsAsync(userId, true);

        result.Success.Should().BeFalse(caseName);
        result.Error.Should().Be(ProviderSyncError.Unknown, caseName);
        result.Measurements.Should().BeNull(caseName);
        VerifyNothingStored();
    }

    public static TheoryData<string, string> UnparseableBodies => new()
    {
        { "empty body", string.Empty },
        { "truncated JSON", GetMeas("UTC", [Group(1, Jan1_0800Utc, Weight79_35)])[..40] }
    };

    #endregion

    #region Authorization code exchange

    [Fact]
    public void ProviderName_ReturnsWithings()
    {
        _sut.ProviderName.Should().Be("withings");
    }

    [Fact]
    public void GetAuthorizationUrl_ReturnsWithingsAuthorizeUrlWithEncodedCallback()
    {
        var result = _sut.GetAuthorizationUrl("test-state", Callback);

        result.Should().StartWith("https://account.withings.com/oauth2_user/authorize2");
        var query = HttpUtility.ParseQueryString(new Uri(result).Query);
        query["client_id"].Should().Be(ClientId);
        query["response_type"].Should().Be("code");
        query["scope"].Should().Be("user.metrics");
        query["state"].Should().Be("test-state");
        query["redirect_uri"].Should().Be(Callback);
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_PostsDocumentedFormBody()
    {
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK, Token());

        var result = await _sut.ExchangeAuthorizationCodeAsync("auth-code-1", Callback, Guid.NewGuid());

        result.Should().BeTrue();
        var request = _http.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.ToString().Should().Be(TokenUrl);
        var form = HttpUtility.ParseQueryString(request.Body!);
        form["action"].Should().Be("requesttoken");
        form["grant_type"].Should().Be("authorization_code");
        form["client_id"].Should().Be(ClientId);
        form["client_secret"].Should().Be(ClientSecret);
        form["code"].Should().Be("auth-code-1");
        form["redirect_uri"].Should().Be(Callback);
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_StoresTokenInOurShape()
    {
        var userId = Guid.NewGuid();
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK, Token("acc-1", "ref-1", 10800));
        Dictionary<string, object>? stored = null;
        _providerLinkServiceMock
            .Setup(x => x.StoreProviderLinkAsync(userId, "withings", It.IsAny<Dictionary<string, object>>(), null))
            .Callback<Guid, string, Dictionary<string, object>, string?>((_, _, token, _) => stored = token)
            .Returns(Task.CompletedTask);
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await _sut.ExchangeAuthorizationCodeAsync("code", Callback, userId);

        result.Should().BeTrue();
        stored.Should().NotBeNull();
        stored!.Keys.Should().BeEquivalentTo("access_token", "refresh_token", "token_type", "scope", "received_at", "expires_in");
        stored["access_token"].Should().Be("acc-1");
        stored["refresh_token"].Should().Be("ref-1");
        stored["token_type"].Should().Be("Bearer");
        stored["scope"].Should().Be("user.metrics");
        stored["expires_in"].Should().Be(10800);
        stored["received_at"].Should().BeOfType<long>().Which.Should().BeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_WithIncompleteToken_DoesNotStoreCredentials()
    {
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK, Token(includeRefresh: false));

        var act = () => _sut.ExchangeAuthorizationCodeAsync("code", Callback, Guid.NewGuid());

        await act.Should().ThrowAsync<JsonException>();
        VerifyNothingStored();
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_DoesNotLogProviderCredentials()
    {
        const string accessToken = "secret-access-token-never-log";
        const string refreshToken = "secret-refresh-token-never-log";
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK, Token(accessToken, refreshToken));

        var result = await _sut.ExchangeAuthorizationCodeAsync("code", Callback, Guid.NewGuid());

        result.Should().BeTrue();
        _logs.ShouldNotMention(accessToken);
        _logs.ShouldNotMention(refreshToken);
    }

    [Theory]
    [InlineData(503, "Invalid Params: invalid code")]
    [InlineData(401, "Unauthorized")]
    [InlineData(2555, "Unknown error")]
    public async Task ExchangeAuthorizationCodeAsync_WhenWithingsRejectsCode_ThrowsInvalidCodeAsBadRequest(int status, string error)
    {
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK, Error(status, error));

        var act = () => _sut.ExchangeAuthorizationCodeAsync("stale-code", Callback, Guid.NewGuid());

        var thrown = await act.Should().ThrowAsync<ProviderException>();
        thrown.Which.ErrorCode.Should().Be("INVALID_CODE");
        thrown.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        thrown.Which.IsRetryable.Should().BeFalse();
        VerifyNothingStored();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_CODE", false)]
    [InlineData(HttpStatusCode.Unauthorized, "UNAUTHORIZED", false)]
    [InlineData(HttpStatusCode.TooManyRequests, "RATE_LIMITED", true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "SERVICE_UNAVAILABLE", true)]
    [InlineData(HttpStatusCode.InternalServerError, "UNEXPECTED_ERROR", false)]
    public async Task ExchangeAuthorizationCodeAsync_WithHttpFailure_MapsStatusWithoutStoring(HttpStatusCode status, string errorCode, bool retryable)
    {
        _http.WhenUrlContains(TokenUrl, status, Error((int)status, status.ToString()));

        var act = () => _sut.ExchangeAuthorizationCodeAsync("code", Callback, Guid.NewGuid());

        var thrown = await act.Should().ThrowAsync<ProviderException>();
        thrown.Which.ErrorCode.Should().Be(errorCode);
        thrown.Which.StatusCode.Should().Be(status);
        thrown.Which.IsRetryable.Should().Be(retryable);
        VerifyNothingStored();
    }

    [Theory]
    [MemberData(nameof(MalformedTokenBodies))]
    public async Task ExchangeAuthorizationCodeAsync_WithMalformedBody_LogsAndThrowsWithoutStoring(string caseName, string body)
    {
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK, body);

        var act = () => _sut.ExchangeAuthorizationCodeAsync("code", Callback, Guid.NewGuid());

        await act.Should().ThrowAsync<JsonException>(caseName);
        _logs.ShouldHaveLogged(LogLevel.Error, "Failed to deserialize Withings token response");
        VerifyNothingStored();
    }

    public static TheoryData<string, string> MalformedTokenBodies => new()
    {
        { "empty body", string.Empty },
        { "truncated JSON", Token()[..30] },
        { "HTML error page", "<html><body>Bad Gateway</body></html>" }
    };

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_WhenDuplicateRequestAndLinkExists_ReturnsTrueWithoutStoring()
    {
        var userId = LinkUser();
        _http.WhenUrlContains(TokenUrl, HttpStatusCode.OK, Error(601, "Same arguments in less than 10 seconds"));

        var result = await _sut.ExchangeAuthorizationCodeAsync("reused-code", Callback, userId);

        result.Should().BeTrue();
        VerifyNothingStored();
    }

    #endregion

    #region Helpers

    private Guid LinkUser(Dictionary<string, object>? token = null)
    {
        var userId = Guid.NewGuid();
        _providerLinkServiceMock.Setup(x => x.GetProviderLinkAsync(userId, "withings"))
            .ReturnsAsync(new DbProviderLink { Uid = userId, Provider = "withings", Token = token ?? ProviderTokens.Valid() });
        return userId;
    }

    private static bool IsMeasureRequest(HttpRequestMessage request)
        => request.RequestUri!.ToString().Contains(MeasureUrl, StringComparison.Ordinal);

    private static string? OffsetOf(HttpRequestMessage request)
        => HttpUtility.ParseQueryString(request.RequestUri!.Query)["offset"];

    /// <summary>Serves <paramref name="json"/> for the getmeas request carrying <paramref name="offset"/> (null for the first page).</summary>
    private void MeasurePage(string? offset, string json)
    {
        _http.When(r => IsMeasureRequest(r) && OffsetOf(r) == offset, _ => RecordingHttpHandler.Json(HttpStatusCode.OK, json));
    }

    private IEnumerable<string?> RequestedOffsets()
        => _http.Requests.Select(r => HttpUtility.ParseQueryString(r.Uri.Query)["offset"]);

    private void VerifyNothingStored()
    {
        _providerLinkServiceMock.Verify(x => x.StoreProviderLinkAsync(It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(), It.IsAny<string?>()), Times.Never);
    }

    #endregion
}
