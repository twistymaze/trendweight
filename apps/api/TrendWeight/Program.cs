using TrendWeight.Infrastructure.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using TrendWeight.Features.ApiV1;
using TrendWeight.Features.Measurements.Manual;
using TrendWeight.Infrastructure.Extensions;
using TrendWeight.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Use camelCase for all JSON property names
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        // Serialize enums by name rather than by number
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.WriteIndented = false;
    })
    // The public v1 API documents { error } for 400s, including binding failures
    .ConfigureApiBehaviorOptions(V1InvalidModelStateResponse.Configure);
builder.Services.AddEndpointsApiExplorer();

// OpenAPI documents served by Scalar: the public v1 API reference (API-key endpoints
// only, grouped via [ApiExplorerSettings(GroupName = "v1")]), plus the full internal
// surface in development.
const string TimeJsonPattern = @"^\d{2}:\d{2}:\d{2}$";
const string ProviderJsonPattern = "^(withings|fitbit|legacy)$";

builder.Services.AddOpenApi("v1", options =>
{
    // .NET 11 defaults to OpenAPI 3.2. Keep the public document on 3.1 so client
    // code generators that do not yet read 3.2 keep working.
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
    options.ShouldInclude = description => description.GroupName == "v1";
    // Document the string formats and value ranges the API enforces. These schema
    // constraints are documentation only - actual validation happens in the
    // controllers (ManualMeasurementValidation) so error responses keep their shape.
    options.AddSchemaTransformer((schema, context, cancellationToken) =>
    {
        switch (context.JsonPropertyInfo?.Name)
        {
            case "date":
                schema.Pattern = ManualMeasurementValidation.DateJsonPattern;
                break;
            case "time":
                schema.Pattern = TimeJsonPattern;
                break;
            case "provider":
                schema.Pattern = ProviderJsonPattern;
                break;
            case "weight":
                schema.ExclusiveMinimum = "0";
                schema.ExclusiveMaximum = "700";
                break;
            case "fatRatio":
                schema.ExclusiveMinimum = "0";
                schema.ExclusiveMaximum = "1";
                break;
        }
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        foreach (var parameter in operation.Parameters ?? [])
        {
            if (parameter is OpenApiParameter { Schema: OpenApiSchema schema } concrete)
            {
                if (concrete.Name is "date" or "since")
                {
                    schema.Pattern = ManualMeasurementValidation.DateJsonPattern;
                }
                else if (concrete.Name == "provider")
                {
                    schema.Pattern = ProviderJsonPattern;
                }
            }
        }
        return Task.CompletedTask;
    });
    // Schema names read better without the C# "V1" DTO prefix (Measurement, not V1Measurement)
    options.CreateSchemaReferenceId = jsonTypeInfo =>
    {
        var id = OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
        return id != null && id.StartsWith("V1", StringComparison.Ordinal) ? id[2..] : id;
    };
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Servers = [new OpenApiServer { Url = context.ApplicationServices.GetRequiredService<PublicUrl>().BaseUri.GetLeftPart(UriPartial.Authority) }];
        document.Info.Title = "TrendWeight API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Programmatic access to your own TrendWeight data: read your measurements, and add or edit weight log entries.\n\n" +
            "Authenticate every request with the API key from your [settings page](/settings), sent as `Authorization: Bearer sk-...` " +
            "(or in an `X-Api-Key` header).\n\n" +
            "All weights are in kilograms, and body fat values are 0–1 ratios (0.225 means 22.5%).";
        // The root tags array controls the sidebar/TOC order in Scalar
        document.Tags = new HashSet<OpenApiTag>
        {
            new OpenApiTag
            {
                Name = "Weight Data",
                Description = "Your daily weight data with trend math applied (combined from every source), "
                    + "plus the raw readings from each connected scale."
            },
            new OpenApiTag
            {
                Name = "Manual Weight Log",
                Description = "Weight entries added manually - in the app or through this API. These endpoints do not "
                    + "cover readings synced from Withings or Fitbit; those are read-only and appear under Weight Data."
            },
        };
        // Declaring the auth scheme gives Scalar an "enter your API key" box and
        // attaches the key to try-it requests
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["API Key"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "sk-...",
            Description = "Your TrendWeight API key from the settings page"
        };
        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("API Key", document)] = new List<string>()
        });
        return Task.CompletedTask;
    });
});
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi("internal", options =>
    {
        options.ShouldInclude = description => description.GroupName != "v1";
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Servers = [new OpenApiServer { Url = context.ApplicationServices.GetRequiredService<PublicUrl>().BaseUri.GetLeftPart(UriPartial.Authority) }];
            return Task.CompletedTask;
        });
    });
}

