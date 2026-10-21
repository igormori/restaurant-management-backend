using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestaurantManagement.Modules.Identity.Services.Registration;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// A zero or negative interval would crash the worker's timer and, by default, stop the whole app,
/// so the app refuses to start and names the setting instead.
/// </summary>
public class PendingEmailIntervalTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [Trait("Rule", "D23")]
    public void Startup_EmailWorkerIntervalZeroOrBelow_RefusesToStartNamingTheSetting(string seconds)
    {
        // Arrange
        using var factory = new AuthApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PendingEmails:Enabled", "true");
            builder.UseSetting("PendingEmails:IntervalSeconds", seconds);
        });

        // Act
        var start = () => factory.Services;

        // Assert
        var error = start.ShouldThrow<Exception>("a bad interval must stop the app at startup, not later inside the worker");
        Messages(error).ShouldContain(m => m.Contains("PendingEmails:IntervalSeconds"), "the error names the setting to fix");
    }

    [Fact]
    [Trait("Rule", "D23")]
    public void Startup_EmailWorkerIntervalAboveZero_StartsAndRegistersWorker()
    {
        // Arrange
        using var factory = new AuthApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PendingEmails:Enabled", "true");
            builder.UseSetting("PendingEmails:IntervalSeconds", "1");
        });

        // Act
        var hostedServices = factory.Services.GetServices<IHostedService>();

        // Assert
        hostedServices.ShouldContain(service => service is PendingEmailWorker, "a valid interval keeps the worker running");
    }

    private static IEnumerable<string> Messages(Exception? error)
    {
        for (; error != null; error = error.InnerException)
            yield return error.Message;
    }
}
