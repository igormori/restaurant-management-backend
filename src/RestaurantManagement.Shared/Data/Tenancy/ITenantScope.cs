namespace RestaurantManagement.Shared.Data.Tenancy
{
    public interface ITenantScope
    {
        Guid? OrganizationId { get; }
        IDisposable Begin(Guid organizationId);
    }
}
