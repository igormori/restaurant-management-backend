using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Modules.Identity.Services.Sessions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The password is already saved when the audit entry is written, so a failing audit write must be
    /// logged and must not turn a completed change or reset into an error.
    /// </summary>
    public class PasswordAuditFailureTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const string OldPassword = "Massa@Fresca2026";
        private const string NewPassword = "Nhoque@Batata2027";

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

        private readonly IPasswordAuditRecorder _auditRecorder = Substitute.For<IPasswordAuditRecorder>();
        private string? _emailedCode;

        public PasswordAuditFailureTests()
        {
            _auditRecorder.RecordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("audit store down"));
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
            return database;
        }

        private static async Task<bool> NewPasswordIsStoredAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            var user = await context.Users.SingleAsync(u => u.Id == MariaId);
            return BCrypt.Net.BCrypt.Verify(NewPassword, user.PasswordHash);
        }

        [Fact]
        [Trait("Rule", "C9")]
        public async Task ChangePasswordAsync_AuditWriteFails_SucceedsAndLogsError()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var logger = Substitute.For<ILogger<PasswordChangeService>>();
            using var context = database.CreateContext();
            var sut = new PasswordChangeService(context, TestLocalizer.Create(), new RefreshTokenService(context, TestJwtOptions.Create()), _auditRecorder, TestPasswordHasher.Create(), logger);

            // Act
            var response = await sut.ChangePasswordAsync(MariaId, new ChangePasswordRequest { CurrentPassword = OldPassword, NewPassword = NewPassword });

            // Assert
            response.Message.ShouldNotBeNullOrEmpty("a failing audit write must not turn a completed password change into an error");
            (await NewPasswordIsStoredAsync(database)).ShouldBeTrue("the new password is saved even though the audit write failed");
            logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log)).ShouldBe(1, "the audit failure is logged exactly once");
            logger.ReceivedCalls().Single().GetArguments()[0].ShouldBe(LogLevel.Error, "the audit failure is logged as an error");
        }

        [Fact]
        [Trait("Rule", "P12")]
        public async Task ResetPasswordAsync_AuditWriteFails_SucceedsAndLogsError()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var logger = Substitute.For<ILogger<PasswordResetService>>();
            using var context = database.CreateContext();
            var sut = new PasswordResetService(
                context,
                TestSecurityOptions.Create(resendCooldownSeconds: 0, maxVerificationAttempts: 5),
                TestLocalizer.Create(),
                logger,
                TestVerificationCodeHasher.Create(),
                new RefreshTokenService(context, TestJwtOptions.Create()),
                _auditRecorder, TestPasswordHasher.Create());
            _emailedCode = "482913";
            context.PasswordResetCodes.Add(new PasswordResetCode
            {
                UserId = MariaId,
                CodeHash = TestVerificationCodeHasher.Create().ComputeHash(MariaId, _emailedCode),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });
            await context.SaveChangesAsync();
            logger.ClearReceivedCalls();

            // Act
            var response = await sut.ResetPasswordAsync(new ResetPasswordRequest { Email = MariaEmail, Code = _emailedCode!, NewPassword = NewPassword });

            // Assert
            response.Message.ShouldNotBeNullOrEmpty("a failing audit write must not turn a completed password reset into an error");
            (await NewPasswordIsStoredAsync(database)).ShouldBeTrue("the new password is saved even though the audit write failed");
            var logCall = logger.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(ILogger.Log));
            logCall.GetArguments()[0].ShouldBe(LogLevel.Error, "the audit failure is logged as an error");
        }
    }
}
