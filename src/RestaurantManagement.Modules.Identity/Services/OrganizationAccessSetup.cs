using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Services.Identity;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class OrganizationAccessSetup : IOrganizationAccessSetup
    {
        public OrganizationAccessSetup(IdentityDbContext db)
        {
        }

        public Task AddOwnerWithDefaultRolesAsync(Guid ownerUserId, Guid organizationId) => throw new NotImplementedException();

        public Task RemoveOwnerAndRolesAsync(Guid ownerUserId, Guid organizationId) => throw new NotImplementedException();
    }
}
