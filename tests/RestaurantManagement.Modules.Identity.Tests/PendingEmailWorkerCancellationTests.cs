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
    /// Only a real shutdown may end the worker. A cancellation that is not a shutdown (a provider or
    /// driver timeout) is a failed pass: logged, and the next tick tries again.
    /// </summary>
    public class PendingEmailWorkerCancellationTests
    {
        private static (PendingEmailWorker Worker, CapturingLogger<PendingEmailWorker> Logger) CreateWorker(Exception processorError)
        {
            var processor = Substitute.For<IPasswordResetEmailProcessor>();
            processor.ProcessDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).ThrowsAsync(processorError);
            var services = new ServiceCollection().AddSingleton(processor).BuildServiceProvider();
            var logger = new CapturingLogger<PendingEmailWorker>();
            return (new PendingEmailWorker(services.GetRequiredService<IServiceScopeFactory>(), logger), logger);
        }

        // Scenario 36
        [Fact]
        [Trait("Rule", "D24")]
        public async Task RunOnceAsync_TimeoutCancellationWhileNotStopping_LogsErrorAndDoesNotThrow()
        {
            // Arrange
            var (sut, logger) = CreateWorker(new TaskCanceledException("The request was canceled due to the configured timeout"));

            // Act
            await sut.RunOnceAsync(CancellationToken.None);

            // Assert
            logger.Levels.ShouldContain(LogLevel.Error, "a timeout is a failed pass, not a shutdown");
        }

        // Scenario 37
        [Fact]
        [Trait("Rule", "D24")]
        public async Task RunOnceAsync_CancellationWhileStopping_Propagates()
        {
            // Arrange
            var (sut, _) = CreateWorker(new OperationCanceledException());
            using var stopping = new CancellationTokenSource();
            await stopping.CancelAsync();

            // Act
            var run = () => sut.RunOnceAsync(stopping.Token);

            // Assert
            await run.ShouldThrowAsync<OperationCanceledException>("a real shutdown must end the worker, not be logged as an error");
        }
    }
}
