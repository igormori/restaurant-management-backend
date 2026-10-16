using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;

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

        public async Task InvokeAsync(
            HttpContext context,
            ICurrentUser currentUser,
            IOrganizationLookup organizationLookup,
            IUserRoleLookup userRoleLookup,
            SelectedLocation selectedLocation,
            IStringLocalizer<SharedResource> localizer)
        {
            var isSignedIn = context.User.Identity?.IsAuthenticated == true && currentUser.UserId is not null;
            if (!isSignedIn || !context.Request.Headers.TryGetValue(HeaderName, out var header))
            {
                await _next(context);
                return;
            }

            if (!Guid.TryParse(header.ToString(), out var locationId))
                throw new BusinessException(localizer["InvalidLocationId"], 400);

            var organizationId = currentUser.OrganizationId
                ?? throw new BusinessException(localizer["LocationAccessDenied"], 403);

            var location = await organizationLookup.GetLocationAsync(locationId);
            if (location is null || location.OrganizationId != organizationId
                || !await userRoleLookup.HasLocationAccessAsync(currentUser.UserId!.Value, organizationId, locationId))
                throw new BusinessException(localizer["LocationAccessDenied"], 403);

            selectedLocation.LocationId = locationId;
            await _next(context);
        }
    }
}
