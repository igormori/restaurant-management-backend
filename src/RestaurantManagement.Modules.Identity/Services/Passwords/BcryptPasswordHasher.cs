namespace RestaurantManagement.Modules.Identity.Services
{
    public class BcryptPasswordHasher : IPasswordHasher
    {
        public const int WorkFactor = 12;

        // BCrypt reads only the first 72 bytes, so a longer value would be silently cut.
        public const int MaxPasswordBytes = 72;

        public string Hash(string password)
        {
            if (ExceedsByteLimit(password))
                throw new ArgumentException($"A password over {MaxPasswordBytes} UTF-8 bytes cannot be hashed without being cut.", nameof(password));

            return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
        }

        public static bool ExceedsByteLimit(string password)
            => System.Text.Encoding.UTF8.GetByteCount(password) > MaxPasswordBytes;

        public bool Verify(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
                return false;

            // A longer value can never match: the stored hash covers only the first 72 bytes.
            if (ExceedsByteLimit(password))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
        }
    }
}
