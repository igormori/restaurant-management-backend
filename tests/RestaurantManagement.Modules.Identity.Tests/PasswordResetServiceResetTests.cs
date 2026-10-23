using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Lockout;
using RestaurantManagement.Modules.Identity.Entities.Passwords;
using RestaurantManagement.Modules.Identity.Entities.Sessions;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Models.Common;
using RestaurantManagement.Modules.Identity.Models.Passwords;
using RestaurantManagement.Modules.Identity.Models.Sessions;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Modules.Identity.Services.Sessions;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Setting a new password with an emailed code (plan T7). A code is stored the way the reset
    /// processor stores it, and its readable form is kept so the test can type it in. A rejected reset changes nothing; a successful one
    /// replaces the password, ends every session of the user and is audited.
    /// Maria owns the account; she is signed in on the kitchen tablet and the manager's laptop.
    /// </summary>
    public class PasswordResetServiceResetTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const string OldPassword = "Massa@Fresca2026";
        private const string NewPassword = "Nhoque@Batata2027";
        private const string NeverIssuedCode = "000000";

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

        private readonly IPasswordAuditRecorder _auditRecorder = Substitute.For<IPasswordAuditRecorder>();
        private string? _lastEmailedCode;

        private static async Task<IdentityTestDatabase> CreateDatabaseAsync()
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.Add(new User
            {
                Id = MariaId,
                Email = MariaEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(OldPassword),
                FirstName = "Maria",
                LastName = "Souza",
                IsVerified = true
            });
            await context.SaveChangesAsync();
            await IssueTokenAsync(database, TestClients.KitchenTablet);
            await IssueTokenAsync(database, TestClients.ManagerLaptop);
            return database;
        }

        private static async Task IssueTokenAsync(IdentityTestDatabase database, ClientInfo client)
        {
            using var context = database.CreateContext();
            await new RefreshTokenService(context, TestJwtOptions.Create())
                .IssueAsync(MariaId, null, client.DeviceDescription, client.IpAddress);
        }

        private PasswordResetService CreateSut(IdentityDbContext context, int maxAttempts)
            => new(
                context,
                TestSecurityOptions.Create(resendCooldownSeconds: 0, maxVerificationAttempts: maxAttempts),
                TestLocalizer.Create(),
                Substitute.For<ILogger<PasswordResetService>>(),
                TestVerificationCodeHasher.Create(),
                new RefreshTokenService(context, TestJwtOptions.Create()),
                _auditRecorder,
                TestPasswordHasher.Create());

        /// <summary>
        /// Gives Maria a new code the way the reset processor does (earlier unused codes stop working)
        /// and returns the readable code, as if she had read it in the email.
        /// </summary>
        private async Task<string> RequestCodeAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            foreach (var earlier in await context.PasswordResetCodes.Where(c => c.UserId == MariaId && !c.IsUsed).ToListAsync())
                earlier.IsUsed = true;
            _lastEmailedCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
            context.PasswordResetCodes.Add(new PasswordResetCode
            {
                UserId = MariaId,
                CodeHash = TestVerificationCodeHasher.Create().ComputeHash(MariaId, _lastEmailedCode),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });
            await context.SaveChangesAsync();
            return _lastEmailedCode;
        }

        private async Task<MessageResponse> ResetAsync(IdentityTestDatabase database, string email, string code, string newPassword, int maxAttempts = 5)
        {
            using var context = database.CreateContext();
            return await CreateSut(context, maxAttempts)
                .ResetPasswordAsync(new ResetPasswordRequest { Email = email, Code = code, NewPassword = newPassword });
        }

        private static async Task<User> ReadMariaAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.Users.SingleAsync(u => u.Id == MariaId);
        }

        private static async Task<List<RefreshToken>> ReadMariaTokensAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            return await context.RefreshTokens.Where(t => t.UserId == MariaId).ToListAsync();
        }

        // Scenario 18
        [Fact]
        [Trait("Rule", "P7")]
        public async Task ResetPasswordAsync_UnknownEmail_SameRejectionAsWrongCode()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);
            var wrongCode = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, NeverIssuedCode, NewPassword));

            // Act
            var unknownEmail = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, "ninguem@cantinadanonna.com.br", code, NewPassword));

            // Assert
            unknownEmail.StatusCode.ShouldBe(401, "an unknown email gets the 401 reset-code rejection");
            unknownEmail.StatusCode.ShouldBe(wrongCode.StatusCode, "an unknown email gets the same status as a wrong code, so nobody can tell which emails exist");
            unknownEmail.Message.ShouldBe(wrongCode.Message, "an unknown email gets the same message as a wrong code");
        }

        // Scenario 19
        [Fact]
        [Trait("Rule", "P7")]
        public async Task ResetPasswordAsync_WrongCode_RejectedAndNothingChanges()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            await RequestCodeAsync(database);

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, NeverIssuedCode, NewPassword));

            // Assert
            rejection.StatusCode.ShouldBe(401, "a wrong reset code is rejected with a 401");
            BCrypt.Net.BCrypt.Verify(OldPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue("a rejected reset leaves the old password working");
            (await ReadMariaTokensAsync(database)).ShouldAllBe(t => t.RevokedAt == null, "a rejected reset does not sign the user out of any device");
            await _auditRecorder.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default);
        }

        // Scenario 20
        [Fact]
        [Trait("Rule", "P7")]
        public async Task ResetPasswordAsync_ExpiredCode_RejectedAndPasswordUnchanged()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);
            using (var context = database.CreateContext())
            {
                await context.Set<PasswordResetCode>()
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
            }

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, code, NewPassword));

            // Assert
            rejection.StatusCode.ShouldBe(401, "an expired reset code is rejected with a 401");
            BCrypt.Net.BCrypt.Verify(OldPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue("an expired code leaves the old password working");
        }

        // Scenario 21
        [Fact]
        [Trait("Rule", "P7")]
        public async Task ResetPasswordAsync_CodeAlreadyUsed_SecondResetRejected()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, code, "Lasanha@Forno2028"));

            // Assert
            rejection.StatusCode.ShouldBe(401, "a reset code that was already used is rejected");
            BCrypt.Net.BCrypt.Verify(NewPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue("the password set by the first reset is not overwritten by a second use of the code");
        }

        // Scenario 22
        [Fact]
        [Trait("Rule", "P8")]
        public async Task ResetPasswordAsync_TooManyWrongCodes_CorrectCodeRejectedAfterwards()
        {
            // Arrange: the limit is three wrong codes
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);
            await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, "111111", NewPassword, maxAttempts: 3));
            await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, "222222", NewPassword, maxAttempts: 3));
            await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, "333333", NewPassword, maxAttempts: 3));

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, code, NewPassword, maxAttempts: 3));

            // Assert
            rejection.StatusCode.ShouldBe(401, "after too many wrong codes even the correct code is rejected");
            BCrypt.Net.BCrypt.Verify(OldPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue("the password is unchanged while the reset is locked");
        }

        // Scenario 23
        [Fact]
        [Trait("Rule", "P7")]
        public async Task ResetPasswordAsync_FirstCodeAfterSecondRequest_Rejected()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var firstCode = await RequestCodeAsync(database);
            await RequestCodeAsync(database);

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, firstCode, NewPassword));

            // Assert
            rejection.StatusCode.ShouldBe(401, "an older code is rejected once a newer one was requested");
            BCrypt.Net.BCrypt.Verify(OldPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue("the old password still works because the replaced code was refused");
        }

        // Scenario 25
        [Fact]
        [Trait("Rule", "P6")]
        public async Task ResetPasswordAsync_ValidCode_NewPasswordVerifiesAndOldDoesNot()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);

            // Act
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Assert
            var maria = await ReadMariaAsync(database);
            BCrypt.Net.BCrypt.Verify(NewPassword, maria.PasswordHash).ShouldBeTrue("after a correct reset the new password works");
            BCrypt.Net.BCrypt.Verify(OldPassword, maria.PasswordHash).ShouldBeFalse("after a correct reset the old password stops working");
        }

        // Scenario 26
        [Fact]
        [Trait("Rule", "P6")]
        public async Task ResetPasswordAsync_ValidCode_RevokesEveryRefreshTokenAsPasswordChanged()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);

            // Act
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Assert
            var tokens = await ReadMariaTokensAsync(database);
            tokens.Count.ShouldBe(2, "the user was signed in on two devices");
            tokens.ShouldAllBe(t => t.RevokedAt != null && t.RevocationReason == RefreshToken.ReasonPasswordChanged, "a correct reset signs the user out of every device");
        }

        // Scenario 27
        [Fact]
        [Trait("Rule", "P6")]
        public async Task ResetPasswordAsync_ValidCode_RecordsPasswordResetAuditOnce()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);

            // Act
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Assert
            await _auditRecorder.Received(1).RecordAsync(MariaId, AuditActions.PasswordReset, Arg.Any<CancellationToken>());
            await _auditRecorder.ReceivedWithAnyArgs(1).RecordAsync(default, default!, default);
        }

        [Fact]
        [Trait("Rule", "P10")]
        public async Task ResetPasswordAsync_ValidCodeOnLockedAccount_ClearsAccountLockAndStoredLoginFailures()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            using (var context = database.CreateContext())
            {
                var maria = await context.Users.SingleAsync(u => u.Id == MariaId);
                maria.LockedUntil = DateTime.UtcNow.AddMinutes(10);
                context.Set<LoginFailure>().AddRange(
                    new LoginFailure { UserId = MariaId, IpAddress = "203.0.113.10", FailedAt = DateTime.UtcNow.AddMinutes(-5), CountsForAddress = true, CountsForAccount = true, AddressLockedUntil = DateTime.UtcNow.AddMinutes(10) },
                    new LoginFailure { UserId = MariaId, IpAddress = "198.51.100.24", FailedAt = DateTime.UtcNow.AddMinutes(-3), CountsForAddress = true, CountsForAccount = true });
                await context.SaveChangesAsync();
            }
            var code = await RequestCodeAsync(database);

            // Act
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Assert
            var after = await ReadMariaAsync(database);
            using var readContext = database.CreateContext();
            (await readContext.Set<LoginFailure>().CountAsync(f => f.UserId == MariaId)).ShouldBe(0, "a successful reset deletes the stored failures, which lifts every address lock");
            after.LockedUntil.ShouldBeNull("a successful reset lifts the whole-account lock");
        }

        [Fact]
        [Trait("Rule", "P14")]
        public async Task ResetPasswordAsync_LockExpired_NewCodeCanBeRequestedAndUsed()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            using (var context = database.CreateContext())
            {
                var maria = await context.Users.SingleAsync(u => u.Id == MariaId);
                maria.PasswordResetLockedUntil = DateTime.UtcNow.AddMinutes(-1);
                await context.SaveChangesAsync();
            }
            var code = await RequestCodeAsync(database);

            // Act
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Assert
            code.ShouldNotBeNullOrEmpty("once the reset lock has expired a new code is emailed");
            BCrypt.Net.BCrypt.Verify(NewPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue("the new code works once the reset lock has expired");
        }
    }
}
