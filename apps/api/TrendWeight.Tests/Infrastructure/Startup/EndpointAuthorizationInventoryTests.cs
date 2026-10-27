using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrendWeight.Infrastructure.Auth;

namespace TrendWeight.Tests.Infrastructure.Startup;

/// <summary>
/// Walks every routed /api endpoint the real app maps, so a new controller, a new
/// action, or a stray [AllowAnonymous] cannot ship without someone updating the
/// allowlist below on purpose.
/// </summary>
public class EndpointAuthorizationInventoryTests : IClassFixture<StartupTestFactory>
{
    // "METHOD route-pattern" for every /api endpoint that may be called without credentials.
    private static readonly HashSet<string> AnonymousEndpoints = new(StringComparer.Ordinal)
    {
        "GET api/health",
        "GET api/data/{sharingCode}",
        "GET api/profile/{sharingCode}",
        "GET api/providers/links/{sharingCode}",
        // Plausible analytics proxy (YARP), throttled by Plausible.
        "* api/event",
    };

    private readonly StartupTestFactory _factory;

    public EndpointAuthorizationInventoryTests(StartupTestFactory factory) => _factory = factory;

    [Fact]
    public void AnonymousApiEndpoints_MatchTheAllowlist()
    {
        var anonymous = ApiEndpoints()
            .Where(e => IsAnonymous(e.Endpoint))
            .SelectMany(e => e.Keys)
            .ToHashSet();

        anonymous.Should().BeEquivalentTo(AnonymousEndpoints,
            "an /api endpoint that skips authentication must be added to the allowlist deliberately");
    }

    [Fact]
    public void V1Endpoints_AcceptOnlyApiKeys_AndInternalEndpointsNever()
    {
        foreach (var (endpoint, keys) in ApiEndpoints().Where(e => !IsAnonymous(e.Endpoint)))
        {
            var schemes = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .SelectMany(a => (a.AuthenticationSchemes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct()
                .ToList();

            if (IsV1(endpoint))
            {
                schemes.Should().Equal([ApiKeyAuthenticationHandler.SchemeName], "{0} is part of the public API", string.Join(", ", keys));
            }
            else
            {
                schemes.Should().NotContain(ApiKeyAuthenticationHandler.SchemeName, "{0} is internal and Clerk-only", string.Join(", ", keys));
            }
        }
    }

    [Fact]
    public async Task EveryProtectedApiEndpoint_RejectsMissingAndWrongKindsOfCredentials()
    {
        // Each request comes from its own client address so the per-client anonymous
        // rate limit (60/min) never turns an expected 401 into a 429.
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["RateLimiting:ClientAddressHeaders"] = "do-connecting-ip" })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var requestCount = 0;
        var protectedEndpoints = ApiEndpoints().Where(e => !IsAnonymous(e.Endpoint)).ToList();
        protectedEndpoints.Should().NotBeEmpty();

        foreach (var (endpoint, _) in protectedEndpoints)
        {
            var path = "/" + SamplePath(endpoint.RoutePattern);
            // A Clerk session must not unlock /api/v1, and an API key must not unlock internal endpoints.
            var wrongCredential = IsV1(endpoint) ? "test-clerk-jwt" : StartupTestFactory.ApiKey;

            foreach (var method in Methods(endpoint))
            {
                foreach (var credential in new[] { null, wrongCredential })
                {
                    using var request = new HttpRequestMessage(new HttpMethod(method), path);
                    request.Headers.Add("do-connecting-ip", $"10.0.{requestCount / 250}.{requestCount++ % 250 + 1}");
                    if (credential != null)
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
                    }
                    if (method is "POST" or "PUT" or "PATCH")
                    {
                        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                    }

                    using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                        "{0} {1} with {2} must be rejected", method, path, credential == null ? "no credentials" : "the wrong kind of credential");
                }
            }
        }
    }

    [Theory]
    [InlineData("api", true, false)]
    [InlineData("API/health", true, false)]
    [InlineData("api/v1", true, true)]
    [InlineData("api/v1/measurements", true, true)]
    [InlineData("api/v10/measurements", true, false)]
    [InlineData("apiary", false, false)]
    [InlineData("openapi/v1.json", false, false)]
    public void RoutePrefixes_IncludeTheExactPrefixRoute(string route, bool underApi, bool underV1)
    {
        IsUnder(route, "api").Should().Be(underApi);
        IsUnder(route, "api/v1").Should().Be(underV1);
    }

    private List<(RouteEndpoint Endpoint, List<string> Keys)> ApiEndpoints()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => IsUnder(RouteText(e), "api"))
            .Select(e => (Endpoint: e, Keys: Methods(e).Select(m => $"{m} {e.RoutePattern.RawText!.TrimStart('/')}").ToList()))
            .ToList();

        // Guard against the walk silently finding nothing (e.g. a routing change).
        endpoints.Should().Contain(e => IsV1(e.Endpoint));
        return endpoints;
    }

    private static IEnumerable<string> Methods(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods is { Count: > 0 } methods ? methods : ["*"];

    private static bool IsAnonymous(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null
        || !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any();

    private static bool IsV1(RouteEndpoint endpoint) => IsUnder(RouteText(endpoint), "api/v1");

    private static string RouteText(RouteEndpoint endpoint) => (endpoint.RoutePattern.RawText ?? "").TrimStart('/');

    // A route is under a prefix when it is the prefix itself or a path below it, so an
    // endpoint mapped at exactly /api or /api/v1 is still walked and classified.
    private static bool IsUnder(string route, string prefix) =>
        route.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || route.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);

    private static string SamplePath(RoutePattern pattern) => string.Join('/', pattern.PathSegments.Select(segment =>
        string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => SampleValue(parameter),
            _ => throw new InvalidOperationException($"Unexpected route part in {pattern.RawText}")
        }))));

    private static string SampleValue(RoutePatternParameterPart parameter)
    {
        var constraints = parameter.ParameterPolicies.Select(p => p.Content ?? "").ToList();
        if (constraints.Contains("guid")) return Guid.Empty.ToString();
        if (constraints.Contains("int") || constraints.Contains("long")) return "1";
        if (parameter.Name.Contains("date", StringComparison.OrdinalIgnoreCase)) return "2024-01-01";
        return "sample";
    }
}
