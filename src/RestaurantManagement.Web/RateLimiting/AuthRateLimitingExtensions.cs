using Microsoft.Extensions.Options;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Web.RateLimiting
{
    public static class AuthRateLimitingExtensions
    {
        private const string ConfigurationSection = "AuthRateLimit";

        public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<AuthRateLimitOptions>()
                .Bind(configuration.GetSection(ConfigurationSection))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<AuthRateLimitOptions>, AuthRateLimitOptionsValidator>();

            return services;
        }
    }
}
