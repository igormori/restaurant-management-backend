using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
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
/// POST api/auth/register end to end on a real PostgreSQL database: the real registration service
/// answers; only the email is substituted so no message is sent.
/// </summary>
public class RegisterEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _postgres;

    public RegisterEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "R2")]
    public async Task Register_ValidRequest_ResponseHasNoAccessTokenNoPasswordDataAndNoCookie()
    {
        // Arrange
        await using var tenantFactory = await TenantApiFactory.CreateAsync(_postgres);
        var factory = tenantFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRegistrationService>();
            services.AddScoped<IRegistrationService, RegistrationService>();
            services.RemoveAll<IEmailService>();
            services.AddSingleton(Substitute.For<IEmailService>());
        }));
        var client = factory.CreateClient();
        const string password = "Massa@Fresca2026";

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "ana@cantinadanonna.com.br",
            password,
            firstName = "Ana",
            lastName = "Souza"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "a valid registration is accepted");
        var rawBody = await response.Content.ReadAsStringAsync();
        var body = JsonNode.Parse(rawBody)!.AsObject();
        body["token"].ShouldBeNull("registering must not sign the person in, because the email is not verified yet");
        body.Select(property => property.Key.ToLowerInvariant()).ShouldNotContain(key => key.Contains("password"), "the response must never carry any password data");
        rawBody.ShouldNotContain(password, customMessage: "the password the person typed must never be echoed back");
        response.Headers.Contains("Set-Cookie").ShouldBeFalse("registering must not start a session, so no cookie is set");
    }

    [Fact]
    [Trait("Rule", "R15")]
    public async Task Register_BodyCarriesProtectedFields_UserIsStillUnverifiedActiveAndWithoutMemberships()
    {
        // Arrange
        await using var tenantFactory = await TenantApiFactory.CreateAsync(_postgres);
        var factory = tenantFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRegistrationService>();
            services.AddScoped<IRegistrationService, RegistrationService>();
            services.RemoveAll<IEmailService>();
            services.AddSingleton(Substitute.For<IEmailService>());
        }));
        var client = factory.CreateClient();
        var forgedId = Guid.Parse("99999999-aaaa-4aaa-8aaa-00000000000f");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "bruno@cantinadanonna.com.br",
            password = "Massa@Fresca2026",
            firstName = "Bruno",
            lastName = "Lima",
            id = forgedId,
            isVerified = true,
            isActive = false,
            failedAttempts = -10,
            roles = new[] { new { role = "Owner" } }
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "a valid registration is accepted even with extra fields");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "bruno@cantinadanonna.com.br");
        user.Id.ShouldNotBe(forgedId, "the caller cannot choose the user ID");
        user.IsVerified.ShouldBeFalse("a caller cannot register an already verified account");
        user.IsActive.ShouldBeTrue("a caller cannot register a deactivated account");
        user.FailedAttempts.ShouldBe(0, "a caller cannot set the login failure counter");
        (await db.Set<OrganizationMember>().IgnoreQueryFilters().AnyAsync(m => m.UserId == user.Id)).ShouldBeFalse("registering never makes the user a member of an organization");
    }
}
