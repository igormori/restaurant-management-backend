using RestaurantManagement.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Shared.Services.Email;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class VerificationService : IVerificationService
    {
        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailService _emailService;
        private readonly ILogger<VerificationService> _logger;

        public VerificationService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IStringLocalizer<SharedResource> localizer,
            IEmailService emailService,
            ILogger<VerificationService> logger)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _localizer = localizer;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<MessageResponse> VerifyEmailAsync(VerifyEmailRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            // 🔒 Check if locked out of verification (only a real, existing account can be locked)
            if (user != null && user.VerificationLockedUntil.HasValue && user.VerificationLockedUntil > DateTime.UtcNow)
                throw new BusinessException(BuildVerificationLockoutMessage(user.VerificationLockedUntil.Value), 423);

            // Run the code lookup even for an unknown email (against Guid.Empty, which never
            // matches a real UserId) so an unregistered email and an invalid code take the
            // same path and produce the same response.
            var verification = await _db.UserVerificationCodes
                .Where(v => v.UserId == (user != null ? user.Id : Guid.Empty) && !v.IsUsed && v.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            if (user == null || verification == null || verification.Code != request.Code)
            {
                if (user != null)
                    await RegisterFailedVerificationAttemptAsync(user); // always throws: 401, or 423 when the lockout trips

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

        public async Task<MessageResponse> ResendVerificationCodeAsync(ResendVerificationRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            // Unknown email and an already-verified account return the same acknowledgement
            // so neither can be told apart from the outside.
            if (user == null)
                return new MessageResponse { Message = _localizer["VerificationResendAcknowledged"].Value };

            if (user.IsVerified)
                return new MessageResponse { Message = _localizer["VerificationResendAcknowledged"].Value };

            // 🔒 Check if locked out of verification
            if (user.VerificationLockedUntil.HasValue && user.VerificationLockedUntil > DateTime.UtcNow)
                throw new BusinessException(BuildVerificationLockoutMessage(user.VerificationLockedUntil.Value), 423);

            // Check cooldown: prevent resending too often. Hitting it returns the same
            // acknowledgement as the cases above instead of a distinct "please wait" signal.
            var lastCode = await _db.UserVerificationCodes
                .Where(v => v.UserId == user.Id)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastCode != null && (DateTime.UtcNow - lastCode.CreatedAt).TotalSeconds < _securityOptions.ResendCooldownSeconds)
                return new MessageResponse { Message = _localizer["VerificationResendAcknowledged"].Value };

            // Invalidate all previously issued, unused codes
            var unusedCodes = await _db.UserVerificationCodes
                .Where(v => v.UserId == user.Id && !v.IsUsed)
                .ToListAsync();

            foreach (var unusedCode in unusedCodes)
            {
                unusedCode.IsUsed = true;
            }

            // Generate new code
            var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

            user.VerificationFailedAttempts = 0;
            user.VerificationLockedUntil = null;

            var verification = new UserVerificationCode
            {
                UserId = user.Id,
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_securityOptions.VerificationCodeExpiryMinutes)
            };

            _db.UserVerificationCodes.Add(verification);
            await _db.SaveChangesAsync();

            // Send email
            try
            {
                await _emailService.SendVerificationEmailAsync(user.Email, user.FirstName, code, _securityOptions.VerificationCodeExpiryMinutes);
            }
            catch (Exception)
            {
                // Decide if we fail the whole request or just log. 
                // fail the request because the user will not receive the code
                _logger.LogError("Failed to send verification email to {Email}", user.Email);
                throw new BusinessException(_localizer["EmailSendingFailed"].Value, 500);
            }

            return new MessageResponse { Message = _localizer["VerificationCodeResent"].Value };
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
            throw new BusinessException(BuildVerificationLockoutMessage(user.VerificationLockedUntil.Value), 423);
        }

        private string BuildVerificationLockoutMessage(DateTime lockedUntilUtc)
        {
            var lockUntilText = lockedUntilUtc.ToLocalTime().ToString("f", CultureInfo.CurrentUICulture);
            return string.Format(_localizer["VerificationLockedUntil"].Value, lockUntilText);
        }
    }
}
