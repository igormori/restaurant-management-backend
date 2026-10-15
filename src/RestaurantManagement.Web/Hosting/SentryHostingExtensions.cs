using Sentry.AspNetCore;

namespace RestaurantManagement.Web.Hosting
{
    public static class SentryHostingExtensions
    {
        private const string DsnKey = "Sentry:Dsn";
        private const string TracesSampleRateKey = "Sentry:TracesSampleRate";
        private const double DevelopmentTracesSampleRate = 1.0;
        private const double DefaultTracesSampleRate = 0.1;
        private const double MinimumTracesSampleRate = 0.0;
        private const double MaximumTracesSampleRate = 1.0;

        // Configuration is read when the options are first resolved, not here, because
        // configuration sources added after the builder is created (deployment overrides) must count.
        public static WebApplicationBuilder AddSentryTracking(this WebApplicationBuilder builder)
        {
            // Sentry's own binding rejects a bad rate with an error that does not name the setting.
            // Options setups run in registration order, so registering this one first makes it fail first.
            builder.Services.Configure<SentryAspNetCoreOptions>(_ =>
                ReadTracesSampleRate(builder.Configuration, builder.Environment.IsDevelopment()));

            builder.WebHost.UseSentry(options =>
            {
                options.Dsn = builder.Configuration[DsnKey];
                options.Debug = builder.Environment.IsDevelopment();
                options.TracesSampleRate = ReadTracesSampleRate(builder.Configuration, builder.Environment.IsDevelopment());
            });

            return builder;
        }

        private static double ReadTracesSampleRate(IConfiguration configuration, bool isDevelopment)
        {
            var defaultRate = isDevelopment ? DevelopmentTracesSampleRate : DefaultTracesSampleRate;
            var rate = configuration.GetValue<double?>(TracesSampleRateKey) ?? defaultRate;

            if (rate < MinimumTracesSampleRate || rate > MaximumTracesSampleRate)
                throw new InvalidOperationException(
                    $"{TracesSampleRateKey} must be between {MinimumTracesSampleRate} and {MaximumTracesSampleRate}.");

            return rate;
        }
    }
}
