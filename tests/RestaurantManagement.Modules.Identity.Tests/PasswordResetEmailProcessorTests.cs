using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Modules.Identity.Services.Registration;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The processor handles the queued forgot-password requests outside the request: only an active,
    /// verified, unlocked user gets a code and an email, a failed send is retried later with growing
    /// waits, and a request is never handled twice. Maria is active and verified; Pedro is inactive;
    /// Lucia has not verified her email; Carlos is locked out of resetting.
    /// </summary>
    public class PasswordResetEmailProcessorTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const int ExpiryMinutes = 20;

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid RequestId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
        private static readonly IVerificationCodeHasher Hasher = TestVerificationCodeHasher.Create();

        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly CapturingLogger<PasswordResetEmailProcessor> _logger = new();
        private readonly List<string> _emailedCodes = new();
        private bool _sendFails;

        public PasswordResetEmailProcessorTests()
        {
            _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(call =>
                {
                    _emailedCodes.Add(call.ArgAt<string>(2));
                    return _sendFails ? Task.FromException(new InvalidOperationException("SMTP server unavailable for " + call.ArgAt<string>(0))) : Task.CompletedTask;
                });
        }

        private static async Task<IdentityTestDatabase> CreateDatabaseAsync()
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.AddRange(
                new User { Id = MariaId, Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true },
                new User { Email = "pedro@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Pedro", LastName = "Alves", IsVerified = true, IsActive = false },
                new User { Email = "lucia@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Lucia", LastName = "Ramos", IsVerified = false },
                new User { Email = "carlos@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Carlos", LastName = "Dias", IsVerified = true, PasswordResetLockedUntil = DateTime.UtcNow.AddMinutes(10) });
            await context.SaveChangesAsync();
            return database;
        }

        private static async Task QueueAsync(
            IdentityTestDatabase database,
            string email,
            DateTime now,
            int attempts = 0,
            DateTime? claimedUntil = null,
            DateTime? failedAt = null,
            Guid? id = null,
            DateTime? nextAttemptAt = null)
        {
            using var context = database.CreateContext();
            context.Set<PendingEmail>().Add(new PendingEmail
            {
                Id = id ?? Guid.NewGuid(),
                Email = email,
                CreatedAt = now.AddSeconds(-5),
                Attempts = attempts,
                NextAttemptAt = nextAttemptAt ?? now.AddSeconds(-1),
                ClaimedUntil = claimedUntil,
                FailedAt = failedAt
            });
            await context.SaveChangesAsync();
        }

        private async Task ProcessAsync(IdentityTestDatabase database, DateTime now)
        {
            using var context = database.CreateContext();
            var sut = new PasswordResetEmailProcessor(
                context,
                TestSecurityOptions.Create(verificationCodeExpiryMinutes: ExpiryMinutes, resendCooldownSeconds: 60),
                _emailService,
                Hasher,
                _logger);
            await sut.ProcessDueAsync(now, CancellationToken.None);
        }

        private static async Task<List<PendingEmail>> ReadQueueAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.Set<PendingEmail>().ToListAsync();
        }

        private static async Task<int> CountCodesAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.PasswordResetCodes.CountAsync();
        }

        [Theory]
        [InlineData("ninguem@cantinadanonna.com.br")]  // no account
        [InlineData("pedro@cantinadanonna.com.br")]    // inactive
        [InlineData("lucia@cantinadanonna.com.br")]    // email not verified
        [Trait("Rule", "P15")]
        public async Task ProcessDueAsync_UnknownInactiveOrUnverifiedEmail_NoEmailNoCodeAndRequestRemoved(string email)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, email, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
            (await CountCodesAsync(database)).ShouldBe(0, "no reset code is created for an unknown, deactivated or unverified account");
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request that gets no email is finished and removed, not retried");
        }

        [Fact]
        [Trait("Rule", "P4")]
        public async Task ProcessDueAsync_ResetLocked_NoEmailNoCodeAndRequestRemoved()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, "carlos@cantinadanonna.com.br", now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
            (await CountCodesAsync(database)).ShouldBe(0, "no code is created while the reset is locked");
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request that gets no email is finished and removed");
        }

        [Fact]
        [Trait("Rule", "P2")]
        public async Task ProcessDueAsync_ActiveVerifiedUser_EmailsSixDigitCodeWithNameAndExpiryAndRemovesRequest()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.Received(1).SendPasswordResetEmailAsync(
                MariaEmail, "Maria", Arg.Is<string>(code => Regex.IsMatch(code, "^[0-9]{6}$")), ExpiryMinutes);
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request that was emailed is finished and removed");
        }

        [Fact]
        [Trait("Rule", "P2")]
        public async Task ProcessDueAsync_ActiveVerifiedUser_StoresOnlyProtectedFormOfCode()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            using var context = database.CreateContext();
            var stored = await context.PasswordResetCodes.SingleAsync();
            var sentCode = _emailedCodes.ShouldHaveSingleItem();
            stored.UserId.ShouldBe(MariaId, "the stored code belongs to the user who asked");
            stored.IsUsed.ShouldBeFalse();
            stored.CodeHash.ShouldNotBe(sentCode, "the reset code is never stored readable");
            stored.CodeHash.ShouldBe(Hasher.ComputeHash(MariaId, sentCode), "only the keyed fingerprint of the emailed code is stored");
        }

        [Fact]
        [Trait("Rule", "P3")]
        public async Task ProcessDueAsync_CodeCreatedWithinResendWait_NoNewCodeNoEmailAndRequestRemoved()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            using (var seed = database.CreateContext())
            {
                seed.PasswordResetCodes.Add(new PasswordResetCode { UserId = MariaId, CodeHash = "earlier", ExpiresAt = now.AddMinutes(10), CreatedAt = now.AddSeconds(-20) });
                await seed.SaveChangesAsync();
            }
            await QueueAsync(database, MariaEmail, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
            (await CountCodesAsync(database)).ShouldBe(1, "a second request within the 60-second wait creates no new code");
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request inside the wait is finished and removed");
        }

        [Fact]
        [Trait("Rule", "P17")]
        public async Task ProcessDueAsync_EarlierUnusedCode_IsInvalidatedSoOnlyTheNewestWorks()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            using (var seed = database.CreateContext())
            {
                seed.PasswordResetCodes.Add(new PasswordResetCode { UserId = MariaId, CodeHash = "earlier", ExpiresAt = now.AddMinutes(5), CreatedAt = now.AddMinutes(-5) });
                await seed.SaveChangesAsync();
            }
            await QueueAsync(database, MariaEmail, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            using var context = database.CreateContext();
            var unused = await context.PasswordResetCodes.Where(c => !c.IsUsed).ToListAsync();
            unused.ShouldHaveSingleItem("only the newest code may still work").CodeHash.ShouldBe(Hasher.ComputeHash(MariaId, _emailedCodes.Single()));
        }

        [Fact]
        [Trait("Rule", "P16")]
        public async Task ProcessDueAsync_SendFails_KeepsRequestWithOneAttemptAndNothingSecretLogged()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now, id: RequestId);
            _sendFails = true;

            // Act
            await ProcessAsync(database, now);

            // Assert
            var kept = (await ReadQueueAsync(database)).ShouldHaveSingleItem("a request whose email failed to send is kept for a retry, not lost");
            kept.Attempts.ShouldBe(1, "the failed send counts as one attempt");
            kept.NextAttemptAt.ShouldBe(now.AddMinutes(1), TimeSpan.FromSeconds(1), "the first retry comes 1 minute later");
            kept.FailedAt.ShouldBeNull("one failure does not give up on the request");
            _logger.Messages.ShouldNotContain(m => m.Contains(_emailedCodes.Single()), "the reset code must never be written to the logs");
            _logger.Messages.ShouldNotContain(m => m.Contains(MariaEmail), "the email address must never be written to the logs");
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(2, 4)]
        [InlineData(3, 8)]
        [Trait("Rule", "P16")]
        public async Task ProcessDueAsync_SendFailsAfterEarlierAttempts_NextAttemptWaitDoubles(int earlierAttempts, int expectedWaitMinutes)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now, attempts: earlierAttempts);
            _sendFails = true;

            // Act
            await ProcessAsync(database, now);

            // Assert
            var kept = (await ReadQueueAsync(database)).ShouldHaveSingleItem();
            kept.Attempts.ShouldBe(earlierAttempts + 1, "each failed send adds one attempt");
            kept.NextAttemptAt.ShouldBe(now.AddMinutes(expectedWaitMinutes), TimeSpan.FromSeconds(1), "the wait before the next try doubles with every failure: 1, 2, 4, 8 minutes");
        }

        [Fact]
        [Trait("Rule", "P16")]
        public async Task ProcessDueAsync_PassBeforeNextAttemptTime_DoesNotRetry()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now);
            _sendFails = true;
            await ProcessAsync(database, now);
            _emailService.ClearReceivedCalls();

            // Act
            await ProcessAsync(database, now.AddSeconds(30));

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
            (await ReadQueueAsync(database)).ShouldHaveSingleItem("the request waits for its next attempt time").Attempts.ShouldBe(1, "no retry before the next attempt time");
        }

        [Fact]
        [Trait("Rule", "P17")]
        public async Task ProcessDueAsync_SendFailedEarlier_NextDuePassSendsNewCodeAndOnlyItWorks()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now);
            _sendFails = true;
            await ProcessAsync(database, now);
            _sendFails = false;

            // Act
            await ProcessAsync(database, now.AddMinutes(1));

            // Assert
            _emailedCodes.Count.ShouldBe(2, "the failed send is tried again on a later pass");
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request that was finally emailed is removed");
            using var context = database.CreateContext();
            var unused = await context.PasswordResetCodes.Where(c => !c.IsUsed).ToListAsync();
            unused.ShouldHaveSingleItem("the retry gets a new code and the earlier one stops working, so only the newest works")
                .CodeHash.ShouldBe(Hasher.ComputeHash(MariaId, _emailedCodes[1]));
        }

        [Fact]
        [Trait("Rule", "P18")]
        public async Task ProcessDueAsync_FifthSendFails_MarksFailedKeepsRequestAndLogsOnlyItsId()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now, attempts: 4, id: RequestId);
            _sendFails = true;

            // Act
            await ProcessAsync(database, now);

            // Assert
            var failed = (await ReadQueueAsync(database)).ShouldHaveSingleItem("a request that failed five times is kept for inspection");
            failed.Attempts.ShouldBe(5);
            failed.FailedAt.ShouldNotBeNull("after the fifth failed attempt the request is marked failed");
            _logger.Levels.ShouldContain(LogLevel.Error, "giving up on a request is logged as an error");
            _logger.Messages.ShouldContain(m => m.Contains(RequestId.ToString()), "the error names the request by its id");
            _logger.Messages.ShouldNotContain(m => m.Contains(MariaEmail), "the email address must never be written to the logs");
            _logger.Messages.ShouldNotContain(m => m.Contains(_emailedCodes.Single()), "the reset code must never be written to the logs");
        }

        [Fact]
        [Trait("Rule", "P19")]
        public async Task ProcessDueAsync_RequestMarkedFailed_IsNotTriedAgain()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now, attempts: 5, failedAt: now.AddMinutes(-30));

            // Act
            await ProcessAsync(database, now.AddHours(1));

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
            (await ReadQueueAsync(database)).ShouldHaveSingleItem("a failed request stays as it is");
        }

        [Fact]
        [Trait("Rule", "P19")]
        public async Task ProcessDueAsync_RequestClaimedByAnotherPass_IsSkipped()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now, claimedUntil: now.AddMinutes(2));

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
            (await ReadQueueAsync(database)).ShouldHaveSingleItem("a request another instance is handling must be left alone, so no one gets two emails");
        }

        [Fact]
        [Trait("Rule", "P21")]
        public async Task ProcessDueAsync_SeveralDueRequests_EligibleEmailGetsExactlyOneEmail()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now);
            await QueueAsync(database, "ninguem@cantinadanonna.com.br", now);
            await QueueAsync(database, MariaEmail, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            _emailedCodes.ShouldHaveSingleItem("repeated requests inside the 60-second wait send one email only, and unknown emails send none");
            (await ReadQueueAsync(database)).ShouldBeEmpty("one pass handles every due request");
        }
    }
}
