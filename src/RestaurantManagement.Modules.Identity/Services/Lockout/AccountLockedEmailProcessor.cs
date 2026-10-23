using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Services.Registration;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;

namespace RestaurantManagement.Modules.Identity.Services.Lockout
{
    // Sends the queued account-locked emails outside the login request, so login timing never depends on them.
    public class AccountLockedEmailProcessor : IAccountLockedEmailProcessor
    {
        private const int ClaimMinutes = 5;

        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IEmailService _emailService;
        private readonly ILogger<AccountLockedEmailProcessor> _logger;

        public AccountLockedEmailProcessor(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IEmailService emailService,
            ILogger<AccountLockedEmailProcessor> logger)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken)
        {
            await DeleteOldFailedAsync(utcNow, cancellationToken);

            var due = await _db.PendingEmails
                .AsNoTracking()
                .Where(r => r.Purpose == PendingEmailPurpose.AccountLocked
                    && r.FailedAt == null && r.NextAttemptAt <= utcNow
                    && (r.ClaimedUntil == null || r.ClaimedUntil <= utcNow))
                .OrderBy(r => r.CreatedAt)
                .Take(PendingEmailQueue.BatchSize)
                .ToListAsync(cancellationToken);

            var handled = 0;
            foreach (var loaded in due)
            {
                var request = await TryClaimAsync(loaded.Id, utcNow, cancellationToken);
                if (request == null)
                    continue;

                await HandleAsync(request, utcNow, cancellationToken);
                handled++;
            }

            return handled;
        }

        // A failed row holds the raw email address, so it is not kept longer than needed to investigate.
        private Task DeleteOldFailedAsync(DateTime utcNow, CancellationToken ct)
        {
            var cutoff = utcNow - PendingEmailQueue.FailedRetention;
            return _db.PendingEmails
                .Where(r => r.Purpose == PendingEmailPurpose.AccountLocked
                    && r.FailedAt != null && r.FailedAt < cutoff)
                .ExecuteDeleteAsync(ct);
        }

        // The conditional update lets only one pass win a row, so nobody gets two emails.
        private async Task<PendingEmail?> TryClaimAsync(Guid id, DateTime utcNow, CancellationToken ct)
        {
            var claimedUntil = DateTime.UtcNow.AddMinutes(ClaimMinutes);
            var updated = await _db.PendingEmails
                .Where(r => r.Id == id && r.Purpose == PendingEmailPurpose.AccountLocked
                    && (r.ClaimedUntil == null || r.ClaimedUntil <= utcNow))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ClaimedUntil, claimedUntil), ct);
            if (updated == 0)
                return null;

            return await _db.PendingEmails.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        }

        private async Task HandleAsync(PendingEmail request, DateTime utcNow, CancellationToken ct)
        {
            // SendStartedAt is set just before the send: still set on a claimed row means a pass crashed mid-send,
            // and at most once wins over a possible second email.
            if (request.SendStartedAt != null)
            {
                _logger.LogWarning("Account-locked request {RequestId} was interrupted during sending; dropped without sending again", request.Id);
                await DeleteAsync(request.Id, ct);
                return;
            }

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == request.Email, ct);
            if (user == null)
            {
                await DeleteAsync(request.Id, ct);
                return;
            }

            await _db.PendingEmails
                .Where(r => r.Id == request.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.SendStartedAt, DateTime.UtcNow), ct);

            try
            {
                await _emailService.SendEmailAsync(
                    user.Email,
                    AccountLockedEmailTemplate.Subject(),
                    AccountLockedEmailTemplate.Build(user.FirstName, _securityOptions.LockoutDurationMinutes));
            }
            catch (Exception ex)
            {
                await RegisterFailureAsync(request, utcNow, ex, ct);
                return;
            }

            await DeleteAsync(request.Id, ct);
        }

        private async Task RegisterFailureAsync(PendingEmail request, DateTime utcNow, Exception ex, CancellationToken ct)
        {
            var attempts = request.Attempts + 1;
            var failedAt = attempts >= PendingEmailQueue.MaxAttempts ? utcNow : (DateTime?)null;
            var nextAttemptAt = utcNow.AddMinutes(Math.Pow(2, attempts - 1));

            await _db.PendingEmails
                .Where(r => r.Id == request.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Attempts, attempts)
                    .SetProperty(r => r.NextAttemptAt, nextAttemptAt)
                    .SetProperty(r => r.ClaimedUntil, (DateTime?)null)
                    .SetProperty(r => r.SendStartedAt, (DateTime?)null)
                    .SetProperty(r => r.FailedAt, failedAt), ct);

            // Neither the email address nor the exception message may reach the logs.
            if (failedAt != null)
                _logger.LogError("Account-locked request {RequestId} failed after {Attempts} attempts ({ExceptionType})", request.Id, attempts, ex.GetType().Name);
            else
                _logger.LogWarning("Account-locked email send failed for request {RequestId} ({ExceptionType})", request.Id, ex.GetType().Name);
        }

        private Task DeleteAsync(Guid id, CancellationToken ct)
            => _db.PendingEmails.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
    }
}
