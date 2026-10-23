using System.Net;
using System.Net.Http.Headers;
using RestaurantManagement.Shared;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Organization endpoints refuse a pass whose user identifier is not a user ID with the translated
/// message, as the location endpoints do. Ana owns Cantina da Nonna.
/// </summary>
public class OrganizationUserIdentifierEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string OrganizationsUrl = "/api/organizations";
    private const string CreateUrl = "/api/organizations/create";
    private const string EditUrl = "/api/organizations/edit";

    private const string CreateJson = """{ "name": "Trattoria do Sol", "currency": "BRL" }""";

    private const string EditJson = """
        {
          "name": "Cantina da Nonna",
          "serviceChargeRate": 10,
          "discountApprovalLimit": 15,
          "businessDayCutoff": "04:00"
        }
        """;

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");

    private readonly PostgresDatabaseFixture _postgres;

    public OrganizationUserIdentifierEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Theory]
    [InlineData("GET", OrganizationsUrl, null, "pt", "Usuário não autorizado!")]
    [InlineData("POST", CreateUrl, CreateJson, "en", "Unauthorized user.")]
    [InlineData("PUT", EditUrl, EditJson, "pt", "Usuário não autorizado!")]
    [Trait("Rule", "OR13")]
    public async Task OrganizationEndpoints_UserIdentifierNotAUserId_ReturnsUnauthorizedWithLocalizedMessage(string method, string url, string? body, string culture, string expected)
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
