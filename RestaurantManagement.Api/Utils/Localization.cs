using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace RestaurantManagement.Api.Utils.Localization
{
    public class CustomPortugueseCultureProvider : RequestCultureProvider
    {
        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            if (httpContext.Request.Headers.TryGetValue("Accept-Language", out StringValues langs))
            {
                var lang = langs.ToString().ToLowerInvariant();

                if (lang.StartsWith("pt"))
                {
                    // Always map any pt-* variant to "pt"
                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult("pt", "pt"));
                }
            }

            return Task.FromResult<ProviderCultureResult?>(null);
        }
    }
}