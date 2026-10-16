using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailService _emailService;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly IVerificationCodeHasher _hasher;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IPasswordAuditRecorder _auditRecorder;
        private readonly IPasswordHasher _passwordHasher;

        public PasswordResetService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IStringLocalizer<SharedResource> localizer,
            IEmailService emailService,
            ILogger<PasswordResetService> logger,
            IVerificationCodeHasher hasher,
            IRefreshTokenService refreshTokenService,
            IPasswordAuditRecorder auditRecorder,
            IPasswordHasher passwordHasher)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _localizer = localizer;
            _emailService = emailService;
            _logger = logger;
            _hasher = hasher;
            _refreshTokenService = refreshTokenService;
            _auditRecorder = auditRecorder;
            _passwordHasher = passwordHasher;
        }

        public async Task<MessageResponse> RequestResetCodeAsync(ForgotPasswordRequest request)
        {
            var acknowledgement = new MessageResponse { Message = _localizer["PasswordResetAcknowledged"].Value };
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            // Every case that must not receive a code returns the same acknowledgement.
            if (user == null || !user.IsActive || !user.IsVerified || IsResetLocked(user))
                return acknowledgement;

            var lastCode = await _db.PasswordResetCodes
                .Where(c => c.UserId == user.Id)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
            if (lastCode != null && (DateTime.UtcNow - lastCode.CreatedAt).TotalSeconds < _securityOptions.ResendCooldownSeconds)
                return acknowledgement;

            await MarkUnusedCodesAsUsedAsync(user.Id);

            var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            user.PasswordResetFailedAttempts = 0;
            _db.PasswordResetCodes.Add(new PasswordResetCode
            {
                UserId = user.Id,
                CodeHash = _hasher.ComputeHash(user.Id, code),
                ExpiresAt = DateTime.UtcNow.AddMinutes(_securityOptions.VerificationCodeExpiryMinutes)
            });
            await _db.SaveChangesAsync();

            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.FirstName, code, _securityOptions.VerificationCodeExpiryMinutes);
            }
            catch (Exception ex)
            {
                // Only the exception type is logged: neither the code nor the email body may reach the logs.
                _logger.LogError("Failed to send password reset email ({ExceptionType})", ex.GetType().Name);
            }

            return acknowledgement;
        }

        public async Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            // The code lookup runs even for an unknown email so both paths look alike.
            var userId = user?.Id ?? Guid.Empty;
            var resetCode = await _db.PasswordResetCodes
                .Where(c => c.UserId == userId && !c.IsUsed && c.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (user == null || !user.IsActive || IsResetLocked(user))
                throw InvalidRequest();

            if (resetCode == null || !_hasher.IsMatch(user.Id, request.Code, resetCode.CodeHash))
            {
                await RegisterFailedAttemptAsync(user);
                throw InvalidRequest();
            }

            await ReplacePasswordAsync(user, resetCode, request.NewPassword);
            await RecordAuditAsync(user.Id);
            return new MessageResponse { Message = _localizer["PasswordResetSucceeded"].Value };
        }

        // The password is already saved; a failed audit write must not fail the request.
        private async Task RecordAuditAsync(Guid userId)
        {
            try
            {
                await _auditRecorder.RecordAsync(userId, AuditActions.PasswordReset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record the password reset of user {UserId}.", userId);
            }
        }

        private async Task ReplacePasswordAsync(User user, PasswordResetCode resetCode, string newPassword)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            user.PasswordHash = _passwordHasher.Hash(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            user.PasswordResetFailedAttempts = 0;
            user.PasswordResetLockedUntil = null;
            user.FailedAttempts = 0;
            user.LockedUntil = null;
            resetCode.IsUsed = true;
            await _db.SaveChangesAsync();
            await _refreshTokenService.RevokeAllForUserAsync(user.Id, RefreshToken.ReasonPasswordChanged);
            await transaction.CommitAsync();
        }

        private async Task RegisterFailedAttemptAsync(User user)
        {
            user.PasswordResetFailedAttempts++;
            if (user.PasswordResetFailedAttempts >= _securityOptions.MaxVerificationAttempts)
            {
                user.PasswordResetLockedUntil = DateTime.UtcNow.AddMinutes(_securityOptions.VerificationLockoutDurationMinutes);
                user.PasswordResetFailedAttempts = 0;
                await MarkUnusedCodesAsUsedAsync(user.Id);
            }

            await _db.SaveChangesAsync();
        }

        private async Task MarkUnusedCodesAsUsedAsync(Guid userId)
        {
            var unusedCodes = await _db.PasswordResetCodes
                .Where(c => c.UserId == userId && !c.IsUsed)
                .ToListAsync();
            foreach (var unusedCode in unusedCodes)
                unusedCode.IsUsed = true;
        }

        private static bool IsResetLocked(User user)
            => user.PasswordResetLockedUntil.HasValue && user.PasswordResetLockedUntil > DateTime.UtcNow;

        private BusinessException InvalidRequest()
            => new(_localizer["InvalidEmailOrResetCode"].Value, 401);
    }
}
