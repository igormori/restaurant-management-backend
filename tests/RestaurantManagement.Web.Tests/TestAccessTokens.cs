using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Signs short-lived access passes (JWT, JSON Web Token) with the app's own settings, so
/// [Authorize] endpoints can be called without going through login.
/// </summary>
public static class TestAccessTokens
{
    public static string Create(IServiceProvider services, Guid userId, string role)
        => Create(services, userId, organizationId: null, role);

    /// <summary>
    /// Signs a pass with any number of role claims and, when given, the selected organization
    /// claim. No organization mimics a user of several organizations who has not selected one.
    /// </summary>
    public static string Create(IServiceProvider services, Guid userId, Guid? organizationId, params string[] roles)
        => CreateWithSubject(services, userId.ToString(), organizationId, roles);

    /// <summary>
    /// Signs a valid pass whose user identifier is any text, for example one that is not a user ID.
    /// </summary>
    public static string CreateWithSubject(IServiceProvider services, string subject, Guid? organizationId, params string[] roles)
    {
        var jwt = services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new(JwtRegisteredClaimNames.Sub, subject)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        if (organizationId.HasValue)
            claims.Add(new Claim(AppClaimTypes.OrganizationId, organizationId.Value.ToString()));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
