using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class RefreshTokenCleanupService : IRefreshTokenCleanupService
    {
        public const int RetentionDays = 30;

        private readonly IdentityDbContext _db;

        public RefreshTokenCleanupService(IdentityDbContext db)
        {
            _db = db;
        }

        // Purges by expiry only: a revoked token that has not expired must stay so that replaying it
        // still triggers reuse detection. Once expired, rotation rejects it before that check.
        public Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken cancellationToken)
        {
            var cutoff = utcNow.AddDays(-RetentionDays);
            return _db.RefreshTokens
                .Where(t => t.ExpiresAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
