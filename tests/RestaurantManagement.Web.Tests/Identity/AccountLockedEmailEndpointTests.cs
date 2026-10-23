using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Sessions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The real worker loop over HTTP on PostgreSQL: 20 wrong passwords from five addresses lock Maria's whole
/// account and, with nobody calling the processor, the worker emails her once.
/// </summary>
public class AccountLockedEmailEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string MariaEmail = "maria@cantinadanonna.com.br";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly PostgresDatabaseFixture _postgres;

    public AccountLockedEmailEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "L34")]
    public async Task Login_TwentyWrongPasswordsAcrossAddressesWithWorkerRunning_EmailsTheAccountExactlyOnce()
    {
        // Arrange
        await using var tenant = await TenantApiFactory.CreateAsync(_postgres);
        var app = tenant.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PendingEmails:Enabled", "true");
            builder.UseSetting("PendingEmails:IntervalSeconds", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISessionService>();
                services.AddScoped<ISessionService, SessionService>();
            });
        });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Users.Add(new User
            {
                Email = MariaEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Massa@Fresca2026", 4),
                FirstName = "Maria",
                LastName = "Souza",
                IsVerified = true
            });
            await db.SaveChangesAsync();
        }
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        // Act
        for (var address = 101; address <= 105; address++)
            for (var attempt = 0; attempt < 4; attempt++)
                await WrongLoginAsync(client, $"203.0.113.{address}");
        await WrongLoginAsync(client, "203.0.113.106");
        var deadline = DateTime.UtcNow + WaitLimit;
        while (DateTime.UtcNow < deadline && tenant.EmailService.ReceivedCalls().Count() < 1)
            await Task.Delay(200);
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert
        await tenant.EmailService.Received(1).SendEmailAsync(MariaEmail, Arg.Any<string>(), Arg.Any<string>());
        using var readScope = app.Services.CreateScope();
        var queued = await readScope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .PendingEmails.CountAsync(r => r.Purpose == PendingEmailPurpose.AccountLocked);
        queued.ShouldBe(0, "the worker removes the request once it has sent the email");
    }

    private static async Task WrongLoginAsync(HttpClient client, string remoteIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = MariaEmail, password = "Errada@Pizza2026" })
        };
        request.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, remoteIp);
        await client.SendAsync(request);
    }
}
