namespace RestaurantManagement.Shared.Data
{
    public interface ITenantScope
    {
        Guid? OrganizationId { get; }
        IDisposable Begin(Guid organizationId);
    }
}
