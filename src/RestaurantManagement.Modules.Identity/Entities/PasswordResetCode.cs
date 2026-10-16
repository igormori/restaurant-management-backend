namespace RestaurantManagement.Modules.Identity.Entities
{
    // Stub (tester): shape of a password reset code row. Not yet mapped in IdentityDbContext (task T3).
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
