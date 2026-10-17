using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestaurantManagement.Modules.Identity.Services;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The reset email worker runs by default and can be switched off. Every test host switches it off
/// (TestHostSettings), so tests run the processor themselves and stay predictable.
/// </summary>
public class PasswordResetEmailWorkerWiringTests
{
    [Fact]
    [Trait("Rule", "D19")]
    public void HostedServices_EmailWorkerEnabled_RegistersWorker()
    {
        // Arrange
        using var factory = new AuthApiFactory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["PasswordResetEmails:Enabled"] = "true" })));

        // Act
        var hostedServices = factory.Services.GetServices<IHostedService>();

        // Assert
        hostedServices.ShouldContain(service => service is PasswordResetEmailWorker, "queued reset emails are only sent while the worker runs");
    }

    [Fact]
    [Trait("Rule", "D20")]
    public void HostedServices_EmailWorkerDisabledInTests_DoesNotRegisterWorker()
    {
        // Arrange
        using var factory = new AuthApiFactory();

        // Act
        var hostedServices = factory.Services.GetServices<IHostedService>();

        // Assert
        hostedServices.ShouldNotContain(service => service is PasswordResetEmailWorker, "tests must not send queued emails on a timer");
    }
}
