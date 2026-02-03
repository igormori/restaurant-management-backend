using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Auth;
using RestaurantManagement.Api.Options;
using RestaurantManagement.Api.Utils.Exceptions;
using RestaurantManagement.Api.Services.Email;

namespace RestaurantManagement.Api.Services.Auth
{
    public class VerificationService : IVerificationService
    {
        private readonly RestaurantDbContext _db;
        private readonly SecurityOptions _securityOptions;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IEmailService _emailService;
        private readonly ILogger<VerificationService> _logger;

        public VerificationService(
            RestaurantDbContext db,
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

        public async Task<string> VerifyEmailAsync(VerifyEmailRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) throw new BusinessException(_localizer["UserNotFound"].Value, 400);

            var verification = await _db.UserVerificationCodes
                .Where(v => v.UserId == user.Id && !v.IsUsed && v.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            if (verification == null || verification.Code != request.Code)
                throw new BusinessException(_localizer["InvalidOrExpiredCode"].Value, 401);

            // ✅ Mark as verified
            user.IsVerified = true;
            verification.IsUsed = true;

            await _db.SaveChangesAsync();
            return _localizer["UserVerified"].Value;
        }

        public async Task<string> ResendVerificationCodeAsync(ResendVerificationRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
                throw new BusinessException(_localizer["UserNotFound"].Value, 404);

            if (user.IsVerified)
                throw new BusinessException(_localizer["UserAlreadyVerified"].Value, 400);

            // Check cooldown: prevent resending too often
            var lastCode = await _db.UserVerificationCodes
                .Where(v => v.UserId == user.Id)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastCode != null && (DateTime.UtcNow - lastCode.CreatedAt).TotalSeconds < _securityOptions.ResendCooldownSeconds)
                throw new BusinessException(_localizer["VerificationCodeRecentlySent"].Value, 429);

            // Invalidate previous unused codes
            if (lastCode != null && !lastCode.IsUsed)
            {
                lastCode.IsUsed = true;
                _db.UserVerificationCodes.Update(lastCode);
            }

            // Generate new code
            var code = new Random().Next(100000, 999999).ToString();

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
               await _emailService.SendVerificationEmailAsync(user.Email, code);
            }
            catch(Exception)
            {
                // Decide if we fail the whole request or just log. 
                // fail the request because the user will not receive the code
                _logger.LogError("Failed to send verification email to {Email}", user.Email);
                throw new BusinessException(_localizer["EmailSendingFailed"].Value, 500);
            }

            return _localizer["VerificationCodeResent"].Value;
        }
    }
}
