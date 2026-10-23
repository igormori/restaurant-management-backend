using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RestaurantManagement.Modules.Identity.Services.Lockout;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Modules.Identity.Services.Registration;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Each worker pass also runs the account-locked processor. A failure in any one processor is logged
    /// without the email address and never stops the others.
    /// </summary>
    public class PendingEmailWorkerAccountLockedTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";

        private readonly IPasswordResetEmailProcessor _resetProcessor = Substitute.For<IPasswordResetEmailProcessor>();
        private readonly IVerificationEmailProcessor _verificationProcessor = Substitute.For<IVerificationEmailProcessor>();
        private readonly IAccountLockedEmailProcessor _accountLockedProcessor = Substitute.For<IAccountLockedEmailProcessor>();
        private readonly CapturingLogger<PendingEmailWorker> _logger = new();

        private PendingEmailWorker CreateSut()
        {
            var services = new ServiceCollection()
                .AddSingleton(_resetProcessor)
                .AddSingleton(_verificationProcessor)
                .AddSingleton(_accountLockedProcessor)
                .BuildServiceProvider();
            return new PendingEmailWorker(services.GetRequiredService<IServiceScopeFactory>(), _logger);
        }

        [Fact]
        [Trait("Rule", "D31")]
        public async Task RunOnceAsync_ResetAndVerificationProcessorsThrow_AccountLockedProcessorStillRuns()
        {
            // Arrange
            _resetProcessor.ProcessDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("reset down"));
            _verificationProcessor.ProcessDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("verification down"));
            var sut = CreateSut();

            // Act
            await sut.RunOnceAsync(CancellationToken.None);

            // Assert
            await _accountLockedProcessor.ReceivedWithAnyArgs(1).ProcessDueAsync(default, default);
        }

        [Fact]
        [Trait("Rule", "D31")]
        public async Task RunOnceAsync_AccountLockedProcessorThrows_LogsWithoutEmailAndOtherProcessorsStillRun()
        {
            // Arrange
            _accountLockedProcessor.ProcessDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException($"SMTP refused {MariaEmail}"));
            var sut = CreateSut();

            // Act
            await sut.RunOnceAsync(CancellationToken.None);

            // Assert
            _logger.Levels.ShouldContain(LogLevel.Error, "a failed account-locked pass is logged as an error");
            _logger.Messages.ShouldNotContain(m => m.Contains(MariaEmail), "the email address must never be written to the logs");
            await _resetProcessor.ReceivedWithAnyArgs(1).ProcessDueAsync(default, default);
            await _verificationProcessor.ReceivedWithAnyArgs(1).ProcessDueAsync(default, default);
        }
    }
}
