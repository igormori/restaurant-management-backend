using System.Text;
using Microsoft.Extensions.Options;

namespace RestaurantManagement.Shared.Options
{
    public class VerificationCodeOptionsValidator : IValidateOptions<VerificationCodeOptions>
    {
        private const int MinimumKeyBytes = 32;

        public ValidateOptionsResult Validate(string? name, VerificationCodeOptions options)
        {
            if (string.IsNullOrEmpty(options.HashKey) || Encoding.UTF8.GetByteCount(options.HashKey) < MinimumKeyBytes)
                return ValidateOptionsResult.Fail($"VerificationCode:HashKey must be at least {MinimumKeyBytes} bytes (UTF-8).");

            return ValidateOptionsResult.Success;
        }
    }
}
