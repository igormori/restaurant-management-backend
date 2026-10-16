namespace RestaurantManagement.Modules.Identity.Services
{
    /// <summary>The only way the Identity module hashes or checks a password.</summary>
    public interface IPasswordHasher
    {
        string Hash(string password);

        // Returns false for blank or malformed input instead of throwing.
        bool Verify(string password, string hash);
    }
}
