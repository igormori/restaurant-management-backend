using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Stub (tester): RequestResetCodeAsync is T6, ResetPasswordAsync is T7.
    public class PasswordResetService : IPasswordResetService
    {
        public PasswordResetService(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IStringLocalizer<SharedResource> localizer,
            IEmailService emailService,
            ILogger<PasswordResetService> logger,
            IVerificationCodeHasher hasher,
            IRefreshTokenService refreshTokenService,
            IPasswordAuditRecorder auditRecorder)
        {
        }

        public Task<MessageResponse> RequestResetCodeAsync(ForgotPasswordRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            throw new NotImplementedException();
        }
    }
}
