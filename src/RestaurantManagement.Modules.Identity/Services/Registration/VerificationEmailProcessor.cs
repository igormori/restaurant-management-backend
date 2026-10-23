using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Modules.Identity.Services.Passwords;

namespace RestaurantManagement.Modules.Identity.Services.Registration
{
    // Same flow as PasswordResetEmailProcessor, for queued "resend verification" requests: the request
    // only queues, and this processor alone decides who gets a code, so the answer never reveals the account.
    public class VerificationEmailProcessor : IVerificationEmailProcessor
    {
        private readonly IdentityDbContext _db;
        private readonly PendingEmailQueue _queue;
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
            _queue = new PendingEmailQueue(db, PendingEmailPurpose.Verification);
            _securityOptions = securityOptions.Value;
            _emailService = emailService;
            _hasher = hasher;
            _logger = logger;
        }

        public async Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken)
        {
            await _queue.DeleteOldFailedAsync(utcNow, cancellationToken);

            var due = await _queue.LoadDueAsync(utcNow, cancellationToken);

            var handled = 0;
            foreach (var loaded in due)
            {
                var request = await _queue.TryClaimAsync(loaded.Id, utcNow, cancellationToken);
                if (request == null)
                    continue;

                await HandleAsync(request, utcNow, cancellationToken);
                handled++;
            }

            return handled;
        }

        private async Task HandleAsync(PendingEmail request, DateTime utcNow, CancellationToken ct)
        {
            // The pass-start time only chooses the due rows; a slow pass must not give a late row an expired code.
            var handledAt = DateTime.UtcNow;

            // Sending again could replace a code the user already received, so at most once wins.
            if (request.CodeIssuedAt != null)
            {
                _logger.LogWarning("Verification request {RequestId} was interrupted after its code was stored; dropped without sending again", request.Id);
                await _queue.DeleteAsync(request.Id, ct);
                return;
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
            if (user == null || user.IsVerified || IsVerificationLocked(user, handledAt)
                || await IsWithinResendWaitAsync(request, user.Id, handledAt, ct))
            {
                await _queue.DeleteAsync(request.Id, ct);
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

            await _queue.DeleteAsync(request.Id, ct);
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
            var (attempts, gaveUp) = await _queue.RegisterFailureAsync(request, utcNow, ct);

            // Neither the code, the email address nor the exception message may reach the logs.
            if (gaveUp)
                _logger.LogError("Verification email for user {UserId} failed after {Attempts} attempts ({ExceptionType})", userId, attempts, ex.GetType().Name);
            else
                _logger.LogWarning("Verification email send failed for user {UserId} ({ExceptionType})", userId, ex.GetType().Name);
        }

        private static bool IsVerificationLocked(User user, DateTime now)
            => user.VerificationLockedUntil.HasValue && user.VerificationLockedUntil > now;
    }
}
