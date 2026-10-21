using RestaurantManagement.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services.Registration
{
    public class VerificationService : IVerificationService
    {
        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<VerificationService> _logger;
        private readonly IVerificationCodeHasher _hasher;

        public VerificationService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IStringLocalizer<SharedResource> localizer,
            ILogger<VerificationService> logger,
            IVerificationCodeHasher hasher)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _localizer = localizer;
            _logger = logger;
            _hasher = hasher;
        }

        public async Task<MessageResponse> VerifyEmailAsync(VerifyEmailRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            // Run the code lookup even for an unknown email (against Guid.Empty, which never
            // matches a real UserId) so an unregistered email and an invalid code take the
            // same path and produce the same response.
            var verification = await _db.UserVerificationCodes
                .Where(v => v.UserId == (user != null ? user.Id : Guid.Empty) && !v.IsUsed && v.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            // 🔒 A locked account gets the same 401 as an unknown email, so the lock never reveals the account.
            // Checked after the lookup so a locked account does the same database work and answers in the same time.
            if (user != null && user.VerificationLockedUntil.HasValue && user.VerificationLockedUntil > DateTime.UtcNow)
                throw new BusinessException(_localizer["InvalidEmailOrCode"].Value, 401);

            if (user == null || verification == null || !_hasher.IsMatch(user.Id, request.Code, verification.CodeHash))
            {
                if (user != null)
                    await RegisterFailedVerificationAttemptAsync(user); // always throws the same 401, also when the lockout trips

                throw new BusinessException(_localizer["InvalidEmailOrCode"].Value, 401);
            }

            // ✅ Mark as verified
            user.IsVerified = true;
            user.UpdatedAt = DateTime.UtcNow;
            user.VerificationFailedAttempts = 0;
            user.VerificationLockedUntil = null;
            verification.IsUsed = true;

            await _db.SaveChangesAsync();
            return new MessageResponse { Message = _localizer["UserVerified"].Value };
        }

        // The request only queues the email, whatever it is: it never reads accounts and never sends,
        // so response time cannot reveal the account state. The verification processor decides later.
        public async Task<MessageResponse> ResendVerificationCodeAsync(ResendVerificationRequest request)
        {
            var now = DateTime.UtcNow;
            _db.PendingEmails.Add(new PendingEmail
            {
                Purpose = PendingEmailPurpose.Verification,
                Email = request.Email,
                CreatedAt = now,
                NextAttemptAt = now
            });
            await _db.SaveChangesAsync();
            return new MessageResponse { Message = _localizer["VerificationResendAcknowledged"].Value };
        }

        private async Task RegisterFailedVerificationAttemptAsync(User user)
        {
            user.VerificationFailedAttempts++;

            if (user.VerificationFailedAttempts < _securityOptions.MaxVerificationAttempts)
            {
                await _db.SaveChangesAsync();
                throw new BusinessException(_localizer["InvalidEmailOrCode"].Value, 401);
            }

            // 🔒 Max attempts reached: invalidate the current code and lock verification
            user.VerificationLockedUntil = DateTime.UtcNow.AddMinutes(_securityOptions.VerificationLockoutDurationMinutes);
            user.VerificationFailedAttempts = 0;

            var unusedCodes = await _db.UserVerificationCodes
                .Where(v => v.UserId == user.Id && !v.IsUsed)
                .ToListAsync();

            foreach (var unusedCode in unusedCodes)
            {
                unusedCode.IsUsed = true;
            }

            await _db.SaveChangesAsync();
            throw new BusinessException(_localizer["InvalidEmailOrCode"].Value, 401);
        }
    }
}
