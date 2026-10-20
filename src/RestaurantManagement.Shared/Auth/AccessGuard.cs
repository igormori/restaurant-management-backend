using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Shared.Auth
{
    public sealed class AccessGuard : IAccessGuard
    {
        private readonly ICurrentUser _currentUser;
        private readonly IMemberAccessLookup _memberAccessLookup;
        private readonly IOrganizationLookup _organizationLookup;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private AccessGridDto? _grid;

        public AccessGuard(
            ICurrentUser currentUser,
            IMemberAccessLookup memberAccessLookup,
            IOrganizationLookup organizationLookup,
            IStringLocalizer<SharedResource> localizer)
        {
            _currentUser = currentUser;
            _memberAccessLookup = memberAccessLookup;
            _organizationLookup = organizationLookup;
            _localizer = localizer;
        }

        public async Task RequireOwnerAsync()
        {
            var grid = await GetGridAsync();
            Ensure(grid.IsOwner);
        }

        public async Task RequireAtLocationAsync(AccessModule module, AccessLevel level, Guid locationId)
        {
            var grid = await GetGridAsync();
            Ensure(AccessGridDecision.HasLevelAt(grid, module, level, locationId));
        }

        public async Task RequireAtLocationsAsync(AccessModule module, AccessLevel level, IReadOnlyCollection<Guid> locationIds)
        {
            var grid = await GetGridAsync();
            Ensure(AccessGridDecision.HasLevelAtAll(grid, module, level, locationIds));
        }

        public async Task RequireAtEveryLocationAsync(AccessModule module, AccessLevel level)
        {
            var grid = await GetGridAsync();
            if (grid.IsOwner)
            {
                return;
            }

            var locationIds = await _organizationLookup.GetNonClosedLocationIdsAsync(_currentUser.OrganizationId!.Value);
            Ensure(AccessGridDecision.HasLevelAtAll(grid, module, level, locationIds));
        }

        public async Task RequireAnyAccessAsync()
        {
            var grid = await GetGridAsync();
            Ensure(AccessGridDecision.HasAnyAccess(grid));
        }

        // Read once per request instance; the guard is scoped, so a later request sees a changed grid.
        private async Task<AccessGridDto> GetGridAsync()
        {
            if (_grid is not null)
            {
                return _grid;
            }

            var organizationId = _currentUser.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"], 403);
            var userId = _currentUser.UserId
                ?? throw new BusinessException(_localizer["AccessDenied"], 403);

            _grid = await _memberAccessLookup.GetAccessGridAsync(userId, organizationId);
            return _grid;
        }

        private void Ensure(bool allowed)
        {
            if (!allowed)
            {
                throw new BusinessException(_localizer["AccessDenied"], 403);
            }
        }
    }
}
