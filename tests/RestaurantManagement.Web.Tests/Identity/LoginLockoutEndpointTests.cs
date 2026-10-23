using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Lockout;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Sessions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// POST api/auth/login end to end on PostgreSQL with the real session service: the login lock is per IP
/// address (taken with the same trusted-proxy rule as rate limiting) and every refusal looks the same.
/// Maria owns Cantina da Nonna; Ana and Bruno are other accounts.
/// </summary>
public class LoginLockoutEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string LoginUrl = "/api/auth/login";
    private const string MariaEmail = "maria@cantinadanonna.com.br";
    private const string CorrectPassword = "Massa@Fresca2026";
    private const string WrongPassword = "Errada@Pizza2026";
    private const string ProxyIp = "10.0.0.1";
    private const string KitchenIp = "203.0.113.10";
    private const string LaptopIp = "198.51.100.24";

    private readonly PostgresDatabaseFixture _postgres;

    public LoginLockoutEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private static async Task<(WebApplicationFactory<Program> App, TenantApiFactory Tenant)> StartAsync(
        PostgresDatabaseFixture postgres, string? trustedProxy = null, DateTime? accountLockedUntil = null)
    {
        var tenant = await TenantApiFactory.CreateAsync(postgres);
        var app = tenant.WithWebHostBuilder(builder =>
        {
            if (trustedProxy is not null)
                builder.UseSetting("AuthRateLimit:TrustedProxies:0", trustedProxy);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISessionService>();
                services.AddScoped<ISessionService, SessionService>();
            });
        });

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Users.AddRange(
            NewUser(MariaEmail, "Maria", null),
            NewUser("bruno@cantinadanonna.com.br", "Bruno", null),
            NewUser("ana@cantinadanonna.com.br", "Ana", accountLockedUntil));
        await db.SaveChangesAsync();
        return (app, tenant);
    }

    private static User NewUser(string email, string firstName, DateTime? lockedUntil) => new()
    {
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword, 4),
        FirstName = firstName,
        LastName = "Souza",
        IsVerified = true,
        LockedUntil = lockedUntil
    };

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string remoteIp, string password, string email = MariaEmail, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LoginUrl)
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.Add(TestRemoteIpStartupFilter.RemoteIpHeader, remoteIp);
        if (forwardedFor is not null)
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }

    private static async Task FailAsync(HttpClient client, string remoteIp, int times, string email = MariaEmail, string? forwardedFor = null)
    {
        for (var attempt = 0; attempt < times; attempt++)
            (await LoginAsync(client, remoteIp, WrongPassword, email, forwardedFor)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Rule", "L22")]
    public async Task Login_FiveWrongPasswordsFromOneForwardedAddress_OtherForwardedAddressSignsInAndThatOneIsRefused()
    {
        // Arrange
        var (app, tenant) = await StartAsync(_postgres, trustedProxy: ProxyIp);
        await using var _ = tenant;
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await FailAsync(client, ProxyIp, 5, forwardedFor: KitchenIp);

        // Act
        var fromOtherAddress = await LoginAsync(client, ProxyIp, CorrectPassword, forwardedFor: LaptopIp);
        var fromLockedAddress = await LoginAsync(client, ProxyIp, CorrectPassword, forwardedFor: KitchenIp);

        // Assert
        fromOtherAddress.StatusCode.ShouldBe(HttpStatusCode.OK, "the owner can still sign in from another address while one address is locked");
        fromLockedAddress.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "the locked address is refused even with the correct password");
    }

    [Fact]
    [Trait("Rule", "L17")]
    public async Task Login_ForwardedForFromUntrustedSender_LockLandsOnTheRealSender()
    {
        // Arrange
        var (app, tenant) = await StartAsync(_postgres);
        await using var _ = tenant;
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        // Act
        await FailAsync(client, KitchenIp, 1, forwardedFor: "198.51.100.1");
        await FailAsync(client, KitchenIp, 1, forwardedFor: "198.51.100.2");
        await FailAsync(client, KitchenIp, 1, forwardedFor: "198.51.100.3");
        await FailAsync(client, KitchenIp, 1, forwardedFor: "198.51.100.4");
        await FailAsync(client, KitchenIp, 1, forwardedFor: "198.51.100.5");
        var correctFromSameSender = await LoginAsync(client, KitchenIp, CorrectPassword, forwardedFor: "198.51.100.6");

        // Assert
        using var scope = app.Services.CreateScope();
        var addresses = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<LoginFailure>().Select(f => f.IpAddress).ToListAsync();
        addresses.ShouldAllBe(a => a == KitchenIp, "a forwarded address from a sender that is not a trusted proxy is ignored, so a caller cannot dodge the lock by faking its address");
        correctFromSameSender.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "five wrong passwords from the real sender lock it, whatever addresses it claims");
    }

    [Fact]
    [Trait("Rule", "L5")]
    public async Task Login_WrongPasswordAddressLockedAndAccountLocked_AllGetTheSameStatusAndBody()
    {
        // Arrange
        var (app, tenant) = await StartAsync(_postgres, accountLockedUntil: DateTime.UtcNow.AddMinutes(10));
        await using var _ = tenant;
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await FailAsync(client, KitchenIp, 5, "bruno@cantinadanonna.com.br");

        // Act
        var wrongPassword = await LoginAsync(client, LaptopIp, WrongPassword);
        var addressLocked = await LoginAsync(client, KitchenIp, CorrectPassword, "bruno@cantinadanonna.com.br");
        var accountLocked = await LoginAsync(client, LaptopIp, CorrectPassword, "ana@cantinadanonna.com.br");

        // Assert
        var expectedBody = await wrongPassword.Content.ReadAsStringAsync();
        addressLocked.StatusCode.ShouldBe(wrongPassword.StatusCode, "an address lock is answered like a wrong password");
        accountLocked.StatusCode.ShouldBe(wrongPassword.StatusCode, "an account lock is answered like a wrong password");
        (await addressLocked.Content.ReadAsStringAsync()).ShouldBe(expectedBody, "nothing in the body reveals an address lock");
        (await accountLocked.Content.ReadAsStringAsync()).ShouldBe(expectedBody, "nothing in the body reveals an account lock");
    }
}
