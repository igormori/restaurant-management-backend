using Microsoft.AspNetCore.Authorization;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Web.Auth
{
    public static class PermissionPolicyExtensions
    {
        public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
        {
            services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                foreach (var permission in Permissions.All)
                {
                    options.AddPolicy(permission, policy => policy
                        .RequireAuthenticatedUser()
                        .AddRequirements(new PermissionRequirement(permission)));
                }
            });
            return services;
        }
    }
}
