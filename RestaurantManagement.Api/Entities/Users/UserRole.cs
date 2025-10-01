using RestaurantManagement.Api.Entities.Organizations;
using RestaurantManagement.Api.Entities.Locations;

namespace RestaurantManagement.Api.Entities.Users
{
    public class UserRole
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? LocationId { get; set; }
        public string Role { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation back
        public User User { get; set; } = null!;
        public Organization Organization { get; set; } = null!;
        public Location? Location { get; set; }
    }
}