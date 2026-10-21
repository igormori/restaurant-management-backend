using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services.Registration;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The real worker loop over HTTP on PostgreSQL: a queued verification request is emailed to Maria
/// by the worker on its own timer, and the code in that email verifies her account.
/// </summary>
public class VerificationEmailWorkerEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string MariaEmail = "maria@cantinadanonna.com.br";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly PostgresDatabaseFixture _postgres;

    public VerificationEmailWorkerEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "D27")]
    public async Task Verify_QueuedVerificationRequestWithWorkerRunning_CodeInTheEmailVerifiesMaria()
    {
        // Arrange
        string? emailedCode = null;
        var emailService = Substitute.For<IEmailService>();
        emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(code => emailedCode = code), Arg.Any<int>())
            .Returns(Task.CompletedTask);
        await using var tenantFactory = await TenantApiFactory.CreateAsync(_postgres);
        var factory = tenantFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PendingEmails:Enabled", "true");
            builder.UseSetting("PendingEmails:IntervalSeconds", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IVerificationService>();
                services.AddScoped<IVerificationService, VerificationService>();
                services.RemoveAll<IEmailService>();
                services.AddSingleton(emailService);
            });
        });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Users.Add(new User { Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = false });
            db.Set<PendingEmail>().Add(new PendingEmail { Purpose = PendingEmailPurpose.Verification, Email = MariaEmail });
            await db.SaveChangesAsync();
        }

        // Act
        var deadline = DateTime.UtcNow + WaitLimit;
        while (emailedCode == null && DateTime.UtcNow < deadline)
            await Task.Delay(200);
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/verify", new { email = MariaEmail, code = emailedCode ?? "000000" });

        // Assert
        emailedCode.ShouldNotBeNull("the worker emails the queued verification request without anyone calling the processor");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the code in the email verifies the account");
        using var checkScope = factory.Services.CreateScope();
        var maria = await checkScope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.SingleAsync(u => u.Email == MariaEmail);
        maria.IsVerified.ShouldBeTrue("the account is verified once the emailed code is accepted");
    }
}
