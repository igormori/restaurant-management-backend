using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Shared.Data.Tenancy
{
    public sealed class TenantContext : ITenantContext
    {
        private readonly ITenantScope _tenantScope;
        private readonly ICurrentUser _currentUser;
        private readonly SelectedLocation _selectedLocation;

        public TenantContext(ITenantScope tenantScope, ICurrentUser currentUser, SelectedLocation selectedLocation)
        {
            _tenantScope = tenantScope;
            _currentUser = currentUser;
            _selectedLocation = selectedLocation;
        }

        public Guid? OrganizationId => _tenantScope.OrganizationId ?? _currentUser.OrganizationId;

        public Guid? LocationId => _selectedLocation.LocationId;
    }
}
