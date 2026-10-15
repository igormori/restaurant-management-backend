using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Stub (tester): implemented in task T2.
    public class RefreshTokenService : IRefreshTokenService
    {
        public RefreshTokenService(IdentityDbContext db, IOptions<JwtOptions> jwtOptions)
        {
        }

        public Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid? organizationId, string? deviceDescription, string? ipAddress)
            => throw new NotImplementedException();

        public Task<IssuedRefreshToken?> RotateAsync(string rawToken, Guid? organizationId = null, Guid? expectedUserId = null)
            => throw new NotImplementedException();

        public Task<bool> RevokeAsync(string rawToken)
            => throw new NotImplementedException();

        public Task<int> RevokeAllForUserAsync(Guid userId)
            => throw new NotImplementedException();
    }
}
