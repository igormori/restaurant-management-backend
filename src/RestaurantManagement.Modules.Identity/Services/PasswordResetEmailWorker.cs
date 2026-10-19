using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Safe on several instances at once: the processor claims each row with a conditional update.
    public class PasswordResetEmailWorker : BackgroundService
    {
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

        private readonly TimeSpan _interval;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PasswordResetEmailWorker> _logger;

        public PasswordResetEmailWorker(IServiceScopeFactory scopeFactory, ILogger<PasswordResetEmailWorker> logger)
            : this(scopeFactory, logger, DefaultInterval)
        {
        }

        public PasswordResetEmailWorker(IServiceScopeFactory scopeFactory, ILogger<PasswordResetEmailWorker> logger, TimeSpan interval)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = interval;
        }

        // A failed pass is logged, never thrown, so one bad pass cannot stop the worker.
        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IPasswordResetEmailProcessor>();
                await processor.ProcessDueAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Only the type is logged: the exception message can hold the email address or code.
                _logger.LogError("Password reset email pass failed with {ExceptionType}", ex.GetType().Name);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
