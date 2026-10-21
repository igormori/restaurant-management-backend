using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Modules.Identity.Services.Registration
{
    public class VerificationCodeHasher : IVerificationCodeHasher
    {
        private readonly byte[] _key;

        public VerificationCodeHasher(IOptions<VerificationCodeOptions> options)
        {
            _key = Encoding.UTF8.GetBytes(options.Value.HashKey);
        }

        public string ComputeHash(Guid userId, string code)
        {
            return Convert.ToHexStringLower(ComputeBytes(userId, code));
        }

        public bool IsMatch(Guid userId, string code, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            byte[] stored;
            try
            {
                stored = Convert.FromHexString(storedHash);
            }
            catch (FormatException)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(ComputeBytes(userId, code), stored);
        }

        // The ":" separator keeps the user ID and the code from running into each other.
        private byte[] ComputeBytes(Guid userId, string code)
        {
            var data = Encoding.UTF8.GetBytes($"{userId:D}:{code}");
            return HMACSHA256.HashData(_key, data);
        }
    }
}
