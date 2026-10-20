using Microsoft.AspNetCore.Authorization;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Web.Auth
{
    public static class PermissionPolicyExtensions
    {
        public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
        {
            // Scoped: the handler uses the request-scoped access guard.
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                foreach (var module in Enum.GetValues<AccessModule>())
                {
                    foreach (var level in new[] { AccessLevel.Read, AccessLevel.Manage })
                    {
                        var name = $"{module}.{level}".ToLowerInvariant();
                        options.AddPolicy(name, policy => policy
                            .RequireAuthenticatedUser()
                            .AddRequirements(new PermissionRequirement(module, level)));
                    }
                }
            });
            return services;
        }
    }
}
