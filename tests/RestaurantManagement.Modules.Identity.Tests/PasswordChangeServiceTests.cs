using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// A signed-in user replacing a known password (plan T8). The user comes only from the token, so the
    /// service takes the user ID separately from the request. A rejected change changes nothing; a
    /// successful one replaces the password, ends every session of the user and is audited.
    /// Maria is active and verified and signed in on two devices; Pedro is inactive; Lucia is unverified.
    /// </summary>
    public class PasswordChangeServiceTests
    {
        private const string OldPassword = "Massa@Fresca2026";
        private const string NewPassword = "Nhoque@Batata2027";

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid PedroId = Guid.Parse("33333333-cccc-4ccc-8ccc-00000000000c");
        private static readonly Guid LuciaId = Guid.Parse("55555555-eeee-4eee-8eee-00000000000e");

        private readonly IPasswordAuditRecorder _auditRecorder = Substitute.For<IPasswordAuditRecorder>();

        private static async Task<IdentityTestDatabase> CreateDatabaseAsync()
        {
            var database = new IdentityTestDatabase();
            var hash = BCrypt.Net.BCrypt.HashPassword(OldPassword);
            using (var context = database.CreateContext())
            {
                context.Users.AddRange(
                    new User { Id = MariaId, Email = "maria@cantinadanonna.com.br", PasswordHash = hash, FirstName = "Maria", LastName = "Souza", IsVerified = true },
                    new User { Id = PedroId, Email = "pedro@cantinadanonna.com.br", PasswordHash = hash, FirstName = "Pedro", LastName = "Alves", IsVerified = true, IsActive = false },
                    new User { Id = LuciaId, Email = "lucia@cantinadanonna.com.br", PasswordHash = hash, FirstName = "Lucia", LastName = "Ramos", IsVerified = false });
                await context.SaveChangesAsync();
            }

            using (var context = database.CreateContext())
            {
                var tokens = new RefreshTokenService(context, TestJwtOptions.Create());
                await tokens.IssueAsync(MariaId, null, TestClients.KitchenTablet.DeviceDescription, TestClients.KitchenTablet.IpAddress);
                await tokens.IssueAsync(MariaId, null, TestClients.ManagerLaptop.DeviceDescription, TestClients.ManagerLaptop.IpAddress);
            }

            return database;
        }

        private async Task<MessageResponse> ChangeAsync(IdentityTestDatabase database, Guid userId, string currentPassword, string newPassword)
        {
            using var context = database.CreateContext();
            var sut = new PasswordChangeService(context, TestLocalizer.Create(), new RefreshTokenService(context, TestJwtOptions.Create()), _auditRecorder, TestPasswordHasher.Create());
            return await sut.ChangePasswordAsync(userId, new ChangePasswordRequest { CurrentPassword = currentPassword, NewPassword = newPassword });
        }

        private static async Task<User> ReadUserAsync(IdentityTestDatabase database, Guid userId)
        {
            using var context = database.CreateContext();
            return await context.Users.SingleAsync(u => u.Id == userId);
        }

        private static async Task<List<RefreshToken>> ReadMariaTokensAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.RefreshTokens.Where(t => t.UserId == MariaId).ToListAsync();
        }

        // Scenario 28
        [Theory]
        [InlineData("99999999-9999-4999-8999-999999999999")]   // no such user
        [InlineData("33333333-cccc-4ccc-8ccc-00000000000c")]   // Pedro, inactive
        [InlineData("55555555-eeee-4eee-8eee-00000000000e")]   // Lucia, email not verified
        [Trait("Rule", "C8")]
        public async Task ChangePasswordAsync_UnknownInactiveOrUnverifiedUser_RejectedAsInvalidSession(string userId)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ChangeAsync(database, Guid.Parse(userId), OldPassword, NewPassword));

            // Assert
            rejection.StatusCode.ShouldBe(401, "a missing, deactivated or unverified user cannot change a password and gets the invalid-session 401");
            await _auditRecorder.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default);
        }

        // Scenario 29
        [Fact]
        [Trait("Rule", "C2")]
        public async Task ChangePasswordAsync_WrongCurrentPassword_RejectedAndNothingChanges()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ChangeAsync(database, MariaId, "Pizza@Errada2025", NewPassword));

            // Assert
            rejection.StatusCode.ShouldBe(400, "a wrong current password is refused with a 400");
            BCrypt.Net.BCrypt.Verify(OldPassword, (await ReadUserAsync(database, MariaId)).PasswordHash).ShouldBeTrue("a refused change leaves the old password working");
            (await ReadMariaTokensAsync(database)).ShouldAllBe(t => t.RevokedAt == null, "a refused change does not sign the user out of any device");
            await _auditRecorder.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default);
        }

        // Scenario 30
        [Fact]
        [Trait("Rule", "C4")]
        public async Task ChangePasswordAsync_NewPasswordSameAsCurrent_RejectedAndSessionsKept()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ChangeAsync(database, MariaId, OldPassword, OldPassword));

            // Assert
            rejection.StatusCode.ShouldBe(400, "a new password equal to the current one is refused with a 400");
            (await ReadMariaTokensAsync(database)).ShouldAllBe(t => t.RevokedAt == null, "a refused change keeps every session signed in");
        }

        // Scenario 31
        [Fact]
        [Trait("Rule", "C5")]
        public async Task ChangePasswordAsync_CorrectCurrentPassword_NewPasswordVerifiesAndOldDoesNot()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();

            // Act
            await ChangeAsync(database, MariaId, OldPassword, NewPassword);

            // Assert
            var maria = await ReadUserAsync(database, MariaId);
            BCrypt.Net.BCrypt.Verify(NewPassword, maria.PasswordHash).ShouldBeTrue("after a successful change the new password works");
            BCrypt.Net.BCrypt.Verify(OldPassword, maria.PasswordHash).ShouldBeFalse("after a successful change the old password stops working");
        }

        // Scenario 32
        [Fact]
        [Trait("Rule", "C5")]
        public async Task ChangePasswordAsync_CorrectCurrentPassword_RevokesEveryRefreshTokenAsPasswordChanged()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();

            // Act
            await ChangeAsync(database, MariaId, OldPassword, NewPassword);

            // Assert
            var tokens = await ReadMariaTokensAsync(database);
            tokens.Count.ShouldBe(2, "the user was signed in on two devices");
            tokens.ShouldAllBe(t => t.RevokedAt != null && t.RevocationReason == RefreshToken.ReasonPasswordChanged, "a successful change signs the user out of every device, marked as a password change");
        }

        // Scenario 33
        [Fact]
        [Trait("Rule", "C5")]
        public async Task ChangePasswordAsync_CorrectCurrentPassword_RecordsPasswordChangedAuditOnce()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();

            // Act
            await ChangeAsync(database, MariaId, OldPassword, NewPassword);

            // Assert
            await _auditRecorder.Received(1).RecordAsync(MariaId, AuditActions.PasswordChanged, Arg.Any<CancellationToken>());
            await _auditRecorder.ReceivedWithAnyArgs(1).RecordAsync(default, default!, default);
        }

        [Fact]
        [Trait("Rule", "C3")]
        public async Task ChangePasswordAsync_WrongCurrentPasswordRepeatedly_NeverCountsTowardLoginLock()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            const int maxFailedLoginAttempts = 5;

            // Act
            for (var attempt = 0; attempt < maxFailedLoginAttempts + 1; attempt++)
                await Should.ThrowAsync<BusinessException>(() => ChangeAsync(database, MariaId, "Pizza@Errada2025", NewPassword));

            // Assert
            var maria = await ReadUserAsync(database, MariaId);
            maria.FailedAttempts.ShouldBe(0, "a wrong current password on change-password must not count toward the login lock");
            maria.LockedUntil.ShouldBeNull("repeated wrong current passwords on change-password must never lock the login");
        }
    }
}
