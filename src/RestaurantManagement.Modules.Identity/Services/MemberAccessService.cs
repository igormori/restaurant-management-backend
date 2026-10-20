using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class MemberAccessService : IMemberAccessService
    {
        public MemberAccessService(
            IdentityDbContext db,
            IOrganizationLookup organizationLookup,
            ITenantContext tenantContext,
            IAuditLog auditLog,
            IStringLocalizer<SharedResource> localizer,
            ILogger<MemberAccessService> logger)
        {
        }

        public Task SetAccessAsync(
            Guid userId,
            Guid locationId,
            IReadOnlyCollection<string> roleNames,
            IReadOnlyDictionary<AccessModule, AccessLevel> changedLevels)
            => throw new NotImplementedException();

        public Task<List<MemberLocationAccessResponse>> GetAccessAsync(Guid userId)
            => throw new NotImplementedException();
    }
}
