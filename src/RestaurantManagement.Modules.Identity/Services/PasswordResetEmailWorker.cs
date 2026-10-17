using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RestaurantManagement.Modules.Identity.Services
{
    /// <summary>
    /// Runs one pass of the password reset email processor every few seconds.
    /// Safe to run on several instances at once: the processor claims rows conditionally.
    /// </summary>
    public class PasswordResetEmailWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PasswordResetEmailWorker> _logger;

        public PasswordResetEmailWorker(IServiceScopeFactory scopeFactory, ILogger<PasswordResetEmailWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>One processor pass; a failure is logged and never thrown.</summary>
        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IPasswordResetEmailProcessor>();
                await processor.ProcessDueAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Only the type is logged: the exception message can hold the email address or code.
                _logger.LogError("Password reset email pass failed with {ExceptionType}", ex.GetType().Name);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
