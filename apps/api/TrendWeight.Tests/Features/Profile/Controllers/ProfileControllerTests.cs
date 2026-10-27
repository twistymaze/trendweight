using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TrendWeight.Features.Profile;
using TrendWeight.Features.Profile.Models;
using TrendWeight.Common.Models;
using TrendWeight.Features.Profile.Services;
using TrendWeight.Features.Measurements;
using TrendWeight.Infrastructure.DataAccess.Models;
using TrendWeight.Tests.Fixtures;
using Xunit;

namespace TrendWeight.Tests.Features.Profile.Controllers;

public class ProfileControllerTests
{
    // A realistic-looking bearer secret; nothing the controller logs may contain it.
    private const string Code = "shr-secret-0123456789abc";

    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<ILegacyMigrationService> _migrationServiceMock;
    private readonly CapturingLoggerProvider _logs;
    private readonly ProfileController _sut;

    public ProfileControllerTests()
    {
        _profileServiceMock = new Mock<IProfileService>();
        _migrationServiceMock = new Mock<ILegacyMigrationService>();
        _logs = new CapturingLoggerProvider();
        _sut = new ProfileController(
            _profileServiceMock.Object,
            _migrationServiceMock.Object,
            _logs.CreateLogger<ProfileController>());
    }

    #region GetProfile Tests

    [Fact]
    public async Task GetProfile_WithValidUser_ReturnsProfileData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _sut.GetProfile();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProfileResponse>().Subject;

