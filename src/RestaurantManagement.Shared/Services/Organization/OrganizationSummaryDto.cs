namespace RestaurantManagement.Shared.Services.Organization
{
    public class OrganizationSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; }
    }
}
