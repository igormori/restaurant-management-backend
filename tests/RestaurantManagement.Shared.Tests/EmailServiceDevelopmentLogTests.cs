using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;
using Shouldly;

namespace RestaurantManagement.Shared.Tests
{
    /// <summary>
    /// In development without an SMTP (Simple Mail Transfer Protocol) host, emails are logged instead
    /// of sent. The log must never hold the body, which carries codes (plan T1, spec rule 10).
    /// </summary>
    public class EmailServiceDevelopmentLogTests
    {
        private sealed class CapturingLogger : ILogger<EmailService>
        {
            public List<string> Messages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Messages.Add(formatter(state, exception));
        }

        // Scenario 3
        [Fact]
        [Trait("Rule", "SH27")]
        public async Task SendEmailAsync_DevelopmentWithoutSmtpHost_LogsRecipientAndSubjectButNotBody()
        {
            // Arrange
            var logger = new CapturingLogger();
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(Environments.Development);
            var options = Microsoft.Extensions.Options.Options.Create(new EmailOptions { SmtpHost = string.Empty, FromEmail = "no-reply@cantinadanonna.com.br" });
            var sut = new EmailService(options, logger, environment);

            // Act
            await sut.SendEmailAsync("maria@cantinadanonna.com.br", "Reset your password", "<p>Your code is 482913</p>");

            // Assert
            logger.Messages.ShouldContain(m => m.Contains("maria@cantinadanonna.com.br"), customMessage: "the log records who the email was sent to");
            logger.Messages.ShouldContain(m => m.Contains("Reset your password"), customMessage: "the log records the email subject");
            logger.Messages.ShouldNotContain(m => m.Contains("482913"), customMessage: "the email body holds the reset code, so it must never be written to the log");
        }
    }
}