        response.User.FirstName.Should().Be("Test User");
        response.IsMe.Should().Be(true);
    }

    [Fact]
    public async Task GetProfile_WithNoUserIdClaim_ThrowsUnauthorized()
    {
        SetupAuthenticatedUser(null, "test@example.com");

        var act = () => _sut.GetProfile();

        // The error middleware turns this into a 401
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetProfile_WhenUserNotFound_ChecksMigrationAndReturnsProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var migratedProfile = CreateTestProfile(userId);
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((DbProfile?)null);
        _migrationServiceMock.Setup(x => x.CheckAndMigrateIfNeededAsync(userId, "test@example.com"))
            .ReturnsAsync(migratedProfile);

        // Act
        var result = await _sut.GetProfile();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProfileResponse>().Subject;

        response.User.FirstName.Should().Be("Test User");
        response.IsMe.Should().Be(true);
    }

    [Fact]
    public async Task GetProfile_WhenUserNotFoundAndNoMigration_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((DbProfile?)null);
        _migrationServiceMock.Setup(x => x.CheckAndMigrateIfNeededAsync(userId, "test@example.com"))
            .ReturnsAsync((DbProfile?)null);

        // Act
        var result = await _sut.GetProfile();

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User not found");
    }

    [Fact]
    public async Task GetProfile_MapsEveryProfileField()
    {
        var userId = Guid.NewGuid();
        var user = CreateFullyPopulatedProfile(userId);
        SetupAuthenticatedUser(userId.ToString(), user.Email);
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _sut.GetProfile();

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<ProfileResponse>().Subject;
        response.User.Should().BeEquivalentTo(ExpectedFullyPopulatedUser());
        response.IsMe.Should().BeTrue();
        response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetProfile_WithNullOptionalFields_MapsDefaults()
    {
        // Nullable storage fields become concrete API values, and an unrecognised
        // preset id (written by a newer version) resolves to the default preset
        var userId = Guid.NewGuid();
        var user = new DbProfile
        {
            Uid = userId,
            Email = "test@example.com",
            Profile = new ProfileData
            {
                FirstName = "Sparse",
                GoalStart = null,
                GoalWeight = null,
                PlannedPoundsPerWeek = null,
                DayStartOffset = null,
                ShowCalories = null,
                SharingToken = null,
                TrendAlgorithm = "not-a-real-preset"
            }
        };
        SetupAuthenticatedUser(userId.ToString(), user.Email);
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _sut.GetProfile();

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<ProfileResponse>().Subject;
        response.User.Should().BeEquivalentTo(new UserProfileData
        {
            FirstName = "Sparse",
            GoalStart = null,
            GoalWeight = null,
            PlannedPoundsPerWeek = null,
            DayStartOffset = 0,
            UseMetric = false,
            ShowCalories = false,
            HideDataBeforeStart = false,
            TrendAlgorithm = TrendAlgorithmPresets.Resolve(null).Id,
            SharingEnabled = false,
            SharingToken = null,
            IsMigrated = false,
            IsNewlyMigrated = false
        });
    }

    [Fact]
    public async Task GetProfile_WhenClerkEmailDiffers_UpdatesStoredEmailThroughProfileService()
    {
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        user.Email = "old@example.com";
        SetupAuthenticatedUser(userId.ToString(), "new@example.com");
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
        _profileServiceMock.Setup(x => x.UpdateAsync(It.IsAny<DbProfile>())).ReturnsAsync((DbProfile p) => p);

        var result = await _sut.GetProfile();

        result.Result.Should().BeOfType<OkObjectResult>();
        _profileServiceMock.Verify(x => x.UpdateAsync(It.Is<DbProfile>(p => p.Uid == userId && p.Email == "new@example.com")), Times.Once);
    }

    [Fact]
    public async Task GetProfile_WhenClerkEmailMatches_DoesNotWrite()
    {
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        SetupAuthenticatedUser(userId.ToString(), user.Email);
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        await _sut.GetProfile();

        _profileServiceMock.Verify(x => x.UpdateAsync(It.IsAny<DbProfile>()), Times.Never);
    }

    [Fact]
    public async Task GetProfile_WithMigratedUser_ChecksForLegacyData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        user.Profile.IsMigrated = true;
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _sut.GetProfile();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;

        // Verify legacy check was made
        _migrationServiceMock.Verify(x => x.CheckAndMigrateLegacyDataIfNeededAsync(userId, "test@example.com"), Times.Once);
    }

    [Fact]
    public async Task GetProfile_WithNonMigratedUser_SkipsLegacyCheck()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        user.Profile.IsMigrated = false; // Not a migrated user
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _sut.GetProfile();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;

        // Verify no legacy check was made
        _migrationServiceMock.Verify(x => x.CheckAndMigrateLegacyDataIfNeededAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetProfile_WithMigratedUserAndNoEmail_DelegatesNullEmailToLegacyImport()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        user.Profile.IsMigrated = true;
        SetupAuthenticatedUser(userId.ToString(), null); // No email in claims
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _sut.GetProfile();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;

        // The migration service decides what a missing email means; the controller just passes it on
        _migrationServiceMock.Verify(x => x.CheckAndMigrateLegacyDataIfNeededAsync(userId, null), Times.Once);
    }

    #endregion

    #region GetProfileBySharingCode Tests

    [Fact]
    public async Task GetProfileBySharingCode_WithValidCodeAndSharingEnabled_ReturnsProfile()
    {
        // Arrange
        var user = CreateTestProfile(Guid.NewGuid());
        user.Profile.SharingEnabled = true;
        user.Profile.SharingToken = Code;
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(Code)).ReturnsAsync(user);

        // Act
        var result = await _sut.GetProfileBySharingCode(Code);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProfileResponse>().Subject;

        response.IsMe.Should().Be(false); // Always false for sharing code
        response.User.SharingEnabled.Should().Be(true);
        _logs.ShouldNotMention(Code);
    }

    [Fact]
    public async Task GetProfileBySharingCode_MapsEveryProfileFieldWithIsMeFalse()
    {
        // The owner previewing their own share link still gets isMe = false
        var user = CreateFullyPopulatedProfile(Guid.NewGuid());
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(user.Profile.SharingToken!)).ReturnsAsync(user);

        var result = await _sut.GetProfileBySharingCode(user.Profile.SharingToken!);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<ProfileResponse>().Subject;
        response.User.Should().BeEquivalentTo(ExpectedFullyPopulatedUser());
        response.IsMe.Should().BeFalse();
        response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetProfileBySharingCode_WhenUserNotFound_ReturnsNotFoundWithoutLoggingTheCode()
    {
        // Arrange
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(Code)).ReturnsAsync((DbProfile?)null);

        // Act
        var result = await _sut.GetProfileBySharingCode(Code);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User not found");
        _logs.ShouldHaveLogged(LogLevel.Warning, "sharing code");
        _logs.ShouldNotMention(Code);
    }

    [Fact]
    public async Task GetProfileBySharingCode_WhenSharingDisabled_ReturnsNotFoundWithoutLoggingTheCode()
    {
        // Arrange - a disabled code can be re-enabled later, so it is still a secret
        var user = CreateTestProfile(Guid.NewGuid());
        user.Profile.SharingEnabled = false;
        user.Profile.SharingToken = Code;
        _profileServiceMock.Setup(x => x.GetBySharingTokenAsync(Code)).ReturnsAsync(user);

        // Act
        var result = await _sut.GetProfileBySharingCode(Code);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User not found");
        _logs.ShouldHaveLogged(LogLevel.Warning, "sharing code");
        _logs.ShouldNotMention(Code);
    }

    #endregion

    #region UpdateProfile Tests

    [Fact]
    public async Task UpdateProfile_WithValidRequest_ReturnsUpdatedProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest
        {
            FirstName = "Updated Name",
            GoalWeight = 75.5m,
            UseMetric = true
        };
        var updatedProfile = CreateTestProfile(userId);
        updatedProfile.Profile.FirstName = request.FirstName;
        updatedProfile.Profile.GoalWeight = request.GoalWeight;
        updatedProfile.Profile.UseMetric = request.UseMetric.Value;

        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.UpdateOrCreateProfileAsync(userId, "test@example.com", request))
            .ReturnsAsync(updatedProfile);

        // Act
        var result = await _sut.UpdateProfile(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProfileResponse>().Subject;

        response.User.FirstName.Should().Be("Updated Name");
        response.User.GoalWeight.Should().Be(75.5m);
        response.User.UseMetric.Should().Be(true);
        response.IsMe.Should().Be(true);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task UpdateProfile_WithInvalidDayStart_RejectsBeforeSaving(int offset)
    {
        SetupAuthenticatedUser(Guid.NewGuid().ToString(), "test@example.com");

        var result = await _sut.UpdateProfile(new UpdateProfileRequest { DayStartOffset = offset });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _profileServiceMock.Verify(x => x.UpdateOrCreateProfileAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<UpdateProfileRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WithZeroGoalWeight_StoresItAsUnset()
    {
        // Regression: accounts migrated before unset goals were kept as null carry a zero
        // goal weight, and the settings form submits it unchanged with every save.
        var userId = Guid.NewGuid();
        var updatedProfile = CreateTestProfile(userId);
        updatedProfile.Profile.GoalWeight = null;
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.UpdateOrCreateProfileAsync(userId, "test@example.com",
                It.Is<UpdateProfileRequest>(r => r.GoalWeight == null && r.FirstName == "Renamed")))
            .ReturnsAsync(updatedProfile);

        var result = await _sut.UpdateProfile(new UpdateProfileRequest { FirstName = "Renamed", GoalWeight = 0 });

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ProfileResponse>()
            .Which.User.GoalWeight.Should().BeNull();
        _profileServiceMock.Verify(x => x.UpdateOrCreateProfileAsync(userId, "test@example.com",
            It.Is<UpdateProfileRequest>(r => r.GoalWeight == null)), Times.Once);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(-70)]
    [InlineData(1500)]
    [InlineData(1_000_000)]
    public async Task UpdateProfile_WithOutOfRangeGoalWeight_RejectsBeforeSaving(decimal goalWeight)
    {
        SetupAuthenticatedUser(Guid.NewGuid().ToString(), "test@example.com");

        var result = await _sut.UpdateProfile(new UpdateProfileRequest { GoalWeight = goalWeight });

        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Contain("Goal weight");
        _profileServiceMock.Verify(x => x.UpdateOrCreateProfileAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<UpdateProfileRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(-5.5)]
    [InlineData(5.5)]
    [InlineData(100)]
    public async Task UpdateProfile_WithOutOfRangePlannedChange_RejectsBeforeSaving(decimal plan)
    {
        SetupAuthenticatedUser(Guid.NewGuid().ToString(), "test@example.com");

        var result = await _sut.UpdateProfile(new UpdateProfileRequest { PlannedPoundsPerWeek = plan });

        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Contain("Planned weekly change");
        _profileServiceMock.Verify(x => x.UpdateOrCreateProfileAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<UpdateProfileRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WithOverlongFirstName_RejectsBeforeSaving()
    {
        SetupAuthenticatedUser(Guid.NewGuid().ToString(), "test@example.com");

        var result = await _sut.UpdateProfile(new UpdateProfileRequest { FirstName = new string('a', 101) });

        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Contain("First name");
        _profileServiceMock.Verify(x => x.UpdateOrCreateProfileAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<UpdateProfileRequest>()), Times.Never);
    }

    public static TheoryData<DateTime> OutOfRangeGoalStarts => new()
    {
        new DateTime(1899, 12, 31),
        DateTime.UtcNow.Date.AddDays(2),
        new DateTime(2999, 1, 1)
    };

    [Theory]
    [MemberData(nameof(OutOfRangeGoalStarts))]
    public async Task UpdateProfile_WithOutOfRangeGoalStart_RejectsBeforeSaving(DateTime goalStart)
    {
        SetupAuthenticatedUser(Guid.NewGuid().ToString(), "test@example.com");

        var result = await _sut.UpdateProfile(new UpdateProfileRequest { GoalStart = goalStart });

        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Contain("Start date");
        _profileServiceMock.Verify(x => x.UpdateOrCreateProfileAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<UpdateProfileRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WithBoundaryValues_PassesThroughToService()
    {
        // Unusual but legitimate values: a large goal in pounds, the steepest plan the UI
        // offers, a 100-character name, tomorrow's date (timezones ahead of UTC), and 1900.
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest
        {
            FirstName = new string('n', 100),
            GoalWeight = 1499.9m,
            PlannedPoundsPerWeek = -5m,
            GoalStart = DateTime.UtcNow.Date.AddDays(1)
        };
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.UpdateOrCreateProfileAsync(userId, "test@example.com", request))
            .ReturnsAsync(CreateTestProfile(userId));

        var result = await _sut.UpdateProfile(request);

        result.Result.Should().BeOfType<OkObjectResult>();
        _profileServiceMock.Verify(x => x.UpdateOrCreateProfileAsync(userId, "test@example.com", request), Times.Once);

        var oldest = new UpdateProfileRequest { GoalStart = new DateTime(1900, 1, 1), PlannedPoundsPerWeek = 5m, GoalWeight = 0.1m };
        _profileServiceMock.Setup(x => x.UpdateOrCreateProfileAsync(userId, "test@example.com", oldest))
            .ReturnsAsync(CreateTestProfile(userId));
        (await _sut.UpdateProfile(oldest)).Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_WithNoGoalFields_PassesThroughToService()
    {
        // Clearing goals (all null) must stay valid
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest { UseMetric = true };
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.UpdateOrCreateProfileAsync(userId, "test@example.com", request))
            .ReturnsAsync(CreateTestProfile(userId));

        var result = await _sut.UpdateProfile(request);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_WithInvalidTrendAlgorithm_ReturnsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        var request = new UpdateProfileRequest { TrendAlgorithm = "not-a-real-preset" };

        // Act
        var result = await _sut.UpdateProfile(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("Invalid trend algorithm");
        _profileServiceMock.Verify(x => x.UpdateOrCreateProfileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<UpdateProfileRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfile_WithValidTrendAlgorithm_ReturnsResolvedId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest { TrendAlgorithm = "holt" };
        var updatedProfile = CreateTestProfile(userId);
        updatedProfile.Profile.TrendAlgorithm = "holt";

        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.UpdateOrCreateProfileAsync(userId, "test@example.com", request))
            .ReturnsAsync(updatedProfile);

        // Act
        var result = await _sut.UpdateProfile(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProfileResponse>().Subject;
        response.User.TrendAlgorithm.Should().Be("holt");
    }

    [Fact]
    public async Task UpdateProfile_WithNoUserIdClaim_ThrowsUnauthorized()
    {
        SetupAuthenticatedUser(null, "test@example.com");

        var act = () => _sut.UpdateProfile(new UpdateProfileRequest { FirstName = "Test" });

        // The error middleware turns this into a 401
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateProfile_WithNoEmailClaim_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString(), null);
        var request = new UpdateProfileRequest { FirstName = "Test" };

        // Act
        var result = await _sut.UpdateProfile(request);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User email not found");
    }

    #endregion

    #region GenerateNewToken Tests

    [Fact]
    public async Task GenerateNewToken_WithValidUser_ReturnsNewToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var updatedUser = CreateTestProfile(userId);
        updatedUser.Profile.SharingToken = "new-token-12345";
        updatedUser.Profile.SharingEnabled = true;

        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.GenerateNewSharingTokenAsync(userId)).ReturnsAsync(updatedUser);

        // Act
        var result = await _sut.GenerateNewToken();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SharingTokenResponse>().Subject;

        response.SharingToken.Should().Be("new-token-12345");
        response.SharingEnabled.Should().Be(true);
    }

    [Fact]
    public async Task GenerateNewToken_WithNoUserIdClaim_ThrowsUnauthorized()
    {
        SetupAuthenticatedUser(null, "test@example.com");

        var act = () => _sut.GenerateNewToken();

        // The error middleware turns this into a 401
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GenerateNewToken_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.GenerateNewSharingTokenAsync(userId)).ReturnsAsync((DbProfile?)null);

        // Act
        var result = await _sut.GenerateNewToken();

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User not found");
    }

    #endregion

    #region CompleteMigration Tests

    [Fact]
    public async Task CompleteMigration_WithValidUser_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.CompleteMigrationAsync(userId)).ReturnsAsync(true);

        // Act
        var result = await _sut.CompleteMigration();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SuccessResponse>().Subject;

        response.Success.Should().Be(true);
    }

    [Fact]
    public async Task CompleteMigration_WithNoUserIdClaim_ThrowsUnauthorized()
    {
        SetupAuthenticatedUser(null, "test@example.com");

        var act = () => _sut.CompleteMigration();

        // The error middleware turns this into a 401
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CompleteMigration_WhenServiceReturnsFalse_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.CompleteMigrationAsync(userId)).ReturnsAsync(false);

        // Act
        var result = await _sut.CompleteMigration();

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User not found");
    }

    #endregion

    #region DeleteAccount Tests

    [Fact]
    public async Task DeleteAccount_WithValidUser_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.DeleteAccountAsync(userId)).ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAccount();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<MessageResponse>().Subject;

        response.Message.Should().Be("Account deleted successfully");
    }

    [Fact]
    public async Task DeleteAccount_WithNoUserIdClaim_ThrowsUnauthorized()
    {
        SetupAuthenticatedUser(null, "test@example.com");

        var act = () => _sut.DeleteAccount();

        // The error middleware turns this into a 401
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteAccount_WithInvalidGuidFormat_ThrowsUnauthorized()
    {
        SetupAuthenticatedUser("invalid-guid", "test@example.com");

        var act = () => _sut.DeleteAccount();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _profileServiceMock.Verify(x => x.DeleteAccountAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAccount_WhenServiceReturnsFalse_ReturnsInternalServerError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString(), "test@example.com");
        _profileServiceMock.Setup(x => x.DeleteAccountAsync(userId)).ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteAccount();

        // Assert
        result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
        var errorResult = result.Result as ObjectResult;
        errorResult!.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("Failed to delete account");
    }

    #endregion

    #region Helper Methods


    private void SetupAuthenticatedUser(string? userId, string? email)
    {
        var claims = new List<Claim>();
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (email != null)
            claims.Add(new Claim(ClaimTypes.Email, email));

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
                GoalStart = DateTime.UtcNow.AddDays(-30),
                GoalWeight = 70,
                PlannedPoundsPerWeek = 1,
                DayStartOffset = 0,
                UseMetric = false,
                ShowCalories = false,
                SharingToken = "test-token-12345",
                SharingEnabled = false,
                IsMigrated = false,
                IsNewlyMigrated = false
            }
        };
    }

    /// <summary>
    /// Every stored field set to a value that differs from its default, so a
    /// dropped or swapped mapping shows up in <see cref="ExpectedFullyPopulatedUser"/>.
    /// </summary>
    private static DbProfile CreateFullyPopulatedProfile(Guid userId)
    {
        return new DbProfile
        {
            Uid = userId,
            Email = "mapped@example.com",
            Profile = new ProfileData
            {
                FirstName = "Mapped Name",
                GoalStart = new DateTime(2023, 3, 7, 15, 45, 30), // time-of-day must not leak into the date string
                GoalWeight = 68.4m,
                PlannedPoundsPerWeek = -1.5m,
                DayStartOffset = 5,
                UseMetric = true,
                ShowCalories = true,
                HideDataBeforeStart = true,
                TrendAlgorithm = "holt-gentle",
                SharingToken = "shr-mapped-token-abcdef",
                SharingEnabled = true,
                IsMigrated = true,
                IsNewlyMigrated = true,
                ApiKeyHash = "hash-never-exposed",
                ApiKeySuffix = "wxyz",
                ApiKeyCreatedAt = "2024-01-02T03:04:05.0000000Z"
            }
        };
    }

    private static UserProfileData ExpectedFullyPopulatedUser()
    {
        return new UserProfileData
        {
            FirstName = "Mapped Name",
            GoalStart = "2023-03-07",
            GoalWeight = 68.4m,
            PlannedPoundsPerWeek = -1.5m,
            DayStartOffset = 5,
            UseMetric = true,
            ShowCalories = true,
            HideDataBeforeStart = true,
            TrendAlgorithm = "holt-gentle",
            SharingEnabled = true,
            SharingToken = "shr-mapped-token-abcdef",
            IsMigrated = true,
            IsNewlyMigrated = true
        };
    }

    #endregion
}
