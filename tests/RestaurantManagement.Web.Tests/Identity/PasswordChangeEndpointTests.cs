using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// POST api/auth/change-password (signed in, no organization needed) end to end on a real PostgreSQL
/// database (plan T9). The user comes only from the access token: a user ID in the body is ignored.
/// Maria (Owner of Cantina da Nonna) and João (Cashier of Cantina da Nonna) are each signed in on one device.
/// </summary>
public class PasswordChangeEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string ChangeUrl = "/api/auth/change-password";
    private const string CookieName = "__Secure-refresh_token";
    private const string CookiePath = "/api/auth";
    private const string MariaPassword = "Massa@Fresca2026";
    private const string JoaoPassword = "Pizza@Margherita2026";
    private const string NewPassword = "Nhoque@Batata2027";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");

    private readonly PostgresDatabaseFixture _postgres;

    public PasswordChangeEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private static async Task SeedUserAsync(TenantApiFactory factory, Guid userId, string email, string firstName, string password, string role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName,
            LastName = "Souza",
            IsVerified = true
        };
        user.Roles.Add(new UserRole { OrganizationId = CantinaId, Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private static async Task<string> IssueRefreshTokenAsync(TenantApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var issued = await scope.ServiceProvider.GetRequiredService<IRefreshTokenService>()
            .IssueAsync(userId, CantinaId, "Mozilla/5.0 (iPad; CPU OS 17_4 like Mac OS X) Safari/604.1", "203.0.113.10");
        return issued.Token;
    }

    private static async Task<bool> IsRefreshAcceptedAsync(TenantApiFactory factory, string rawToken)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRefreshTokenService>().RotateAsync(rawToken) is not null;
    }

    private static async Task<User> ReadUserAsync(TenantApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    /// <summary>A change request signed in as Maria with no organization selected.</summary>
    private static HttpRequestMessage ChangeRequestAsMaria(TenantApiFactory factory, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ChangeUrl) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestAccessTokens.Create(factory.Services, MariaId, organizationId: null, Roles.Owner));
        return request;
    }

    // Scenario 34
    [Fact]
    [Trait("Rule", "C1")]
    public async Task ChangePassword_NoAccessToken_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(ChangeUrl, new { currentPassword = MariaPassword, newPassword = NewPassword });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "changing a password requires being signed in");
    }

    // Scenario 35
    [Fact]
    [Trait("Rule", "C6")]
    public async Task ChangePassword_BodyNamesAnotherUser_ChangesOnlyCallerAndOtherSessionsStayActive()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await SeedUserAsync(factory, MariaId, "maria@cantinadanonna.com.br", "Maria", MariaPassword, Roles.Owner);
        await SeedUserAsync(factory, JoaoId, "joao@cantinadanonna.com.br", "João", JoaoPassword, Roles.Cashier);
        var joaoToken = await IssueRefreshTokenAsync(factory, JoaoId);
        var client = factory.CreateClient();

        // Act
        var response = await client.SendAsync(ChangeRequestAsMaria(factory, new { userId = JoaoId, currentPassword = MariaPassword, newPassword = NewPassword }));

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue("the caller's own password change is accepted");
        BCrypt.Net.BCrypt.Verify(NewPassword, (await ReadUserAsync(factory, MariaId)).PasswordHash).ShouldBeTrue("the signed-in caller's password is the one that changes");
        BCrypt.Net.BCrypt.Verify(JoaoPassword, (await ReadUserAsync(factory, JoaoId)).PasswordHash).ShouldBeTrue("a user ID in the request body must be ignored, so the other user's password stays the same");
        (await IsRefreshAcceptedAsync(factory, joaoToken)).ShouldBeTrue("the other user's sessions must stay active");
    }

    // Scenario 39
    [Fact]
    [Trait("Rule", "C5")]
    public async Task ChangePassword_CorrectCurrentPassword_ClearsRefreshCookieAndEarlierTokenRefused()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await SeedUserAsync(factory, MariaId, "maria@cantinadanonna.com.br", "Maria", MariaPassword, Roles.Owner);
        var earlierToken = await IssueRefreshTokenAsync(factory, MariaId);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var request = ChangeRequestAsMaria(factory, new { currentPassword = MariaPassword, newPassword = NewPassword });
        request.Headers.Add("Cookie", $"{CookieName}={earlierToken}");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue("a change with the correct current password is accepted");
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values).ShouldBeTrue("the response must clear the cookie");
        var cookie = SetCookieHeaderValue.ParseList(values!.ToList()).Single(c => c.Name == CookieName);
        cookie.Value.ToString().ShouldBeEmpty("the cleared cookie must carry no token");
        cookie.Path.ToString().ShouldBe(CookiePath, "the browser only removes a cookie sent with the same path it was set with");
        (await IsRefreshAcceptedAsync(factory, earlierToken)).ShouldBeFalse("changing the password signs the person out everywhere, so an earlier session token must be refused");
    }

    // 36 two-byte letters plus one ASCII letter: 37 characters, 73 UTF-8 bytes.
    private static readonly string CurrentPasswordOf73Bytes = new string('é', 36) + "a";
    private static readonly string CurrentPasswordOf72Bytes = new string('é', 36);

    [Theory]
    [InlineData("en", "The field CurrentPassword must be a maximum of 72 bytes. Special characters and emoji take more than one byte, so use fewer of them.")]
    [InlineData("pt-BR", "O campo CurrentPassword deve ter no máximo 72 bytes. Caracteres especiais e emojis ocupam mais de um byte; use menos deles.")]
    [Trait("Rule", "C7")]
    public async Task ChangePassword_CurrentPasswordOver72Bytes_ReturnsBadRequestWithTranslatedMessage(string culture, string expected)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = factory.CreateClient();
        var request = LocalizedResponses.Request("POST", ChangeUrl, culture, new { currentPassword = CurrentPasswordOf73Bytes, newPassword = NewPassword });
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestAccessTokens.Create(factory.Services, MariaId, organizationId: null, Roles.Owner));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a current password over 72 bytes is refused before it is checked");
        (await LocalizedResponses.FieldErrorsAsync(response)).ShouldContain(expected, "the error must be in the requested language and explain the 72-byte limit");
    }

    [Fact]
    [Trait("Rule", "C7")]
    public async Task ChangePassword_CurrentPasswordOf72Bytes_IsNotRejectedByValidation()
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = factory.CreateClient();
        var request = LocalizedResponses.Request("POST", ChangeUrl, "en", new { currentPassword = CurrentPasswordOf72Bytes, newPassword = NewPassword });
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestAccessTokens.Create(factory.Services, MariaId, organizationId: null, Roles.Owner));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        (await LocalizedResponses.FieldErrorsAsync(response)).ShouldNotContain(m => m.Contains("72 bytes"), "a current password of exactly 72 bytes is within the limit");
    }
}
