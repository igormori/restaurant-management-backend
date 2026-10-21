using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
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
/// "Resend verification" over HTTP on PostgreSQL with the real service: every email, whatever its
/// account state and whether or not sending works, gets the same status and body. Maria is unverified
/// and eligible; Pedro is verified; Lucia is locked out of verifying; Carlos asked for a code seconds ago.
/// </summary>
public class VerificationResendEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string MariaEmail = "maria@cantinadanonna.com.br";
    private const string ResendUrl = "/api/auth/resend-verification";

    private readonly PostgresDatabaseFixture _postgres;

    public VerificationResendEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<WebApplicationFactoryWithEmail> CreateFactoryAsync(IEmailService emailService)
    {
        var tenantFactory = await TenantApiFactory.CreateAsync(_postgres);
        var factory = tenantFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IVerificationService>();
            services.AddScoped<IVerificationService, VerificationService>();
            services.RemoveAll<IEmailService>();
            services.AddSingleton(emailService);
        }));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var carlos = new User { Email = "carlos@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Carlos", LastName = "Dias", IsVerified = false };
            db.Users.AddRange(
                new User { Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = false },
                new User { Email = "pedro@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Pedro", LastName = "Alves", IsVerified = true },
                new User { Email = "lucia@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Lucia", LastName = "Ramos", IsVerified = false, VerificationLockedUntil = DateTime.UtcNow.AddMinutes(10) },
                carlos);
            db.UserVerificationCodes.Add(new UserVerificationCode { UserId = carlos.Id, CodeHash = "earlier", ExpiresAt = DateTime.UtcNow.AddMinutes(10), CreatedAt = DateTime.UtcNow.AddSeconds(-20) });
            await db.SaveChangesAsync();
        }
        return new WebApplicationFactoryWithEmail(tenantFactory, factory);
    }

    private static async Task<(HttpStatusCode Status, string Body)> ResendAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(ResendUrl, new { email });
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Rule", "RS1")]
    public async Task ResendVerification_EveryAccountState_ReturnsTheSameStatusAndBody()
    {
        // Arrange
        await using var fixture = await CreateFactoryAsync(Substitute.For<IEmailService>());
        var client = fixture.Factory.CreateClient();

        // Act
        var eligible = await ResendAsync(client, MariaEmail);
        var unknown = await ResendAsync(client, "ninguem@cantinadanonna.com.br");
        var verified = await ResendAsync(client, "pedro@cantinadanonna.com.br");
        var locked = await ResendAsync(client, "lucia@cantinadanonna.com.br");
        var withinWait = await ResendAsync(client, "carlos@cantinadanonna.com.br");

        // Assert
        new[] { eligible, unknown, verified, locked, withinWait }
            .Distinct().ShouldHaveSingleItem("an eligible, unknown, verified, locked and within-wait email must get the same status and body, so nobody can tell which emails are unverified accounts");
        eligible.Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RS6")]
    public async Task ResendVerification_EmailServiceFails_ReturnsTheSameAnswerAsAnUnknownEmail()
    {
        // Arrange
        var emailService = Substitute.For<IEmailService>();
        emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP server unavailable")));
        await using var fixture = await CreateFactoryAsync(emailService);
        var client = fixture.Factory.CreateClient();

        // Act
        var eligible = await ResendAsync(client, MariaEmail);
        var unknown = await ResendAsync(client, "ninguem@cantinadanonna.com.br");

        // Assert
        eligible.ShouldBe(unknown, "a sending failure never reaches the requester: the answer is the same 200 acknowledgement as for an unknown email");
    }

    private sealed class WebApplicationFactoryWithEmail : IAsyncDisposable
    {
        private readonly TenantApiFactory _tenantFactory;

        public WebApplicationFactoryWithEmail(TenantApiFactory tenantFactory, Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
        {
            _tenantFactory = tenantFactory;
            Factory = factory;
        }

        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory { get; }

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            await _tenantFactory.DisposeAsync();
        }
    }
}
