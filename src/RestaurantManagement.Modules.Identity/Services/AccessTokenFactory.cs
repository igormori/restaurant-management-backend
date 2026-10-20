using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Builds the signed access token: the user and the organization, never a role claim.
    internal class AccessTokenFactory
    {
        private readonly JwtOptions _jwtOptions;

        public AccessTokenFactory(JwtOptions jwtOptions)
        {
            _jwtOptions = jwtOptions;
        }

        public string Create(User user, IReadOnlyList<Guid> memberOrganizationIds, Guid? selectedOrganizationId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
            };

            // Users in several organizations must pick one, so only a single org is claimed automatically.
            var organizationIds = memberOrganizationIds.Distinct().ToList();
            var organizationId = selectedOrganizationId
                ?? (organizationIds.Count == 1 ? organizationIds[0] : (Guid?)null);

            if (organizationId.HasValue)
            {
                claims.Add(new Claim(AppClaimTypes.OrganizationId, organizationId.Value.ToString()));
            }

            var jwtKey = _jwtOptions.Key;
            if (string.IsNullOrEmpty(jwtKey))
                throw new InvalidOperationException("JWT key is not configured.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpireMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
