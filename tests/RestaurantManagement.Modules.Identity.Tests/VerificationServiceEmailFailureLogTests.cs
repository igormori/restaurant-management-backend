using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class VerificationServiceEmailFailureLogTests
    {
        [Fact]
        [Trait("Rule", "RS6")]
        public async Task ResendVerificationCodeAsync_EmailSendingFails_LogsTheCaughtException()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var seedContext = database.CreateContext();
            seedContext.Users.Add(new User { Email = "maria@test.com", PasswordHash = "hash", FirstName = "Maria", LastName = "Silva", IsVerified = false });
            await seedContext.SaveChangesAsync();

            using var context = database.CreateContext();
            var smtpFailure = new Exception("SMTP unavailable");
            var emailService = Substitute.For<IEmailService>();
            emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(Task.FromException(smtpFailure));
            var logger = Substitute.For<ILogger<VerificationService>>();
            var sut = new VerificationService(
                context,
                TestSecurityOptions.Create(),
                TestLocalizer.Create(),
                emailService,
                logger,
                TestVerificationCodeHasher.Create());

            // Act
            await Should.ThrowAsync<BusinessException>(
                () => sut.ResendVerificationCodeAsync(new ResendVerificationRequest { Email = "maria@test.com" }));

            // Assert
            var loggedException = logger.ReceivedCalls()
                .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
                .Select(call => call.GetArguments()[3])
                .OfType<Exception>()
                .SingleOrDefault();
            loggedException.ShouldBeSameAs(smtpFailure);
        }
    }
}
