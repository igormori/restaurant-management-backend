using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestaurantManagement.Modules.Identity.Services.Sessions;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The cleanup job is switched off for every test host (TestHostSettings), so test runs never
/// delete rows from the database the host is configured with.
/// </summary>
public class RefreshTokenCleanupWiringTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    [Trait("Rule", "D7")]
    public void HostedServices_CleanupDisabledInTests_DoesNotRegisterCleanupWorker()
    {
        // Arrange / Act
        var hostedServices = _factory.Services.GetServices<IHostedService>();

        // Assert
        hostedServices.ShouldNotContain(service => service is RefreshTokenCleanupWorker, "tests must not run the cleanup job, so test runs never delete rows from a real database");
    }
}
