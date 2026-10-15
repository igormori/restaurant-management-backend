using System.Security.Claims;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Web.Auth
{
    public sealed class ClaimsCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId => ReadGuidClaim(ClaimTypes.NameIdentifier);

        public Guid? OrganizationId => ReadGuidClaim(AppClaimTypes.OrganizationId);

        private Guid? ReadGuidClaim(string claimType)
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
