using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Email;
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
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private string? _emailedCode;

        public PasswordAuditFailureTests()
        {
            _auditRecorder.RecordAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("audit store down"));
            _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(call =>
                {
                    _emailedCode = call.ArgAt<string>(2);
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
            return database;
        }

        private static async Task<bool> NewPasswordIsStoredAsync(IdentityTestDatabase database)
        {
            using var context = database.CreateContext();
            var user = await context.Users.SingleAsync(u => u.Id == MariaId);
            return BCrypt.Net.BCrypt.Verify(NewPassword, user.PasswordHash);
        }

        [Fact]
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
            response.Message.ShouldNotBeNullOrEmpty();
            (await NewPasswordIsStoredAsync(database)).ShouldBeTrue();
            logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log)).ShouldBe(1);
            logger.ReceivedCalls().Single().GetArguments()[0].ShouldBe(LogLevel.Error);
        }

        [Fact]
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
                _emailService,
                logger,
                TestVerificationCodeHasher.Create(),
                new RefreshTokenService(context, TestJwtOptions.Create()),
                _auditRecorder, TestPasswordHasher.Create());
            await sut.RequestResetCodeAsync(new ForgotPasswordRequest { Email = MariaEmail });
            logger.ClearReceivedCalls();

            // Act
            var response = await sut.ResetPasswordAsync(new ResetPasswordRequest { Email = MariaEmail, Code = _emailedCode!, NewPassword = NewPassword });

            // Assert
            response.Message.ShouldNotBeNullOrEmpty();
            (await NewPasswordIsStoredAsync(database)).ShouldBeTrue();
            var logCall = logger.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(ILogger.Log));
            logCall.GetArguments()[0].ShouldBe(LogLevel.Error);
        }
    }
}
