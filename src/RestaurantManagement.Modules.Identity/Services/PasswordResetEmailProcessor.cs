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
    public class PasswordResetEmailProcessor : IPasswordResetEmailProcessor
    {
        public const int MaxAttempts = 5;
        public const int BatchSize = 100;
        public static readonly TimeSpan FailedRetention = TimeSpan.FromDays(7);
        private const int ClaimMinutes = 5;

        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IEmailService _emailService;
        private readonly IVerificationCodeHasher _hasher;
        private readonly ILogger<PasswordResetEmailProcessor> _logger;

        public PasswordResetEmailProcessor(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IEmailService emailService,
            IVerificationCodeHasher hasher,
            ILogger<PasswordResetEmailProcessor> logger)
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

            var due = await _db.PasswordResetRequests
                .AsNoTracking()
                .Where(r => r.FailedAt == null && r.NextAttemptAt <= utcNow
                    && (r.ClaimedUntil == null || r.ClaimedUntil <= utcNow))
                .OrderBy(r => r.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            var handled = 0;
            foreach (var request in due)
            {
                if (!await TryClaimAsync(request.Id, utcNow, cancellationToken))
                    continue;

                await HandleAsync(request, utcNow, cancellationToken);
                handled++;
            }

            return handled;
        }

        // A failed row holds the raw email address, so it is not kept longer than needed to investigate.
        private Task DeleteOldFailedAsync(DateTime utcNow, CancellationToken ct)
        {
            var cutoff = utcNow - FailedRetention;
            return _db.PasswordResetRequests
                .Where(r => r.FailedAt != null && r.FailedAt < cutoff)
                .ExecuteDeleteAsync(ct);
        }

        // The conditional update lets only one pass win a row, so nobody gets two emails.
        private async Task<bool> TryClaimAsync(Guid id, DateTime utcNow, CancellationToken ct)
        {
            var claimedUntil = utcNow.AddMinutes(ClaimMinutes);
            var updated = await _db.PasswordResetRequests
                .Where(r => r.Id == id && (r.ClaimedUntil == null || r.ClaimedUntil <= utcNow))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ClaimedUntil, claimedUntil), ct);
            return updated > 0;
        }

        private async Task HandleAsync(PasswordResetRequest request, DateTime utcNow, CancellationToken ct)
        {
            // Sending again could replace a code the user already received, so at most once wins.
            if (request.CodeIssuedAt != null)
            {
                _logger.LogWarning("Password reset request {RequestId} was interrupted after its code was stored; dropped without sending again", request.Id);
                await DeleteAsync(request.Id, ct);
                return;
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
            if (user == null || !user.IsActive || !user.IsVerified || IsResetLocked(user, utcNow)
                || await IsWithinResendWaitAsync(request, user.Id, utcNow, ct))
            {
                await DeleteAsync(request.Id, ct);
                return;
            }

            var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            await StoreCodeAsync(user, request.Id, code, utcNow, ct);

            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.FirstName, code, _securityOptions.VerificationCodeExpiryMinutes);
            }
            catch (Exception ex)
            {
                await RegisterFailureAsync(request, utcNow, ex, ct);
                return;
            }

            await DeleteAsync(request.Id, ct);
        }

        // A retry skips the wait: the code of its own failed attempt would otherwise block it.
        private async Task<bool> IsWithinResendWaitAsync(PasswordResetRequest request, Guid userId, DateTime utcNow, CancellationToken ct)
        {
            if (request.Attempts > 0)
                return false;

            var lastCode = await _db.PasswordResetCodes
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);
            return lastCode != null && (utcNow - lastCode.CreatedAt).TotalSeconds < _securityOptions.ResendCooldownSeconds;
        }

        private async Task StoreCodeAsync(User user, Guid requestId, string code, DateTime utcNow, CancellationToken ct)
        {
            var unusedCodes = await _db.PasswordResetCodes
                .Where(c => c.UserId == user.Id && !c.IsUsed)
                .ToListAsync(ct);
            foreach (var unusedCode in unusedCodes)
                unusedCode.IsUsed = true;

            user.PasswordResetFailedAttempts = 0;
            _db.PasswordResetCodes.Add(new PasswordResetCode
            {
                UserId = user.Id,
                CodeHash = _hasher.ComputeHash(user.Id, code),
                CreatedAt = utcNow,
                ExpiresAt = utcNow.AddMinutes(_securityOptions.VerificationCodeExpiryMinutes)
            });

            var marked = new PasswordResetRequest { Id = requestId, CodeIssuedAt = utcNow };
            _db.PasswordResetRequests.Attach(marked).Property(r => r.CodeIssuedAt).IsModified = true;
            await _db.SaveChangesAsync(ct);
        }

        private async Task RegisterFailureAsync(PasswordResetRequest request, DateTime utcNow, Exception ex, CancellationToken ct)
        {
            var attempts = request.Attempts + 1;
            var failedAt = attempts >= MaxAttempts ? utcNow : (DateTime?)null;
            var nextAttemptAt = utcNow.AddMinutes(Math.Pow(2, attempts - 1));

            await _db.PasswordResetRequests
                .Where(r => r.Id == request.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Attempts, attempts)
                    .SetProperty(r => r.NextAttemptAt, nextAttemptAt)
                    .SetProperty(r => r.ClaimedUntil, (DateTime?)null)
                    .SetProperty(r => r.CodeIssuedAt, (DateTime?)null)
                    .SetProperty(r => r.FailedAt, failedAt), ct);

            // Neither the code, the email address nor the exception message may reach the logs.
            if (failedAt != null)
                _logger.LogError("Password reset request {RequestId} failed after {Attempts} attempts ({ExceptionType})", request.Id, attempts, ex.GetType().Name);
            else
                _logger.LogWarning("Password reset email send failed for request {RequestId} ({ExceptionType})", request.Id, ex.GetType().Name);
        }

        private Task DeleteAsync(Guid id, CancellationToken ct)
            => _db.PasswordResetRequests.Where(r => r.Id == id).ExecuteDeleteAsync(ct);

        private static bool IsResetLocked(User user, DateTime utcNow)
            => user.PasswordResetLockedUntil.HasValue && user.PasswordResetLockedUntil > utcNow;
    }
}
