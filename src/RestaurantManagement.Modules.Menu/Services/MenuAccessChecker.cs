using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Menu.Services
{
    /// <summary>
    /// Every access question of the Menu module, asked of the member's access grid through the
    /// guard. An action tied to one location needs the level there; an action across locations needs
    /// it at every location involved, or at every non-closed location when none is involved.
    /// </summary>
    internal class MenuAccessChecker
    {
        private readonly IAccessGuard _guard;

        public MenuAccessChecker(IAccessGuard guard)
        {
            _guard = guard;
        }

        public Task RequireReadAtLocationAsync(Guid locationId)
            => _guard.RequireAtLocationAsync(AccessModule.Menus, AccessLevel.Read, locationId);

        public Task RequireManageAtLocationAsync(Guid locationId)
            => _guard.RequireAtLocationAsync(AccessModule.Menus, AccessLevel.Manage, locationId);

        public Task RequireReadEverywhereAsync()
            => _guard.RequireAtEveryLocationAsync(AccessModule.Menus, AccessLevel.Read);

        public Task RequireManageEverywhereAsync()
            => _guard.RequireAtEveryLocationAsync(AccessModule.Menus, AccessLevel.Manage);

        /// <summary>A category of one location needs the level there; one without a location needs it everywhere.</summary>
        public Task RequireReadCategoryAsync(Guid? locationId)
            => locationId.HasValue ? RequireReadAtLocationAsync(locationId.Value) : RequireReadEverywhereAsync();

        public Task RequireManageCategoryAsync(Guid? locationId)
            => locationId.HasValue ? RequireManageAtLocationAsync(locationId.Value) : RequireManageEverywhereAsync();

        /// <summary>A menu needs Manage at every location it is linked to; linked to none, at every non-closed location.</summary>
        public Task RequireManageMenuAsync(IReadOnlyCollection<Guid> linkedLocationIds)
            => linkedLocationIds.Count == 0
                ? RequireManageEverywhereAsync()
                : _guard.RequireAtLocationsAsync(AccessModule.Menus, AccessLevel.Manage, linkedLocationIds);
    }
}
