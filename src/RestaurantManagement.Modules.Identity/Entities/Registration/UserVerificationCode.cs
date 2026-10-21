using RestaurantManagement.Modules.Identity.Entities.Users;

namespace RestaurantManagement.Modules.Identity.Entities.Registration
{
    public class UserVerificationCode
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string CodeHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}
