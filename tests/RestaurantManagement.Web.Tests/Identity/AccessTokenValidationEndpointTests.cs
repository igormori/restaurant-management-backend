using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using RestaurantManagement.Shared.Options;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Access passes (JWT, JSON Web Token) that must be refused on a signed-in endpoint
/// (POST api/auth/logout-all). The session service is substituted; only the pass check is under test.
/// </summary>
public class AccessTokenValidationEndpointTests : IDisposable
{
    private const string LogoutAllUrl = "/api/auth/logout-all";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

    private readonly AuthApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private JwtOptions AppJwt => _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

    private async Task<HttpResponseMessage> LogoutAllWithAsync(string token)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var request = new HttpRequestMessage(HttpMethod.Post, LogoutAllUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static string SignToken(string key, string issuer, string audience, DateTime expires, DateTime notBefore)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, MariaId.ToString()),
            new(JwtRegisteredClaimNames.Sub, MariaId.ToString()),
            new(ClaimTypes.Role, "Owner")
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, notBefore, expires, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Matrix T1
    [Fact]
    [Trait("Rule", "T1")]
    public async Task LogoutAll_AccessTokenExpiredTenMinutesAgo_ReturnsUnauthorized()
    {
        // Arrange
        var jwt = AppJwt;
        var token = SignToken(jwt.Key, jwt.Issuer, jwt.Audience,
            expires: DateTime.UtcNow.AddMinutes(-10), notBefore: DateTime.UtcNow.AddMinutes(-25));

        // Act
        var response = await LogoutAllWithAsync(token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "an access token that expired 10 minutes ago must be refused");
        await _factory.SessionService.DidNotReceive().LogoutAllAsync(Arg.Any<Guid>());
    }

    // Matrix T15: no grace period after expiry (the framework would otherwise allow 5 minutes).
    [Fact]
    [Trait("Rule", "T15")]
    public async Task LogoutAll_AccessTokenExpiredThirtySecondsAgo_ReturnsUnauthorized()
    {
        // Arrange
        var jwt = AppJwt;
        var token = SignToken(jwt.Key, jwt.Issuer, jwt.Audience,
            expires: DateTime.UtcNow.AddSeconds(-30), notBefore: DateTime.UtcNow.AddMinutes(-15));

        // Act
        var response = await LogoutAllWithAsync(token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "an access token is refused as soon as it expires, with no grace period");
        await _factory.SessionService.DidNotReceive().LogoutAllAsync(Arg.Any<Guid>());
    }

    private static string UnsignedToken(JwtOptions jwt)
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode(
            $$"""{"sub":"{{MariaId}}","iss":"{{jwt.Issuer}}","aud":"{{jwt.Audience}}","exp":{{DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds()}},"http://schemas.microsoft.com/ws/2008/06/identity/claims/role":"Owner"}""");
        return $"{header}.{payload}.";
    }

    private string ForgedToken(string forgery)
    {
        var jwt = AppJwt;
        var expires = DateTime.UtcNow.AddMinutes(15);
        var notBefore = DateTime.UtcNow.AddMinutes(-1);
        return forgery switch
        {
            "other-key" => SignToken("another-restaurant-signing-key-of-at-least-32-bytes", jwt.Issuer, jwt.Audience, expires, notBefore),
            "wrong-issuer" => SignToken(jwt.Key, "https://evil.example.com", jwt.Audience, expires, notBefore),
            "wrong-audience" => SignToken(jwt.Key, jwt.Issuer, "another-api", expires, notBefore),
            _ => UnsignedToken(jwt)
        };
    }

    // Matrix T2
    [Theory]
    [InlineData("other-key")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("alg-none")]
    [Trait("Rule", "T2")]
    public async Task LogoutAll_ForgedAccessToken_ReturnsUnauthorized(string forgery)
    {
        // Arrange
        var token = ForgedToken(forgery);

        // Act
        var response = await LogoutAllWithAsync(token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a token signed with another key, issuer or audience, or not signed at all, is forged and must be refused");
        await _factory.SessionService.DidNotReceive().LogoutAllAsync(Arg.Any<Guid>());
    }

    // Matrix T21
    [Fact]
    [Trait("Rule", "T21")]
    public async Task LogoutAll_AccessTokenNotValidForAnotherTenMinutes_ReturnsUnauthorized()
    {
        // Arrange
        var jwt = AppJwt;
        var token = SignToken(jwt.Key, jwt.Issuer, jwt.Audience,
            expires: DateTime.UtcNow.AddMinutes(25), notBefore: DateTime.UtcNow.AddMinutes(10));

        // Act
        var response = await LogoutAllWithAsync(token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "an access token used before its not-before time must be refused");
        await _factory.SessionService.DidNotReceive().LogoutAllAsync(Arg.Any<Guid>());
    }
}
