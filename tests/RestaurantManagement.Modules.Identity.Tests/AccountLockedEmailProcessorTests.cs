using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Lockout;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The processor sends the queued account-locked emails outside the login request. Maria Souza is the
    /// locked account; her email says someone tried to sign in, sign-in is paused for 15 minutes and she
    /// should reset her password if it was not her. It holds no code, link or IP address.
    /// </summary>
    public class AccountLockedEmailProcessorTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";

        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly CapturingLogger<AccountLockedEmailProcessor> _logger = new();
        private bool _sendFails;

        public AccountLockedEmailProcessorTests()
        {
            _emailService.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(_ => _sendFails ? Task.FromException(new InvalidOperationException($"SMTP refused {MariaEmail}")) : Task.CompletedTask);
        }

        private static async Task<IdentityTestDatabase> CreateDatabaseAsync(string firstName = "Maria")
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.Add(new User { Email = MariaEmail, PasswordHash = "hash", FirstName = firstName, LastName = "Souza", IsVerified = true });
            await context.SaveChangesAsync();
            return database;
        }

        private static async Task QueueAsync(
            IdentityTestDatabase database, DateTime now, string email = MariaEmail,
            PendingEmailPurpose purpose = PendingEmailPurpose.AccountLocked, int attempts = 0)
        {
            using var context = database.CreateContext();
            context.PendingEmails.Add(new PendingEmail
            {
                Purpose = purpose,
                Email = email,
                CreatedAt = now.AddSeconds(-5),
                Attempts = attempts,
                NextAttemptAt = now.AddSeconds(-1)
            });
            await context.SaveChangesAsync();
        }

        private async Task ProcessAsync(IdentityTestDatabase database, DateTime now)
        {
            using var context = database.CreateContext();
            var sut = new AccountLockedEmailProcessor(context, TestSecurityOptions.Create(lockoutDurationMinutes: 15), _emailService, _logger);
            await sut.ProcessDueAsync(now, CancellationToken.None);
        }

        private (string To, string Subject, string Body) SingleSentEmail()
        {
            var call = _emailService.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IEmailService.SendEmailAsync)).ShouldHaveSingleItem("one email is sent");
            var args = call.GetArguments();
            return ((string)args[0]!, (string)args[1]!, (string)args[2]!);
        }

        private static async Task<List<PendingEmail>> ReadQueueAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.PendingEmails.AsNoTracking().ToListAsync();
        }

        [Fact]
        [Trait("Rule", "L30")]
        public async Task ProcessDueAsync_DueRequest_SendsEnglishEmailWithFirstNameAndResetAdviceAndRemovesRequest()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            var email = SingleSentEmail();
            email.To.ShouldBe(MariaEmail, "the email goes to the locked account");
            email.Subject.ShouldNotBeNullOrWhiteSpace();
            email.Body.ShouldContain("Maria", customMessage: "the email greets the user by first name");
            email.Body.ShouldContain("15 minutes", customMessage: "the email says how long sign-in is paused");
            email.Body.ShouldContain("reset your password", Case.Insensitive, "the email tells the user to reset the password if it was not them");
            (await ReadQueueAsync(database)).ShouldBeEmpty("a sent request is removed from the queue");
        }

        [Fact]
        [Trait("Rule", "L30")]
        public async Task ProcessDueAsync_DueRequest_EmailHoldsNoCodeNoLinkAndNoIpAddress()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            var body = SingleSentEmail().Body;
            body.ShouldNotContain("href", Case.Insensitive, "the email holds no link that could unlock the account");
            body.ShouldNotContain("http", Case.Insensitive, "the email holds no web address");
            Regex.IsMatch(body, @"\b\d{6}\b").ShouldBeFalse("the email holds no six-digit code");
            Regex.IsMatch(body, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b").ShouldBeFalse("the email never shows the IP address of the attempts");
        }

        [Fact]
        [Trait("Rule", "L30")]
        public async Task ProcessDueAsync_FirstNameWithHtml_IsShownAsText()
        {
            // Arrange
            using var database = await CreateDatabaseAsync("Maria <script>alert(1)</script>");
            var now = DateTime.UtcNow;
            await QueueAsync(database, now);

            // Act
            await ProcessAsync(database, now);

            // Assert
            var body = SingleSentEmail().Body;
            body.ShouldNotContain("<script>", customMessage: "a first name with HTML must not become markup");
            body.ShouldContain("&lt;script&gt;", customMessage: "the first name is HTML-encoded");
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(2, 4)]
        [InlineData(3, 8)]
        [Trait("Rule", "L31")]
        public async Task ProcessDueAsync_SendFails_RetriesAfterGrowingWait(int attemptsSoFar, int waitMinutes)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, now, attempts: attemptsSoFar);
            _sendFails = true;

            // Act
            await ProcessAsync(database, now);

            // Assert
            var request = (await ReadQueueAsync(database)).ShouldHaveSingleItem("a failed send keeps the request for a retry");
            request.Attempts.ShouldBe(attemptsSoFar + 1);
            request.NextAttemptAt.ShouldBe(now.AddMinutes(waitMinutes), TimeSpan.FromSeconds(5), $"the retry after failure number {attemptsSoFar + 1} waits {waitMinutes} minutes");
            request.FailedAt.ShouldBeNull("the request is not failed yet");
            request.ClaimedUntil.ShouldBeNull("the claim is released so the retry can run");
        }

        [Fact]
        [Trait("Rule", "L31")]
        public async Task ProcessDueAsync_FifthSendFails_MarksTheRequestFailedAndKeepsIt()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, now, attempts: 4);
            _sendFails = true;

            // Act
            await ProcessAsync(database, now);

            // Assert
            var request = (await ReadQueueAsync(database)).ShouldHaveSingleItem("a failed request is kept for 7 days");
            request.FailedAt.ShouldNotBeNull("the request is marked failed after the 5th failed send");
        }

        [Fact]
        [Trait("Rule", "L32")]
        public async Task ProcessDueAsync_SendFails_LogsWithoutTheEmailAddress()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, now);
            _sendFails = true;

            // Act
            await ProcessAsync(database, now);

            // Assert
            _logger.Levels.ShouldContain(LogLevel.Warning, "a failed send is logged");
            _logger.Messages.ShouldNotContain(m => m.Contains(MariaEmail), "the email address must never be written to the logs");
        }

        [Fact]
        [Trait("Rule", "L33")]
        public async Task ProcessDueAsync_AccountNoLongerExists_RemovesRequestAndSendsNothing()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, now, email: "apagada@cantinadanonna.com.br");

            // Act
            await ProcessAsync(database, now);

            // Assert
            (await ReadQueueAsync(database)).ShouldBeEmpty("the request of an unknown account is removed");
            await _emailService.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);
        }

        [Theory]
        [InlineData(PendingEmailPurpose.PasswordReset)]
        [InlineData(PendingEmailPurpose.Verification)]
        [Trait("Rule", "L33")]
        public async Task ProcessDueAsync_RequestOfAnotherPurpose_IsLeftUntouchedAndSendsNothing(PendingEmailPurpose purpose)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var now = DateTime.UtcNow;
            await QueueAsync(database, now, purpose: purpose);

            // Act
            await ProcessAsync(database, now);

            // Assert
            (await ReadQueueAsync(database)).ShouldHaveSingleItem("this processor never takes another purpose's request").Purpose.ShouldBe(purpose);
            await _emailService.DidNotReceiveWithAnyArgs().SendEmailAsync(default!, default!, default!);
        }
    }
}
