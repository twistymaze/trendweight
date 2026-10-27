using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrendWeight.Features.ApiKeys.Models;
using TrendWeight.Features.Measurements.Manual.Models;
using TrendWeight.Features.Measurements.Models;
using TrendWeight.Features.Profile.Models;
using TrendWeight.Features.Providers.Models;
using TrendWeight.Features.Sharing.Models;
using TrendWeight.Features.SyncProgress;
using TrendWeight.Infrastructure.Configuration;
using TrendWeight.Infrastructure.DataAccess;
using TrendWeight.Tests.Infrastructure.Startup;

namespace TrendWeight.Tests.Contracts;

/// <summary>
/// Serializes every response type the web app reads, with every property populated,
/// exactly as the running API would, and compares the result with the JSON files in
/// apps/web/src/test/contracts. The web suite checks its TypeScript types against the
/// same files, so renaming or retyping a property fails one side or the other.
/// After an intentional contract change, regenerate the files with
/// <c>UPDATE_API_CONTRACTS=1 dotnet test --filter-class '*ApiContractSnapshotTests'</c>.
/// </summary>
public class ApiContractSnapshotTests : IClassFixture<StartupTestFactory>
{
    private static readonly DateTime SampleTime = new(2024, 1, 15, 6, 30, 0, DateTimeKind.Utc);
    private static readonly Guid SampleGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly StartupTestFactory _factory;

    public ApiContractSnapshotTests(StartupTestFactory factory) => _factory = factory;

    public static TheoryData<string, Type> HttpContracts() => new()
    {
        { "measurements-response", typeof(MeasurementsResponse) },
        { "profile-response", typeof(ProfileResponse) },
        { "sharing-response", typeof(SharingResponse) },
        { "sharing-token-response", typeof(SharingTokenResponse) },
        { "provider-links", typeof(List<ProviderLinkResponse>) },
        { "providers-config", typeof(ProvidersConfigResponse) },
        { "api-key-metadata", typeof(ApiKeyMetadata) },
        { "generated-api-key", typeof(GeneratedApiKey) },
        { "manual-readings", typeof(List<ManualMeasurementResponse>) },
    };

    [Theory]
    [MemberData(nameof(HttpContracts))]
    public void HttpResponse_MatchesTheWebContract(string name, Type responseType)
    {
        // The MVC serializer options the controllers actually use (camelCase, string
        // enums, nulls omitted), not a copy of them.
        var options = _factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;

        var json = JsonSerializer.SerializeToNode(Populate(responseType), responseType, options)!;

        AssertMatchesContract(name, json);
    }

    [Fact]
    public async Task RealtimeSyncProgress_MatchesTheWebContract()
    {
        string? body = null;
        var handler = new CapturingHandler(content => body = content);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        var supabase = new SupabaseService(
            Options.Create(new AppOptions { Supabase = new SupabaseConfig { Url = "https://contract.example", ServiceKey = "sb_secret_contract" } }),
            Mock.Of<ILogger<SupabaseService>>(),
            httpClientFactory.Object);

        (await supabase.BroadcastAsync("sync-progress", "progress", Populate(typeof(SyncProgressMessage)))).Should().BeTrue();

        var payload = JsonNode.Parse(body!)!["messages"]![0]!["payload"]!;
        AssertMatchesContract("sync-progress-message", payload);
    }

    [Fact]
    public async Task PublicOpenApiDocument_MatchesTheSnapshot()
    {
        // The v1 document is what external API users code against; any change to it
        // should show up as a reviewed diff of openapi-v1.json.
        using var client = _factory.CreateHttpsClient();

        var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken))!;

        AssertMatchesSnapshot(RepoPath("apps", "api", "TrendWeight.Tests", "Contracts", "openapi-v1.json"), document,
            "the public v1 OpenAPI document changed");
    }

    private static void AssertMatchesContract(string name, JsonNode actual) =>
        AssertMatchesSnapshot(RepoPath("apps", "web", "src", "test", "contracts", $"{name}.json"), actual,
            $"the {name} response shape changed; make the web types match");

    private static void AssertMatchesSnapshot(string path, JsonNode actual, string because)
    {
        var serialized = actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";

        if (Environment.GetEnvironmentVariable("UPDATE_API_CONTRACTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, serialized);
            return;
        }

        File.Exists(path).Should().BeTrue($"{path} is missing; run with UPDATE_API_CONTRACTS=1 to create it");
        JsonNode.DeepEquals(actual, JsonNode.Parse(File.ReadAllText(path))).Should().BeTrue(
            $"{because}. If that is intended, regenerate {path} with UPDATE_API_CONTRACTS=1. Actual:\n{serialized}");
    }

    private static string RepoPath(params string[] segments)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "apps", "web")))
            {
                return Path.Combine([dir.FullName, .. segments]);
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root above the test output directory");
    }

    // Builds an instance with every settable property filled in, so a property added,
    // renamed, or removed on the C# side always shows up in the serialized contract.
    private static object Populate(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string)) return "sample";
        if (underlying == typeof(bool)) return true;
        if (underlying == typeof(int) || underlying == typeof(long)) return Convert.ChangeType(3, underlying);
        if (underlying == typeof(decimal)) return 1.5m;
        if (underlying == typeof(double)) return 1.5d;
        if (underlying == typeof(DateTime)) return SampleTime;
        if (underlying == typeof(DateTimeOffset)) return new DateTimeOffset(SampleTime);
        if (underlying == typeof(Guid)) return SampleGuid;
        if (underlying.IsEnum) return Enum.GetValues(underlying).GetValue(0)!;

        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var dictionary = (IDictionary)Activator.CreateInstance(underlying)!;
            var args = underlying.GetGenericArguments();
            dictionary.Add(args[0] == typeof(string) ? "withings" : Populate(args[0]), Populate(args[1]));
            return dictionary;
        }

        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)Activator.CreateInstance(underlying)!;
            list.Add(Populate(underlying.GetGenericArguments()[0]));
            return list;
        }

        var instance = Activator.CreateInstance(underlying)
            ?? throw new InvalidOperationException($"Cannot create {underlying}");
        foreach (var property in underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            property.SetValue(instance, Populate(property.PropertyType));
        }

        return instance;
    }

    private sealed class CapturingHandler(Action<string> capture) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            capture(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }
}