// Add Clerk authentication (includes Supabase as fallback)
builder.Services.AddClerkAuthentication(builder.Configuration);

// Add TrendWeight services
builder.Services.AddTrendWeightServices(builder.Configuration);

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add rate limiting. Anonymous API traffic is limited per client address and, in
// case that address is ever spoofable, capped by one shared ceiling as well.
var rateLimitingConfig = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingConfig>() ?? new RateLimitingConfig();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartitionResolver.Resolve(context, rateLimitingConfig)),
        PartitionedRateLimiter.Create<HttpContext, string>(RateLimitPartitionResolver.ResolveAnonymousCeiling));

    // Return 429 Too Many Requests, with Retry-After, when rate limit is exceeded
    options.OnRejected = (context, token) => new ValueTask(RateLimitRejection.WriteAsync(context.HttpContext, context.Lease, token));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Add HTTP logging. Sharing tokens travel in the request path, so the logged
// path is redacted by the interceptor.
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath |
                          Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod |
                          Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode |
                          Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
});
builder.Services.AddHttpLoggingInterceptor<SharingTokenLoggingInterceptor>();

// Resolve after building so all configuration sources participate in startup validation.
builder.Services.AddSingleton(services => new PublicUrl(
    services.GetRequiredService<IConfiguration>()["PublicBaseUrl"],
    services.GetRequiredService<IHostEnvironment>().IsDevelopment()));

// Host validation is done by the middleware below so the health probe can be exempt.
// The framework's own host filter also reads AllowedHosts and would reject the probe
// first, so it is told to accept everything.
builder.Services.Configure<HostFilteringOptions>(options => options.AllowedHosts = ["*"]);

var app = builder.Build();
_ = app.Services.GetRequiredService<PublicUrl>(); // Fail startup for missing/invalid origins.
if (!app.Environment.IsDevelopment())
{
    // Development keeps the appsettings placeholders usable for partial local
    // setups; everywhere else the process refuses to start without real values.
    RequiredConfiguration.Validate(app.Configuration);
}

// Configure the HTTP request pipeline
app.UseMiddleware<RequestTimingMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

// Enable HTTP request logging
app.UseHttpLogging();

