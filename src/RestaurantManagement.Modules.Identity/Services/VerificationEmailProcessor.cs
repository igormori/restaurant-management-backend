using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;

namespace RestaurantManagement.Modules.Identity.Services
{
    // STUB (tester): T2 implements it.
    public class VerificationEmailProcessor : IVerificationEmailProcessor
    {
        public VerificationEmailProcessor(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IEmailService emailService,
            IVerificationCodeHasher hasher,
            ILogger<VerificationEmailProcessor> logger)
        {
        }

        public Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
