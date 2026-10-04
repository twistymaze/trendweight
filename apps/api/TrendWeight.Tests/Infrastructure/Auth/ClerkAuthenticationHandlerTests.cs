using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrendWeight.Infrastructure.Auth;
using TrendWeight.Infrastructure.Configuration;
using TrendWeight.Infrastructure.DataAccess;
using TrendWeight.Infrastructure.DataAccess.Models;
using Xunit;

namespace TrendWeight.Tests.Infrastructure.Auth;

public class ClerkAuthenticationHandlerTests
{
    private readonly Mock<IClerkTokenService> _clerkTokenServiceMock;
    private readonly Mock<IUserAccountMappingService> _userAccountMappingServiceMock;
    private readonly Mock<IOptionsMonitor<AuthenticationSchemeOptions>> _optionsMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<ClerkAuthenticationHandler>> _loggerMock;
    private readonly Mock<UrlEncoder> _encoderMock;
    private readonly ClerkAuthenticationHandler _handler;
    private readonly HttpContext _context;

    public ClerkAuthenticationHandlerTests()
    {
        _clerkTokenServiceMock = new Mock<IClerkTokenService>();
        _userAccountMappingServiceMock = new Mock<IUserAccountMappingService>();
        _optionsMock = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<ClerkAuthenticationHandler>>();
        _encoderMock = new Mock<UrlEncoder>();

        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);

        _optionsMock.Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AuthenticationSchemeOptions());

        var services = new ServiceCollection();
        services.AddSingleton(_clerkTokenServiceMock.Object);
        services.AddSingleton(_userAccountMappingServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        _context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        _handler = new ClerkAuthenticationHandler(
            _optionsMock.Object,
            _loggerFactoryMock.Object,
            _encoderMock.Object,
            _clerkTokenServiceMock.Object,
            _userAccountMappingServiceMock.Object,
            new PublicUrl("https://example.com", false));

        var scheme = new AuthenticationScheme("Clerk", "Clerk", typeof(ClerkAuthenticationHandler));
        _handler.InitializeAsync(scheme, _context).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task AuthenticateAsync_WithNoAuthorizationHeader_ReturnsNoResult()
    {
        // Arrange
        _context.Request.Headers.Clear();

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidAuthorizationScheme_ReturnsNoResult()
    {
        // Arrange
        _context.Request.Headers["Authorization"] = "Basic sometoken";

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyBearerToken_ReturnsNoResult()
    {
        // Arrange
        _context.Request.Headers["Authorization"] = "Bearer ";

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidToken_ReturnsFailure()
    {
        // Arrange
        _context.Request.Headers["Authorization"] = "Bearer invalid-token";

        _clerkTokenServiceMock
            .Setup(x => x.ValidateTokenAsync("invalid-token", "https://example.com"))
            .ReturnsAsync((ClaimsPrincipal?)null);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Invalid token", result.Failure?.Message);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidTokenButMissingClaims_ReturnsFailure()
    {
        // Arrange
        _context.Request.Headers["Authorization"] = "Bearer valid-token";

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("some-claim", "some-value")
        }));

        _clerkTokenServiceMock
            .Setup(x => x.ValidateTokenAsync("valid-token", "https://example.com"))
            .ReturnsAsync(principal);

        _clerkTokenServiceMock
            .Setup(x => x.GetClerkUserId(principal))
            .Returns((string?)null);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Token missing required claims", result.Failure?.Message);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidToken_CreatesAuthenticatedPrincipal()
    {
        // Arrange
        _context.Request.Headers["Authorization"] = "Bearer valid-token";

        var clerkPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "clerk_user_123"),
            new Claim("email", "test@example.com")
        }));

        var userAccount = new DbUserAccount
        {
            Uid = Guid.NewGuid(),
            ExternalId = "clerk_user_123",
            Provider = "clerk"
        };

        _clerkTokenServiceMock
            .Setup(x => x.ValidateTokenAsync("valid-token", "https://example.com"))
            .ReturnsAsync(clerkPrincipal);

        _clerkTokenServiceMock
            .Setup(x => x.GetClerkUserId(clerkPrincipal))
            .Returns("clerk_user_123");

        _clerkTokenServiceMock
            .Setup(x => x.GetEmail(clerkPrincipal))
            .Returns("test@example.com");

        _userAccountMappingServiceMock
            .Setup(x => x.GetOrCreateMappingAsync("clerk_user_123", "test@example.com", "clerk"))
            .ReturnsAsync(userAccount);

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);

        var principal = result.Principal!;
        Assert.Equal(userAccount.Uid.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("test@example.com", principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("clerk_user_123", principal.FindFirst("clerk_user_id")?.Value);
        Assert.Equal("clerk", principal.FindFirst("provider")?.Value);
    }

    [Fact]
    public async Task AuthenticateAsync_UsesConfiguredOriginDespiteInternalRequest()
    {
        // Arrange
        _context.Request.Headers["Authorization"] = "Bearer valid-token";
        _context.Request.Scheme = "http";
        _context.Request.Host = new HostString("localhost", 5199);

        var clerkPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "clerk_user_123"),
            new Claim("email", "test@example.com")
        }));

        _clerkTokenServiceMock
            .Setup(x => x.ValidateTokenAsync("valid-token", "https://example.com"))
            .ReturnsAsync(clerkPrincipal);

        _clerkTokenServiceMock
            .Setup(x => x.GetClerkUserId(clerkPrincipal))
            .Returns("clerk_user_123");

        _clerkTokenServiceMock
            .Setup(x => x.GetEmail(clerkPrincipal))
            .Returns("test@example.com");

        _userAccountMappingServiceMock
            .Setup(x => x.GetOrCreateMappingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DbUserAccount
            {
                Uid = Guid.NewGuid(),
                ExternalId = "clerk_user_123",
                Provider = "clerk"
            });

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.True(result.Succeeded);
        _clerkTokenServiceMock.Verify(x => x.ValidateTokenAsync("valid-token", "https://example.com"), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_HandlesExceptions_ReturnsFailure()
    {
        // Arrange
        _context.Request.Headers["Authorization"] = "Bearer valid-token";

        _clerkTokenServiceMock
            .Setup(x => x.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _handler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("Authentication failed", result.Failure?.Message);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Authentication failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

}
