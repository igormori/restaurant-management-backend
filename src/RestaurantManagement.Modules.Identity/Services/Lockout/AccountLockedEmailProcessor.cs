using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Services.Registration;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;

namespace RestaurantManagement.Modules.Identity.Services.Lockout
{
    // Sends the queued account-locked emails outside the login request, so login timing never depends on them.
    public class AccountLockedEmailProcessor : IAccountLockedEmailProcessor
    {

        private readonly IdentityDbContext _db;
        private readonly PendingEmailStore _queue;
        private readonly SecurityOptions _securityOptions;
        private readonly IEmailService _emailService;
        private readonly ILogger<AccountLockedEmailProcessor> _logger;

        public AccountLockedEmailProcessor(
            IdentityDbContext db,
            IOptions<SecurityOptions> securityOptions,
            IEmailService emailService,
            ILogger<AccountLockedEmailProcessor> logger)
        {
            _db = db;
            _queue = new PendingEmailStore(db, PendingEmailPurpose.AccountLocked);
            _securityOptions = securityOptions.Value;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken)
        {
            await _queue.DeleteOldFailedAsync(utcNow, cancellationToken);

            var due = await _queue.LoadDueAsync(utcNow, cancellationToken);

            var handled = 0;
            foreach (var loaded in due)
            {
                var request = await _queue.TryClaimAsync(loaded.Id, utcNow, cancellationToken);
                if (request == null)
                    continue;

                await HandleAsync(request, utcNow, cancellationToken);
                handled++;
            }

            return handled;
        }

        private async Task HandleAsync(PendingEmail request, DateTime utcNow, CancellationToken ct)
        {
            // SendStartedAt is set just before the send: still set on a claimed row means a pass crashed mid-send,
            // and at most once wins over a possible second email.
            if (request.SendStartedAt != null)
            {
                _logger.LogWarning("Account-locked request {RequestId} was interrupted during sending; dropped without sending again", request.Id);
                await _queue.DeleteAsync(request.Id, ct);
                return;
            }

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == request.Email, ct);
            if (user == null)
            {
                await _queue.DeleteAsync(request.Id, ct);
                return;
            }

            await _db.PendingEmails
                .Where(r => r.Id == request.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.SendStartedAt, DateTime.UtcNow), ct);

            try
            {
                await _emailService.SendEmailAsync(
                    user.Email,
                    AccountLockedEmailTemplate.Subject(),
                    AccountLockedEmailTemplate.Build(user.FirstName, _securityOptions.LockoutDurationMinutes));
            }
            catch (Exception ex)
            {
                await RegisterFailureAsync(request, utcNow, ex, ct);
                return;
            }

            await _queue.DeleteAsync(request.Id, ct);
        }

        private async Task RegisterFailureAsync(PendingEmail request, DateTime utcNow, Exception ex, CancellationToken ct)
        {
            var (attempts, gaveUp) = await _queue.RegisterFailureAsync(request, utcNow, ct);

            // Neither the email address nor the exception message may reach the logs.
            if (gaveUp)
                _logger.LogError("Account-locked request {RequestId} failed after {Attempts} attempts ({ExceptionType})", request.Id, attempts, ex.GetType().Name);
            else
                _logger.LogWarning("Account-locked email send failed for request {RequestId} ({ExceptionType})", request.Id, ex.GetType().Name);
        }
    }
}
