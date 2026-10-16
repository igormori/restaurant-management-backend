using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Localization;

/// <summary>
/// The shared message catalog read through the same localizer the app uses (resource files under
/// Localization/). Every new message must read as real text in English (en) and Portuguese (pt).
/// </summary>
public class SharedResourceCatalogTests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public void Dispose() => CultureInfo.CurrentUICulture = _originalUiCulture;

    private static IStringLocalizer<SharedResource> CreateLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Localization");
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResource>>();
    }

    // Scenario 1
    [Theory]
    [InlineData("en", "MenuNotFound", "Menu not found.")]
    [InlineData("pt", "MenuNotFound", "Menu não encontrado.")]
    [InlineData("en", "MenuCategoryNotFound", "Menu category not found.")]
    [InlineData("pt", "MenuCategoryNotFound", "Categoria do menu não encontrada.")]
    [InlineData("en", "PhoneAttribute_ValidationError", "The {0} field is not a valid phone number.")]
    [InlineData("pt", "PhoneAttribute_ValidationError", "O campo {0} não é um número de telefone válido.")]
    [InlineData("en", "UnexpectedError", "An unexpected error occurred.")]
    [InlineData("pt", "UnexpectedError", "Ocorreu um erro inesperado.")]
    [InlineData("en", "HexColor_ValidationError", "{0} must be in hex format (#RRGGBB).")]
    [InlineData("pt", "HexColor_ValidationError", "O campo {0} deve estar no formato hexadecimal (#RRGGBB).")]
    [InlineData("en", "Currency_ValidationError", "{0} must be BRL, USD or CAD.")]
    [InlineData("pt", "Currency_ValidationError", "O campo {0} deve ser BRL, USD ou CAD.")]
    [InlineData("en", "RangeAttribute_ValidationError", "{0} must be between {1} and {2}.")]
    [InlineData("pt", "RangeAttribute_ValidationError", "O campo {0} deve estar entre {1} e {2}.")]
    [InlineData("en", "TwoDecimals_ValidationError", "{0} allows at most 2 decimals.")]
    [InlineData("pt", "TwoDecimals_ValidationError", "O campo {0} permite no máximo 2 casas decimais.")]
    [InlineData("en", "TimeFormat_ValidationError", "{0} must be in HH:mm format.")]
    [InlineData("pt", "TimeFormat_ValidationError", "O campo {0} deve estar no formato HH:mm.")]
    [InlineData("en", "LocationType_ValidationError", "{0} must be Store or CentralKitchen.")]
    [InlineData("pt", "LocationType_ValidationError", "O campo {0} deve ser Store ou CentralKitchen.")]
    [InlineData("en", "LettersAndDigits_ValidationError", "{0} allows letters and digits only.")]
    [InlineData("pt", "LettersAndDigits_ValidationError", "O campo {0} aceita apenas letras e números.")]
    [InlineData("en", "IanaTimeZone_ValidationError", "{0} must be a valid IANA time zone name.")]
    [InlineData("pt", "IanaTimeZone_ValidationError", "O campo {0} deve ser um fuso horário IANA válido.")]
    [InlineData("en", "UrlAttribute_ValidationError", "The {0} field is not a valid URL.")]
    [InlineData("pt", "UrlAttribute_ValidationError", "O campo {0} não é uma URL válida.")]
    public void Localizer_NewMessageKey_ReturnsExpectedTextInEachLanguage(string culture, string key, string expected)
    {
        // Arrange
        var localizer = CreateLocalizer();
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        // Act
        var message = localizer[key];

        // Assert
        message.ResourceNotFound.ShouldBeFalse();
        message.Value.ShouldBe(expected);
    }

    // Scenario 2
    [Theory]
    [InlineData("pt-BR", "Menu não encontrado.")]
    [InlineData("fr", "Menu not found.")]
    public void Localizer_RegionalOrUnsupportedCulture_ReturnsPortugueseOrEnglishFallback(string culture, string expected)
    {
        // Arrange
        var localizer = CreateLocalizer();
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        // Act
        var message = localizer["MenuNotFound"];

        // Assert
        message.Value.ShouldBe(expected);
    }

    // Scenario 3
    [Theory]
    [InlineData("en", "UnauthorizedMessage", "Unauthorized user.")]
    [InlineData("pt", "UnauthorizedMessage", "Usuário não autorizado!")]
    [InlineData("en", "RequiredAttribute_ValidationError", "The {0} field is required.")]
    [InlineData("pt", "RequiredAttribute_ValidationError", "O campo {0} é obrigatório.")]
    [InlineData("en", "MaxLengthAttribute_ValidationError", "The field {0} must be a maximum of {1} characters.")]
    [InlineData("pt", "MaxLengthAttribute_ValidationError", "O campo {0} deve ter no máximo {1} caracteres.")]
    public void Localizer_ExistingMessageKey_KeepsTodaysText(string culture, string key, string expected)
    {
        // Arrange
        var localizer = CreateLocalizer();
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        // Act
        var message = localizer[key];

        // Assert
        message.Value.ShouldBe(expected);
    }
}
