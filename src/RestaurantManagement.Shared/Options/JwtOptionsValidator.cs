using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace RestaurantManagement.Shared.Options
{
    public class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        public JwtOptionsValidator(IHostEnvironment environment)
        {
            throw new NotImplementedException();
        }

        public ValidateOptionsResult Validate(string? name, JwtOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
