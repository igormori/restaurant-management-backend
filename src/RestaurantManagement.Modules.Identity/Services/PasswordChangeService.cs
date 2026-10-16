using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Stub (tester): implemented in T8.
    public class PasswordChangeService : IPasswordChangeService
    {
        public PasswordChangeService(
            IdentityDbContext db,
            IStringLocalizer<SharedResource> localizer,
            IRefreshTokenService refreshTokenService,
            IPasswordAuditRecorder auditRecorder)
        {
        }

        public Task<MessageResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            throw new NotImplementedException();
        }
    }
}
