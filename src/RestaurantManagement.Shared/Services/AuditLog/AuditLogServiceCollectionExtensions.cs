using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Shared.Services.AuditLog
{
    public static class AuditLogServiceCollectionExtensions
    {
        // The caller supplies the database setup: only the Web project references the snake-case naming package.
        public static IServiceCollection AddAuditLog(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase)
        {
            services.AddDbContext<AuditLogDbContext>(configureDatabase);
            services.AddScoped<IAuditLog, AuditLogWriter>();
            services.AddScoped<IAuditLogReader, AuditLogReader>();
            return services;
        }
    }
}
