using Microsoft.Extensions.Caching.Memory;
using Sentry;

namespace RestaurantManagement.Web.RateLimiting
{
    public class RateLimitRejectionReporter : IRateLimitRejectionReporter
    {
        public const string EndpointTag = "endpoint";
        public const string ClientIpTag = "client_ip";

        private readonly IHub _hub;
        private readonly IMemoryCache _cache;

        public RateLimitRejectionReporter(IHub hub, IMemoryCache cache)
        {
            _hub = hub;
            _cache = cache;
        }

        public void Report(string endpoint, string clientIp, TimeSpan retryAfter)
        {
            var dedupKey = $"rate-limit-reported:{endpoint}:{clientIp}";
            if (_cache.TryGetValue(dedupKey, out _)) return;

            // The cache entry lives until the window resets, so only the first rejection in a window is reported.
            _cache.Set(dedupKey, true, retryAfter);

            // A fresh scope keeps request data (body, headers) out of the event: only endpoint and IP are sent.
            var scope = new Scope(new SentryOptions());
            scope.SetTag(EndpointTag, endpoint);
            scope.SetTag(ClientIpTag, clientIp);

            var rateLimitEvent = new SentryEvent
            {
                Message = "Auth rate limit exceeded",
                Level = SentryLevel.Warning
            };
            _hub.CaptureEvent(rateLimitEvent, scope);
        }
    }
}
