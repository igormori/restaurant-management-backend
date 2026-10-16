using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Asking for a password reset code (plan T6): every email gets the same acknowledgement, only
    /// active and verified users receive a code, and only a protected form of the code is stored.
    /// Maria is active and verified; Pedro is inactive; Lucia has not verified her email.
    /// </summary>
    public class PasswordResetServiceRequestTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const string UnknownEmail = "ninguem@cantinadanonna.com.br";
        private const int ExpiryMinutes = 20;

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly IVerificationCodeHasher Hasher = TestVerificationCodeHasher.Create();

        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly CapturingLogger _logger = new();

        private sealed class CapturingLogger : ILogger<PasswordResetService>
        {
            public List<string> Messages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Messages.Add(formatter(state, exception) + " " + exception?.Message);
        }

        private static async Task<IdentityTestDatabase> CreateDatabaseAsync()
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.AddRange(
                new User { Id = MariaId, Email = MariaEmail, PasswordHash = BCrypt.Net.BCrypt.HashPassword("Massa@Fresca2026"), FirstName = "Maria", LastName = "Souza", IsVerified = true },
                new User { Email = "pedro@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Pedro", LastName = "Alves", IsVerified = true, IsActive = false },
                new User { Email = "lucia@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Lucia", LastName = "Ramos", IsVerified = false });
            await context.SaveChangesAsync();
            return database;
        }

        private async Task<MessageResponse> RequestAsync(IdentityTestDatabase database, string email)
        {
            using var context = database.CreateContext();
            var sut = new PasswordResetService(
                context,
                TestSecurityOptions.Create(verificationCodeExpiryMinutes: ExpiryMinutes, resendCooldownSeconds: 60),
                TestLocalizer.Create(),
                _emailService,
                _logger,
                Hasher,
                new RefreshTokenService(context, TestJwtOptions.Create()),
                Substitute.For<IPasswordAuditRecorder>(),
                TestPasswordHasher.Create());
            return await sut.RequestResetCodeAsync(new ForgotPasswordRequest { Email = email });
        }

        // Scenario 13
        [Fact]
        public async Task RequestResetCodeAsync_UnknownEmail_SameAcknowledgementAsKnownEmailAndNoEmail()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var known = await RequestAsync(database, MariaEmail);
            _emailService.ClearReceivedCalls();

            // Act
            var unknown = await RequestAsync(database, UnknownEmail);

            // Assert
            unknown.Message.ShouldBe(known.Message);
            await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
        }

        // Scenario 14
        [Theory]
        [InlineData("pedro@cantinadanonna.com.br")]    // inactive
        [InlineData("lucia@cantinadanonna.com.br")]    // email not verified
        public async Task RequestResetCodeAsync_InactiveOrUnverifiedUser_NoEmailAndUnknownEmailAcknowledgement(string email)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var unknown = await RequestAsync(database, UnknownEmail);

            // Act
            var response = await RequestAsync(database, email);

            // Assert
            response.Message.ShouldBe(unknown.Message);
            await _emailService.DidNotReceiveWithAnyArgs().SendPasswordResetEmailAsync(default!, default!, default!, default);
            using var context = database.CreateContext();
            (await context.Set<PasswordResetCode>().CountAsync()).ShouldBe(0);
        }

        // Scenario 15
        [Fact]
        public async Task RequestResetCodeAsync_EmailSendFails_SameAcknowledgementAndCodeNotLogged()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var unknown = await RequestAsync(database, UnknownEmail);
            string? sentCode = null;
            _emailService.SendPasswordResetEmailAsync(MariaEmail, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(call =>
                {
                    sentCode = call.ArgAt<string>(2);
                    return Task.FromException(new InvalidOperationException("SMTP server unavailable"));
                });

            // Act
            var response = await RequestAsync(database, MariaEmail);

            // Assert
            response.Message.ShouldBe(unknown.Message);
            sentCode.ShouldNotBeNullOrEmpty();
            _logger.Messages.ShouldNotBeEmpty();
            _logger.Messages.ShouldNotContain(m => m.Contains(sentCode!));
        }

        // Scenario 16
        [Fact]
        public async Task RequestResetCodeAsync_ActiveVerifiedUser_EmailsSixDigitCodeWithNameAndExpiry()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();

            // Act
            await RequestAsync(database, MariaEmail);

            // Assert
            await _emailService.Received(1).SendPasswordResetEmailAsync(
                MariaEmail, "Maria", Arg.Is<string>(code => Regex.IsMatch(code, "^[0-9]{6}$")), ExpiryMinutes);
        }

        // Scenario 17
        [Fact]
        public async Task RequestResetCodeAsync_ActiveVerifiedUser_StoresOnlyProtectedFormOfCode()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            string? sentCode = null;
            _emailService.SendPasswordResetEmailAsync(MariaEmail, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(call =>
                {
                    sentCode = call.ArgAt<string>(2);
                    return Task.CompletedTask;
                });

            // Act
            await RequestAsync(database, MariaEmail);

            // Assert
            using var context = database.CreateContext();
            var stored = await context.Set<PasswordResetCode>().SingleAsync();
            stored.UserId.ShouldBe(MariaId);
            stored.IsUsed.ShouldBeFalse();
            stored.CodeHash.ShouldNotBe(sentCode);
            stored.CodeHash.ShouldBe(Hasher.ComputeHash(MariaId, sentCode!));
        }

        [Fact]
        public async Task RequestResetCodeAsync_SecondRequestWithinResendWait_SameAcknowledgementAndNoNewCode()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var first = await RequestAsync(database, MariaEmail);

            // Act
            var second = await RequestAsync(database, MariaEmail);

            // Assert
            second.Message.ShouldBe(first.Message);
            await _emailService.ReceivedWithAnyArgs(1).SendPasswordResetEmailAsync(default!, default!, default!, default);
            using var context = database.CreateContext();
            (await context.Set<PasswordResetCode>().CountAsync()).ShouldBe(1);
        }
    }
}
