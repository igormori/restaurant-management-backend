using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ClearExtensions;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// A request whose code was stored but whose send never finished (the app crashed) is not sent
    /// again, because a second code would break the first one the user may already have. A send that
    /// fails normally still retries.
    /// </summary>
    public class PasswordResetEmailProcessorCrashTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private static readonly Guid RequestId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");

        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly CapturingLogger<PasswordResetEmailProcessor> _logger = new();

        private async Task<IdentityTestDatabase> SeedAsync(DateTime now, DateTime? codeIssuedAt)
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.Add(new User { Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true });
            context.Set<PendingEmail>().Add(new PendingEmail
            {
                Id = RequestId,
                Email = MariaEmail,
                CreatedAt = now.AddMinutes(-10),
                NextAttemptAt = now.AddMinutes(-9),
                ClaimedUntil = codeIssuedAt == null ? null : now.AddMinutes(-1),
                CodeIssuedAt = codeIssuedAt
            });
            await context.SaveChangesAsync();
            return database;
        }

        private async Task ProcessAsync(IdentityTestDatabase database, DateTime now)
        {
            using var context = database.CreateContext();
            var sut = new PasswordResetEmailProcessor(
                context,
                TestSecurityOptions.Create(verificationCodeExpiryMinutes: 20, resendCooldownSeconds: 60),
                _emailService,
                TestVerificationCodeHasher.Create(),
                _logger);
            await sut.ProcessDueAsync(now, CancellationToken.None);
        }

        private static async Task<List<PendingEmail>> ReadQueueAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.Set<PendingEmail>().ToListAsync();
        }

        // Scenario 29
        [Fact]
        [Trait("Rule", "P24")]
        public async Task ProcessDueAsync_ClaimedRequestWithCodeAlreadyIssued_DropsItWithoutSendingAndLogsOnlyItsId()
        {
            // Arrange
            var now = DateTime.UtcNow;
            using var database = await SeedAsync(now, codeIssuedAt: now.AddMinutes(-6));

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request interrupted after its code was stored is finished, not sent again");
            _logger.Levels.ShouldContain(LogLevel.Warning, "dropping an interrupted request is logged");
            _logger.Messages.ShouldContain(m => m.Contains(RequestId.ToString()), "the warning names the request by its id");
            _logger.Messages.ShouldNotContain(m => m.Contains(MariaEmail), "the email address must never be written to the logs");
        }

        // Scenario 30
        [Fact]
        [Trait("Rule", "P24")]
        public async Task ProcessDueAsync_SendInProgressOrFailed_MarkSetThenClearedSoRetryStillSends()
        {
            // Arrange
            var now = DateTime.UtcNow;
            using var database = await SeedAsync(now, codeIssuedAt: null);
            DateTime? markDuringSend = null;
            _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(_ =>
                {
                    markDuringSend = ReadQueueAsync(database).GetAwaiter().GetResult().Single().CodeIssuedAt;
                    return Task.FromException(new InvalidOperationException("SMTP unavailable"));
                });
            await ProcessAsync(database, now);
            _emailService.ClearSubstitute();

            // Act
            await ProcessAsync(database, now.AddMinutes(2));

            // Assert
            markDuringSend.ShouldNotBeNull("the code is marked as issued before the send starts");
            await _emailService.ReceivedWithAnyArgs(1).SendPasswordResetEmailAsync(default!, default!, default!, default);
            (await ReadQueueAsync(database)).ShouldBeEmpty("the retry after a caught failure sends normally and finishes the request");
        }
    }
}
