using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Localization;

/// <summary>Every message the invitation and delegation rules refuse with reads as real text in English, Portuguese and French.</summary>
public class InvitationMessageKeysTests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public void Dispose() => CultureInfo.CurrentUICulture = _originalUiCulture;

    [Theory]
    [InlineData("en", "AlreadyMemberOfOrganization")]
    [InlineData("pt", "AlreadyMemberOfOrganization")]
    [InlineData("fr", "AlreadyMemberOfOrganization")]
    [InlineData("en", "InvitationLinkInvalid")]
    [InlineData("pt", "InvitationLinkInvalid")]
    [InlineData("fr", "InvitationLinkInvalid")]
    [InlineData("en", "InvitationNotFound")]
    [InlineData("pt", "InvitationNotFound")]
    [InlineData("fr", "InvitationNotFound")]
    [InlineData("en", "CannotChangeOwnAccess")]
    [InlineData("pt", "CannotChangeOwnAccess")]
    [InlineData("fr", "CannotChangeOwnAccess")]
    [InlineData("en", "CannotGrantAboveOwnLevel")]
    [InlineData("pt", "CannotGrantAboveOwnLevel")]
    [InlineData("fr", "CannotGrantAboveOwnLevel")]
    [InlineData("en", "InvitationForAnotherAccount")]
    [InlineData("pt", "InvitationForAnotherAccount")]
    [InlineData("fr", "InvitationForAnotherAccount")]
    [InlineData("en", "InvitationAccountExistsSignIn")]
    [InlineData("pt", "InvitationAccountExistsSignIn")]
    [InlineData("fr", "InvitationAccountExistsSignIn")]
    [Trait("Rule", "SH44")]
    public void Localizer_InvitationMessageKey_ReturnsRealTextInEachLanguage(string culture, string key)
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
