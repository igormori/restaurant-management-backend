using System.Net;
using Microsoft.Extensions.Options;

namespace RestaurantManagement.Shared.Options
{
    public class AuthRateLimitOptionsValidator : IValidateOptions<AuthRateLimitOptions>
    {
        public ValidateOptionsResult Validate(string? name, AuthRateLimitOptions options)
        {
            var failures = new List<string>();

            ValidateEndpoint(nameof(options.Login), options.Login, failures);
            ValidateEndpoint(nameof(options.Register), options.Register, failures);
            ValidateEndpoint(nameof(options.ResendVerification), options.ResendVerification, failures);
            ValidateEndpoint(nameof(options.Verify), options.Verify, failures);
            ValidateEndpoint(nameof(options.Refresh), options.Refresh, failures);
            ValidateTrustedProxies(options.TrustedProxies, failures);

            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }

        private static void ValidateEndpoint(string endpoint, EndpointRateLimit limit, List<string> failures)
        {
            if (limit.PermitLimit <= 0)
                failures.Add($"AuthRateLimit:{endpoint}:PermitLimit must be greater than zero.");

            if (limit.WindowSeconds <= 0)
                failures.Add($"AuthRateLimit:{endpoint}:WindowSeconds must be greater than zero.");
        }

        private static void ValidateTrustedProxies(string[] trustedProxies, List<string> failures)
        {
            foreach (var entry in trustedProxies)
            {
                if (!IPAddress.TryParse(entry, out _) && !IPNetwork.TryParse(entry, out _))
                    failures.Add($"AuthRateLimit:TrustedProxies entry '{entry}' is not a valid IP address or CIDR network.");
            }
        }
    }
}
