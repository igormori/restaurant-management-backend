using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
/// POST api/auth/login then an Owner or Admin action, end to end on a real PostgreSQL database:
/// the token of a user with roles in two organizations, who has not chosen one, is refused.
/// </summary>
public class TokenRolesEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string Email = "maria@cantinadanonna.com.br";
    private const string Password = "Massa@Fresca2026";

    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");

    private readonly PostgresDatabaseFixture _postgres;

    public TokenRolesEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "L21")]
    public async Task CreateMenu_TokenOfOwnerInOneAndWaiterInAnotherOrganizationBeforeChoosing_Returns403()
    {
        // Arrange
        await using var tenant = await TenantApiFactory.CreateAsync(_postgres);
        var app = tenant.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISessionService>();
            services.AddScoped<ISessionService, SessionService>();
        }));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = new User
            {
                Email = Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                FirstName = "Maria",
                LastName = "Souza",
                IsVerified = true
            };
            user.Roles.Add(new UserRole { Role = "Owner", OrganizationId = CantinaId });
            user.Roles.Add(new UserRole { Role = "Waiter", OrganizationId = TrattoriaId });
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });
        login.StatusCode.ShouldBe(HttpStatusCode.OK, "valid credentials log the person in");
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/menus/create")
        {
            Content = JsonContent.Create(new { name = "Almoço executivo" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a token with no organization carries no role, so an action that requires Owner or Admin is refused");
    }
}
