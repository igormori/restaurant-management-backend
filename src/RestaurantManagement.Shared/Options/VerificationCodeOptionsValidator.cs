using Microsoft.Extensions.Options;

namespace RestaurantManagement.Shared.Options
{
    public class VerificationCodeOptionsValidator : IValidateOptions<VerificationCodeOptions>
    {
        public ValidateOptionsResult Validate(string? name, VerificationCodeOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
