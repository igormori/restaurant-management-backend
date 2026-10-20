using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Safe on several instances at once: the processor claims each row with a conditional update.
    public class PendingEmailWorker : BackgroundService
    {
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

        private readonly TimeSpan _interval;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PendingEmailWorker> _logger;

        public PendingEmailWorker(IServiceScopeFactory scopeFactory, ILogger<PendingEmailWorker> logger)
            : this(scopeFactory, logger, DefaultInterval)
        {
        }

        public PendingEmailWorker(IServiceScopeFactory scopeFactory, ILogger<PendingEmailWorker> logger, TimeSpan interval)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = interval;
        }

        // A failed pass is logged, never thrown, so one bad pass cannot stop the worker. Each processor
        // runs on its own so a failure in one does not stop the other.
        public async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            await RunPassAsync<IPasswordResetEmailProcessor>(
                (processor, ct) => processor.ProcessDueAsync(DateTime.UtcNow, ct), "Password reset", stoppingToken);
            await RunPassAsync<IVerificationEmailProcessor>(
                (processor, ct) => processor.ProcessDueAsync(DateTime.UtcNow, ct), "Verification", stoppingToken);
        }

        private async Task RunPassAsync<TProcessor>(
            Func<TProcessor, CancellationToken, Task<int>> run, string name, CancellationToken stoppingToken)
            where TProcessor : notnull
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await run(scope.ServiceProvider.GetRequiredService<TProcessor>(), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Only the type is logged: the exception message can hold the email address or code.
                _logger.LogError("{Pass} email pass failed with {ExceptionType}", name, ex.GetType().Name);
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
