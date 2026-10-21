using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RestaurantManagement.Modules.Identity.Services.Sessions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Only a real shutdown may end the cleanup worker. A cancellation that is not a shutdown (a database
    /// timeout) is a failed run: logged, and the next daily run tries again.
    /// </summary>
    public class RefreshTokenCleanupWorkerCancellationTests
    {
        private static (RefreshTokenCleanupWorker Worker, CapturingLogger<RefreshTokenCleanupWorker> Logger) CreateWorker(IRefreshTokenCleanupService cleanup)
        {
            var services = new ServiceCollection().AddSingleton(cleanup).BuildServiceProvider();
            var logger = new CapturingLogger<RefreshTokenCleanupWorker>();
            return (new RefreshTokenCleanupWorker(services.GetRequiredService<IServiceScopeFactory>(), logger), logger);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }

        // Scenario 1
        [Fact]
        [Trait("Rule", "D28")]
        public async Task ExecuteAsync_TimeoutCancellationWhileNotStopping_LogsErrorAndWorkerKeepsRunning()
        {
            // Arrange
            var cleanup = Substitute.For<IRefreshTokenCleanupService>();
            cleanup.PurgeExpiredAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("The operation was canceled due to the database command timeout"));
            var (sut, logger) = CreateWorker(cleanup);

            // Act
            await sut.StartAsync(CancellationToken.None);
            await WaitUntilAsync(() => logger.Levels.Contains(LogLevel.Error) || sut.ExecuteTask!.IsCompleted);

            // Assert
            logger.Levels.ShouldContain(LogLevel.Error, "a database timeout is a failed run that is logged, not a shutdown");
            sut.ExecuteTask!.IsCompleted.ShouldBeFalse("a timeout must not end the worker or stop the application; the next daily run tries again");

            await sut.StopAsync(CancellationToken.None);
        }

        // Scenario 2
        [Fact]
        [Trait("Rule", "D28")]
        public async Task ExecuteAsync_ShutdownRequestedDuringRun_WorkerEndsWithoutErrorLog()
        {
            // Arrange
            var running = new TaskCompletionSource();
            var cleanup = Substitute.For<IRefreshTokenCleanupService>();
            cleanup.PurgeExpiredAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    running.SetResult();
                    await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                    return 0;
                });
            var (sut, logger) = CreateWorker(cleanup);
            await sut.StartAsync(CancellationToken.None);
            await running.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Act
            await sut.StopAsync(CancellationToken.None);

            // Assert
            sut.ExecuteTask!.IsCompleted.ShouldBeTrue("a shutdown during a run must end the worker");
            logger.Levels.ShouldNotContain(LogLevel.Error, "a shutdown is not a failure and is not logged as an error");
        }
    }
}
