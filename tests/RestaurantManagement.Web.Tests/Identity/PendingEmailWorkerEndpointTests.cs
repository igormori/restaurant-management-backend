using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The real worker loop over HTTP on PostgreSQL: Maria asks for a reset and, with no test calling
/// the processor, the worker sends her the email and removes the queued request on its own timer.
/// </summary>
public class PendingEmailWorkerEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string MariaEmail = "maria@cantinadanonna.com.br";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly PostgresDatabaseFixture _postgres;

    public PendingEmailWorkerEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    // Scenario 28
    [Fact]
    [Trait("Rule", "D22")]
    public async Task ForgotPassword_WorkerEnabledWithShortInterval_EmailsMariaAndRemovesRequestWithoutAnyoneCallingTheProcessor()
    {
        // Arrange
        var emailService = Substitute.For<IEmailService>();
        await using var tenantFactory = await TenantApiFactory.CreateAsync(_postgres);
        // UseSetting, unlike in-memory configuration, is visible while Program.cs registers the worker.
        var factory = tenantFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PendingEmails:Enabled", "true");
            builder.UseSetting("PendingEmails:IntervalSeconds", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton(emailService);
            });
        });
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.Add(new User
            {
                Email = MariaEmail,
                PasswordHash = "hash",
                FirstName = "Maria",
                LastName = "Souza",
                IsVerified = true
            });
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().SaveChangesAsync();
        }

        // Act
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = MariaEmail });
        var queued = 1;
        var deadline = DateTime.UtcNow + WaitLimit;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = factory.Services.CreateScope();
            queued = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<PendingEmail>().CountAsync();
            if (queued == 0)
                break;
            await Task.Delay(200);
        }

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        queued.ShouldBe(0, "the worker removes the request once it has handled it");
        await emailService.Received(1).SendPasswordResetEmailAsync(MariaEmail, "Maria", Arg.Any<string>(), Arg.Any<int>());
    }
}
