namespace RestaurantManagement.Modules.Identity.Entities
{
    // Same shape as UserVerificationCode, kept in its own table so reset and verification never mix.
    public class PasswordResetCode
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string CodeHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; }

        public User User { get; set; } = null!;
    }
}
