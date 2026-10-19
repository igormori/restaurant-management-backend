using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Two application instances running a processor pass at the same moment over the same due requests
/// on PostgreSQL: the conditional-update claim lets only one of them handle each request.
/// </summary>
public class PasswordResetEmailProcessorConcurrencyTests : IClassFixture<PostgresDatabaseFixture>
{
    private const int RequestCount = 20;

    private readonly PostgresDatabaseFixture _postgres;

    public PasswordResetEmailProcessorConcurrencyTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    // Scenario 31
    [Fact]
    [Trait("Rule", "P25")]
    public async Task ProcessDueAsync_TwoProcessorsRunningTogether_SendExactlyOneEmailPerEligibleRequest()
    {
        // Arrange
        var emailService = Substitute.For<IEmailService>();
        // The first two sends wait for each other, so both passes have claimed rows before either finishes.
        using var overlap = new Barrier(2);
        var sendCalls = 0;
        var passesOverlapped = true;
        emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref sendCalls) <= 2 && !overlap.SignalAndWait(TimeSpan.FromSeconds(10)))
                    passesOverlapped = false;
                return Task.CompletedTask;
            });
        await using var tenantFactory = await TenantApiFactory.CreateAsync(_postgres);
        var factory = tenantFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton(emailService);
        }));
        var now = DateTime.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            for (var i = 0; i < RequestCount; i++)
            {
                var email = $"cliente{i}@cantinadanonna.com.br";
                db.Users.Add(new User { Email = email, PasswordHash = "hash", FirstName = "Cliente", LastName = $"{i}", IsVerified = true });
                db.PasswordResetRequests.Add(new PasswordResetRequest { Email = email, CreatedAt = now.AddSeconds(-10), NextAttemptAt = now.AddSeconds(-1) });
            }
            await db.SaveChangesAsync();
        }
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var processorA = scopeA.ServiceProvider.GetRequiredService<IPasswordResetEmailProcessor>();
        var processorB = scopeB.ServiceProvider.GetRequiredService<IPasswordResetEmailProcessor>();

        // Act
        var handled = await Task.WhenAll(
            Task.Run(() => processorA.ProcessDueAsync(now, CancellationToken.None)),
            Task.Run(() => processorB.ProcessDueAsync(now, CancellationToken.None)));

        // Assert
        passesOverlapped.ShouldBeTrue("both passes must be sending at the same time, or the claim is not tested under contention");
        handled.ShouldAllBe(count => count > 0, "each pass handles some requests, so the two really competed for them");
        await emailService.ReceivedWithAnyArgs(RequestCount).SendPasswordResetEmailAsync(default!, default!, default!, default);
        var sentTo = emailService.ReceivedCalls().Select(c => (string)c.GetArguments()[0]!).ToList();
        sentTo.Distinct().Count().ShouldBe(RequestCount, "each eligible request gets exactly one email, even when two instances run a pass together");
    }
}
