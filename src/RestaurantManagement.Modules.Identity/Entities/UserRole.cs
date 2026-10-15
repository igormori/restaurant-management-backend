using RestaurantManagement.Shared.Data;
namespace RestaurantManagement.Modules.Identity.Entities
{
    public class UserRole : BaseEntity
    {
        public Guid UserId { get; set; }

        // Cross-module references: Use IDs only to avoid circular dependencies
        // Organization and Location entities live in Organization module
        public Guid? OrganizationId { get; set; }
        public Guid? LocationId { get; set; }

        public string Role { get; set; } = null!;

        // Navigation property within same module only
        public User User { get; set; } = null!;
    }
}