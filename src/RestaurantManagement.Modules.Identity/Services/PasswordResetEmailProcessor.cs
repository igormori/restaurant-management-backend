using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class PasswordResetEmailProcessor : IPasswordResetEmailProcessor
    {
        public PasswordResetEmailProcessor(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IEmailService emailService,
            IVerificationCodeHasher hasher,
            ILogger<PasswordResetEmailProcessor> logger)
        {
        }

        public Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
