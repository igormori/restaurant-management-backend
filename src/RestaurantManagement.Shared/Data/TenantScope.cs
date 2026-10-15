namespace RestaurantManagement.Shared.Data
{
    public sealed class TenantScope : ITenantScope
    {
        private readonly AsyncLocal<Guid?> _organizationId = new();

        public Guid? OrganizationId => _organizationId.Value;

        public IDisposable Begin(Guid organizationId)
        {
            var previousOrganizationId = _organizationId.Value;
            _organizationId.Value = organizationId;
            return new ScopeRestorer(() => _organizationId.Value = previousOrganizationId);
        }

        private sealed class ScopeRestorer : IDisposable
        {
            private readonly Action _restore;

            public ScopeRestorer(Action restore)
            {
                _restore = restore;
            }

            public void Dispose() => _restore();
        }
    }
}
