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
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly IVerificationCodeHasher _hasher;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IPasswordAuditRecorder _auditRecorder;
        private readonly IPasswordHasher _passwordHasher;

        public PasswordResetService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IStringLocalizer<SharedResource> localizer,
            ILogger<PasswordResetService> logger,
            IVerificationCodeHasher hasher,
            IRefreshTokenService refreshTokenService,
            IPasswordAuditRecorder auditRecorder,
            IPasswordHasher passwordHasher)
        {
            _db = db;
            _securityOptions = securityOptions.Value;
            _localizer = localizer;
            _logger = logger;
            _hasher = hasher;
            _refreshTokenService = refreshTokenService;
            _auditRecorder = auditRecorder;
            _passwordHasher = passwordHasher;
        }

        // The request only queues the email, whatever it is: it never reads accounts and never sends,
        // so response time cannot reveal whether the email has an account. The processor decides later.
        public async Task<MessageResponse> RequestResetCodeAsync(ForgotPasswordRequest request)
        {
            var now = DateTime.UtcNow;
            _db.PasswordResetRequests.Add(new PasswordResetRequest
            {
                Email = request.Email,
                CreatedAt = now,
                NextAttemptAt = now
            });
            await _db.SaveChangesAsync();
            return new MessageResponse { Message = _localizer["PasswordResetAcknowledged"].Value };
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
