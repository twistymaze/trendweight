using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace TrendWeight.Tests.Infrastructure.Startup;

/// <summary>
/// Controller unit tests construct controllers with <c>new</c>, so a missing or
/// mis-scoped registration would only surface as a 500 in production. These tests
/// build the real Production container instead.
/// </summary>
public class ServiceRegistrationTests : IClassFixture<StartupTestFactory>
{
    private readonly StartupTestFactory _factory;

    public ServiceRegistrationTests(StartupTestFactory factory) => _factory = factory;

    public static TheoryData<Type> ControllerTypes() => new(typeof(Program).Assembly.GetTypes()
        .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && t is { IsAbstract: false, IsPublic: true })
        .OrderBy(t => t.FullName));

    [Theory]
    [MemberData(nameof(ControllerTypes))]
    public void EveryController_CanBeActivatedFromTheRequestScope(Type controllerType)
    {
        using var scope = _factory.Services.CreateScope();

        // MVC activates controllers this way (they are not registered themselves).
        var controller = ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);

        controller.Should().BeOfType(controllerType);
    }

    [Fact]
    public void ControllerDiscovery_FindsTheKnownControllers()
    {
        // Guards the theory above against silently enumerating nothing.
        ControllerTypes().Should().HaveCountGreaterThanOrEqualTo(11);
    }

    [Fact]
    public void Container_ValidatesOnBuild_WithScopeChecks()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            }));

        var act = () => factory.Services;

        act.Should().NotThrow("every registered service must be constructible without a captive scoped dependency");
    }
}
