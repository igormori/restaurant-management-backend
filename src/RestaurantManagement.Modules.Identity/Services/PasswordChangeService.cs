using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class PasswordChangeService : IPasswordChangeService
    {
        private readonly IdentityDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IPasswordAuditRecorder _auditRecorder;

        public PasswordChangeService(
            IdentityDbContext db,
            IStringLocalizer<SharedResource> localizer,
            IRefreshTokenService refreshTokenService,
            IPasswordAuditRecorder auditRecorder)
        {
            _db = db;
            _localizer = localizer;
            _refreshTokenService = refreshTokenService;
            _auditRecorder = auditRecorder;
        }

        public async Task<MessageResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || !user.IsActive || !user.IsVerified)
                throw new BusinessException(_localizer["UnauthorizedMessage"].Value, 401);

            // A wrong current password is not counted toward the login lockout: the caller already holds a valid session.
            if (!PasswordVerifier.Verify(request.CurrentPassword, user.PasswordHash))
                throw new BusinessException(_localizer["CurrentPasswordIncorrect"].Value, 400);

            if (request.NewPassword == request.CurrentPassword)
                throw new BusinessException(_localizer["NewPasswordSameAsCurrent"].Value, 400);

            await ReplacePasswordAsync(user, request.NewPassword);
            await _auditRecorder.RecordAsync(user.Id, AuditActions.PasswordChanged);
            return new MessageResponse { Message = _localizer["PasswordChangeSucceeded"].Value };
        }

        private async Task ReplacePasswordAsync(User user, string newPassword)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _refreshTokenService.RevokeAllForUserAsync(user.Id, RefreshToken.ReasonPasswordChanged);
            await transaction.CommitAsync();
        }
    }
}
