using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    // The pass-start time only chooses which rows are due; each row's code is timed when it is handled.
    public class PasswordResetEmailProcessorClockTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";

        // Scenario 38
        [Fact]
        [Trait("Rule", "P27")]
        public async Task ProcessDueAsync_PassStartedLongAgo_CodeExpiryCountsFromWhenTheRowIsHandled()
        {
            // Arrange
            var passStart = DateTime.UtcNow.AddHours(-1);
            using var database = new IdentityTestDatabase();
            using (var seed = database.CreateContext())
            {
                seed.Users.Add(new User { Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true });
                seed.Set<PendingEmail>().Add(new PendingEmail { Email = MariaEmail, CreatedAt = passStart.AddMinutes(-10), NextAttemptAt = passStart.AddMinutes(-9) });
                await seed.SaveChangesAsync();
            }
            using var context = database.CreateContext();
            var sut = new PasswordResetEmailProcessor(
                context,
                TestSecurityOptions.Create(verificationCodeExpiryMinutes: 20, resendCooldownSeconds: 60),
                Substitute.For<IEmailService>(),
                TestVerificationCodeHasher.Create(),
                new CapturingLogger<PasswordResetEmailProcessor>());
            var beforeHandling = DateTime.UtcNow;

            // Act
            await sut.ProcessDueAsync(passStart, CancellationToken.None);

            // Assert
            using var check = database.CreateContext();
            var code = await check.PasswordResetCodes.SingleAsync();
            code.CreatedAt.ShouldBeGreaterThanOrEqualTo(beforeHandling, "the code is created when its row is handled, not at the start of a slow pass");
            code.ExpiresAt.ShouldBeGreaterThanOrEqualTo(beforeHandling.AddMinutes(20), "the user gets the full expiry time from when the code is made, so the emailed code still works");
        }
    }
}
