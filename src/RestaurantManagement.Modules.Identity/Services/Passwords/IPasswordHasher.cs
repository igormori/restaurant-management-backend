namespace RestaurantManagement.Modules.Identity.Services.Passwords
{
    /// <summary>The only way the Identity module hashes or checks a password.</summary>
    public interface IPasswordHasher
    {
        // Throws for a password over 72 UTF-8 bytes; validation rejects those before they get here.
        string Hash(string password);

        // Returns false for blank, malformed or over-72-byte input instead of throwing.
        bool Verify(string password, string hash);
    }
}
