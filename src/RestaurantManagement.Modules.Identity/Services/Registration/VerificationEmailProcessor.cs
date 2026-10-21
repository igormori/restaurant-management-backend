using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Same flow as PasswordResetEmailProcessor, for queued "resend verification" requests: the request
    // only queues, and this processor alone decides who gets a code, so the answer never reveals the account.
    public class VerificationEmailProcessor : IVerificationEmailProcessor
    {
        private const int MaxAttempts = PendingEmailQueue.MaxAttempts;
        private const int BatchSize = PendingEmailQueue.BatchSize;
        private const int ClaimMinutes = 5;
        private const PendingEmailPurpose Purpose = PendingEmailPurpose.Verification;

        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IEmailService _emailService;
        private readonly IVerificationCodeHasher _hasher;
        private readonly ILogger<VerificationEmailProcessor> _logger;

        public VerificationEmailProcessor(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IEmailService emailService,
            IVerificationCodeHasher hasher,
            ILogger<VerificationEmailProcessor> logger)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _emailService = emailService;
            _hasher = hasher;
            _logger = logger;
        }

        public async Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken)
        {
            await DeleteOldFailedAsync(utcNow, cancellationToken);

            var due = await _db.PendingEmails
                .AsNoTracking()
                .Where(r => r.Purpose == Purpose && r.FailedAt == null && r.NextAttemptAt <= utcNow
                    && (r.ClaimedUntil == null || r.ClaimedUntil <= utcNow))
                .OrderBy(r => r.CreatedAt)
                .Take(BatchSize)
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
                .Where(r => r.Purpose == Purpose && r.FailedAt != null && r.FailedAt < cutoff)
                .ExecuteDeleteAsync(ct);
        }

        // The conditional update lets only one pass win a row; the row is read again because another
        // instance may have changed it since the pass loaded it.
        private async Task<PendingEmail?> TryClaimAsync(Guid id, DateTime utcNow, CancellationToken ct)
        {
            var claimedUntil = DateTime.UtcNow.AddMinutes(ClaimMinutes);
            var updated = await _db.PendingEmails
                .Where(r => r.Id == id && r.Purpose == Purpose && (r.ClaimedUntil == null || r.ClaimedUntil <= utcNow))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ClaimedUntil, claimedUntil), ct);
            if (updated == 0)
                return null;

            return await _db.PendingEmails.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        }

        private async Task HandleAsync(PendingEmail request, DateTime utcNow, CancellationToken ct)
        {
            // The pass-start time only chooses the due rows; a slow pass must not give a late row an expired code.
            var handledAt = DateTime.UtcNow;

            // Sending again could replace a code the user already received, so at most once wins.
            if (request.CodeIssuedAt != null)
            {
                _logger.LogWarning("Verification request {RequestId} was interrupted after its code was stored; dropped without sending again", request.Id);
                await DeleteAsync(request.Id, ct);
                return;
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
            if (user == null || user.IsVerified || IsVerificationLocked(user, handledAt)
                || await IsWithinResendWaitAsync(request, user.Id, handledAt, ct))
            {
                await DeleteAsync(request.Id, ct);
                return;
            }

            var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            await StoreCodeAsync(user, request.Id, code, handledAt, ct);

            try
            {
                await _emailService.SendVerificationEmailAsync(user.Email, user.FirstName, code, _securityOptions.VerificationCodeExpiryMinutes);
            }
            catch (Exception ex)
            {
                // Retry times stay on the pass clock so the 1, 2, 4, 8 minute schedule is counted from the pass.
                await RegisterFailureAsync(request, user.Id, utcNow, ex, ct);
                return;
            }

            await DeleteAsync(request.Id, ct);
        }

        // A retry skips the wait: the code of its own failed attempt would otherwise block it.
        private async Task<bool> IsWithinResendWaitAsync(PendingEmail request, Guid userId, DateTime now, CancellationToken ct)
        {
            if (request.Attempts > 0)
                return false;

            var lastCode = await _db.UserVerificationCodes
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);
            return lastCode != null && (now - lastCode.CreatedAt).TotalSeconds < _securityOptions.ResendCooldownSeconds;
        }

        private async Task StoreCodeAsync(User user, Guid requestId, string code, DateTime now, CancellationToken ct)
        {
            var unusedCodes = await _db.UserVerificationCodes
                .Where(c => c.UserId == user.Id && !c.IsUsed)
                .ToListAsync(ct);
            foreach (var unusedCode in unusedCodes)
                unusedCode.IsUsed = true;

            user.VerificationFailedAttempts = 0;
            user.VerificationLockedUntil = null;
            _db.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = user.Id,
                CodeHash = _hasher.ComputeHash(user.Id, code),
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(_securityOptions.VerificationCodeExpiryMinutes)
            });

            var marked = new PendingEmail { Id = requestId, CodeIssuedAt = now };
            _db.PendingEmails.Attach(marked).Property(r => r.CodeIssuedAt).IsModified = true;
            await _db.SaveChangesAsync(ct);
        }

        private async Task RegisterFailureAsync(PendingEmail request, Guid userId, DateTime utcNow, Exception ex, CancellationToken ct)
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
                    .SetProperty(r => r.FailedAt, failedAt), ct);

            // Neither the code, the email address nor the exception message may reach the logs.
            if (failedAt != null)
                _logger.LogError("Verification email for user {UserId} failed after {Attempts} attempts ({ExceptionType})", userId, attempts, ex.GetType().Name);
            else
                _logger.LogWarning("Verification email send failed for user {UserId} ({ExceptionType})", userId, ex.GetType().Name);
        }

        private Task DeleteAsync(Guid id, CancellationToken ct)
            => _db.PendingEmails.Where(r => r.Id == id).ExecuteDeleteAsync(ct);

        private static bool IsVerificationLocked(User user, DateTime now)
            => user.VerificationLockedUntil.HasValue && user.VerificationLockedUntil > now;
    }
}
