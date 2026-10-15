using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Web.Auth
{
    /// <summary>
    /// Succeeds only when the token names an organization and one of its roles grants the permission.
    /// A token without an organization claim (none selected) never passes, so roles from other
    /// organizations cannot leak into the decision.
    /// </summary>
    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (context.User.FindFirst(AppClaimTypes.OrganizationId) is null)
                return Task.CompletedTask;

            var granted = context.User.FindAll(ClaimTypes.Role)
                .Any(role => RolePermissions.HasPermission(role.Value, requirement.Permission));

            if (granted)
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
