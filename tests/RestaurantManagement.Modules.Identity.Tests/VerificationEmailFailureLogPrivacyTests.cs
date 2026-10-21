using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using RestaurantManagement.Modules.Identity.Models.Registration;
using RestaurantManagement.Modules.Identity.Services.Registration;
using RestaurantManagement.Shared.Services.Email;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    public class VerificationEmailFailureLogPrivacyTests
    {
        private const string Email = "maria.silva@test.com";

        [Fact]
        [Trait("Rule", "R12")]
        public async Task RegisterAsync_EmailSendingFails_LogsTheUserIdAndNotTheEmailAddress()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            var logger = new CapturingLogger<RegistrationService>();
            var sut = new RegistrationService(context, TestSecurityOptions.Create(), TestLocalizer.Create(), FailingEmailService(), logger, TestVerificationCodeHasher.Create(), TestPasswordHasher.Create());

            // Act
            await sut.RegisterAsync(new RegisterRequest { Email = Email, Password = "Password1!", FirstName = "Maria", LastName = "Silva" });

            // Assert
            var userId = context.Users.Single().Id;
            logger.Entries.ShouldNotBeEmpty("a failed verification email is logged");
            logger.Entries.ShouldAllBe(entry => !entry.Contains(Email), "the email address never reaches the logs");
            logger.Entries.ShouldContain(entry => entry.Contains(userId.ToString()), "the log names the user by ID");
        }

        private static IEmailService FailingEmailService()
        {
            var emailService = Substitute.For<IEmailService>();
            emailService.SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
                .Returns(Task.FromException(new Exception("SMTP unavailable")));
            return emailService;
        }

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<string> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add(formatter(state, exception));
        }
    }
}
