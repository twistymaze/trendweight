using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TrendWeight.Features.Measurements;
using TrendWeight.Features.Measurements.Models;
using TrendWeight.Features.Profile.Services;
using TrendWeight.Infrastructure.DataAccess.Models;
using TrendWeight.Features.Profile.Models;
using TrendWeight.Common.Models;
using TrendWeight.Tests.Fixtures;
using Xunit;
using TrendWeight.Features.Common;

namespace TrendWeight.Tests.Features.Measurements.Controllers;

public class MeasurementsControllerTests
{
    // A realistic-looking bearer secret; nothing the controller logs may contain it.
    private const string Code = "shr-secret-0123456789abc";

    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<IMeasurementOrchestrationService> _orchestrationServiceMock;
    private readonly CurrentRequestContext _requestContext;
    private readonly CapturingLoggerProvider _logs;
    private readonly MeasurementsController _sut;

    public MeasurementsControllerTests()
    {
        _profileServiceMock = new Mock<IProfileService>();
        _orchestrationServiceMock = new Mock<IMeasurementOrchestrationService>();
        _requestContext = new CurrentRequestContext();
        _logs = new CapturingLoggerProvider();

        _sut = new MeasurementsController(
            _profileServiceMock.Object,
            _orchestrationServiceMock.Object,
            _logs.CreateLogger<MeasurementsController>(),
            _requestContext);
    }

    #region GetMeasurements Tests

    [Fact]
    public async Task GetMeasurements_WithValidUser_ReturnsDataWithProviderStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dataResult = CreateDataResult(userId);

        SetupAuthenticatedUser(userId.ToString());
        _orchestrationServiceMock.Setup(x => x.GetForUserAsync(userId, null)).ReturnsAsync(dataResult);

