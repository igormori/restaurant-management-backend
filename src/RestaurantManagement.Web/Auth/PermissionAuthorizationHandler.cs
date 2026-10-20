using Microsoft.AspNetCore.Authorization;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Web.Auth
{
    /// <summary>
    /// Succeeds when the access guard allows the level at every non-closed location of the token
    /// organization (the Owner always passes). No organization in the token, or no access, leaves the
    /// requirement unmet, so the forbidden event answers 403. Role claims are never read.
    /// </summary>
    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IAccessGuard _guard;

        public PermissionAuthorizationHandler(IAccessGuard guard)
        {
            _guard = guard;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            try
            {
                await _guard.RequireAtEveryLocationAsync(requirement.Module, requirement.Level);
                context.Succeed(requirement);
            }
            catch (BusinessException ex) when (ex.StatusCode == 403)
            {
                // The guard refused: leave the requirement unmet.
            }
        }
    }
}
