using System.Net;
using System.Net.Http.Headers;
using RestaurantManagement.Shared;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Menu and menu category endpoints refuse a pass whose user identifier is not a user ID with the
/// translated message, as the location endpoints do. Ana owns Cantina da Nonna.
/// </summary>
public class MenuUserIdentifierEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string MenuId = "55555555-0000-4000-8000-000000000001";
    private const string LocationId = "33333333-cccc-4ccc-8ccc-000000000001";
    private const string CategoryId = "66666666-0000-4000-8000-000000000001";

    private const string MenuJson = """{ "name": "Jantar", "description": "Menu do jantar", "isActive": true }""";
    private const string CategoryJson = """{ "menuId": "55555555-0000-4000-8000-000000000001", "name": "Massas" }""";
    private const string CategoryUpdateJson = """{ "name": "Massas" }""";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");

    private readonly PostgresDatabaseFixture _postgres;

    public MenuUserIdentifierEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Theory]
    [InlineData("POST", "/api/menus/create", MenuJson, "pt", "Usuário não autorizado!")]
    [InlineData("PUT", $"/api/menus/{MenuId}", MenuJson, "en", "Unauthorized user.")]
    [InlineData("DELETE", $"/api/menus/{MenuId}", null, "pt", "Usuário não autorizado!")]
    [InlineData("POST", $"/api/menus/{MenuId}/locations/{LocationId}", null, "en", "Unauthorized user.")]
    [InlineData("DELETE", $"/api/menus/{MenuId}/locations/{LocationId}", null, "pt", "Usuário não autorizado!")]
    [InlineData("GET", "/api/menus", null, "en", "Unauthorized user.")]
    [InlineData("GET", $"/api/menus/location/{LocationId}", null, "pt", "Usuário não autorizado!")]
    [Trait("Rule", "M44")]
    public async Task MenuEndpoints_UserIdentifierNotAUserId_ReturnsUnauthorizedWithLocalizedMessage(string method, string url, string? body, string culture, string expected)
        => await AssertUnauthorizedAsync(method, url, body, culture, expected);

    [Theory]
    [InlineData("POST", "/api/menu-categories", CategoryJson, "en", "Unauthorized user.")]
    [InlineData("GET", $"/api/menu-categories/{CategoryId}", null, "pt", "Usuário não autorizado!")]
    [InlineData("GET", $"/api/menus/{MenuId}/categories", null, "en", "Unauthorized user.")]
    [InlineData("GET", "/api/menu-categories", null, "pt", "Usuário não autorizado!")]
    [InlineData("GET", $"/api/locations/{LocationId}/menu-categories", null, "en", "Unauthorized user.")]
    [InlineData("PUT", $"/api/menu-categories/{CategoryId}", CategoryUpdateJson, "pt", "Usuário não autorizado!")]
    [InlineData("DELETE", $"/api/menu-categories/{CategoryId}", null, "en", "Unauthorized user.")]
    [Trait("Rule", "M44")]
    public async Task MenuCategoryEndpoints_UserIdentifierNotAUserId_ReturnsUnauthorizedWithLocalizedMessage(string method, string url, string? body, string culture, string expected)
        => await AssertUnauthorizedAsync(method, url, body, culture, expected);

    private async Task AssertUnauthorizedAsync(string method, string url, string? body, string culture, string expected)
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            TenantApiFactory.OwnerOf(CantinaId));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.CreateWithSubject(factory.Services, "caixa-batel-01", CantinaId));
        var request = Request(method, url, culture, body);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a token whose user identifier is not a user ID is refused");
        (await MessageAsync(response)).ShouldBe(expected, "the refusal is in the language the user asked for");
    }
}
