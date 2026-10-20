using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Shared.Auth
{
    public sealed class AccessGuard : IAccessGuard
    {
        public AccessGuard(
            ICurrentUser currentUser,
            IMemberAccessLookup memberAccessLookup,
            IOrganizationLookup organizationLookup,
            IStringLocalizer<SharedResource> localizer)
        {
        }

        public Task RequireOwnerAsync() => throw new NotImplementedException();

        public Task RequireAtLocationAsync(AccessModule module, AccessLevel level, Guid locationId)
            => throw new NotImplementedException();

        public Task RequireAtLocationsAsync(AccessModule module, AccessLevel level, IReadOnlyCollection<Guid> locationIds)
            => throw new NotImplementedException();

        public Task RequireAtEveryLocationAsync(AccessModule module, AccessLevel level)
            => throw new NotImplementedException();

        public Task RequireAnyAccessAsync() => throw new NotImplementedException();
    }
}
