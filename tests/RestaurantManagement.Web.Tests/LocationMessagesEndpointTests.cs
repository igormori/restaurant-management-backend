using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Location messages in the requested language: field errors on create and edit (spec Outcome #6)
/// and a sign-in pass whose user identifier is not a user ID (spec Outcome #8).
/// Ana is Owner of Cantina da Nonna, which has the Batel location.
/// </summary>
public class LocationMessagesEndpointTests : IClassFixture<AuditFieldsPostgresFixture>
{
    private const string LocationsUrl = "/api/locations";
    private const string BatelUrl = "/api/locations/33333333-cccc-4ccc-8ccc-000000000001";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid BatelId = Guid.Parse("33333333-cccc-4ccc-8ccc-000000000001");

    private const string ValidLocationJson = """
        {
          "name": "Unidade Batel",
          "address": "Avenida do Batel, 1550",
          "city": "Curitiba",
          "state": "PR",
          "postalCode": "80420-090",
          "country": "Brasil",
          "phoneNumber": "+55 41 3333-4444",
          "timeZone": "America/Sao_Paulo",
          "type": "Store",
          "code": "BATEL"
        }
        """;

    private readonly AuditFieldsPostgresFixture _postgres;

    public LocationMessagesEndpointTests(AuditFieldsPostgresFixture postgres) => _postgres = postgres;

    /// <summary>A valid location body with one field replaced.</summary>
    private static string LocationBodyWith(string field, string? value)
    {
        var body = JsonNode.Parse(ValidLocationJson)!.AsObject();
        body[field] = value;
        return body.ToJsonString();
    }

    private static HttpClient SignedInClient(AuthApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Scenario 18
    [Theory]
    [InlineData("POST", LocationsUrl, "pt", "name", null, "O campo Name é obrigatório.")]
    [InlineData("PUT", BatelUrl, "pt", "type", "Warehouse", "O campo Type deve ser Store ou CentralKitchen.")]
    [InlineData("POST", LocationsUrl, "pt", "code", "CTB-01", "O campo Code aceita apenas letras e números.")]
    [InlineData("PUT", BatelUrl, "pt", "timeZone", "Brasilia", "O campo TimeZone deve ser um fuso horário IANA válido.")]
    [InlineData("POST", LocationsUrl, "en", "postalCode", "80020-310-0000-000000", "The field PostalCode must be a maximum of 20 characters.")]
    public async Task LocationRequest_InvalidField_ReturnsLocalizedFieldError(string method, string url, string culture, string field, string? value, string expected)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = SignedInClient(factory, TestAccessTokens.Create(factory.Services, AnaId, CantinaId, Roles.Owner));
        var request = Request(method, url, culture, LocationBodyWith(field, value));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorsAsync(response)).ShouldContain(expected);
    }

    // Scenario 19
    [Theory]
    [InlineData("GET", LocationsUrl, "pt", "Usuário não autorizado!")]
    [InlineData("POST", LocationsUrl, "en", "Unauthorized user.")]
    [InlineData("PUT", BatelUrl, "pt", "Usuário não autorizado!")]
    [InlineData("DELETE", BatelUrl, "en", "Unauthorized user.")]
    public async Task LocationEndpoints_UserIdentifierNotAUserId_ReturnsUnauthorizedWithLocalizedMessage(string method, string url, string culture, string expected)
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            new UserRole { OrganizationId = CantinaId, Role = Roles.Owner });
        await factory.SeedLocationAsync(CantinaId, BatelId, "Unidade Batel", "BATEL", "America/Sao_Paulo");
        var token = TestAccessTokens.CreateWithSubject(factory.Services, "caixa-batel-01", CantinaId, Roles.Owner);
        var client = SignedInClient(factory, token);
        var request = Request(method, url, culture, ValidLocationJson);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await MessageAsync(response)).ShouldBe(expected);
    }
}
