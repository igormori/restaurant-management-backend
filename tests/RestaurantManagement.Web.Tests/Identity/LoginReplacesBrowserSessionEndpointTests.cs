using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// POST api/auth/login end to end on a real PostgreSQL database with the real session service:
/// the refresh cookie the browser already holds is revoked once the new login succeeds.
/// </summary>
public class LoginReplacesBrowserSessionEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string CookieName = "__Secure-refresh_token";
    private const string Email = "maria@cantinadanonna.com.br";
    private const string Password = "Massa@Fresca2026";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

    private readonly PostgresDatabaseFixture _postgres;

    public LoginReplacesBrowserSessionEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private static async Task<(WebApplicationFactory<Program> App, TenantApiFactory Tenant)> StartAsync(PostgresDatabaseFixture postgres)
    {
        var tenant = await TenantApiFactory.CreateAsync(postgres);
        var app = tenant.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISessionService>();
            services.AddScoped<ISessionService, SessionService>();
        }));

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Users.Add(new User
        {
            Id = MariaId,
            Email = Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            FirstName = "Maria",
            LastName = "Souza",
            IsVerified = true
        });
        await db.SaveChangesAsync();
        return (app, tenant);
    }

    private static async Task<string> IssueBrowserTokenAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        var issued = await scope.ServiceProvider.GetRequiredService<IRefreshTokenService>()
            .IssueAsync(MariaId, null, "Mozilla/5.0 (Macintosh) Safari/605.1.15", "203.0.113.20");
        return issued.Token;
    }

    private static async Task<bool> IsRefreshAcceptedAsync(WebApplicationFactory<Program> app, string rawToken)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRefreshTokenService>().RotateAsync(rawToken) is not null;
    }

    private static HttpRequestMessage LoginWithCookie(string cookieToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = Email, password = Password })
        };
        request.Headers.Add("Cookie", $"{CookieName}={cookieToken}");
        return request;
    }

    [Fact]
    [Trait("Rule", "L20")]
    public async Task Login_BrowserSendsItsExistingRefreshCookie_OldTokenIsRefusedOnRefreshAfterwards()
    {
        // Arrange
        var (app, tenant) = await StartAsync(_postgres);
        await using var _ = tenant;
        var oldToken = await IssueBrowserTokenAsync(app);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        // Act
        var response = await client.SendAsync(LoginWithCookie(oldToken));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "valid credentials log the person in");
        (await IsRefreshAcceptedAsync(app, oldToken)).ShouldBeFalse("a login from a browser that sends its existing cookie revokes that cookie's token, so it is refused on refresh");
    }
}
