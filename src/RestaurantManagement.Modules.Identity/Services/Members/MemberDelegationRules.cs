using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data.Tenancy;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services.Members
{
    public class MemberDelegationRules : IMemberDelegationRules
    {
        private readonly IdentityDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly ICurrentUser _currentUser;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MemberDelegationRules(
            IdentityDbContext db, ITenantContext tenantContext, ICurrentUser currentUser, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _tenantContext = tenantContext;
            _currentUser = currentUser;
            _localizer = localizer;
        }

        public async Task RequireAsync(Guid? targetUserId, IReadOnlyCollection<LocationGrant> grants)
        {
            var organizationId = _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);
            var callerId = _currentUser.UserId
                ?? throw new BusinessException(_localizer["AccessDenied"].Value, 403);

            if (targetUserId == callerId)
                throw new BusinessException(_localizer["CannotChangeOwnAccess"].Value, 403);
            if (targetUserId is not null && await IsOwnerAsync(targetUserId.Value, organizationId))
                throw new BusinessException(_localizer["AccessDenied"].Value, 403);

            var caller = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m => m.UserId == callerId && m.OrganizationId == organizationId)
                ?? throw new BusinessException(_localizer["AccessDenied"].Value, 403);
            if (caller.IsOwner)
                return;

            var callerCells = await LoadCellsAsync(callerId, organizationId);
            var targetCells = targetUserId is null ? new List<(Guid, AccessModule, AccessLevel)>() : await LoadCellsAsync(targetUserId.Value, organizationId);

            foreach (var grant in grants)
            {
                if (LevelOf(callerCells, grant.LocationId, AccessModule.Members) != AccessLevel.Manage)
                    throw new BusinessException(_localizer["AccessDenied"].Value, 403);
            }

            foreach (var grant in grants)
                foreach (var (module, level) in grant.Levels)
                {
                    var isRaise = level > LevelOf(targetCells, grant.LocationId, module);
                    if (isRaise && level > LevelOf(callerCells, grant.LocationId, module))
                        throw new BusinessException(_localizer["CannotGrantAboveOwnLevel"].Value, 403);
                }
        }

        private async Task<bool> IsOwnerAsync(Guid userId, Guid organizationId)
        {
            return await _db.OrganizationMembers
                .AnyAsync(m => m.UserId == userId && m.OrganizationId == organizationId && m.IsOwner);
        }

        private async Task<List<(Guid LocationId, AccessModule Module, AccessLevel Level)>> LoadCellsAsync(Guid userId, Guid organizationId)
        {
            var cells = await _db.MemberLocationLevels
                .Where(l => l.UserId == userId && l.OrganizationId == organizationId)
                .ToListAsync();
            return cells.Select(c => (c.LocationId, c.Module, c.Level)).ToList();
        }

        private static AccessLevel LevelOf(
            List<(Guid LocationId, AccessModule Module, AccessLevel Level)> cells, Guid locationId, AccessModule module)
            => cells.FirstOrDefault(c => c.LocationId == locationId && c.Module == module).Level;
    }
}