// Validate the actual Host header; forwarded headers are not consumed.
var allowedHosts = app.Configuration["AllowedHosts"];
if (!string.IsNullOrEmpty(allowedHosts) && allowedHosts != "*")
{
    var allowedHostList = allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    app.Use(async (context, next) =>
    {
        // Container and platform probes reach the health endpoint by localhost or
        // IP; it discloses nothing host-dependent, so it is exempt from the check.
        if (context.Request.Path.Equals("/api/health", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var host = context.Request.Host.Host;
        if (!allowedHostList.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            app.Logger.LogWarning("Rejected request with invalid host: {Host}", host);
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Invalid host header");
            return;
        }
        await next();
    });
}

// API reference docs: /openapi/{document}.json + Scalar UI at /api-docs/{document}.
// Only the "v1" document exists outside development.
app.MapOpenApi();

// The docs header wordmark uses the same Zilla Slab file the SPA ships rather
// than a second copy. In the container the built font sits in wwwroot/assets
// under a hashed name; in dev there is no wwwroot, but the Vite dev server
// serves the file straight out of node_modules via its /@fs/ route. If neither
// resolves, the @font-face is omitted and the wordmark falls back to Georgia.
string? zillaSlabUrl = null;
var spaAssetsDir = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "assets");
if (Directory.Exists(spaAssetsDir))
{
    var fontFile = Directory.EnumerateFiles(spaAssetsDir, "zilla-slab-latin-700-normal-*.woff2").FirstOrDefault();
    if (fontFile != null)
    {
        zillaSlabUrl = "/assets/" + Path.GetFileName(fontFile);
    }
}
if (zillaSlabUrl == null && app.Environment.IsDevelopment())
{
    var devFontPath = Path.GetFullPath(Path.Combine(
        app.Environment.ContentRootPath, "../../../node_modules/@fontsource/zilla-slab/files/zilla-slab-latin-700-normal.woff2"));
    if (File.Exists(devFontPath))
    {
        zillaSlabUrl = "/@fs/" + devFontPath.Replace('\\', '/').TrimStart('/');
    }
}
var zillaSlabFontFace = zillaSlabUrl == null ? "" :
    $$"""
      @font-face {
        font-family: "Zilla Slab";
        font-style: normal;
        font-weight: 700;
        font-display: swap;
        src: url("{{zillaSlabUrl}}") format("woff2");
      }
    """;

app.MapScalarApiReference("/api-docs", options =>
{
    // Keep the reference fully self-contained: no Scalar-hosted AI chat, MCP
    // generation, telemetry, or CDN-served fonts. The only network calls the
    // page should make are to this API itself.
    options.DisableAgent();
    options.DisableMcp();
    options.DisableTelemetry();
    options.DisableDefaultFonts();

    // Presentation defaults: open everything up front and show operation ids
    options.ExpandAllTags();
    options.ExpandAllModelSections();
    options.ExpandAllResponses();
    options.HideClientButton();
    options.ShowOperationId();
    options.WithFavicon("/favicon.ico");

    // Site header above the reference so readers can find their way back to the
    // app. Mirrors the main site's header: solid brand-blue bar in light mode,
    // blue-tinted bar with link-blue accents in dark mode (Scalar toggles the
    // dark-mode/light-mode classes on <body>). The wordmark's Zilla Slab and
    // the logo SVG are served from the frontend's static assets (Vite in dev,
    // wwwroot in the container); if either is unreachable the wordmark falls
    // back to Georgia per the site's font-logo stack.
    // --scalar-custom-header-height tells Scalar's layout to reserve room for a
    // sticky header (its sidebar height becomes 100dvh minus this), so the bar
    // can stay pinned without hiding the bottom of the sidebar.
    options.AddHeadContent(
        $$"""
        <style>
          {{zillaSlabFontFace}}
          :root {
            --scalar-custom-header-height: 48px;
          }
          .tw-header {
            position: sticky;
            top: 0;
            z-index: 100;
            box-sizing: border-box;
            height: var(--scalar-custom-header-height);
            display: flex;
            align-items: center;
            gap: 12px;
            padding: 0 20px;
            background: oklch(0.59 0.12 257.6);
            color: #fff;
            font-family: "Inter Variable", Inter, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
          }
          .tw-header a { color: inherit; text-decoration: none; }
          .tw-brand { display: flex; align-items: center; gap: 8px; }
          .tw-wordmark {
            font-family: "Zilla Slab", Georgia, serif;
            font-weight: 700;
            font-size: 22px;
            line-height: 1.2;
          }
          .tw-brand-mark {
            width: 48px;
            height: 20px;
            background-color: currentColor;
            -webkit-mask: url("/logo-line.svg") no-repeat center / contain;
            mask: url("/logo-line.svg") no-repeat center / contain;
          }
          .tw-label {
            border-left: 1px solid color-mix(in srgb, currentColor 40%, transparent);
            padding-left: 12px;
            font-size: 13px;
            opacity: 0.85;
          }
          .tw-back { margin-left: auto; font-size: 13px; opacity: 0.9; }
          .tw-back:hover { opacity: 1; text-decoration: underline; }
          .dark-mode .tw-header {
            background: color-mix(in oklab, oklch(0.56 0.13 257.6) 10%, var(--scalar-background-1, oklch(0.145 0 0)));
            color: var(--scalar-color-2, oklch(0.708 0 0));
          }
          .dark-mode .tw-brand { color: oklch(0.65 0.16 257.6); }
          @media (max-width: 640px) { .tw-label { display: none; } }
        </style>
        """);
    options.AddHeaderContent(
        """
        <header class="tw-header">
          <a class="tw-brand" href="/">
            <span class="tw-wordmark">TrendWeight</span>
            <span class="tw-brand-mark" aria-hidden="true"></span>
          </a>
          <span class="tw-label">API Reference</span>
          <a class="tw-back" href="/">&larr; Back to the site</a>
        </header>
        """);
});

// The hosting ingress enforces HTTPS. Internal HTTP must not cause redirect loops.

// Handle legacy chart image URLs
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path != null && path.StartsWith("/u/", StringComparison.OrdinalIgnoreCase))
    {
        var segments = path.Split('/');
        // Check if it matches pattern /u/{id}/chart/{filename}.png
        if (segments.Length >= 5 &&
            segments[3].Equals("chart", StringComparison.OrdinalIgnoreCase) &&
            segments[4].EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/chart-not-available.png", permanent: true);
            return;
        }
    }
    await next();
});

