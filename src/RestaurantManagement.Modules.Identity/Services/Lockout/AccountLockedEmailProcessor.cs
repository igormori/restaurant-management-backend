using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;

namespace RestaurantManagement.Modules.Identity.Services.Lockout
{
    // Stub (tester): sends the queued account-locked emails (task T2).
    public class AccountLockedEmailProcessor : IAccountLockedEmailProcessor
    {
        public AccountLockedEmailProcessor(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IEmailService emailService,
            ILogger<AccountLockedEmailProcessor> logger)
        {
        }

        public Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
