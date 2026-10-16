using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.RateLimiting;

namespace RestaurantManagement.Web.RateLimiting
{
    public static class AuthRateLimitingExtensions
    {
        private const string ConfigurationSection = "AuthRateLimit";
        private const string UnknownClientIp = "unknown";
        private const string TooManyRequestsMessageKey = "TooManyRequests";

        public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<AuthRateLimitOptions>()
                .Bind(configuration.GetSection(ConfigurationSection))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<AuthRateLimitOptions>, AuthRateLimitOptionsValidator>();

            services.AddMemoryCache();
            services.AddSingleton<IRateLimitRejectionReporter, RateLimitRejectionReporter>();

            services.AddOptions<ForwardedHeadersOptions>()
                .Configure<IOptions<AuthRateLimitOptions>>(ConfigureForwardedHeaders);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = WriteRejectionAsync;
                AddIpPolicy(options, AuthRateLimitPolicies.Login, limits => limits.Login);
                AddIpPolicy(options, AuthRateLimitPolicies.Register, limits => limits.Register);
                AddIpPolicy(options, AuthRateLimitPolicies.Refresh, limits => limits.Refresh);
                AddIpPolicy(options, AuthRateLimitPolicies.Verify, limits => limits.Verify);
                AddIpPolicy(options, AuthRateLimitPolicies.ResendVerification, limits => limits.ResendVerification);
                AddIpPolicy(options, AuthRateLimitPolicies.ForgotPassword, limits => limits.ForgotPassword);
                AddIpPolicy(options, AuthRateLimitPolicies.ResetPassword, limits => limits.ResetPassword);
                AddIpPolicy(options, AuthRateLimitPolicies.ChangePassword, limits => limits.ChangePassword);
            });

            return services;
        }

        // Only configured proxies may set X-Forwarded-For; the framework default of trusting loopback is removed.
        private static void ConfigureForwardedHeaders(ForwardedHeadersOptions options, IOptions<AuthRateLimitOptions> rateLimitOptions)
        {
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();

            var trustedProxies = rateLimitOptions.Value.TrustedProxies;
            // With no known proxies the middleware would trust every sender, so it is switched off instead.
            if (trustedProxies.Length == 0)
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            foreach (var entry in trustedProxies)
            {
                if (IPAddress.TryParse(entry, out var address))
                {
                    options.KnownProxies.Add(address);
                    continue;
                }

                var network = System.Net.IPNetwork.Parse(entry);
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(network.BaseAddress, network.PrefixLength));
            }
        }

        // Limits are read when a partition is first created, so configuration changes need no code changes.
        private static void AddIpPolicy(RateLimiterOptions options, string policyName, Func<AuthRateLimitOptions, EndpointRateLimit> selectLimit)
        {
            options.AddPolicy(policyName, httpContext =>
            {
                var limit = selectLimit(httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value);
                return RateLimitPartition.GetFixedWindowLimiter(GetClientIp(httpContext), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit.PermitLimit,
                    Window = TimeSpan.FromSeconds(limit.WindowSeconds),
                    QueueLimit = 0
                });
            });
        }

        private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            var httpContext = context.HttpContext;
            context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter);
            var retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);

            httpContext.RequestServices.GetRequiredService<IRateLimitRejectionReporter>()
                .Report(httpContext.Request.Path, GetClientIp(httpContext), retryAfter);

            var localizer = httpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
            await httpContext.Response.WriteAsJsonAsync(new { message = localizer[TooManyRequestsMessageKey].Value }, cancellationToken);
        }

        private static string GetClientIp(HttpContext httpContext)
        {
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClientIp;
        }
    }
}