        // Act
        var result = await _sut.GetMeasurements();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<MeasurementsResponse>().Subject;
        response.IsMe.Should().Be(true);
        response.ComputedMeasurements.Should().BeSameAs(dataResult.ComputedMeasurements);
        response.SourceData.Should().BeNull(); // Default includeSource=false
        response.ProviderStatus.Should().ContainKey("withings");
    }

    [Fact]
    public async Task GetMeasurements_WithIncludeSource_ReturnsSourceData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dataResult = CreateDataResult(userId);

        SetupAuthenticatedUser(userId.ToString());
        _orchestrationServiceMock.Setup(x => x.GetForUserAsync(userId, null)).ReturnsAsync(dataResult);

        // Act
        var result = await _sut.GetMeasurements(includeSource: true);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<MeasurementsResponse>().Subject;
        response.SourceData.Should().BeSameAs(dataResult.SourceData);
    }

    [Fact]
    public async Task GetMeasurements_PassesProgressIdToOrchestration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var progressId = Guid.NewGuid();
        var dataResult = CreateDataResult(userId);

        SetupAuthenticatedUser(userId.ToString());
        _orchestrationServiceMock.Setup(x => x.GetForUserAsync(userId, progressId)).ReturnsAsync(dataResult);

        // Act
        var result = await _sut.GetMeasurements(progressId: progressId.ToString());

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _orchestrationServiceMock.Verify(x => x.GetForUserAsync(userId, progressId), Times.Once);
    }

    [Fact]
    public async Task GetMeasurements_WithMalformedProgressId_PassesNullToOrchestration()
    {
        // Arrange - progress reporting is best effort; a bad id must not block the data
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString());
        _orchestrationServiceMock.Setup(x => x.GetForUserAsync(userId, null)).ReturnsAsync(CreateDataResult(userId));

        // Act
        var result = await _sut.GetMeasurements(progressId: "nope");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _orchestrationServiceMock.Verify(x => x.GetForUserAsync(userId, null), Times.Once);
        _orchestrationServiceMock.Verify(x => x.GetForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Once);
    }

    [Fact]
    public async Task GetMeasurements_WithNoUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        SetupAuthenticatedUser(null);

        // Act
        var result = await _sut.GetMeasurements();

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User ID not found in token");
    }

    [Fact]
    public async Task GetMeasurements_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString());
        _orchestrationServiceMock.Setup(x => x.GetForUserAsync(userId, null))
            .ReturnsAsync((MeasurementDataResult?)null);

        // Act
        var result = await _sut.GetMeasurements();

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User not found");
    }

    [Fact]
    public async Task GetMeasurements_WhenExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString());
        _orchestrationServiceMock.Setup(x => x.GetForUserAsync(userId, It.IsAny<Guid?>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _sut.GetMeasurements();

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetMeasurementsBySharingCode Tests

    [Fact]
    public async Task GetMeasurementsBySharingCode_WithValidCode_ReturnsDataWithoutProviderStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dataResult = CreateDataResult(userId);
        var user = dataResult.Profile;
        user.Profile.SharingEnabled = true;
        user.Profile.SharingToken = Code;

        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(Code)).ReturnsAsync(user);
        _orchestrationServiceMock.Setup(x => x.GetForProfileAsync(user)).ReturnsAsync(dataResult);

        // Act
        var result = await _sut.GetMeasurementsBySharingCode(Code);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<MeasurementsResponse>().Subject;
        response.IsMe.Should().Be(false);
        response.ComputedMeasurements.Should().NotBeNull();
        response.SourceData.Should().BeNull(); // Default includeSource=false
        response.ProviderStatus.Should().BeNull(); // No provider status for shared view
        _logs.ShouldHaveLogged(LogLevel.Information, "via sharing code");
        _logs.ShouldNotMention(Code);
    }

    [Fact]
    public async Task GetMeasurementsBySharingCode_WithProgressId_SetsRequestContext()
    {
        // Arrange - the sync pipeline reads the progress id from the scoped request context
        var progressId = Guid.NewGuid();
        var dataResult = CreateDataResult(Guid.NewGuid());
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(Code)).ReturnsAsync(dataResult.Profile);
        _orchestrationServiceMock.Setup(x => x.GetForProfileAsync(dataResult.Profile)).ReturnsAsync(dataResult);

        // Act
        var result = await _sut.GetMeasurementsBySharingCode(Code, progressId: progressId.ToString());

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _requestContext.ProgressId.Should().Be(progressId);
    }

    [Fact]
    public async Task GetMeasurementsBySharingCode_WithMalformedProgressId_LeavesRequestContextUnset()
    {
        // Arrange
        var dataResult = CreateDataResult(Guid.NewGuid());
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(Code)).ReturnsAsync(dataResult.Profile);
        _orchestrationServiceMock.Setup(x => x.GetForProfileAsync(dataResult.Profile)).ReturnsAsync(dataResult);

        // Act
        var result = await _sut.GetMeasurementsBySharingCode(Code, progressId: "nope");

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _requestContext.ProgressId.Should().BeNull();
    }

    [Fact]
    public async Task GetMeasurementsBySharingCode_WithSinceFilter_FiltersMeasurements()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharingCode = "test-sharing-code";
        var dataResult = CreateDataResult(userId, computedMeasurements: new List<ComputedMeasurement>
        {
            CreateComputedMeasurement("2024-01-01"),
            CreateComputedMeasurement("2024-06-15"),
            CreateComputedMeasurement("2024-12-31")
        });
        var user = dataResult.Profile;
        user.Profile.SharingEnabled = true;

        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(sharingCode)).ReturnsAsync(user);
        _orchestrationServiceMock.Setup(x => x.GetForProfileAsync(user)).ReturnsAsync(dataResult);

        // Act
        var result = await _sut.GetMeasurementsBySharingCode(sharingCode, since: "2024-06-15");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<MeasurementsResponse>().Subject;
        response.ComputedMeasurements.Should().HaveCount(2);
        response.ComputedMeasurements.Select(m => m.Date).Should().Equal("2024-06-15", "2024-12-31");
    }

    [Theory]
    [InlineData(null, "2024-06-15", "2024-06-16")]
    [InlineData("2024-06-16", "2024-06-16", null)]
    public async Task GetMeasurementsBySharingCode_RawDataRespectsHiddenHistoryAndSince(
        string? since, string firstDate, string? secondDate)
    {
        var dataResult = CreateDataResult(Guid.NewGuid());
        var user = dataResult.Profile;
        user.Profile.HideDataBeforeStart = true;
        user.Profile.GoalStart = new DateTime(2024, 6, 15);
        user.Profile.DayStartOffset = 4;
        var originalReadings = new List<RawMeasurement>
        {
            new() { Date = "2024-06-14", Time = "12:00:00", Weight = 80m },
            new() { Date = "2024-06-15", Time = "03:59:59", Weight = 81m },
            new() { Date = "2024-06-15", Time = "04:00:00", Weight = 82m },
            new() { Date = "2024-06-16", Time = "08:00:00", Weight = 83m }
        };
        dataResult.SourceData[0].Measurements = originalReadings;
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync("code")).ReturnsAsync(user);
        _orchestrationServiceMock.Setup(x => x.GetForProfileAsync(user)).ReturnsAsync(dataResult);

        var result = await _sut.GetMeasurementsBySharingCode("code", includeSource: true, since: since);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<MeasurementsResponse>().Subject;
        var readings = response.SourceData!.Single().Measurements!;
        readings.Select(m => m.Date).Should().Equal(
            secondDate == null ? new[] { firstDate } : new[] { firstDate, secondDate });
        readings.Should().NotContain(m => m.Weight == 81m);
        originalReadings.Should().HaveCount(4, "filtering public exports must not modify the owner's data");
    }

    [Fact]
    public async Task GetMeasurementsBySharingCode_WithInvalidSince_ReturnsBadRequest()
    {
        // Act
        var result = await _sut.GetMeasurementsBySharingCode("code", since: "not-a-date");

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMeasurementsBySharingCode_WhenUserNotFound_ReturnsNotFoundWithoutLoggingTheCode()
    {
        // Arrange
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(Code)).ReturnsAsync((DbProfile?)null);

        // Act
        var result = await _sut.GetMeasurementsBySharingCode(Code);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User not found");
        _logs.ShouldHaveLogged(LogLevel.Warning, "unknown sharing code");
        _logs.ShouldNotMention(Code);
    }

    [Fact]
    public async Task GetMeasurementsBySharingCode_WhenSharingDisabled_ReturnsNotFoundWithoutLoggingTheCode()
    {
        // Arrange - a disabled code can be re-enabled later, so it is still a secret
        var user = CreateTestProfile(Guid.NewGuid());
        user.Profile.SharingEnabled = false;
        user.Profile.SharingToken = Code;

        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(Code)).ReturnsAsync(user);

        // Act
        var result = await _sut.GetMeasurementsBySharingCode(Code);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        _orchestrationServiceMock.Verify(x => x.GetForProfileAsync(It.IsAny<DbProfile>()), Times.Never);
        _logs.ShouldHaveLogged(LogLevel.Warning, "sharing is disabled");
        _logs.ShouldNotMention(Code);
    }

    [Fact]
    public async Task GetMeasurementsBySharingCode_WhenLookupThrows_ReturnsInternalServerErrorWithoutLoggingTheCode()
    {
        // Arrange
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _sut.GetMeasurementsBySharingCode(Code);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
        _logs.ShouldHaveLogged(LogLevel.Error, "Error getting measurements");
        _logs.ShouldNotMention(Code);
    }

    [Fact]
    public async Task GetMeasurementsBySharingCode_WhenOrchestrationThrows_ReturnsInternalServerErrorWithoutLoggingTheCode()
    {
        // Arrange - the failure happens after the code resolved to a user, so the
        // error log has the user in hand and must still not fall back to the code
        var user = CreateTestProfile(Guid.NewGuid());
        user.Profile.SharingToken = Code;
        var failure = new InvalidOperationException("Provider sync failed");
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(Code)).ReturnsAsync(user);
        _orchestrationServiceMock.Setup(x => x.GetForProfileAsync(user)).ThrowsAsync(failure);

        // Act
        var result = await _sut.GetMeasurementsBySharingCode(Code);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
        _logs.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error)
            .Which.Exception.Should().BeSameAs(failure);
        _logs.ShouldNotMention(Code);
    }

    #endregion

    #region Helper Methods

    private void SetupAuthenticatedUser(string? userId)
    {
        var claims = new List<Claim>();
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static DbProfile CreateTestProfile(Guid userId)
    {
        return new DbProfile
        {
            Uid = userId,
            Email = "test@example.com",
            Profile = new ProfileData
            {
                FirstName = "Test User",
                UseMetric = false,
                SharingToken = "test-token",
                SharingEnabled = true
            }
        };
    }

    private static ComputedMeasurement CreateComputedMeasurement(string date)
    {
        return new ComputedMeasurement
        {
            Date = date,
            ActualWeight = 75.5m,
            TrendWeight = 75.5m,
            WeightIsInterpolated = false,
            FatIsInterpolated = false
        };
    }

    private static MeasurementDataResult CreateDataResult(Guid userId, List<ComputedMeasurement>? computedMeasurements = null)
    {
        var sourceData = new List<SourceData>
        {
            new SourceData
            {
                Source = "withings",
                LastUpdate = DateTime.UtcNow,
                Measurements = new List<RawMeasurement>
                {
                    new RawMeasurement
                    {
                        Date = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
                        Time = "08:00:00",
                        Weight = 75.5m,
                        FatRatio = 0.225m
                    }
                }
            }
        };

        return new MeasurementDataResult(
            CreateTestProfile(userId),
            computedMeasurements ?? new List<ComputedMeasurement>(),
            sourceData,
            new Dictionary<string, ProviderSyncStatus>
            {
                { "withings", new ProviderSyncStatus { Success = true } }
            });
    }

    #endregion
}
