using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RestaurantManagement.Modules.Identity.Services
{
    // STUB (tester): replaces PasswordResetEmailWorker; the implementer moves the logic here.
    public class PendingEmailWorker : BackgroundService
    {
        public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

        public PendingEmailWorker(IServiceScopeFactory scopeFactory, ILogger<PendingEmailWorker> logger)
        {
            throw new NotImplementedException();
        }

        public PendingEmailWorker(IServiceScopeFactory scopeFactory, ILogger<PendingEmailWorker> logger, TimeSpan interval)
        {
            throw new NotImplementedException();
        }

        public Task RunOnceAsync(CancellationToken stoppingToken) => throw new NotImplementedException();

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => throw new NotImplementedException();
    }
}
