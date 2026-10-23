using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ClearExtensions;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Lockout;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// An account-locked request whose send started but never finished (the app crashed) is not sent
    /// again, because at most one email wins over a possible second one. A send that fails normally
    /// still retries. The request carries no code, so its own "send started" mark is used.
    /// </summary>
    public class AccountLockedEmailProcessorCrashTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private static readonly Guid RequestId = Guid.Parse("33333333-cccc-4ccc-8ccc-00000000000c");

        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly CapturingLogger<AccountLockedEmailProcessor> _logger = new();

        private static async Task<IdentityTestDatabase> SeedAsync(DateTime now, DateTime? sendStartedAt)
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.Add(new User { Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true });
            context.PendingEmails.Add(new PendingEmail
            {
                Id = RequestId,
                Purpose = PendingEmailPurpose.AccountLocked,
                Email = MariaEmail,
                CreatedAt = now.AddMinutes(-10),
                NextAttemptAt = now.AddMinutes(-9),
                ClaimedUntil = sendStartedAt == null ? null : now.AddMinutes(-1),
                SendStartedAt = sendStartedAt
            });
            await context.SaveChangesAsync();
            return database;
        }

        private async Task ProcessAsync(IdentityTestDatabase database, DateTime now)
        {
            using var context = database.CreateContext();
            var sut = new AccountLockedEmailProcessor(context, TestSecurityOptions.Create(lockoutDurationMinutes: 15), _emailService, _logger);
            await sut.ProcessDueAsync(now, CancellationToken.None);
        }

        private static async Task<List<PendingEmail>> ReadQueueAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.PendingEmails.AsNoTracking().ToListAsync();
        }

        [Fact]
        [Trait("Rule", "L37")]
        public async Task ProcessDueAsync_ClaimedRequestWhoseSendAlreadyStarted_DropsItWithoutSendingAndLogsOnlyItsId()
        {
            // Arrange
            var now = DateTime.UtcNow;
            using var database = await SeedAsync(now, sendStartedAt: now.AddMinutes(-6));

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request interrupted mid-send is finished, not sent again");
            _logger.Levels.ShouldContain(LogLevel.Warning, "dropping an interrupted request is logged");
            _logger.Messages.ShouldContain(m => m.Contains(RequestId.ToString()), "the warning names the request by its id");
            _logger.Messages.ShouldNotContain(m => m.Contains(MariaEmail), "the email address must never be written to the logs");
        }

        [Fact]
        [Trait("Rule", "L37")]
        public async Task ProcessDueAsync_SendInProgressOrFailed_MarkSetThenClearedSoRetryStillSends()
        {
            // Arrange
            var now = DateTime.UtcNow;
            using var database = await SeedAsync(now, sendStartedAt: null);
            DateTime? markDuringSend = null;
            _emailService.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(_ =>
                {
                    markDuringSend = ReadQueueAsync(database).GetAwaiter().GetResult().Single().SendStartedAt;
                    return Task.FromException(new InvalidOperationException("SMTP unavailable"));
                });
            await ProcessAsync(database, now);
            _emailService.ClearSubstitute();

            // Act
            await ProcessAsync(database, now.AddMinutes(2));

            // Assert
            markDuringSend.ShouldNotBeNull("the request is marked as started before the send begins");
            await _emailService.ReceivedWithAnyArgs(1).SendEmailAsync(default!, default!, default!);
            (await ReadQueueAsync(database)).ShouldBeEmpty("the retry after a caught failure sends normally and finishes the request");
        }
    }
}
