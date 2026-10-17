using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class PasswordResetEmailWorker : BackgroundService
    {
        public PasswordResetEmailWorker(IServiceScopeFactory scopeFactory, ILogger<PasswordResetEmailWorker> logger)
        {
        }

        /// <summary>One processor pass; a failure is logged and never thrown.</summary>
        public Task RunOnceAsync(CancellationToken stoppingToken)
            => throw new NotImplementedException();

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
            => throw new NotImplementedException();
    }
}
