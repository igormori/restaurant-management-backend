using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RestaurantManagement.Shared.Options;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// A forged or expired access token gets the translated "invalid token" message and nothing that
/// says why the token failed.
/// </summary>
public class InvalidTokenMessageTests : IDisposable
{
    private const string ProtectedUrl = "/api/test-probe/undeclared";
    private const string EnglishInvalidTokenMessage = "Invalid token.";
    private const string PortugueseInvalidTokenMessage = "Token inválido.";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

    private readonly PermissionProbeApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private string SignToken(string key, DateTime expires, DateTime notBefore)
    {
        var jwt = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, MariaId.ToString()),
            new(JwtRegisteredClaimNames.Sub, MariaId.ToString()),
            new(ClaimTypes.Role, "Waiter")
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, notBefore, expires, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string ForgedToken()
        => SignToken("another-restaurant-signing-key-of-at-least-32-bytes",
            DateTime.UtcNow.AddMinutes(15), DateTime.UtcNow.AddMinutes(-1));

    private string ExpiredToken()
    {
        var jwt = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        return SignToken(jwt.Key, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-25));
    }

    private async Task<HttpResponseMessage> GetWithAsync(string token, string? language = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var request = new HttpRequestMessage(HttpMethod.Get, ProtectedUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (language is not null)
            request.Headers.Add("Accept-Language", language);
        return await client.SendAsync(request);
    }

    [Fact]
    [Trait("Rule", "W2")]
    public async Task ProtectedEndpoint_TokenSignedWithAnotherKey_ReturnsInvalidTokenMessageInEnglish()
    {
        // Arrange
        var token = ForgedToken();

        // Act
        var response = await GetWithAsync(token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a token signed with another key is forged and must be refused");
        (await LocalizedResponses.MessageAsync(response)).ShouldBe(EnglishInvalidTokenMessage, "the answer is exactly the catalog message and never says why the token failed");
    }

    [Fact]
    [Trait("Rule", "W2")]
    public async Task ProtectedEndpoint_TokenSignedWithAnotherKeyInPortuguese_ReturnsInvalidTokenMessageInPortuguese()
    {
        // Arrange
        var token = ForgedToken();

        // Act
        var response = await GetWithAsync(token, "pt");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a token signed with another key is forged and must be refused");
        (await LocalizedResponses.MessageAsync(response)).ShouldBe(PortugueseInvalidTokenMessage, "the invalid token answer follows the caller's language");
    }

    [Fact]
    [Trait("Rule", "W2")]
    public async Task ProtectedEndpoint_ExpiredToken_ReturnsInvalidTokenMessageWithoutSayingItExpired()
    {
        // Arrange
        var token = ExpiredToken();

        // Act
        var response = await GetWithAsync(token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "an expired token must be refused");
        (await LocalizedResponses.MessageAsync(response)).ShouldBe(EnglishInvalidTokenMessage, "an expired token gets the same message as a forged one, so the answer never says why");
    }
}
