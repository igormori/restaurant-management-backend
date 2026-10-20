using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RestaurantManagement.Modules.Identity.Services;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Each worker pass runs the password reset processor and the verification processor. A failure in
    /// one is logged without any code or email address and never stops the other.
    /// </summary>
    public class PendingEmailWorkerBothProcessorsTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const string EmailedCode = "482913";

        private readonly IPasswordResetEmailProcessor _resetProcessor = Substitute.For<IPasswordResetEmailProcessor>();
        private readonly IVerificationEmailProcessor _verificationProcessor = Substitute.For<IVerificationEmailProcessor>();
        private readonly CapturingLogger<PendingEmailWorker> _logger = new();

        private PendingEmailWorker CreateSut()
        {
            var services = new ServiceCollection()
                .AddSingleton(_resetProcessor)
                .AddSingleton(_verificationProcessor)
                .BuildServiceProvider();
            return new PendingEmailWorker(services.GetRequiredService<IServiceScopeFactory>(), _logger);
        }

        [Fact]
        [Trait("Rule", "D26")]
        public async Task RunOnceAsync_ResetProcessorThrows_VerificationProcessorStillRuns()
        {
            // Arrange
            _resetProcessor.ProcessDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException($"SMTP refused {MariaEmail}"));
            var sut = CreateSut();

            // Act
            await sut.RunOnceAsync(CancellationToken.None);

            // Assert
            await _verificationProcessor.ReceivedWithAnyArgs(1).ProcessDueAsync(default, default);
            _logger.Levels.ShouldContain(LogLevel.Error, "the failed reset pass is still logged");
        }

        [Fact]
        [Trait("Rule", "D26")]
        public async Task RunOnceAsync_VerificationProcessorThrows_ResetProcessorStillRunsAndLogsNoSecrets()
        {
            // Arrange
            _verificationProcessor.ProcessDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException($"SMTP refused {MariaEmail} with code {EmailedCode}"));
            var sut = CreateSut();

            // Act
            await sut.RunOnceAsync(CancellationToken.None);

            // Assert
            await _resetProcessor.ReceivedWithAnyArgs(1).ProcessDueAsync(default, default);
            _logger.Levels.ShouldContain(LogLevel.Error, "a failed verification pass is logged as an error");
            _logger.Messages.ShouldNotContain(m => m.Contains(MariaEmail), "the email address must never be written to the logs");
            _logger.Messages.ShouldNotContain(m => m.Contains(EmailedCode), "the verification code must never be written to the logs");
        }
    }
}
