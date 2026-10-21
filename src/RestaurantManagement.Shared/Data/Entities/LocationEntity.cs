namespace RestaurantManagement.Shared.Data.Entities
{
    public abstract class LocationEntity : TenantEntity
    {
        public Guid LocationId { get; set; }
    }
}
