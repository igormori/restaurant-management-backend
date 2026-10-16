using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Old refresh tokens are deleted once they have been expired for the retention period.
    /// Revoked tokens that have not expired are kept: reuse detection still needs them.
    /// </summary>
    public class RefreshTokenCleanupServiceTests
    {
        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

        private static async Task<IdentityTestDatabase> CreateDatabaseAsync(params RefreshToken[] tokens)
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.Add(new User { Id = MariaId, Email = "maria@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true });
            context.RefreshTokens.AddRange(tokens);
            await context.SaveChangesAsync();
            return database;
        }

        private static RefreshToken Token(DateTime expiresAt, DateTime? revokedAt = null) => new()
        {
            Id = Guid.NewGuid(),
            UserId = MariaId,
            FamilyId = Guid.NewGuid(),
            TokenHash = Guid.NewGuid().ToString("N"),
            CreatedAt = expiresAt.AddDays(-30),
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            RevocationReason = revokedAt == null ? null : RefreshToken.ReasonLogout
        };

        private static async Task<int> PurgeAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await new RefreshTokenCleanupService(context).PurgeExpiredAsync(Now, CancellationToken.None);
        }

        private static async Task<List<Guid>> RemainingIdsAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.RefreshTokens.Select(t => t.Id).ToListAsync();
        }

        [Fact]
        public async Task PurgeExpiredAsync_TokenExpiredLongerThanRetention_DeletesIt()
        {
            var old = Token(Now.AddDays(-RefreshTokenCleanupService.RetentionDays - 1));
            using var database = await CreateDatabaseAsync(old);

            var deleted = await PurgeAsync(database);

            deleted.ShouldBe(1);
            (await RemainingIdsAsync(database)).ShouldBeEmpty();
        }

        [Fact]
        public async Task PurgeExpiredAsync_TokenExpiredWithinRetention_KeepsIt()
        {
            var recent = Token(Now.AddDays(-RefreshTokenCleanupService.RetentionDays + 1));
            using var database = await CreateDatabaseAsync(recent);

            var deleted = await PurgeAsync(database);

            deleted.ShouldBe(0);
            (await RemainingIdsAsync(database)).ShouldBe(new[] { recent.Id });
        }

        [Fact]
        public async Task PurgeExpiredAsync_ActiveToken_KeepsIt()
        {
            var active = Token(Now.AddDays(10));
            using var database = await CreateDatabaseAsync(active);

            await PurgeAsync(database);

            (await RemainingIdsAsync(database)).ShouldBe(new[] { active.Id });
        }

        [Fact]
        public async Task PurgeExpiredAsync_RevokedLongAgoButNotExpired_KeepsItForReuseDetection()
        {
            var revoked = Token(Now.AddDays(1), revokedAt: Now.AddDays(-RefreshTokenCleanupService.RetentionDays - 1));
            using var database = await CreateDatabaseAsync(revoked);

            await PurgeAsync(database);

            (await RemainingIdsAsync(database)).ShouldBe(new[] { revoked.Id });
        }

        [Fact]
        public async Task PurgeExpiredAsync_MixedTokens_DeletesOnlyTheOldOnes()
        {
            var old = Token(Now.AddDays(-RefreshTokenCleanupService.RetentionDays - 5));
            var oldRevoked = Token(Now.AddDays(-RefreshTokenCleanupService.RetentionDays - 2), revokedAt: Now.AddDays(-50));
            var active = Token(Now.AddDays(20));
            using var database = await CreateDatabaseAsync(old, oldRevoked, active);

            var deleted = await PurgeAsync(database);

            deleted.ShouldBe(2);
            (await RemainingIdsAsync(database)).ShouldBe(new[] { active.Id });
        }
    }
}
