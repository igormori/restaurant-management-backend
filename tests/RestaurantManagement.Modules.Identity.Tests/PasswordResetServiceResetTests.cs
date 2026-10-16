using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Setting a new password with an emailed code (plan T7). Codes are obtained the way a user gets
    /// them: by asking for one and reading the email. A rejected reset changes nothing; a successful one
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

        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly IPasswordAuditRecorder _auditRecorder = Substitute.For<IPasswordAuditRecorder>();
        private string? _lastEmailedCode;

        public PasswordResetServiceResetTests()
        {
            _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(call =>
                {
                    _lastEmailedCode = call.ArgAt<string>(2);
                    return Task.CompletedTask;
                });
        }

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
                _emailService,
                Substitute.For<ILogger<PasswordResetService>>(),
                TestVerificationCodeHasher.Create(),
                new RefreshTokenService(context, TestJwtOptions.Create()),
                _auditRecorder,
                TestPasswordHasher.Create());

        /// <summary>Asks for a code for Maria and returns the one that was emailed.</summary>
        private async Task<string> RequestCodeAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            await CreateSut(context, maxAttempts: 5).RequestResetCodeAsync(new ForgotPasswordRequest { Email = MariaEmail });
            return _lastEmailedCode!;
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
        public async Task ResetPasswordAsync_UnknownEmail_SameRejectionAsWrongCode()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);
            var wrongCode = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, NeverIssuedCode, NewPassword));

            // Act
            var unknownEmail = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, "ninguem@cantinadanonna.com.br", code, NewPassword));

            // Assert
            unknownEmail.StatusCode.ShouldBe(401);
            unknownEmail.StatusCode.ShouldBe(wrongCode.StatusCode);
            unknownEmail.Message.ShouldBe(wrongCode.Message);
        }

        // Scenario 19
        [Fact]
        public async Task ResetPasswordAsync_WrongCode_RejectedAndNothingChanges()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            await RequestCodeAsync(database);

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, NeverIssuedCode, NewPassword));

            // Assert
            rejection.StatusCode.ShouldBe(401);
            BCrypt.Net.BCrypt.Verify(OldPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue();
            (await ReadMariaTokensAsync(database)).ShouldAllBe(t => t.RevokedAt == null);
            await _auditRecorder.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default);
        }

        // Scenario 20
        [Fact]
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
            rejection.StatusCode.ShouldBe(401);
            BCrypt.Net.BCrypt.Verify(OldPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue();
        }

        // Scenario 21
        [Fact]
        public async Task ResetPasswordAsync_CodeAlreadyUsed_SecondResetRejected()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, code, "Lasanha@Forno2028"));

            // Assert
            rejection.StatusCode.ShouldBe(401);
            BCrypt.Net.BCrypt.Verify(NewPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue();
        }

        // Scenario 22
        [Fact]
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
            rejection.StatusCode.ShouldBe(401);
            BCrypt.Net.BCrypt.Verify(OldPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue();
        }

        // Scenario 23
        [Fact]
        public async Task ResetPasswordAsync_FirstCodeAfterSecondRequest_Rejected()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var firstCode = await RequestCodeAsync(database);
            await RequestCodeAsync(database);

            // Act
            var rejection = await Should.ThrowAsync<BusinessException>(() => ResetAsync(database, MariaEmail, firstCode, NewPassword));

            // Assert
            rejection.StatusCode.ShouldBe(401);
            BCrypt.Net.BCrypt.Verify(OldPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue();
        }

        // Scenario 25
        [Fact]
        public async Task ResetPasswordAsync_ValidCode_NewPasswordVerifiesAndOldDoesNot()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);

            // Act
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Assert
            var maria = await ReadMariaAsync(database);
            BCrypt.Net.BCrypt.Verify(NewPassword, maria.PasswordHash).ShouldBeTrue();
            BCrypt.Net.BCrypt.Verify(OldPassword, maria.PasswordHash).ShouldBeFalse();
        }

        // Scenario 26
        [Fact]
        public async Task ResetPasswordAsync_ValidCode_RevokesEveryRefreshTokenAsPasswordChanged()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var code = await RequestCodeAsync(database);

            // Act
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Assert
            var tokens = await ReadMariaTokensAsync(database);
            tokens.Count.ShouldBe(2);
            tokens.ShouldAllBe(t => t.RevokedAt != null && t.RevocationReason == RefreshToken.ReasonPasswordChanged);
        }

        // Scenario 27
        [Fact]
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
        public async Task ResetPasswordAsync_ValidCodeOnLockedAccount_ClearsLoginLockAndFailedAttempts()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            using (var context = database.CreateContext())
            {
                var maria = await context.Users.SingleAsync(u => u.Id == MariaId);
                maria.FailedAttempts = 3;
                maria.LockedUntil = DateTime.UtcNow.AddMinutes(10);
                await context.SaveChangesAsync();
            }
            var code = await RequestCodeAsync(database);

            // Act
            await ResetAsync(database, MariaEmail, code, NewPassword);

            // Assert
            var after = await ReadMariaAsync(database);
            after.FailedAttempts.ShouldBe(0);
            after.LockedUntil.ShouldBeNull();
        }

        [Fact]
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
            code.ShouldNotBeNullOrEmpty();
            BCrypt.Net.BCrypt.Verify(NewPassword, (await ReadMariaAsync(database)).PasswordHash).ShouldBeTrue();
        }
    }
}
