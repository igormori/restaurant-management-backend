namespace RestaurantManagement.Shared.Data.Entities
{
    public abstract class TenantEntity : BaseEntity
    {
        public Guid OrganizationId { get; set; }
    }
}
