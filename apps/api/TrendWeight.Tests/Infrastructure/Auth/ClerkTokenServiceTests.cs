using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Moq.Protected;
using TrendWeight.Infrastructure.Auth;
using TrendWeight.Infrastructure.Configuration;
using Xunit;

namespace TrendWeight.Tests.Infrastructure.Auth;

public class ClerkTokenServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<ClerkTokenService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly TestTimeProvider _clock = new();
    private readonly IOptions<AppOptions> _options;
    private readonly string _testAuthority = "https://test.clerk.accounts.dev";
    private readonly string _testJwksUrl;
    private readonly string _testSigningKey = "test-signing-key-that-is-long-enough-for-hs256";

    public ClerkTokenServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<ClerkTokenService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _testJwksUrl = $"{_testAuthority}/.well-known/jwks.json";

        _options = Options.Create(new AppOptions
        {
            Clerk = new ClerkConfig
            {
                Authority = _testAuthority,
                SecretKey = "test-secret"
            }
        });

        // Like the real factory, hand out a fresh client per call over a shared handler.
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_httpMessageHandlerMock.Object, disposeHandler: false));
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenSigningKeyRotates_RefreshesCachedKeys()
    {
        var oldKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey)) { KeyId = "old-key" };
        var newKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey + "-rotated")) { KeyId = "new-key" };
        _httpMessageHandlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(KeyResponse(oldKey))
            .ReturnsAsync(KeyResponse(newKey));
        using var service = CreateService();

        Assert.NotNull(await service.ValidateTokenAsync(TokenWithKey(oldKey)));
        _clock.Advance(TimeSpan.FromSeconds(31));
        Assert.NotNull(await service.ValidateTokenAsync(TokenWithKey(newKey)));
        VerifyJwksFetches(2);
        // Each fetch builds its own client so the factory can rotate handlers.
        _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ValidateTokenAsync_WithRepeatedUnknownKeys_DoesNotRepeatedlyFetchJwks()
    {
        var knownKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey)) { KeyId = "known-key" };
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(KeyResponse(knownKey)));
        using var service = CreateService();
        Assert.NotNull(await service.ValidateTokenAsync(TokenWithKey(knownKey)));
        _clock.Advance(TimeSpan.FromSeconds(31));

        for (var i = 0; i < 3; i++)
        {
            var unknownKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey + i)) { KeyId = $"unknown-{i}" };
            Assert.Null(await service.ValidateTokenAsync(TokenWithKey(unknownKey)));
        }

        VerifyJwksFetches(2);
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenRefreshFailsAfterCacheExpiry_KeepsServingCachedKeys()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey)) { KeyId = "current-key" };
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(KeyResponse(key)));
        using var service = CreateService();
        Assert.NotNull(await service.ValidateTokenAsync(TokenWithKey(key)));

        // The cache lapses and Clerk starts failing.
        _clock.Advance(TimeSpan.FromMinutes(61));
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)));

        Assert.NotNull(await service.ValidateTokenAsync(TokenWithKey(key)));
        VerifyLogged(LogLevel.Warning, "continuing with the cached key set", Times.Once());

        // Further validations inside the retry interval do not hit Clerk again...
        Assert.NotNull(await service.ValidateTokenAsync(TokenWithKey(key)));
        Assert.NotNull(await service.ValidateTokenAsync(TokenWithKey(key)));
        VerifyJwksFetches(2);

        // ...but once it lapses, a refresh is attempted again and the stale keys still serve.
        _clock.Advance(TimeSpan.FromSeconds(31));
        Assert.NotNull(await service.ValidateTokenAsync(TokenWithKey(key)));
        VerifyJwksFetches(3);
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenFirstJwksFetchFails_ReturnsNull()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey)) { KeyId = "current-key" };
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)));
        using var service = CreateService();

        Assert.Null(await service.ValidateTokenAsync(TokenWithKey(key)));

        VerifyLogged(LogLevel.Error, "Failed to fetch Clerk JWKS", Times.Once());
        VerifyLogged(LogLevel.Error, "Failed to validate Clerk JWT token", Times.Once());
    }

    [Fact]
    public async Task ValidateTokenAsync_WithTokenSignedByDifferentKey_ReturnsNull()
    {
        var publishedKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey)) { KeyId = "current-key" };
        var forgedKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey + "-forged")) { KeyId = "current-key" };
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(KeyResponse(publishedKey)));
        using var service = CreateService();

        Assert.Null(await service.ValidateTokenAsync(TokenWithKey(forgedKey)));
        Assert.NotNull(await service.ValidateTokenAsync(TokenWithKey(publishedKey)));
        VerifyJwksFetches(1);
    }

    private static HttpResponseMessage KeyResponse(SecurityKey key)
    {
        var keySet = new JsonWebKeySet();
        keySet.Keys.Add(JsonWebKeyConverter.ConvertFromSecurityKey(key));
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(keySet))
        };
    }

    private string TokenWithKey(SecurityKey key)
    {
        var token = new JwtSecurityToken(_testAuthority, claims: new[] { new Claim("sub", "user_123") },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void VerifyJwksFetches(int count)
    {
        _httpMessageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(count),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == _testJwksUrl), ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyLogged(LogLevel level, string messageFragment, Times times)
    {
        _loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains(messageFragment)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithValidToken_ReturnsClaimsPrincipal()
    {
        // Arrange
        var jwks = CreateTestJwks();
        SetupHttpResponse(jwks);

        var token = CreateTestToken(
            issuer: _testAuthority,
            audience: null,
            claims: new[]
            {
                new Claim("sub", "user_123"),
                new Claim("email", "test@example.com"),
                new Claim("azp", "https://example.com")
            });

        var service = CreateService();

        // Act
        var principal = await service.ValidateTokenAsync(token);

        // Assert
        Assert.NotNull(principal);
        // JWT handler maps 'sub' claim to ClaimTypes.NameIdentifier
        var nameIdentifier = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var sub = principal.FindFirst("sub")?.Value;
        Assert.True(nameIdentifier == "user_123" || sub == "user_123",
            $"Expected user_123 but got NameIdentifier: {nameIdentifier}, sub: {sub}");

        // JWT handler maps 'email' claim to ClaimTypes.Email
        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value;
        var email = principal.FindFirst("email")?.Value;
        Assert.True(emailClaim == "test@example.com" || email == "test@example.com",
            $"Expected test@example.com but got Email: {emailClaim}, email: {email}");
    }

    [Fact]
    public async Task ValidateTokenAsync_WithInvalidIssuer_ReturnsNull()
    {
        // Arrange
        var jwks = CreateTestJwks();
        SetupHttpResponse(jwks);

        var token = CreateTestToken(
            issuer: "https://wrong.issuer.com",
            audience: null,
            claims: new[]
            {
                new Claim("sub", "user_123"),
                new Claim("email", "test@example.com")
            });

        var service = CreateService();

        // Act
        var principal = await service.ValidateTokenAsync(token);

        // Assert
        Assert.Null(principal);
        VerifyLogged(LogLevel.Error, "Failed to validate Clerk JWT token", Times.Once());
    }

    [Fact]
    public async Task ValidateTokenAsync_WithExpiredToken_ReturnsNull()
    {
        // Arrange
        var jwks = CreateTestJwks();
        SetupHttpResponse(jwks);

        var token = CreateExpiredTestToken(
            issuer: _testAuthority,
            audience: null,
            claims: new[]
            {
                new Claim("sub", "user_123"),
                new Claim("email", "test@example.com")
            });

        var service = CreateService();

        // Act
        var principal = await service.ValidateTokenAsync(token);

        // Assert
        Assert.Null(principal);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithRequestOrigin_ValidatesAzpClaim()
    {
        // Arrange
        var jwks = CreateTestJwks();
        SetupHttpResponse(jwks);

        var requestOrigin = "https://example.com";
        var token = CreateTestToken(
            issuer: _testAuthority,
            audience: null,
            claims: new[]
            {
                new Claim("sub", "user_123"),
                new Claim("email", "test@example.com"),
                new Claim("azp", requestOrigin)
            });

        var service = CreateService();

        // Act
        var principal = await service.ValidateTokenAsync(token, requestOrigin);

        // Assert
        Assert.NotNull(principal);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithMismatchedAzp_ReturnsNull()
    {
        // Arrange
        var jwks = CreateTestJwks();
        SetupHttpResponse(jwks);

        var token = CreateTestToken(
            issuer: _testAuthority,
            audience: null,
            claims: new[]
            {
                new Claim("sub", "user_123"),
                new Claim("email", "test@example.com"),
                new Claim("azp", "https://wrong-origin.com")
            });

        var service = CreateService();

        // Act
        var principal = await service.ValidateTokenAsync(token, "https://example.com");

        // Assert
        Assert.Null(principal);
        VerifyLogged(LogLevel.Warning, "does not match request origin", Times.Once());
    }

    [Fact]
    public async Task ValidateTokenAsync_WithoutAzpClaim_IsAcceptedWhenOriginIsSupplied()
    {
        // Documents the current policy: azp is only compared when the token carries
        // one (Clerk mints tokens without an origin in some flows). A token with no
        // azp claim is accepted for any request origin.
        SetupHttpResponse(CreateTestJwks());
        var token = CreateTestToken(
            issuer: _testAuthority,
            audience: null,
            claims: new[]
            {
                new Claim("sub", "user_123"),
                new Claim("email", "test@example.com")
            });
        var service = CreateService();

        var principal = await service.ValidateTokenAsync(token, "https://example.com");

        Assert.NotNull(principal);
        VerifyLogged(LogLevel.Warning, "does not match request origin", Times.Never());
    }

    [Fact]
    public async Task ValidateTokenAsync_CachesJwks()
    {
        // Arrange
        var jwks = CreateTestJwks();
        SetupHttpResponse(jwks);

        var token = CreateTestToken(
            issuer: _testAuthority,
            audience: null,
            claims: new[]
            {
                new Claim("sub", "user_123"),
                new Claim("email", "test@example.com")
            });

        var service = CreateService();

        // Act
        var principal1 = await service.ValidateTokenAsync(token);
        var principal2 = await service.ValidateTokenAsync(token);

        // Assert
        Assert.NotNull(principal1);
        Assert.NotNull(principal2);

        // Verify HTTP call was made only once due to caching
        VerifyJwksFetches(1);
    }

    [Fact]
    public void GetClerkUserId_ExtractsUserIdFromClaims()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user_123"),
            new Claim("sub", "user_456")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var service = CreateService();

        // Act
        var userId = service.GetClerkUserId(principal);

        // Assert
        Assert.Equal("user_123", userId); // ClaimTypes.NameIdentifier takes precedence
    }

    [Fact]
    public void GetClerkUserId_FallsBackToSubClaim()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("sub", "user_456")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var service = CreateService();

        // Act
        var userId = service.GetClerkUserId(principal);

        // Assert
        Assert.Equal("user_456", userId);
    }

    [Fact]
    public void GetEmail_ExtractsEmailFromClaims()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim("email", "other@example.com")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var service = CreateService();

        // Act
        var email = service.GetEmail(principal);

        // Assert
        Assert.Equal("test@example.com", email); // ClaimTypes.Email takes precedence
    }

    private ClerkTokenService CreateService() =>
        new(_httpClientFactoryMock.Object, _loggerMock.Object, _options, _clock);

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private void SetupHttpResponse(string content)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == _testJwksUrl),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(content)
            }));
    }

    private string CreateTestJwks()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey));
        var jwk = JsonWebKeyConverter.ConvertFromSecurityKey(key);
        jwk.Kid = "test-key-id";
        jwk.Use = "sig";

        return $$"""
        {
            "keys": [
                {
                    "kid": "{{jwk.Kid}}",
                    "kty": "{{jwk.Kty}}",
                    "use": "{{jwk.Use}}",
                    "k": "{{jwk.K}}"
                }
            ]
        }
        """;
    }

    private string CreateTestToken(string issuer, string? audience, Claim[] claims, DateTime? expires = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            NotBefore = DateTime.UtcNow.AddMinutes(-5), // Ensure token is valid now
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string CreateExpiredTestToken(string issuer, string? audience, Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_testSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(-1), // Expired
            NotBefore = DateTime.UtcNow.AddHours(-2), // Valid from 2 hours ago
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
