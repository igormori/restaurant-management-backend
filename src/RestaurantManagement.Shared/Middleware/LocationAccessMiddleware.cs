using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Shared.Middleware
{
    /// <summary>
    /// Checks the X-Location-Id header of a signed-in request against the user's role assignments
    /// and stores the location in <see cref="SelectedLocation"/> when allowed.
    /// </summary>
    public sealed class LocationAccessMiddleware
    {
        public const string HeaderName = "X-Location-Id";

        private readonly RequestDelegate _next;

        public LocationAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task InvokeAsync(
            HttpContext context,
            ICurrentUser currentUser,
            IOrganizationLookup organizationLookup,
            IUserRoleLookup userRoleLookup,
            SelectedLocation selectedLocation,
            IStringLocalizer<SharedResource> localizer)
            => throw new NotImplementedException();
    }
}
