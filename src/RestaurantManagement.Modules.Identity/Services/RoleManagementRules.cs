using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class RoleManagementRules : IRoleManagementRules
    {
        public RoleManagementRules(
            IdentityDbContext db,
            ITenantContext tenantContext,
            ICurrentUser currentUser,
            IOrganizationLookup organizationLookup,
            IStringLocalizer<SharedResource> localizer)
        {
        }

        public Task RequireAsync(
            IReadOnlyDictionary<AccessModule, AccessLevel> currentLevels,
            IReadOnlyDictionary<AccessModule, AccessLevel> proposedLevels)
            => throw new NotImplementedException();
    }
}
