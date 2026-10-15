using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace RestaurantManagement.Shared.Options
{
    public class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        private const int MinimumKeyBytes = 32;

        private readonly IHostEnvironment _environment;

        public JwtOptionsValidator(IHostEnvironment environment)
        {
            _environment = environment;
        }

        public ValidateOptionsResult Validate(string? name, JwtOptions options)
        {
            var failures = new List<string>();

            ValidateKey(options.Key, failures);

            if (options.ExpireMinutes <= 0)
                failures.Add("Jwt:ExpireMinutes must be greater than zero.");

            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }

        private void ValidateKey(string? key, List<string> failures)
        {
            if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes)
            {
                failures.Add($"Jwt:Key must be at least {MinimumKeyBytes} bytes (UTF-8).");
                return;
            }

            if (!_environment.IsDevelopment() && key == JwtOptions.PlaceholderKey)
                failures.Add("Jwt:Key must not be the placeholder value from appsettings.json outside Development.");
        }
    }
}
