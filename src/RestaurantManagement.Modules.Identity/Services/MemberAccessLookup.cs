using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.Identity;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class MemberAccessLookup : IMemberAccessLookup
    {
        public MemberAccessLookup(IdentityDbContext db)
        {
        }

        public Task<bool> UserExistsAsync(Guid userId) => throw new NotImplementedException();

        public Task<List<Guid>> GetOrganizationIdsForUserAsync(Guid userId) => throw new NotImplementedException();

        public Task<AccessGridDto> GetAccessGridAsync(Guid userId, Guid organizationId) => throw new NotImplementedException();
    }
}
