using Microsoft.AspNetCore.Authorization;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Web.Auth
{
    public sealed class PermissionRequirement : IAuthorizationRequirement
    {
        public PermissionRequirement(AccessModule module, AccessLevel level)
        {
            Module = module;
            Level = level;
        }

        public AccessModule Module { get; }

        public AccessLevel Level { get; }
    }
}
