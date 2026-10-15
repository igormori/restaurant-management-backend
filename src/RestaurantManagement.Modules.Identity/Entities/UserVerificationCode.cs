using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagement.Modules.Identity.Entities
{
    public class UserVerificationCode
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Code { get; set; } = string.Empty;

        // Stub for T3: replaces Code (column code_hash). Remove [NotMapped] and Code when implementing.
        [NotMapped]
        public string CodeHash
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
    }
}
