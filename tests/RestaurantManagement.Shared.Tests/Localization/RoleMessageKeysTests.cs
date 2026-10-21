using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Localization;

/// <summary>The two messages a refused role name answers with read as real text in English and Portuguese.</summary>
public class RoleMessageKeysTests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public void Dispose() => CultureInfo.CurrentUICulture = _originalUiCulture;

    [Theory]
    [InlineData("en", "RoleNameAlreadyUsed")]
    [InlineData("pt", "RoleNameAlreadyUsed")]
    [InlineData("en", "RoleNameInvalid")]
    [InlineData("pt", "RoleNameInvalid")]
    [Trait("Rule", "SH46")]
    public void Localizer_RoleNameMessageKey_ReturnsRealTextInEachLanguage(string culture, string key)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Localization");
        var localizer = services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResource>>();
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        // Act
        var message = localizer[key];

        // Assert
        message.ResourceNotFound.ShouldBeFalse($"the key {key} must exist in the {culture} catalog");
        message.Value.ShouldNotBe(key, "the client gets a translated sentence, never the key itself");
    }
}
