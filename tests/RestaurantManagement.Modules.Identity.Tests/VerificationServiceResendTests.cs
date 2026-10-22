using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Models.Common;
using RestaurantManagement.Modules.Identity.Models.Registration;
using RestaurantManagement.Modules.Identity.Services.Registration;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Asking to resend a verification code only queues the request: every email, whatever its account
    /// state, does the same work and gets the same acknowledgement, and the request never waits for an
    /// email. Maria is unverified and eligible; Pedro is verified; Lucia is locked out of verifying;
    /// Carlos asked for a code seconds ago.
    /// </summary>
    public class VerificationServiceResendTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const string UnknownEmail = "ninguem@cantinadanonna.com.br";

        private static async Task<IdentityTestDatabase> CreateDatabaseAsync()
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var carlos = new User { Email = "carlos@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Carlos", LastName = "Dias", IsVerified = false };
            context.Users.AddRange(
                new User { Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = false },
                new User { Email = "pedro@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Pedro", LastName = "Alves", IsVerified = true },
                new User { Email = "lucia@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Lucia", LastName = "Ramos", IsVerified = false, VerificationLockedUntil = DateTime.UtcNow.AddMinutes(10) },
                carlos);
            context.UserVerificationCodes.Add(new UserVerificationCode { UserId = carlos.Id, CodeHash = "earlier", ExpiresAt = DateTime.UtcNow.AddMinutes(10), CreatedAt = DateTime.UtcNow.AddSeconds(-20) });
            await context.SaveChangesAsync();
            return database;
        }

        private static async Task<MessageResponse> ResendAsync(IdentityTestDatabase database, string email, params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
        {
            using var context = database.CreateContext(interceptors);
            return await TestVerificationService.Create(context).ResendVerificationCodeAsync(new ResendVerificationRequest { Email = email });
        }

        [Theory]
        [InlineData("maria@cantinadanonna.com.br")]     // unverified, eligible
        [InlineData("ninguem@cantinadanonna.com.br")]   // no account
        [InlineData("pedro@cantinadanonna.com.br")]     // already verified
        [InlineData("lucia@cantinadanonna.com.br")]     // verification locked
        [InlineData("carlos@cantinadanonna.com.br")]    // within the 60 s wait
        [Trait("Rule", "RS2")]
        public async Task ResendVerificationCodeAsync_AnyEmail_QueuesOneVerificationRequestAndChangesNothingElse(string email)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            using var before = database.CreateContext();
            var codesBefore = await before.UserVerificationCodes.CountAsync();
            var usersBefore = await before.Users.OrderBy(u => u.Email)
                .Select(u => new { u.Email, u.IsVerified, u.VerificationFailedAttempts, u.VerificationLockedUntil }).ToListAsync();

            // Act
            await ResendAsync(database, email);

            // Assert
            using var context = database.CreateContext();
            var queued = (await context.Set<PendingEmail>().ToListAsync()).ShouldHaveSingleItem("every email, known or not, adds exactly one queued request");
            queued.Purpose.ShouldBe(PendingEmailPurpose.Verification, "the request is queued for the verification email processor");
            queued.Email.ShouldBe(email, "the request keeps the email as typed; the account is checked later, outside the request");
            (await context.UserVerificationCodes.CountAsync()).ShouldBe(codesBefore, "the request itself creates no code, so it cannot reveal which emails have an account");
            var usersAfter = await context.Users.OrderBy(u => u.Email)
                .Select(u => new { u.Email, u.IsVerified, u.VerificationFailedAttempts, u.VerificationLockedUntil }).ToListAsync();
            usersAfter.ShouldBe(usersBefore, "the request changes no account: not its verified state, failed-attempt counter or lock");
        }

        [Fact]
        [Trait("Rule", "RS3")]
        public async Task ResendVerificationCodeAsync_EligibleAndUnknownEmail_RunTheSameDatabaseCommands()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var eligibleCommands = new QueryCommandRecorder();
            var unknownCommands = new QueryCommandRecorder();

            // Act
            await ResendAsync(database, MariaEmail, eligibleCommands);
            await ResendAsync(database, UnknownEmail, unknownCommands);

            // Assert
            eligibleCommands.Commands.ShouldNotBeEmpty("the request runs at least the insert of the queued request");
            eligibleCommands.Commands.ShouldBe(unknownCommands.Commands, "an eligible and an unknown email must run exactly the same database commands, so response times cannot tell them apart");
        }

        [Fact]
        [Trait("Rule", "RS2")]
        public void VerificationService_Constructor_TakesNoEmailService()
        {
            // Arrange
            var constructor = typeof(VerificationService).GetConstructors().Single();

            // Act
            var parameterTypes = constructor.GetParameters().Select(p => p.ParameterType).ToList();

            // Assert
            parameterTypes.ShouldNotContain(typeof(IEmailService), "resend never depends on sending: the service can neither call the email service nor wait for it");
            parameterTypes.ShouldNotContain(t => typeof(Delegate).IsAssignableFrom(t), "no sender hidden behind a delegate either");
        }
    }
}
