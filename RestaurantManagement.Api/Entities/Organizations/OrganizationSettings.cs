namespace RestaurantManagement.Api.Entities.Organizations
{
    public class OrganizationSettings
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public int MaxLocations { get; set; } = 1;
        public string PlanType { get; set; } = "FREE";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Organization Organization { get; set; } = null!;
    }
}