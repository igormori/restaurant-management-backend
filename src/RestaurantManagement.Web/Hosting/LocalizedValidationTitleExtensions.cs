using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared;

namespace RestaurantManagement.Web.Hosting
{
    public static class LocalizedValidationTitleExtensions
    {
        /// <summary>
        /// Replaces only the English title of the platform's validation response with the catalog
        /// message in the requested language; status, errors and the rest of the body stay as the platform builds them.
        /// </summary>
        public static IServiceCollection AddLocalizedValidationTitle(this IServiceCollection services)
        {
            services.PostConfigure<ApiBehaviorOptions>(options =>
            {
                var platformFactory = options.InvalidModelStateResponseFactory;
                options.InvalidModelStateResponseFactory = context =>
                {
                    var result = platformFactory(context);
                    if (result is ObjectResult { Value: ProblemDetails problem })
                    {
                        var localizer = context.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
                        problem.Title = localizer["ValidationFailed"].Value;
                    }
                    return result;
                };
            });
            return services;
        }
    }
}
