namespace RestaurantManagement.Shared.Data
{
    public sealed class TenantScope : ITenantScope
    {
        public Guid? OrganizationId => throw new NotImplementedException();

        public IDisposable Begin(Guid organizationId) => throw new NotImplementedException();
    }
}
