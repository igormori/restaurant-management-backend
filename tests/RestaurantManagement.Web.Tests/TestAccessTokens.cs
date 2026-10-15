using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Signs short-lived access passes (JWT, JSON Web Token) with the app's own settings, so
/// [Authorize] endpoints can be called without going through login.
/// </summary>
public static class TestAccessTokens
{
    public static string Create(IServiceProvider services, Guid userId, string role)
    {
        var jwt = services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