// Use static files with custom caching rules for the SPA
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        var headers = ctx.Context.Response.Headers;

        // The shell must be reloaded on deploy, including direct /index.html requests.
        if (path.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            headers.CacheControl = "no-cache, no-store, must-revalidate";
            headers.Pragma = "no-cache";
            headers.Expires = "0";
        }
        // Vite content-hashes everything it emits under /assets (scripts, styles,
        // fonts, images), so the whole directory is immutable. Matching on the
        // directory rather than the file name covers hashes that contain '-' too.
        else if (ctx.Context.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase))
        {
            // Long-term immutable caching for hashed assets
            headers.CacheControl = "public,max-age=31536000,immutable";
        }
        else
        {
            // Short cache for other static files (1 hour)
            headers.CacheControl = "public,max-age=3600";
        }
    }
});

// Analytics proxy endpoints (anonymous, including /api/event) are Plausible's to throttle.
app.MapReverseProxy().DisableRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

// Rate limiting partitions on the authenticated principal, so it must run AFTER
// UseAuthorization: endpoints authenticated via a non-default scheme (e.g. API keys)
// only get their principal assigned to HttpContext.User by the authorization
// middleware's policy evaluation, not by UseAuthentication itself. Requests that
// authorization rejects are counted by RateLimitedAuthorizationResultHandler.
app.UseRateLimiter();

app.MapControllers();

// Health check endpoint for container health checks
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", service = "TrendWeight API", timestamp = DateTime.UtcNow }))
    .DisableRateLimiting();

// Serve the SPA for any non-API route whenever the built shell is present. The
// container image always has one (in any environment, including the documented
// local Development container); in local development the Vite dev server hosts
// the SPA instead and there is no wwwroot.
var indexPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
if (File.Exists(indexPath))
{
    // Custom fallback handler that redirects trailing slashes and serves SPA
    app.MapFallback(async context =>
    {
        var path = context.Request.Path.Value;

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Browser redirects interpret //host and slash-backslash paths as external
        // URLs. Reject these ambiguous paths instead of reflecting them in Location.
        if (path != null && (path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\')))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Redirect trailing slash requests to non-slash URLs (except root "/")
        if (path != null &&
            path.EndsWith('/') &&
            path.Length > 1 &&
            context.Request.Method == "GET")
        {
            var redirectUrl = UriHelper.BuildRelative(
                context.Request.PathBase, new PathString(path.TrimEnd('/')), context.Request.QueryString);

            context.Response.Redirect(redirectUrl, permanent: true);
            return;
        }

        context.Response.ContentType = "text/html";
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";

        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await context.Response.SendFileAsync(indexPath);
        }
    });
}

app.Run();

// Make Program accessible to test projects
public partial class Program { }
