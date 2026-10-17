using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Every endpoint needs a signed-in user unless it is explicitly public (spec Outcome #3).
/// The six sign-in endpoints are the only public ones.
/// </summary>
public class PrivateByDefaultTests : IDisposable
{
    private const string UndeclaredUrl = "/api/test-probe/undeclared";
    private const string EnglishUnauthorizedMessage = "Unauthorized user.";
    private const string PortugueseUnauthorizedMessage = "Usuário não autorizado!";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

    private readonly PermissionProbeApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    // Scenario 27
    [Fact]
    [Trait("Rule", "T3")]
    [Trait("Rule", "W4")]
    public async Task UndeclaredEndpoint_NoAccessToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync(UndeclaredUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "an endpoint with no access rule is private by default, so an anonymous caller must be refused");
    }

    // Scenario 28
    [Fact]
    [Trait("Rule", "W1")]
    public async Task UndeclaredEndpoint_NoAccessToken_ReturnsStandardUnauthorizedMessage()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.GetAsync(UndeclaredUrl);

        // Assert
        response.Content.Headers.ContentType.ShouldNotBeNull("the refusal must be a JSON answer");
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/json", "the refusal must be a JSON answer");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body.ShouldNotBeNull();
        body["message"].ShouldBe(EnglishUnauthorizedMessage, "an anonymous caller gets the standard unauthorized message");
    }

    [Fact]
    [Trait("Rule", "W1")]
    public async Task UndeclaredEndpoint_NoAccessTokenInPortuguese_ReturnsPortugueseUnauthorizedMessage()
    {
        // Arrange
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, UndeclaredUrl);
        request.Headers.Add("Accept-Language", "pt-PT");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body.ShouldNotBeNull();
        body["message"].ShouldBe(PortugueseUnauthorizedMessage, "the unauthorized message follows the caller's language");
    }

    // Scenario 29
    [Fact]
    [Trait("Rule", "W4")]
    public async Task UndeclaredEndpoint_SignedInWaiter_ReturnsOk()
    {
        // Arrange
        var client = CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, UndeclaredUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(_factory.Services, MariaId, Roles.Waiter));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an endpoint with no access rule only needs a signed-in user, any role");
    }

    // Scenario 30
    [Theory]
    [InlineData("/api/auth/register", """{"email":"maria.souza@cantina.com.br","password":"Feijoada#2026","firstName":"Maria","lastName":"Souza"}""")]
    [InlineData("/api/auth/login", """{"email":"maria.souza@cantina.com.br","password":"Feijoada#2026"}""")]
    [InlineData("/api/auth/refresh", "")]
    [InlineData("/api/auth/logout", "")]
    [InlineData("/api/auth/verify", """{"email":"maria.souza@cantina.com.br","code":"482913"}""")]
    [InlineData("/api/auth/resend-verification", """{"email":"maria.souza@cantina.com.br"}""")]
    [Trait("Rule", "T3")]
    [Trait("Rule", "W4")]
    public async Task PublicAuthEndpoint_NoAccessToken_IsNotRefusedAsUnauthorized(string url, string jsonBody)
    {
        // Arrange
        var client = CreateClient();
        var issuedSession = new SessionResult
        {
            Auth = new AuthResponse(),
            RefreshToken = "a2l0Y2hlbi10YWJsZXQtcmVmcmVzaC10b2tlbi0wMDAx",
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _factory.SessionService.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<ClientInfo>()).Returns(issuedSession);
        _factory.SessionService.RefreshTokenAsync(Arg.Any<string?>()).Returns(issuedSession);
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized, "the sign-in endpoints are public, so a caller with no token must not be turned away");
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden, "the sign-in endpoints are public, so a caller with no token must not be turned away");
        ((int)response.StatusCode).ShouldBeLessThan(500, "a public endpoint must answer without a server error");
    }
}
