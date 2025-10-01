using RestaurantManagement.Api.Entities.Locations;

namespace RestaurantManagement.Api.Entities.Organizations
{
    public class Organization
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? AccentColor { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public OrganizationSettings? Settings { get; set; }
        public ICollection<Location> Locations { get; set; } = new List<Location>();
    }
}