namespace RestaurantManagement.Shared.Data
{
    public abstract class TenantEntity : BaseEntity
    {
        public Guid OrganizationId { get; set; }
    }
}
