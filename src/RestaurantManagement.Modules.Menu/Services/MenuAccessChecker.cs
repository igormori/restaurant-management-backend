using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Menu.Services
{
    // Stub: T9 rewrites these checks on the access grid through the guard.
    internal class MenuAccessChecker
    {
        public MenuAccessChecker(IAccessGuard accessGuard, IStringLocalizer<SharedResource> localizer)
        {
        }

        public Task RequireOrganizationAdminAsync(Guid userId, Guid organizationId) => throw new NotImplementedException();

        public Task CheckAdminAtLocationAsync(Guid userId, Guid organizationId, Guid? locationId) => throw new NotImplementedException();

        public Task CheckReadAllAsync(Guid userId, Guid organizationId) => throw new NotImplementedException();

        public Task CheckReadAtLocationAsync(Guid userId, Guid organizationId, Guid locationId) => throw new NotImplementedException();
    }
}
