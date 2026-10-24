using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace RestaurantManagement.Shared.Utils.Localization
{
    public class RegionalCultureProvider : RequestCultureProvider
    {
        public static readonly CultureInfo[] SupportedCultures =
        [
            new("en"),
            new("pt"),
            new("pt-BR"),
            new("pt-PT"),
            new("fr"),
            new("fr-CA")
        ];

        // Every supported language other than the default has its own catalog file
        private static readonly string[] LanguagesWithCatalog = SupportedCultures
            .Where(culture => culture.Name != "en" && !culture.Name.Contains('-'))
            .Select(culture => culture.Name)
            .ToArray();

        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            if (httpContext.Request.Headers.TryGetValue("Accept-Language", out StringValues langs))
            {
                var lang = langs.ToString().ToLowerInvariant();

                // Always map any pt-* or fr-* variant to its base language, the only one with a catalog
                foreach (var baseLanguage in LanguagesWithCatalog)
                {
                    if (lang.StartsWith(baseLanguage, StringComparison.Ordinal))
                        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(baseLanguage, baseLanguage));
                }
            }

            return Task.FromResult<ProviderCultureResult?>(null);
        }
    }
}