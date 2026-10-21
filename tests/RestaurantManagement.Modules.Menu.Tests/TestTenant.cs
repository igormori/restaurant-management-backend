using NSubstitute;
using RestaurantManagement.Shared.Data.Tenancy;

namespace RestaurantManagement.Modules.Menu.Tests
{
    /// <summary>
    /// Builds the tenant (organization from the sign-in token, optional selected location) a
    /// MenuDbContext and the Menu services see.
    /// </summary>
    public static class TestTenant
    {
        public static ITenantContext Of(Guid? organizationId, Guid? locationId = null)
        {
            var tenantContext = Substitute.For<ITenantContext>();
            tenantContext.OrganizationId.Returns(organizationId);
            tenantContext.LocationId.Returns(locationId);
            return tenantContext;
        }

        /// <summary>No organization: reads return no tenant rows once the filters apply.</summary>
        public static ITenantContext None => Of(null);
    }
}
