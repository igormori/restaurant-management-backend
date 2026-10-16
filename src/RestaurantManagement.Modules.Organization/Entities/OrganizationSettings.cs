using RestaurantManagement.Shared.Data;
namespace RestaurantManagement.Modules.Organization.Entities
{
    public class OrganizationSettings : TenantEntity
    {
        public int MaxLocations { get; set; } = 1;
        public string PlanType { get; set; } = "TRIAL";
        public DateTime TrialEndDate { get; set; } = DateTime.UtcNow.AddDays(30);
        public bool IsTrialActive { get; set; } = true;
        public decimal ServiceChargeRate { get; set; }
        public decimal DiscountApprovalLimit { get; set; }
        public TimeOnly BusinessDayCutoff { get; set; } = new TimeOnly(4, 0);

        // Navigation
        public Organization Organization { get; set; } = null!;
    }
}