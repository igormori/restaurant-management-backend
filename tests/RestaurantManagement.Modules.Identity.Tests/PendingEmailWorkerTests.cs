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
    /// The worker runs one processor pass per tick. A failing pass is logged without any code or email
    /// address and never stops the worker; the next tick tries again.
    /// </summary>
    public class PendingEmailWorkerTests
    {
        private const string MariaEmail = "maria@cantinadanonna.com.br";
        private const string EmailedCode = "482913";

        [Fact]
        [Trait("Rule", "D21")]
        public async Task RunOnceAsync_ProcessorThrows_LogsErrorWithoutSecretsAndNextRunStillCallsProcessor()
        {
            // Arrange
            var processor = Substitute.For<IPasswordResetEmailProcessor>();
            processor.ProcessDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException($"SMTP refused {MariaEmail} with code {EmailedCode}"));
            var services = new ServiceCollection().AddSingleton(processor).BuildServiceProvider();
            var logger = new CapturingLogger<PendingEmailWorker>();
            var sut = new PendingEmailWorker(services.GetRequiredService<IServiceScopeFactory>(), logger);

            // Act
            await sut.RunOnceAsync(CancellationToken.None);
            await sut.RunOnceAsync(CancellationToken.None);

            // Assert
            logger.Levels.ShouldContain(LogLevel.Error, "a failed pass is logged as an error");
            logger.Messages.ShouldNotContain(m => m.Contains(MariaEmail), "the email address must never be written to the logs");
            logger.Messages.ShouldNotContain(m => m.Contains(EmailedCode), "the reset code must never be written to the logs");
            await processor.ReceivedWithAnyArgs(2).ProcessDueAsync(default, default);
        }
    }
}
