using RestaurantManagement.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            user.UpdatedAt = DateTime.UtcNow;
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
            catch (Exception)
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
