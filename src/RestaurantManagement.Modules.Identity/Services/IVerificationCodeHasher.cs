namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IVerificationCodeHasher
    {
        string ComputeHash(Guid userId, string code);
        bool IsMatch(Guid userId, string code, string storedHash);
    }
}
