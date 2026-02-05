namespace RestaurantManagement.Modules.Organization.Entities
{
    public class OrganizationSettings
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public int MaxLocations { get; set; } = 1;
        public string PlanType { get; set; } = "TRIAL";
        public DateTime TrialEndDate { get; set; } = DateTime.UtcNow.AddDays(30);
        public bool IsTrialActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        

        // Navigation
        public Organization Organization { get; set; } = null!;
    }
}