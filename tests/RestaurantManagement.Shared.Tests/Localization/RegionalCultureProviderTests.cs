using Microsoft.AspNetCore.Http;
using RestaurantManagement.Shared.Utils.Localization;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Localization;

/// <summary>The language the provider picks from the request is always one the app supports.</summary>
public class RegionalCultureProviderTests
{
    [Theory]
    [InlineData("pt-BR", "pt")]
    [InlineData("pt-PT", "pt")]
    [InlineData("fr-CA", "fr")]
    [InlineData("fr", "fr")]
    [Trait("Rule", "SH19")]
    public async Task DetermineProviderCultureResult_RegionalLanguage_ReturnsSupportedBaseLanguage(string header, string expected)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept-Language"] = header;

        // Act
        var result = await new RegionalCultureProvider().DetermineProviderCultureResult(context);

        // Assert
        result.ShouldNotBeNull("a language with a catalog is picked");
        result.Cultures[0].Value.ShouldBe(expected, "a regional variant maps to its base language");
        RegionalCultureProvider.SupportedCultures.Select(culture => culture.Name)
            .ShouldContain(expected, "the picked language must be one the app supports");
    }

    [Fact]
    [Trait("Rule", "SH19")]
    public async Task DetermineProviderCultureResult_EveryCatalogLanguage_IsMappedBySupportedCultures()
    {
        // Arrange
        var baseLanguages = RegionalCultureProvider.SupportedCultures
            .Select(culture => culture.Name)
            .Where(name => name != "en" && !name.Contains('-'));

        // Act
        var picked = new List<string?>();
        foreach (var language in baseLanguages)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["Accept-Language"] = language;
            picked.Add((await new RegionalCultureProvider().DetermineProviderCultureResult(context))?.Cultures[0].Value);
        }

        // Assert
        picked.ShouldBe(baseLanguages, "every supported language with a catalog is picked from the request");
    }
}
