using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services.Registration;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The processor handles the queued "resend verification" requests outside the request: only an
    /// unverified, unlocked account outside the 60-second wait gets a code and an email, a failed send is
    /// retried later with growing waits, and a request is never handled twice. Maria is unverified and
    /// eligible; Pedro is already verified; Lucia is locked out of verifying; Carlos was locked but the
    /// lock has expired.
    /// </summary>
    public class VerificationEmailProcessorTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const int ExpiryMinutes = 20;

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid CarlosId = Guid.Parse("33333333-cccc-4ccc-8ccc-00000000000c");
        private static readonly Guid RequestId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
        private static readonly IVerificationCodeHasher Hasher = TestVerificationCodeHasher.Create();

        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly CapturingLogger<VerificationEmailProcessor> _logger = new();
        private readonly List<string> _emailedCodes = new();
        private bool _sendFails;

        public VerificationEmailProcessorTests()
        {
            _emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
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
                new User { Id = MariaId, Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = false },
                new User { Email = "pedro@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Pedro", LastName = "Alves", IsVerified = true },
                new User { Email = "lucia@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Lucia", LastName = "Ramos", IsVerified = false, VerificationLockedUntil = DateTime.UtcNow.AddMinutes(10) },
                new User { Id = CarlosId, Email = "carlos@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Carlos", LastName = "Dias", IsVerified = false, VerificationFailedAttempts = 3, VerificationLockedUntil = DateTime.UtcNow.AddMinutes(-5) });
            await context.SaveChangesAsync();
            return database;
        }

        private static async Task QueueAsync(
            IdentityTestDatabase database,
            string email,
            DateTime now,
            int attempts = 0,
            DateTime? claimedUntil = null,
            DateTime? codeIssuedAt = null,
            Guid? id = null)
        {
            using var context = database.CreateContext();
            context.Set<PendingEmail>().Add(new PendingEmail
            {
                Id = id ?? Guid.NewGuid(),
                Purpose = PendingEmailPurpose.Verification,
                Email = email,
                CreatedAt = now.AddSeconds(-5),
                Attempts = attempts,
                NextAttemptAt = now.AddSeconds(-1),
                ClaimedUntil = claimedUntil,
                CodeIssuedAt = codeIssuedAt
            });
            await context.SaveChangesAsync();
        }

        private async Task ProcessAsync(IdentityTestDatabase database, DateTime now)
        {
            using var context = database.CreateContext();
            var sut = new VerificationEmailProcessor(
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
            return await context.UserVerificationCodes.CountAsync();
        }

        private static async Task SeedCodeAsync(IdentityTestDatabase database, Guid userId, DateTime createdAt)
        {
            using var context = database.CreateContext();
            context.UserVerificationCodes.Add(new UserVerificationCode { UserId = userId, CodeHash = "earlier", ExpiresAt = DateTime.UtcNow.AddMinutes(10), CreatedAt = createdAt });
            await context.SaveChangesAsync();
        }

        [Theory]
        [InlineData("ninguem@cantinadanonna.com.br")]  // no account
        [InlineData("pedro@cantinadanonna.com.br")]    // already verified
        [InlineData("lucia@cantinadanonna.com.br")]    // verification locked
        [Trait("Rule", "RS7")]
        public async Task ProcessDueAsync_UnknownVerifiedOrLockedEmail_NoEmailNoCodeAndRequestRemoved(string email)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, email, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendVerificationEmailAsync(default!, default!, default!, default);
            (await CountCodesAsync(database)).ShouldBe(0, "no verification code is created for an unknown, verified or locked account");
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request that gets no email is finished and removed, not retried");
        }

        [Fact]
        [Trait("Rule", "RS7")]
        public async Task ProcessDueAsync_CodeCreatedWithinResendWait_NoNewCodeNoEmailAndRequestRemoved()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await SeedCodeAsync(database, MariaId, now.AddSeconds(-20));
            await QueueAsync(database, MariaEmail, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendVerificationEmailAsync(default!, default!, default!, default);
            (await CountCodesAsync(database)).ShouldBe(1, "a request within the 60-second wait creates no new code");
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request inside the wait is finished and removed");
        }

        [Fact]
        [Trait("Rule", "RS9")]
        public async Task ProcessDueAsync_SendFails_KeepsRequestAndSchedulesRetryInOneMinute()
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
        }

        [Fact]
        [Trait("Rule", "RS11")]
        public async Task ProcessDueAsync_SendFails_LogsTheUserIdAndNeverTheEmailAddressOrCode()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now);
            _sendFails = true;

            // Act
            await ProcessAsync(database, now);

            // Assert
            _logger.Messages.ShouldContain(m => m.Contains(MariaId.ToString()), "a failed verification email is logged with the user ID");
            _logger.Messages.ShouldNotContain(m => m.Contains(MariaEmail), "the email address must never be written to the logs");
            _logger.Messages.ShouldNotContain(m => m.Contains(_emailedCodes.Single()), "the verification code must never be written to the logs");
        }

        [Fact]
        [Trait("Rule", "RS9")]
        public async Task ProcessDueAsync_RetryAfterFailedSend_SendsDespiteItsOwnRecentCodeAndRemovesRequest()
        {
            // Arrange: the first attempt stored a code 10 seconds ago and its send failed
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await SeedCodeAsync(database, MariaId, now.AddSeconds(-10));
            await QueueAsync(database, MariaEmail, now, attempts: 1);

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.Received(1).SendVerificationEmailAsync(MariaEmail, "Maria", Arg.Any<string>(), ExpiryMinutes);
            (await ReadQueueAsync(database)).ShouldBeEmpty("a retry that finally sends is finished and removed; the 60-second wait does not block a retry");
        }

        [Fact]
        [Trait("Rule", "RS9")]
        public async Task ProcessDueAsync_FifthSendFails_MarksRequestFailedAndKeepsIt()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now, attempts: 4, id: RequestId);
            _sendFails = true;

            // Act
            await ProcessAsync(database, now);

            // Assert
            var kept = (await ReadQueueAsync(database)).ShouldHaveSingleItem("the failed request is kept so it can be investigated");
            kept.Attempts.ShouldBe(5, "the fifth failure is the last attempt");
            kept.FailedAt.ShouldNotBeNull("after the fifth failed send the request is marked failed and never retried");
        }

        [Fact]
        [Trait("Rule", "RS10")]
        public async Task ProcessDueAsync_ClaimedRequestWithCodeAlreadyIssued_DropsItWithoutSending()
        {
            // Arrange: an earlier pass crashed after storing the code, so whether the email left is unknown
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now, claimedUntil: now.AddMinutes(-1), codeIssuedAt: now.AddMinutes(-6));

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendVerificationEmailAsync(default!, default!, default!, default);
            (await CountCodesAsync(database)).ShouldBe(0, "a second code would break the first, so none is created");
            (await ReadQueueAsync(database)).ShouldBeEmpty("at most one email after a crash: the request is dropped");
        }

        [Fact]
        [Trait("Rule", "RS10")]
        public async Task ProcessDueAsync_RequestClaimedByAnotherPass_IsSkipped()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now, claimedUntil: now.AddMinutes(5));

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.DidNotReceiveWithAnyArgs().SendVerificationEmailAsync(default!, default!, default!, default);
            (await ReadQueueAsync(database)).ShouldHaveSingleItem("a request another pass is handling is left alone, so nobody gets two emails");
        }

        [Fact]
        [Trait("Rule", "RS8")]
        public async Task ProcessDueAsync_EligibleAccount_EmailsOneSixDigitCodeWithNameAndExpiryAndRemovesRequest()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            await _emailService.Received(1).SendVerificationEmailAsync(
                MariaEmail, "Maria", Arg.Is<string>(code => Regex.IsMatch(code, "^[0-9]{6}$")), ExpiryMinutes);
            (await ReadQueueAsync(database)).ShouldBeEmpty("a request that was emailed is finished and removed");
        }

        [Fact]
        [Trait("Rule", "RS8")]
        public async Task ProcessDueAsync_EligibleAccount_StoresOnlyTheFingerprintOfTheEmailedCode()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, MariaEmail, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            using var context = database.CreateContext();
            var stored = await context.UserVerificationCodes.SingleAsync();
            var sentCode = _emailedCodes.ShouldHaveSingleItem();
            stored.UserId.ShouldBe(MariaId, "the stored code belongs to the account that asked");
            stored.CodeHash.ShouldNotContain(sentCode, customMessage: "the verification code is never stored readable");
            Hasher.IsMatch(MariaId, sentCode, stored.CodeHash).ShouldBeTrue("only the keyed fingerprint of the emailed code is stored");
        }

        [Fact]
        [Trait("Rule", "RS4")]
        public async Task ProcessDueAsync_LockExpired_IssuesCodeAndClearsFailedAttemptsAndLock()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, "carlos@cantinadanonna.com.br", now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            using var context = database.CreateContext();
            var carlos = await context.Users.SingleAsync(u => u.Id == CarlosId);
            _emailedCodes.ShouldHaveSingleItem("an account whose lock has expired gets a new code");
            carlos.VerificationFailedAttempts.ShouldBe(0, "a new code resets the failed-attempt counter");
            carlos.VerificationLockedUntil.ShouldBeNull("the expired lock is cleared");
        }
    }
}
