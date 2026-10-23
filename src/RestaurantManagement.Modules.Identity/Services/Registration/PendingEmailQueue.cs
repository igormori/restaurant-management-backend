using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Registration;

namespace RestaurantManagement.Modules.Identity.Services.Registration
{
    // The queue mechanics the three email processors share for one purpose: find the due rows, claim one so only
    // one pass sends it, schedule retries (1, 2, 4, 8 minutes), delete, and purge old failed rows.
    // The processors keep only what is specific to their purpose.
    public class PendingEmailQueue
    {
        public const int MaxAttempts = 5;
        public const int BatchSize = 100;
        public static readonly TimeSpan FailedRetention = TimeSpan.FromDays(7);

        private const int ClaimMinutes = 5;

        private readonly IdentityDbContext _db;
        private readonly PendingEmailPurpose _purpose;

        public PendingEmailQueue(IdentityDbContext db, PendingEmailPurpose purpose)
        {
            _db = db;
            _purpose = purpose;
        }

        public Task<List<PendingEmail>> LoadDueAsync(DateTime utcNow, CancellationToken ct)
            => _db.PendingEmails
                .AsNoTracking()
                .Where(r => r.Purpose == _purpose
                    && r.FailedAt == null && r.NextAttemptAt <= utcNow
                    && (r.ClaimedUntil == null || r.ClaimedUntil <= utcNow))
                .OrderBy(r => r.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

        // A failed row holds the raw email address, so it is not kept longer than needed to investigate.
        public Task DeleteOldFailedAsync(DateTime utcNow, CancellationToken ct)
        {
            var cutoff = utcNow - FailedRetention;
            return _db.PendingEmails
                .Where(r => r.Purpose == _purpose && r.FailedAt != null && r.FailedAt < cutoff)
                .ExecuteDeleteAsync(ct);
        }

        // The conditional update lets only one pass win a row, so nobody gets two emails. The claim counts
        // from the real clock because a slow pass would otherwise claim later rows with an expired time.
        // The row is read again because another instance may have changed it since the pass loaded it.
        public async Task<PendingEmail?> TryClaimAsync(Guid id, DateTime utcNow, CancellationToken ct)
        {
            var claimedUntil = DateTime.UtcNow.AddMinutes(ClaimMinutes);
            var updated = await _db.PendingEmails
                .Where(r => r.Id == id && r.Purpose == _purpose
                    && (r.ClaimedUntil == null || r.ClaimedUntil <= utcNow))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ClaimedUntil, claimedUntil), ct);
            if (updated == 0)
                return null;

            return await _db.PendingEmails.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        }

        public Task DeleteAsync(Guid id, CancellationToken ct)
            => _db.PendingEmails.Where(r => r.Id == id).ExecuteDeleteAsync(ct);

        // Releases the claim and the send marks so a retry can send. Retry times stay on the pass clock so the
        // 1, 2, 4, 8 minute schedule is counted from the pass. Returns the new attempt count and whether the
        // row is now failed for good.
        public async Task<(int Attempts, bool GaveUp)> RegisterFailureAsync(PendingEmail request, DateTime utcNow, CancellationToken ct)
        {
            var attempts = request.Attempts + 1;
            var failedAt = attempts >= MaxAttempts ? utcNow : (DateTime?)null;
            var nextAttemptAt = utcNow.AddMinutes(Math.Pow(2, attempts - 1));

            await _db.PendingEmails
                .Where(r => r.Id == request.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Attempts, attempts)
                    .SetProperty(r => r.NextAttemptAt, nextAttemptAt)
                    .SetProperty(r => r.ClaimedUntil, (DateTime?)null)
                    .SetProperty(r => r.CodeIssuedAt, (DateTime?)null)
                    .SetProperty(r => r.SendStartedAt, (DateTime?)null)
                    .SetProperty(r => r.FailedAt, failedAt), ct);

            return (attempts, failedAt != null);
        }
    }
}
