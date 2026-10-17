using System.Net;
using RestaurantManagement.Shared;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// A missing, null or malformed ID in a request body is answered with a field error in the requested language.
/// </summary>
public class RequiredIdFieldEndpointTests : IDisposable
{
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");

    private readonly AuthApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient SignedInClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(_factory.Services, AnaId, CantinaId, Roles.Owner));
        return client;
    }

    [Theory]
    [InlineData("/api/auth/select-organization", "{}", "OrganizationId", "pt", "O campo OrganizationId é obrigatório.")]
    [InlineData("/api/auth/select-organization", "{}", "OrganizationId", "en", "The OrganizationId field is required.")]
    [InlineData("/api/auth/select-organization", """{ "organizationId": null }""", "OrganizationId", "pt", "O campo OrganizationId é obrigatório.")]
    [InlineData("/api/auth/select-organization", """{ "organizationId": null }""", "OrganizationId", "en", "The OrganizationId field is required.")]
    [InlineData("/api/menu-categories", """{ "name": "Massas" }""", "MenuId", "pt", "O campo MenuId é obrigatório.")]
    [InlineData("/api/menu-categories", """{ "name": "Massas" }""", "MenuId", "en", "The MenuId field is required.")]
    [InlineData("/api/menu-categories", """{ "menuId": null, "name": "Massas" }""", "MenuId", "pt", "O campo MenuId é obrigatório.")]
    [InlineData("/api/menu-categories", """{ "menuId": null, "name": "Massas" }""", "MenuId", "en", "The MenuId field is required.")]
    [Trait("Rule", "M23")]
    [Trait("Rule", "S2")]
    public async Task IdMissingOrNull_ReturnsLocalizedRequiredError(string url, string body, string field, string culture, string expected)
    {
        // Arrange
        var client = SignedInClient();
        var request = Request("POST", url, culture, body);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a missing or null ID is refused with a 400");
        (await FieldErrorsAsync(response, field)).ShouldContain(expected, "the error must name the missing ID field in the requested language");
    }

    [Theory]
    [InlineData("/api/auth/select-organization", """{ "organizationId": "cantina" }""", "pt", "O campo OrganizationId é obrigatório.")]
    [InlineData("/api/auth/select-organization", """{ "organizationId": "cantina" }""", "en", "The OrganizationId field is required.")]
    [InlineData("/api/menu-categories", """{ "menuId": "jantar", "name": "Massas" }""", "pt", "O campo MenuId é obrigatório.")]
    [InlineData("/api/menu-categories", """{ "menuId": "jantar", "name": "Massas" }""", "en", "The MenuId field is required.")]
    [Trait("Rule", "M23")]
    [Trait("Rule", "S2")]
    public async Task IdNotAnId_ReturnsLocalizedRequiredError(string url, string body, string culture, string expected)
    {
        // Arrange
        var client = SignedInClient();
        var request = Request("POST", url, culture, body);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a value that is not an ID is refused with a 400");
        (await FieldErrorsAsync(response)).ShouldContain(expected, "the error must say the ID is required, in the requested language");
    }
}
