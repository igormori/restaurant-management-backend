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
    /// Asking for a password reset code only queues the request: every email, known or not, does the
    /// same work and gets the same acknowledgement, and the request never waits for an email.
    /// Maria is active and verified; Pedro is inactive; Lucia has not verified her email; Carlos is
    /// locked out of resetting.
    /// </summary>
    public class PasswordResetServiceRequestTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const string UnknownEmail = "ninguem@cantinadanonna.com.br";

        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

        private static async Task<IdentityTestDatabase> CreateDatabaseAsync()
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.AddRange(
                new User { Id = MariaId, Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true },
                new User { Email = "pedro@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Pedro", LastName = "Alves", IsVerified = true, IsActive = false },
                new User { Email = "lucia@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Lucia", LastName = "Ramos", IsVerified = false },
                new User { Email = "carlos@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Carlos", LastName = "Dias", IsVerified = true, PasswordResetLockedUntil = DateTime.UtcNow.AddMinutes(10) });
            await context.SaveChangesAsync();
            return database;
        }

        private async Task<MessageResponse> RequestAsync(IdentityTestDatabase database, string email, params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
        {
            using var context = database.CreateContext(interceptors);
            var sut = new PasswordResetService(
                context,
                TestSecurityOptions.Create(verificationCodeExpiryMinutes: 20, resendCooldownSeconds: 60),
                TestLocalizer.Create(),
                Substitute.For<ILogger<PasswordResetService>>(),
                TestVerificationCodeHasher.Create(),
                new RefreshTokenService(context, TestJwtOptions.Create()),
                Substitute.For<IPasswordAuditRecorder>(),
                TestPasswordHasher.Create());
            return await sut.RequestResetCodeAsync(new ForgotPasswordRequest { Email = email });
        }

        [Theory]
        [InlineData("maria@cantinadanonna.com.br")]     // active and verified
        [InlineData("ninguem@cantinadanonna.com.br")]   // no account
        [InlineData("pedro@cantinadanonna.com.br")]     // inactive
        [InlineData("lucia@cantinadanonna.com.br")]     // email not verified
        [InlineData("carlos@cantinadanonna.com.br")]    // reset locked
        [Trait("Rule", "P1")]
        public async Task RequestResetCodeAsync_AnyEmail_QueuesOneRequestAndCreatesNoCode(string email)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();

            // Act
            var response = await RequestAsync(database, email);

            // Assert
            response.Message.ShouldNotBeNullOrEmpty("every email gets the same acknowledgement");
            using var context = database.CreateContext();
            var queued = (await context.Set<PasswordResetRequest>().ToListAsync()).ShouldHaveSingleItem("every email, known or not, adds exactly one queued request");
            queued.Email.ShouldBe(email, "the request keeps the email as typed; the account is checked later, outside the request");
            queued.Attempts.ShouldBe(0, "a new request has not been tried yet");
            queued.FailedAt.ShouldBeNull("a new request has not failed");
            (await context.PasswordResetCodes.CountAsync()).ShouldBe(0, "the request itself creates no reset code, so it cannot reveal which emails have an account");
        }

        [Fact]
        [Trait("Rule", "P20")]
        public async Task RequestResetCodeAsync_KnownAndUnknownEmail_RunTheSameDatabaseCommands()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var knownCommands = new QueryCommandRecorder();
            var unknownCommands = new QueryCommandRecorder();

            // Act
            await RequestAsync(database, MariaEmail, knownCommands);
            await RequestAsync(database, UnknownEmail, unknownCommands);

            // Assert
            knownCommands.Commands.ShouldNotBeEmpty("the request writes its queue row");
            knownCommands.Commands.ShouldBe(unknownCommands.Commands, "a known and an unknown email must run exactly the same database commands, so response times cannot tell them apart");
        }

        [Fact]
        [Trait("Rule", "P1")]
        public async Task RequestResetCodeAsync_KnownEmail_NeverReadsUsersOrResetCodes()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var commands = new QueryCommandRecorder();

            // Act
            await RequestAsync(database, MariaEmail, commands);

            // Assert
            commands.Commands.ShouldNotContain(c => c.Contains("users", StringComparison.OrdinalIgnoreCase), "the request must not look at the users table, or an existing account would take longer");
            commands.Commands.ShouldNotContain(c => c.Contains("password_reset_codes", StringComparison.OrdinalIgnoreCase), "the request must not look at reset codes");
        }

        [Fact]
        [Trait("Rule", "P5")]
        public void RequestResetCodeAsync_ServiceHasNoEmailService_RequestNeverDependsOnSending()
        {
            // Act
            var dependencies = typeof(PasswordResetService).GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType);

            // Assert
            dependencies.ShouldNotContain(typeof(IEmailService), "the request must never depend on sending an email, so the reset service must not be able to reach the email service");
        }
    }
}
