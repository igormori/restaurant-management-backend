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

    // Matrix T1: Program.cs sets no ClockSkew, so the framework's 5-minute allowance applies;
    // 10 minutes past expiry is clearly outside it.
    [Fact]
    public async Task LogoutAll_AccessTokenExpiredTenMinutesAgo_ReturnsUnauthorized()
    {
        // Arrange
        var jwt = AppJwt;
        var token = SignToken(jwt.Key, jwt.Issuer, jwt.Audience,
            expires: DateTime.UtcNow.AddMinutes(-10), notBefore: DateTime.UtcNow.AddMinutes(-25));

        // Act
        var response = await LogoutAllWithAsync(token);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await _factory.SessionService.DidNotReceive().LogoutAllAsync(Arg.Any<Guid>());
    }
}
