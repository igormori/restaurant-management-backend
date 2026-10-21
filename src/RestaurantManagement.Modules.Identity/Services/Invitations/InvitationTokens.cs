using System.Security.Cryptography;
using System.Text;

namespace RestaurantManagement.Modules.Identity.Services.Invitations
{
    /// <summary>The one-time invitation token: random bytes for the link, only a SHA-256 fingerprint stored.</summary>
    public static class InvitationTokens
    {
        private const int TokenBytes = 32;

        public static string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
