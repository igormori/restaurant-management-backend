using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class RoleManagementRules : IRoleManagementRules
    {
        private readonly IdentityDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly ICurrentUser _currentUser;
        private readonly IOrganizationLookup _organizationLookup;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public RoleManagementRules(
            IdentityDbContext db,
            ITenantContext tenantContext,
            ICurrentUser currentUser,
            IOrganizationLookup organizationLookup,
            IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _tenantContext = tenantContext;
            _currentUser = currentUser;
            _organizationLookup = organizationLookup;
            _localizer = localizer;
        }

        public async Task RequireAsync(
            IReadOnlyDictionary<AccessModule, AccessLevel> currentLevels,
            IReadOnlyDictionary<AccessModule, AccessLevel> proposedLevels)
        {
            var organizationId = _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);
            var callerId = _currentUser.UserId
                ?? throw new BusinessException(_localizer["AccessDenied"].Value, 403);

            var caller = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m => m.UserId == callerId && m.OrganizationId == organizationId)
                ?? throw new BusinessException(_localizer["AccessDenied"].Value, 403);
            if (caller.IsOwner)
                return;

            var locationIds = await _organizationLookup.GetNonClosedLocationIdsAsync(organizationId);
            if (locationIds.Count == 0)
                throw new BusinessException(_localizer["AccessDenied"].Value, 403);

            var cells = await _db.MemberLocationLevels
                .Where(l => l.UserId == callerId && l.OrganizationId == organizationId && locationIds.Contains(l.LocationId))
                .ToListAsync();

            if (CapOf(cells, locationIds, AccessModule.Members) != AccessLevel.Manage)
                throw new BusinessException(_localizer["AccessDenied"].Value, 403);

            foreach (var (module, level) in currentLevels.Concat(proposedLevels))
            {
                if (level > CapOf(cells, locationIds, module))
                    throw new BusinessException(_localizer["CannotGrantAboveOwnLevel"].Value, 403);
            }
        }

        // The lowest level over the non-closed locations; a missing cell counts as None.
        private static AccessLevel CapOf(List<MemberLocationLevel> cells, List<Guid> locationIds, AccessModule module)
            => locationIds
                .Select(id => cells.FirstOrDefault(c => c.LocationId == id && c.Module == module)?.Level ?? AccessLevel.None)
                .Min();
    }
}
