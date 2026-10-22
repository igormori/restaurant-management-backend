using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The queue holds two kinds of request, password reset and verification. The password reset pass
    /// only handles its own kind, so a queued verification request is left for the verification pass.
    /// Maria is active and verified (she could get a reset email); Lucia is unverified.
    /// </summary>
    public class PasswordResetEmailProcessorPurposeTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";

        private static async Task<IdentityTestDatabase> CreateDatabaseWithQueuedRowAsync(string email, DateTime now, DateTime? failedAt = null)
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.Add(new User { Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true });
            context.Set<PendingEmail>().Add(new PendingEmail
            {
                Purpose = PendingEmailPurpose.Verification,
                Email = email,
                CreatedAt = now.AddSeconds(-5),
                NextAttemptAt = now.AddSeconds(-1),
                FailedAt = failedAt,
                Attempts = failedAt == null ? 0 : 5
            });
            await context.SaveChangesAsync();
            return database;
        }

        private static async Task<IEmailService> RunResetPassAsync(IdentityTestDatabase database, DateTime now)
        {
            var emailService = Substitute.For<IEmailService>();
            using var context = database.CreateContext();
            var sut = new PasswordResetEmailProcessor(
                context,
                TestSecurityOptions.Create(),
                emailService,
                TestVerificationCodeHasher.Create(),
                new CapturingLogger<PasswordResetEmailProcessor>());
            await sut.ProcessDueAsync(now, CancellationToken.None);
            return emailService;
        }

        [Fact]
        [Trait("Rule", "P28")]
        public async Task ProcessDueAsync_DueVerificationRequest_IsLeftUntouchedByAResetPass()
        {
            // Arrange
            var now = DateTime.UtcNow;
            using var database = await CreateDatabaseWithQueuedRowAsync(MariaEmail, now);

            // Act
            var emailService = await RunResetPassAsync(database, now);

            // Assert
            using var context = database.CreateContext();
            var kept = (await context.Set<PendingEmail>().ToListAsync()).ShouldHaveSingleItem("the reset pass must not remove a verification request");
            kept.ClaimedUntil.ShouldBeNull("the reset pass must not claim a verification request");
            kept.Attempts.ShouldBe(0, "the reset pass must not try a verification request");
            await emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
            (await context.PasswordResetCodes.CountAsync()).ShouldBe(0, "a verification request never creates a reset code");
        }

        [Fact]
        [Trait("Rule", "P28")]
        public async Task ProcessDueAsync_OldFailedVerificationRequest_IsNotPurgedByAResetPass()
        {
            // Arrange
            var now = DateTime.UtcNow;
            using var database = await CreateDatabaseWithQueuedRowAsync("lucia@cantinadanonna.com.br", now, failedAt: now.AddDays(-8));

            // Act
            await RunResetPassAsync(database, now);

            // Assert
            using var context = database.CreateContext();
            (await context.Set<PendingEmail>().CountAsync()).ShouldBe(1, "the reset pass purges only its own failed requests");
        }
    }
}
