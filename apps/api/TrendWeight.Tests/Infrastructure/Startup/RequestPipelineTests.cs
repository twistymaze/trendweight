using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Moq;
using TrendWeight.Features.ApiKeys;
using TrendWeight.Features.Measurements.Manual;
using TrendWeight.Features.Measurements.Models;
using TrendWeight.Features.Profile.Models;
using TrendWeight.Features.Profile.Services;
using TrendWeight.Infrastructure.Auth;
using TrendWeight.Infrastructure.DataAccess;
using TrendWeight.Infrastructure.DataAccess.Models;
using TrendWeight.Infrastructure.Middleware;
using TrendWeight.Infrastructure.Services;
using TrendWeight.Tests.Fixtures;

namespace TrendWeight.Tests.Infrastructure.Startup;

public class RequestPipelineTests : IClassFixture<StartupTestFactory>
{
    private readonly StartupTestFactory _factory;

    public RequestPipelineTests(StartupTestFactory factory) => _factory = factory;

    [Theory]
    [InlineData("Development", null, "http://localhost:5173")]
    [InlineData("Production", "https://canonical.example/", "https://canonical.example")]
    public async Task ClerkAuthentication_UsesPublicOriginBehindProxy(string environment, string? configuredOrigin, string expectedOrigin)
    {
        var tokens = new Mock<IClerkTokenService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "clerk-test") }, "jwt"));
        tokens.Setup(x => x.ValidateTokenAsync("origin-test-jwt", expectedOrigin)).ReturnsAsync(principal);
        tokens.Setup(x => x.GetClerkUserId(principal)).Returns("clerk-test");
        tokens.Setup(x => x.GetEmail(principal)).Returns("clerk@example.com");
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PublicBaseUrl"] = configuredOrigin }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IClerkTokenService>();
                services.AddSingleton(tokens.Object);
            });
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5199/api/measurements/manual");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "origin-test-jwt");
        request.Headers.Add("Origin", "https://attacker.example");
        request.Headers.Add("X-Forwarded-Host", "attacker.example");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tokens.Verify(x => x.ValidateTokenAsync("origin-test-jwt", expectedOrigin), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("test-clerk-jwt")]
    [InlineData("sk-invalid")]
    public async Task Settings_RequiresValidApiKey(string? token)
    {
        using var client = _factory.CreateHttpsClient();
        if (token != null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        using var response = await client.GetAsync("/api/v1/settings", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(true, 70, -0.5)]
    [InlineData(false, 155, -1)]
    [InlineData(false, 160, 0)]
    [InlineData(true, 75, 0.25)]
    public async Task Settings_ReturnsOnlyDisplayPreferencesInSelectedUnits(bool metric, decimal goal, decimal weeklyChange)
    {
        using var factory = SettingsFactory(new ProfileData
        {
            FirstName = "Private name",
            SharingEnabled = false,
            SharingToken = "private-sharing-token",
            ApiKeyHash = "private-hash",
            ApiKeySuffix = "private-suffix",
            ApiKeyCreatedAt = "private-date",
            IsMigrated = true,
            IsNewlyMigrated = true,
            GoalStart = new DateTime(2026, 9, 1),
            GoalWeight = goal,
            PlannedPoundsPerWeek = weeklyChange,
            UseMetric = metric,
            DayStartOffset = 4,
            ShowCalories = true,
            HideDataBeforeStart = true,
            TrendAlgorithm = "holt-gentle"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", StartupTestFactory.ApiKey);
        using var response = await client.GetAsync("/api/v1/settings", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var settings = document.RootElement;
        settings.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "goalStart", "goalWeight", "plannedWeightChangePerWeek", "useMetric", "dayStartOffset",
            "showCalories", "hideDataBeforeStart", "trendAlgorithm");
        settings.GetProperty("goalStart").GetString().Should().Be("2026-09-01");
        settings.GetProperty("goalWeight").GetDecimal().Should().Be(goal);
        settings.GetProperty("plannedWeightChangePerWeek").GetDecimal().Should().Be(weeklyChange);
        settings.GetProperty("useMetric").GetBoolean().Should().Be(metric);
        settings.GetProperty("dayStartOffset").GetInt32().Should().Be(4);
        settings.GetProperty("showCalories").GetBoolean().Should().BeTrue();
        settings.GetProperty("hideDataBeforeStart").GetBoolean().Should().BeTrue();
        settings.GetProperty("trendAlgorithm").GetString().Should().Be("holt-gentle");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown-preset")]
    public async Task Settings_UsesDashboardDefaultsAndOmitsUnsetGoals(string? preset)
    {
        using var factory = SettingsFactory(new ProfileData { TrendAlgorithm = preset });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", StartupTestFactory.ApiKey);
        using var response = await client.GetAsync("/api/v1/settings", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var settings = document.RootElement;
        settings.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "useMetric", "dayStartOffset", "showCalories", "hideDataBeforeStart", "trendAlgorithm");
        settings.GetProperty("dayStartOffset").GetInt32().Should().Be(0);
        settings.GetProperty("showCalories").GetBoolean().Should().BeFalse();
        settings.GetProperty("trendAlgorithm").GetString().Should().Be("default");
    }

    [Fact]
    public async Task Settings_TreatsZeroGoalWeightAsUnset()
    {
        // Profiles migrated before unset goals were preserved as null store a zero goal weight.
        using var factory = SettingsFactory(new ProfileData { GoalWeight = 0, PlannedPoundsPerWeek = 0 });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", StartupTestFactory.ApiKey);
        using var response = await client.GetAsync("/api/v1/settings", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var settings = document.RootElement;
        settings.TryGetProperty("goalWeight", out _).Should().BeFalse("a zero goal weight means no goal");
        settings.GetProperty("plannedWeightChangePerWeek").GetDecimal().Should().Be(0, "zero weekly change is a real 'maintain' plan");
    }

    [Fact]
    public async Task Settings_ProfileRemovedAfterAuthentication_ReturnsNotFound()
    {
        using var factory = SettingsFactory(null);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", StartupTestFactory.ApiKey);
        using var response = await client.GetAsync("/api/v1/settings", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private WebApplicationFactory<Program> SettingsFactory(ProfileData? settings)
    {
        var uid = Guid.NewGuid();
        var profile = new DbProfile { Uid = uid, Email = "private@example.com", Profile = settings ?? new ProfileData() };
        var profiles = new Mock<IProfileService>(MockBehavior.Strict);
        profiles.Setup(x => x.GetByApiKeyHashAsync(ApiKeyService.HashKey(StartupTestFactory.ApiKey))).ReturnsAsync(profile);
        profiles.Setup(x => x.GetByIdAsync(uid)).ReturnsAsync(settings == null ? null : profile);
        return _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IProfileService>();
            services.AddSingleton(profiles.Object);
        }));
    }

    [Fact]
    public async Task ApiDocumentation_AdvertisesCanonicalOriginForInternalHttp()
    {
        using var client = _factory.CreateHttpsClient();
        using var response = await client.GetAsync("http://localhost/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("servers")[0].GetProperty("url").GetString()
            .Should().Be("https://canonical.example");
    }

    [Fact]
    public void ClerkClients_ResolveFromTheContainer()
    {
        // The token service builds its client from the factory; the management
        // client is a typed client, so each resolution gets a factory-managed handler.
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IClerkTokenService>();
        var first = scope.ServiceProvider.GetRequiredService<IClerkService>();
        var second = scope.ServiceProvider.GetRequiredService<IClerkService>();

        first.Should().BeOfType<ClerkService>();
        second.Should().NotBeSameAs(first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://canonical.example")]
    public void InvalidPublicOrigin_PreventsProductionStartup(string? origin)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PublicBaseUrl"] = origin })));
        var start = () => factory.CreateClient();
        start.Should().Throw<InvalidOperationException>().WithMessage("PublicBaseUrl*");
    }

    [Theory]
    [InlineData("Supabase:ServiceKey", "paste-your-service-role-key-here")]
    [InlineData("Clerk:SecretKey", "your-clerk-secret-key")]
    [InlineData("Clerk:Authority", "")]
    public void PlaceholderOrMissingConfiguration_PreventsProductionStartup(string key, string value)
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { [key] = value })));
        var start = () => factory.CreateClient();
        start.Should().Throw<InvalidOperationException>().WithMessage($"Required configuration*{key}*");
    }

    [Fact]
    public async Task PlaceholderConfiguration_StillStartsInDevelopment()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Supabase:ServiceKey"] = "paste-your-service-role-key-here" }));
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HostValidation_RejectsInvalidHostEvenWhenForwardedHostIsAllowed()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AllowedHosts"] = "localhost" })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://attacker.example/dashboard");
        request.Headers.Add("X-Forwarded-Host", "localhost");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("canonical.example", null)]
    [InlineData("Canonical.EXAMPLE", null)]
    [InlineData("canonical.example", 8080)]
    [InlineData("other.example", 443)]
    public async Task HostValidation_AcceptsListedHostsRegardlessOfCaseAndPort(string host, int? port)
    {
        // Set the host directly: HttpClient would lower-case it before it reaches the server.
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AllowedHosts"] = "canonical.example; other.example" })));

        var response = await factory.Server.SendAsync(context =>
        {
            context.Request.Method = "GET";
            context.Request.Scheme = "https";
            context.Request.Host = port.HasValue ? new HostString(host, port.Value) : new HostString(host);
            context.Request.Path = "/dashboard";
        }, TestContext.Current.CancellationToken);

        response.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        response.Response.ContentType.Should().Be("text/html");
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("10.0.0.7")]
    public async Task HealthCheck_IsExemptFromHostValidation(string probeHost)
    {
        // The Docker HEALTHCHECK curls localhost and platform probes may use an IP;
        // neither needs to appear in AllowedHosts, but nothing else on that host passes.
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AllowedHosts"] = "canonical.example" })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var health = await client.GetAsync($"http://{probeHost}/api/health", TestContext.Current.CancellationToken);
        using var shell = await client.GetAsync($"http://{probeHost}/dashboard", TestContext.Current.CancellationToken);

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        shell.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/api/measurements/manual", null, HttpStatusCode.Unauthorized)]
    [InlineData("/api/v1/measurements/manual", null, HttpStatusCode.Unauthorized)]
    [InlineData("/api/measurements/manual", StartupTestFactory.ApiKey, HttpStatusCode.Unauthorized)]
    [InlineData("/api/v1/measurements/manual", "test-clerk-jwt", HttpStatusCode.Unauthorized)]
    [InlineData("/api/measurements/manual", "test-clerk-jwt", HttpStatusCode.OK)]
    [InlineData("/api/v1/measurements/manual", StartupTestFactory.ApiKey, HttpStatusCode.OK)]
    public async Task MeasurementEndpoints_EnforceAuthenticationScheme(string path, string? token, HttpStatusCode expected)
    {
        using var client = _factory.CreateHttpsClient();
        if (token != null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(expected);
        if (expected == HttpStatusCode.OK)
        {
            // Each real authentication handler resolves a different owner. The
            // manual-service double returns that owner's distinct measurement.
            var expectedWeight = token == StartupTestFactory.ApiKey ? "82" : "73";
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain($"\"weight\":{expectedWeight}");
        }
    }

    [Theory]
    [InlineData("{\"weight\":\"heavy\"}")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task ApiV1BindingFailures_UseTheDocumentedErrorShape(string body)
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", StartupTestFactory.ApiKey);
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PutAsync("/api/v1/measurements/manual/2024-01-01", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
        document.RootElement.TryGetProperty("title", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("status", out _).Should().BeFalse();
    }

    [Fact]
    public async Task InternalBindingFailures_KeepTheFrameworkProblemDetails()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-clerk-jwt");
        using var content = new StringContent("{\"weight\":\"heavy\"}", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PutAsync("/api/measurements/manual/2024-01-01", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.TryGetProperty("title", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/measurements/manual", "test-clerk-jwt")]
    [InlineData("/api/v1/measurements/manual", StartupTestFactory.ApiKey)]
    public async Task UnhandledServiceExceptions_BecomeServerErrorsWithoutTheirMessage(string path, string token)
    {
        // Controllers let unexpected service failures propagate; the pipeline must turn
        // them into a 500 that does not echo the exception in production.
        const string secret = "Npgsql connection to db.internal:5432 failed for role service_role";
        var manual = new Mock<IManualDataService>();
        manual.Setup(x => x.GetReadingsAsync(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException(secret));
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IManualDataService>();
            services.AddSingleton(manual.Object);
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("db.internal").And.NotContain(nameof(InvalidOperationException));
        using var document = System.Text.Json.JsonDocument.Parse(body);
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("INTERNAL_ERROR");
        document.RootElement.GetProperty("error").GetString().Should().Be("An error occurred while processing your request");
        document.RootElement.TryGetProperty("details", out _).Should().BeFalse();
        manual.Verify(x => x.GetReadingsAsync(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task ApiKeyHeader_WorksOnlyOnExternalEndpoints()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", StartupTestFactory.ApiKey);

        using var external = await client.GetAsync("/api/v1/measurements/manual", TestContext.Current.CancellationToken);
        using var internalResponse = await client.GetAsync("/api/measurements/manual", TestContext.Current.CancellationToken);

        external.StatusCode.Should().Be(HttpStatusCode.OK);
        internalResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApiKeyAuthorization_ReplacesDefaultClerkPrincipal()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-clerk-jwt");
        client.DefaultRequestHeaders.Add("X-Api-Key", StartupTestFactory.ApiKey);

        using var response = await client.GetAsync("/api/v1/measurements/manual", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("\"weight\":82");
    }

    [Fact]
    public async Task ApiKeyRequests_AreLimitedAfterSchemeAuthorization()
    {
        // A separate host gives this test an unused fixed-window partition.
        using var factory = new StartupTestFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", StartupTestFactory.ApiKey);
        for (var i = 0; i < 60; i++)
        {
            using var allowed = await client.GetAsync("/api/v1/measurements/manual", TestContext.Current.CancellationToken);
            allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var rejected = await client.GetAsync("/api/v1/measurements/manual", TestContext.Current.CancellationToken);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(1));
        using var body = System.Text.Json.JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("error").GetString().Should().Be("Too many requests. Please try again later.");
        body.RootElement.GetProperty("errorCode").GetString().Should().Be("RATE_LIMITED");
        body.RootElement.GetProperty("isRetryable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AnonymousApiRequestsAndRejectedCredentials_ShareOnePeerRateLimit()
    {
        // A separate host gives this test an unused fixed-window partition.
        using var factory = new StartupTestFactory();
        using var client = factory.CreateHttpsClient();
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 60; i++)
        {
            var guessing = i % 2 == 1;
            using var request = new HttpRequestMessage(HttpMethod.Get, guessing ? "/api/v1/settings" : "/api/profile/disabled-share");
            if (guessing)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "sk-guessed-key");
            }
            using var allowed = await client.SendAsync(request, ct);
            allowed.StatusCode.Should().Be(guessing ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound);
        }

        using var anonymous = await client.GetAsync("/api/profile/disabled-share", ct);
        anonymous.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        anonymous.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(1));

        // Rejected credentials are answered by the authorization handler rather than
        // the limiter middleware; both 429 paths carry the same body and Retry-After.
        using var guess = new HttpRequestMessage(HttpMethod.Get, "/api/v1/settings");
        guess.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "sk-guessed-key");
        using var rejectedGuess = await client.SendAsync(guess, ct);
        rejectedGuess.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejectedGuess.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(1));
        (await rejectedGuess.Content.ReadAsStringAsync(ct)).Should().Contain("\"errorCode\":\"RATE_LIMITED\"");

        // Valid credentials, the application shell, and the health check keep their own budget.
        using var authenticated = new HttpRequestMessage(HttpMethod.Get, "/api/v1/measurements/manual");
        authenticated.Headers.Add("X-Api-Key", StartupTestFactory.ApiKey);
        using var authenticatedResponse = await client.SendAsync(authenticated, ct);
        authenticatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var shell = await client.GetAsync("/dashboard", ct);
        shell.StatusCode.Should().Be(HttpStatusCode.OK);
        using var health = await client.GetAsync("/api/health", ct);
        health.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RejectedClerkTokens_AreChargedToTheAnonymousRateLimit()
    {
        // Internal endpoints use the default (Clerk) scheme, so UseAuthentication
        // fails the token first and the policy evaluator re-challenges it; that
        // path must still be charged by RateLimitedAuthorizationResultHandler.
        using var factory = new StartupTestFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "forged-clerk-jwt");
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 60; i++)
        {
            using var rejected = await client.GetAsync("/api/measurements/manual", ct);
            rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"request {i} should be under the limit");
        }

        using var limited = await client.GetAsync("/api/measurements/manual", ct);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        (await limited.Content.ReadAsStringAsync(ct)).Should().Contain("\"errorCode\":\"RATE_LIMITED\"");

        // The forged tokens drained the same per-peer bucket anonymous sharing reads use.
        using var anonymousClient = factory.CreateHttpsClient();
        using var anonymousResponse = await anonymousClient.GetAsync("/api/profile/disabled-share", ct);
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static WebApplicationFactory<Program> IngressHeaderFactory(StartupTestFactory root) =>
        root.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["RateLimiting:ClientAddressHeaders"] = "do-connecting-ip;cf-connecting-ip" })));

    private static HttpRequestMessage AnonymousRequest(string clientAddress)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/profile/disabled-share");
        request.Headers.Add("do-connecting-ip", clientAddress);
        return request;
    }

    [Fact]
    public async Task AnonymousApiRequests_ArePartitionedByTheConfiguredClientAddressHeader()
    {
        using var root = new StartupTestFactory();
        using var factory = IngressHeaderFactory(root);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 60; i++)
        {
            using var allowed = await client.SendAsync(AnonymousRequest("203.0.113.10"), ct);
            allowed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        using var exhausted = await client.SendAsync(AnonymousRequest("203.0.113.10"), ct);
        exhausted.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // A different client address, and a request without the header (peer fallback), keep their own budget.
        using var otherClient = await client.SendAsync(AnonymousRequest("203.0.113.11"), ct);
        otherClient.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var noHeader = await client.GetAsync("/api/profile/disabled-share", ct);
        noHeader.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnonymousApiRequests_AreCappedByOneCeilingAcrossClientAddresses()
    {
        using var root = new StartupTestFactory();
        using var factory = IngressHeaderFactory(root);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var ct = TestContext.Current.CancellationToken;

        // Rotating the client address on every request never exhausts a per-client
        // budget, so only the shared ceiling can stop this burst.
        for (var i = 0; i < 300; i++)
        {
            using var allowed = await client.SendAsync(AnonymousRequest($"198.51.100.{i % 250 + 1}"), ct);
            allowed.StatusCode.Should().Be(HttpStatusCode.NotFound, $"request {i} should be under the ceiling");
        }

        using var rejected = await client.SendAsync(AnonymousRequest("192.0.2.99"), ct);
        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await rejected.Content.ReadAsStringAsync(ct)).Should().Contain("\"errorCode\":\"RATE_LIMITED\"");

        // Authenticated traffic does not share the ceiling.
        using var authenticated = new HttpRequestMessage(HttpMethod.Get, "/api/v1/measurements/manual");
        authenticated.Headers.Add("X-Api-Key", StartupTestFactory.ApiKey);
        using var authenticatedResponse = await client.SendAsync(authenticated, ct);
        authenticatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/profile/disabled-share")]
    [InlineData("/api/data/disabled-share")]
    [InlineData("/api/providers/links/disabled-share")]
    public async Task DisabledSharing_CannotBeReadAnonymously(string path)
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/profile/disabled-share", "/api/profile/{sharing-token}")]
    [InlineData("/api/data/disabled-share", "/api/data/{sharing-token}")]
    [InlineData("/api/providers/links/disabled-share", "/api/providers/links/{sharing-token}")]
    public async Task SharingTokens_AreRedactedFromHttpRequestLogs(string path, string loggedPath)
    {
        // Production raises the HTTP logging middleware to Information, so every
        // shared-dashboard hit is logged; the token segment must not be.
        var logs = new CapturingLoggerProvider();
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var httpLogs = logs.Entries
            .Where(e => e.Category == "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware")
            .Select(e => e.Message)
            .ToList();
        httpLogs.Should().Contain(m => m.Contains($"Path: {loggedPath}"));
        httpLogs.Should().NotContain(m => m.Contains("disabled-share"));
    }

    [Fact]
    public async Task SharedRoutes_NeverWriteTheSharingCodeToAnyLog()
    {
        // The HTTP log is only one place a code could leak: routing, auth, rate
        // limiting and the controllers all log too, so every category is captured.
        // A dedicated host keeps these three anonymous requests out of the shared
        // rate-limit budget described on StartupTestFactory.
        const string code = "shr-secret-0123456789abc";
        var logs = new CapturingLoggerProvider();
        using var host = new StartupTestFactory();
        using var factory = host.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        foreach (var path in new[] { $"/api/profile/{code}", $"/api/data/{code}", $"/api/providers/links/{code}" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, "the factory's profile service knows no such token ({0})", path);
        }

        logs.ShouldNotMention(code);
        logs.Entries.Should().Contain(
            e => e.Category == "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware" && e.Message.Contains(LogSafePath.Placeholder),
            "the redacting interceptor must be active for the absence of the code to mean anything");
    }

    [Theory]
    [InlineData("/api/missing", "GET")]
    [InlineData("/api/missing/", "GET")]
    [InlineData("/API/missing", "HEAD")]
    [InlineData("/api", "GET")]
    [InlineData("/dashboard", "POST")]
    public async Task NonSpaRequests_DoNotReceiveApplicationShell(string path, string method)
    {
        using var client = _factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Location.Should().BeNull();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(StartupTestFactory.Shell);
    }

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/index.html")]
    public async Task ApplicationShell_IsNeverStoredInCaches(string path)
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be(StartupTestFactory.Shell);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    public async Task DevelopmentHostWithBuiltShell_ServesTheApplicationShell(string path)
    {
        // The documented local container runs with ASPNETCORE_ENVIRONMENT=Development
        // (the only way PublicBaseUrl accepts http) and must still serve the SPA.
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be(StartupTestFactory.Shell);
    }

    [Fact]
    public async Task HostWithoutBuiltShell_DoesNotServeTheApplicationShell()
    {
        // Local development has no wwwroot: the Vite dev server hosts the SPA and
        // the API must not claim non-API routes.
        var emptyWebRoot = Path.Combine(Path.GetTempPath(), $"trendweight-noshell-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyWebRoot);
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseWebRoot(emptyWebRoot);
            });
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

            using var response = await client.GetAsync("/dashboard", TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            Directory.Delete(emptyWebRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("/assets/app-abcdefgh.js")]
    [InlineData("/assets/index-CGpSZy-r.css")]
    [InlineData("/assets/inter-latin-wght-normal-D-QHGJNG.woff2")]
    public async Task HashedAssets_KeepImmutableCaching(string path)
    {
        // Vite's base64url hashes can contain '-', and fonts and images are hashed too.
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromDays(365));
        response.Headers.CacheControl.ToString().Should().Contain("immutable");
    }

    [Fact]
    public async Task UnhashedStaticFiles_KeepShortCaching()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync("/robots.txt", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromHours(1));
        response.Headers.CacheControl.ToString().Should().NotContain("immutable");
    }

    [Theory]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    public async Task AmbiguousPaths_CannotRedirectToExternalHost(string path)
    {
        // Set the raw path directly; HttpClient/Uri would normalize these paths
        // before they reach the server and hide the redirect vulnerability.
        var response = await _factory.Server.SendAsync(context =>
        {
            context.Request.Method = "GET";
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = path;
        }, TestContext.Current.CancellationToken);

        response.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        response.Response.Headers.Location.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/u/12345/chart/weight.png")]
    [InlineData("/U/legacy-user/CHART/trend.PNG")]
    public async Task LegacyChartImages_RedirectPermanentlyToThePlaceholder(string path)
    {
        // Classic TrendWeight chart images were embedded on other sites.
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.OriginalString.Should().Be("/chart-not-available.png");
    }

    [Theory]
    [InlineData("/u/12345", HttpStatusCode.OK)]
    [InlineData("/u/12345/chart/weight.svg", HttpStatusCode.NotFound)]
    [InlineData("/u/12345/profile/weight.png", HttpStatusCode.NotFound)]
    public async Task OtherLegacyUserPaths_AreNotRedirected(string path, HttpStatusCode expected)
    {
        // Only chart PNGs are redirected; other file-like paths are unknown files
        // (the fallback pattern excludes them) and extension-less ones get the shell.
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(expected);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task TrailingSlashRedirect_StaysLocalAndPreservesQuery()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync("/dashboard/?range=month", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.OriginalString.Should().Be("/dashboard?range=month");
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.20.30.40")]
    [InlineData("10.30.40.50")]
    [InlineData("203.0.113.10")]
    public async Task ForwardedHeaders_AreIgnoredAndInternalHttpDoesNotRedirect(string peer)
    {
        var response = await _factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            context.Request.Method = "GET";
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = "/api/health";
            context.Request.Headers["X-Forwarded-For"] = "198.51.100.20";
            context.Request.Headers["X-Forwarded-Host"] = "forwarded.example";
            context.Request.Headers["X-Forwarded-Proto"] = "https";
        }, TestContext.Current.CancellationToken);

        response.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        response.Request.Scheme.Should().Be("http");
        response.Request.Host.Host.Should().Be("localhost");
        response.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse(peer));
    }
}

// Shared as an IClassFixture, so every test that hits _factory directly runs
// against one host. TestServer supplies no RemoteIpAddress, so anonymous and
// rejected-credential API requests all land in the single "anonymous:unknown"
// partition (60/min) and the shared anonymous ceiling. Roughly 15 such requests
// accumulate across the class today; a test that needs to exhaust a limit must
// create its own StartupTestFactory instead of drawing down the shared budget.
public sealed class StartupTestFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "sk-pipeline-test-key";
    public const string Shell = "<!doctype html><title>Pipeline test shell</title>";
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"trendweight-pipeline-{Guid.NewGuid():N}");

    public StartupTestFactory()
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), Shell);
        File.WriteAllText(Path.Combine(_webRoot, "robots.txt"), "User-agent: *");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app-abcdefgh.js"), "/* test asset */");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-CGpSZy-r.css"), "/* test asset */");
        File.WriteAllBytes(Path.Combine(_webRoot, "assets", "inter-latin-wght-normal-D-QHGJNG.woff2"), [0x77, 0x4F, 0x46, 0x32]);
    }

    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseWebRoot(_webRoot);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
            ["PublicBaseUrl"] = "https://canonical.example",
            // Production refuses the appsettings.json placeholders; these are synthetic.
            ["Clerk:Authority"] = "https://pipeline-test.clerk.accounts.dev",
            ["Clerk:SecretKey"] = "sk_test_pipeline_synthetic",
            ["Supabase:Url"] = "https://pipeline-test.supabase.co",
            ["Supabase:ServiceKey"] = "sb_secret_pipeline_synthetic"
        }));
        builder.ConfigureServices(services =>
        {
            var clerkUserId = Guid.NewGuid();
            var apiUserId = Guid.NewGuid();
            var tokens = new Mock<IClerkTokenService>();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "clerk-test") }, "jwt"));
            tokens.Setup(x => x.ValidateTokenAsync("test-clerk-jwt", It.IsAny<string>())).ReturnsAsync(principal);
            tokens.Setup(x => x.GetClerkUserId(principal)).Returns("clerk-test");
            tokens.Setup(x => x.GetEmail(principal)).Returns("clerk@example.com");
            var mapping = new Mock<IUserAccountMappingService>();
            mapping.Setup(x => x.GetOrCreateMappingAsync("clerk-test", "clerk@example.com", "clerk"))
                .ReturnsAsync(new DbUserAccount { Uid = clerkUserId, ExternalId = "clerk-test", Provider = "clerk" });
            var profiles = new Mock<IProfileService>();
            profiles.Setup(x => x.GetByApiKeyHashAsync(ApiKeyService.HashKey(ApiKey)))
                .ReturnsAsync(new DbProfile { Uid = apiUserId, Email = "api@example.com", Profile = new ProfileData() });
            profiles.Setup(x => x.GetBySharingTokenAsync("disabled-share"))
                .ReturnsAsync(new DbProfile { Uid = Guid.NewGuid(), Profile = new ProfileData { SharingEnabled = false } });
            var manual = new Mock<IManualDataService>();
            manual.Setup(x => x.GetReadingsAsync(clerkUserId))
                .ReturnsAsync(new List<RawMeasurement> { new() { Date = "2024-01-01", Weight = 73m } });
            manual.Setup(x => x.GetReadingsAsync(apiUserId))
                .ReturnsAsync(new List<RawMeasurement> { new() { Date = "2024-01-01", Weight = 82m } });

            services.RemoveAll<IClerkTokenService>();
            services.RemoveAll<IUserAccountMappingService>();
            services.RemoveAll<IProfileService>();
            services.RemoveAll<IManualDataService>();
            services.RemoveAll<ISupabaseService>();
            services.AddSingleton(tokens.Object);
            services.AddSingleton(mapping.Object);
            services.AddSingleton(profiles.Object);
            services.AddSingleton(manual.Object);
            services.AddSingleton(Mock.Of<ISupabaseService>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }
}
