using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// A claim lasts from the moment it is taken, not from the moment the pass started, so a slow
    /// provider cannot make a later claim already expired; and what a pass acts on is read after the
    /// claim, so it never uses a row another instance changed in the meantime.
    /// </summary>
    public class PasswordResetEmailProcessorClaimTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const string JoaoEmail = "joao@cantinadanonna.com.br";

        private readonly IEmailService _emailService = Substitute.For<IEmailService>();

        private static async Task<IdentityTestDatabase> SeedAsync(DateTime passStart)
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.Add(new User { Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true });
            context.Users.Add(new User { Email = JoaoEmail, PasswordHash = "hash", FirstName = "Joao", LastName = "Lima", IsVerified = true });
            context.Set<PendingEmail>().Add(new PendingEmail { Email = MariaEmail, CreatedAt = passStart.AddMinutes(-10), NextAttemptAt = passStart.AddMinutes(-9) });
            context.Set<PendingEmail>().Add(new PendingEmail { Email = JoaoEmail, CreatedAt = passStart.AddMinutes(-8), NextAttemptAt = passStart.AddMinutes(-7) });
            await context.SaveChangesAsync();
            return database;
        }

        private async Task ProcessAsync(IdentityTestDatabase database, DateTime passStart)
        {
            using var context = database.CreateContext();
            var sut = new PasswordResetEmailProcessor(
                context,
                TestSecurityOptions.Create(verificationCodeExpiryMinutes: 20, resendCooldownSeconds: 60),
                _emailService,
                TestVerificationCodeHasher.Create(),
                new CapturingLogger<PasswordResetEmailProcessor>());
            await sut.ProcessDueAsync(passStart, CancellationToken.None);
        }

        private static async Task<List<PendingEmail>> ReadQueueAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.Set<PendingEmail>().OrderBy(r => r.CreatedAt).ToListAsync();
        }

        // Scenario 32
        [Fact]
        [Trait("Rule", "P26")]
        public async Task ProcessDueAsync_PassStartedLongAgo_ClaimLastsFromTheMomentItIsTaken()
        {
            // Arrange
            var passStart = DateTime.UtcNow.AddHours(-1);
            using var database = await SeedAsync(passStart);
            DateTime? claimedUntilDuringSend = null;
            _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(_ =>
                {
                    claimedUntilDuringSend ??= ReadQueueAsync(database).GetAwaiter().GetResult().First().ClaimedUntil;
                    return Task.CompletedTask;
                });

            // Act
            await ProcessAsync(database, passStart);

            // Assert
            claimedUntilDuringSend.ShouldNotBeNull("a request being sent is claimed");
            claimedUntilDuringSend.Value.ShouldBeGreaterThan(DateTime.UtcNow, "a claim counted from the start of a slow pass would already have expired, letting another instance send the same request");
        }

        // Scenario 33
        [Fact]
        [Trait("Rule", "P26")]
        public async Task ProcessDueAsync_RowChangedByAnotherInstanceAfterLoading_ActsOnTheRowAsClaimed()
        {
            // Arrange
            var passStart = DateTime.UtcNow;
            using var database = await SeedAsync(passStart);
            var sends = 0;
            _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(_ =>
                {
                    if (sends++ == 0)
                        MarkSecondRequestCodeIssuedAsync(database, passStart).GetAwaiter().GetResult();
                    return Task.CompletedTask;
                });

            // Act
            await ProcessAsync(database, passStart);

            // Assert
            sends.ShouldBe(1, "a request another instance already stored a code for must be dropped, not sent a second code");
            (await ReadQueueAsync(database)).ShouldBeEmpty("both requests are finished");
        }

        private static async Task MarkSecondRequestCodeIssuedAsync(IdentityTestDatabase database, DateTime at)
        {
            using var context = database.CreateContext();
            await context.Set<PendingEmail>()
                .Where(r => r.Email == JoaoEmail)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CodeIssuedAt, at));
        }
    }
}
