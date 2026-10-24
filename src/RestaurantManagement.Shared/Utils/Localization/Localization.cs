using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace RestaurantManagement.Shared.Utils.Localization
{
    public class RegionalCultureProvider : RequestCultureProvider
    {
        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            if (httpContext.Request.Headers.TryGetValue("Accept-Language", out StringValues langs))
            {
                var lang = langs.ToString().ToLowerInvariant();

                // Always map any pt-* or fr-* variant to its base language, the only one with a catalog
                foreach (var baseLanguage in new[] { "pt", "fr" })
                {
                    if (lang.StartsWith(baseLanguage))
                        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(baseLanguage, baseLanguage));
                }
            }

            return Task.FromResult<ProviderCultureResult?>(null);
        }
    }
}