using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RestaurantManagement.Modules.Identity.Services.Sessions
{
    /// <summary>
    /// Runs the refresh token cleanup once at startup and then once a day.
    /// Safe to run on several instances at once: the delete is idempotent.
    /// </summary>
    public class RefreshTokenCleanupWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RefreshTokenCleanupWorker> _logger;

        public RefreshTokenCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<RefreshTokenCleanupWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await PurgeOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task PurgeOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IRefreshTokenCleanupService>();
                var deleted = await cleanup.PurgeExpiredAsync(DateTime.UtcNow, stoppingToken);
                _logger.LogInformation("Refresh token cleanup deleted {Count} expired tokens", deleted);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // A failed run, including a database timeout, must not stop the host; the next tick retries.
                // A cancellation during shutdown is not caught, so it ends the worker quietly.
                _logger.LogError(ex, "Refresh token cleanup failed");
            }
        }
    }
}
