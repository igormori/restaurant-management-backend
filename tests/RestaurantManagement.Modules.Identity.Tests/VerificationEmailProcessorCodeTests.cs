using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Models.Registration;
using RestaurantManagement.Modules.Identity.Services.Registration;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// What a queued resend does to the codes Maria can use to verify: the code in the new email
    /// verifies her account and any code issued before it stops working.
    /// </summary>
    public class VerificationEmailProcessorCodeTests
    {
        private const string MariaEmail = "maria.souza@cantina.com.br";

        private static readonly IVerificationCodeHasher Hasher = TestVerificationCodeHasher.Create();

        private static async Task<IdentityTestDatabase> CreateDatabaseWithEarlierCodeAsync()
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var maria = new User { Email = MariaEmail, PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = false };
            context.Users.Add(maria);
            context.UserVerificationCodes.Add(new UserVerificationCode
            {
                UserId = maria.Id,
                CodeHash = Hasher.ComputeHash(maria.Id, "111111"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });
            context.Set<PendingEmail>().Add(new PendingEmail
            {
                Purpose = PendingEmailPurpose.Verification,
                Email = MariaEmail,
                CreatedAt = DateTime.UtcNow.AddSeconds(-5),
                NextAttemptAt = DateTime.UtcNow.AddSeconds(-1)
            });
            await context.SaveChangesAsync();
            return database;
        }

        private static async Task<string> RunPassAsync(IdentityTestDatabase database)
        {
            string? emailedCode = null;
            var emailService = Substitute.For<IEmailService>();
            emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(code => emailedCode = code), Arg.Any<int>()).Returns(Task.CompletedTask);
            using var context = database.CreateContext();
            var processor = new VerificationEmailProcessor(context, TestSecurityOptions.Create(), emailService, Hasher, new CapturingLogger<VerificationEmailProcessor>());
            await processor.ProcessDueAsync(DateTime.UtcNow, CancellationToken.None);
            return emailedCode ?? throw new InvalidOperationException("no verification email was sent");
        }

        [Fact]
        [Trait("Rule", "V7")]
        public async Task VerifyEmailAsync_CodeIssuedBeforeAQueuedResend_IsRefusedWithA401()
        {
            // Arrange
            using var database = await CreateDatabaseWithEarlierCodeAsync();
            await RunPassAsync(database);
            using var context = database.CreateContext();
            var sut = TestVerificationService.Create(context);

            // Act
            var act = () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = MariaEmail, Code = "111111" });

            // Assert
            var thrown = await Should.ThrowAsync<BusinessException>(act);
            thrown.StatusCode.ShouldBe(401, "a code issued before a resend is refused");
        }

        [Fact]
        [Trait("Rule", "V7")]
        public async Task VerifyEmailAsync_CodeFromAQueuedResend_MarksUserVerified()
        {
            // Arrange
            using var database = await CreateDatabaseWithEarlierCodeAsync();
            var emailedCode = await RunPassAsync(database);
            using var context = database.CreateContext();
            var sut = TestVerificationService.Create(context);

            // Act
            await sut.VerifyEmailAsync(new VerifyEmailRequest { Email = MariaEmail, Code = emailedCode });

            // Assert
            using var verifyContext = database.CreateContext();
            var maria = await verifyContext.Users.SingleAsync(u => u.Email == MariaEmail);
            maria.IsVerified.ShouldBeTrue("the newest code sent by a resend verifies the account");
        }
    }
}
