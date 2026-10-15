using Microsoft.Extensions.Options;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class VerificationCodeHasher : IVerificationCodeHasher
    {
        public VerificationCodeHasher(IOptions<VerificationCodeOptions> options)
        {
        }

        public string ComputeHash(Guid userId, string code)
        {
            throw new NotImplementedException();
        }

        public bool IsMatch(Guid userId, string code, string storedHash)
        {
            throw new NotImplementedException();
        }
    }
}
