using Microsoft.AspNetCore.Authorization;

namespace RestaurantManagement.Web.Auth
{
    public sealed class PermissionRequirement : IAuthorizationRequirement
    {
        public PermissionRequirement(string permission) => Permission = permission;

        public string Permission { get; }
    }
}
