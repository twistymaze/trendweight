using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using TrendWeight.Common.Models;
using TrendWeight.Features.Profile.Services;
using TrendWeight.Features.Sharing;
using TrendWeight.Features.Sharing.Models;
using TrendWeight.Infrastructure.DataAccess.Models;
using Xunit;

namespace TrendWeight.Tests.Features.Sharing.Controllers;

public class SharingControllerTests
{
    private readonly Mock<IProfileService> _profileServiceMock;
    private readonly Mock<ILogger<SharingController>> _loggerMock;
    private readonly SharingController _sut;

    public SharingControllerTests()
    {
        _profileServiceMock = new Mock<IProfileService>();
        _loggerMock = new Mock<ILogger<SharingController>>();

        _sut = new SharingController(
            _profileServiceMock.Object,
            _loggerMock.Object);
    }

    #region GetSharingSettings Tests

    [Fact]
    public async Task GetSharingSettings_WithValidUser_ReturnsSharingData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        user.Profile.SharingEnabled = true;
        user.Profile.SharingToken = "test-sharing-token";

        SetupAuthenticatedUser(userId.ToString());
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.GetSharingSettings();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SharingResponse>().Subject;

        response.SharingEnabled.Should().Be(true);
        response.SharingToken.Should().Be("test-sharing-token");
    }

    [Fact]
    public async Task GetSharingSettings_WithNoUserIdClaim_ThrowsUnauthorized()
    {
        // Arrange
        SetupAuthenticatedUser(null);

        // Act
        var act = () => _sut.GetSharingSettings();

        // Assert - the error middleware turns this into a 401
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetSharingSettings_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString());
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync((DbProfile?)null);

        // Act
        var result = await _sut.GetSharingSettings();

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User not found");
    }

    #endregion

    #region ToggleSharing Tests

    [Fact]
    public async Task ToggleSharing_WithValidUserAndRequest_ReturnsUpdatedSharingData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        user.Profile.SharingEnabled = false;
        user.Profile.SharingToken = "test-token";

        var updatedUser = CreateTestProfile(userId);
        updatedUser.Profile.SharingEnabled = true;
        updatedUser.Profile.SharingToken = "test-token";

        var request = new ToggleSharingRequest { Enabled = true };

        SetupAuthenticatedUser(userId.ToString());
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);
        _profileServiceMock.Setup(x => x.UpdateAsync(It.IsAny<DbProfile>()))
            .ReturnsAsync(updatedUser);

        // Act
        var result = await _sut.ToggleSharing(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SharingResponse>().Subject;

        response.SharingEnabled.Should().Be(true);
        response.SharingToken.Should().Be("test-token");

        // Verify the profile was updated correctly
        _profileServiceMock.Verify(x => x.UpdateAsync(It.Is<DbProfile>(p =>
            p.Profile.SharingEnabled == true)), Times.Once);
    }

    [Fact]
    public async Task ToggleSharing_DisablingSharing_ReturnsUpdatedSharingData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        user.Profile.SharingEnabled = true;
        user.Profile.SharingToken = "test-token";

        var updatedUser = CreateTestProfile(userId);
        updatedUser.Profile.SharingEnabled = false;
        updatedUser.Profile.SharingToken = "test-token";

        var request = new ToggleSharingRequest { Enabled = false };

        SetupAuthenticatedUser(userId.ToString());
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);
        _profileServiceMock.Setup(x => x.UpdateAsync(It.IsAny<DbProfile>()))
            .ReturnsAsync(updatedUser);

        // Act
        var result = await _sut.ToggleSharing(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SharingResponse>().Subject;

        response.SharingEnabled.Should().Be(false);
        response.SharingToken.Should().Be("test-token");

        // Verify the profile was updated correctly
        _profileServiceMock.Verify(x => x.UpdateAsync(It.Is<DbProfile>(p =>
            p.Profile.SharingEnabled == false)), Times.Once);
    }

    [Fact]
    public async Task ToggleSharing_WithNoUserIdClaim_ThrowsUnauthorized()
    {
        // Arrange
        SetupAuthenticatedUser(null);
        var request = new ToggleSharingRequest { Enabled = true };

        // Act
        var act = () => _sut.ToggleSharing(request);

        // Assert - the error middleware turns this into a 401
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _profileServiceMock.Verify(x => x.UpdateAsync(It.IsAny<DbProfile>()), Times.Never);
    }

    [Fact]
    public async Task ToggleSharing_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId.ToString());
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync((DbProfile?)null);
        var request = new ToggleSharingRequest { Enabled = true };

        // Act
        var result = await _sut.ToggleSharing(request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().BeOfType<ErrorResponse>()
            .Which.Error.Should().Be("User not found");
    }

    [Fact]
    public async Task ToggleSharing_UpdatesTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateTestProfile(userId);
        var originalTimestamp = user.UpdatedAt;

        var request = new ToggleSharingRequest { Enabled = true };

        SetupAuthenticatedUser(userId.ToString());
        _profileServiceMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);
        _profileServiceMock.Setup(x => x.UpdateAsync(It.IsAny<DbProfile>()))
            .ReturnsAsync(user);

        // Act
        await _sut.ToggleSharing(request);

        // Assert
        _profileServiceMock.Verify(x => x.UpdateAsync(It.Is<DbProfile>(p =>
            p.UpdatedAt != originalTimestamp)), Times.Once);
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
            Profile = new TrendWeight.Features.Profile.Models.ProfileData
            {
                FirstName = "Test User",
                UseMetric = false,
                SharingToken = "test-token",
                SharingEnabled = false
            },
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };
    }

    #endregion
}
