namespace RestaurantManagement.Modules.Identity.Services
{
    public class BcryptPasswordHasher : IPasswordHasher
    {
        public const int WorkFactor = 12;

        public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

        public bool Verify(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
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
