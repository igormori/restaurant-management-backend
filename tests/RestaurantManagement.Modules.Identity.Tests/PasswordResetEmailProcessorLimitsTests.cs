using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Modules.Identity.Services.Registration;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Limits of one processor pass: it handles at most one batch of due requests, so a flood of
    /// anonymous requests cannot overload it, and it removes failed requests (which hold a raw email
    /// address) once they are older than the retention period.
    /// </summary>
    public class PasswordResetEmailProcessorLimitsTests
    {
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();

        private async Task ProcessAsync(IdentityTestDatabase database, DateTime now)
        {
            using var context = database.CreateContext();
            var sut = new PasswordResetEmailProcessor(
                context,
                TestSecurityOptions.Create(verificationCodeExpiryMinutes: 20, resendCooldownSeconds: 60),
                _emailService,
                TestVerificationCodeHasher.Create(),
                new CapturingLogger<PasswordResetEmailProcessor>());
            await sut.ProcessDueAsync(now, CancellationToken.None);
        }

        private static async Task AddRequestsAsync(IdentityTestDatabase database, params PendingEmail[] requests)
        {
            using var context = database.CreateContext();
            context.Set<PendingEmail>().AddRange(requests);
            await context.SaveChangesAsync();
        }

        private static async Task<List<PendingEmail>> ReadQueueAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.Set<PendingEmail>().ToListAsync();
        }

        // Scenario 26
        [Fact]
        [Trait("Rule", "P22")]
        public async Task ProcessDueAsync_MoreDueRequestsThanBatchSize_HandlesOneBatchAndLeavesTheRest()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var now = DateTime.UtcNow;
            var requests = Enumerable.Range(0, PendingEmailQueue.BatchSize + 1)
                .Select(i => new PendingEmail { Email = $"ninguem{i}@cantinadanonna.com.br", CreatedAt = now.AddSeconds(-10 + i * 0.001), NextAttemptAt = now.AddSeconds(-1) })
                .ToArray();
            await AddRequestsAsync(database, requests);

            // Act
            await ProcessAsync(database, now);

            // Assert
            var left = await ReadQueueAsync(database);
            left.ShouldHaveSingleItem("one pass handles at most one batch; the next pass picks up the rest");
            left.Single().Email.ShouldBe(requests.Last().Email, "the oldest requests are handled first");
        }

        // Scenario 27
        [Fact]
        [Trait("Rule", "P23")]
        public async Task ProcessDueAsync_FailedRequestsOlderAndNewerThanRetention_RemovesOnlyTheOlder()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            var now = DateTime.UtcNow;
            var older = new PendingEmail { Email = "antigo@cantinadanonna.com.br", Attempts = 5, FailedAt = now - PendingEmailQueue.FailedRetention - TimeSpan.FromMinutes(1) };
            var newer = new PendingEmail { Email = "recente@cantinadanonna.com.br", Attempts = 5, FailedAt = now - PendingEmailQueue.FailedRetention + TimeSpan.FromMinutes(1) };
            await AddRequestsAsync(database, older, newer);

            // Act
            await ProcessAsync(database, now);

            // Assert
            var left = await ReadQueueAsync(database);
            left.ShouldHaveSingleItem("a failed request older than 7 days is removed and a newer one is kept");
            left.Single().Id.ShouldBe(newer.Id);
        }
    }
}
