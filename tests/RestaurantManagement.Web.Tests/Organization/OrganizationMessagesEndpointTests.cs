using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using RestaurantManagement.Shared;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Organization create and edit field errors in the requested language (spec Outcomes #5 and #6).
/// Ana is Owner of Cantina da Nonna. Field errors are answered before any database work.
/// </summary>
public class OrganizationMessagesEndpointTests : IDisposable
{
    private const string CreateUrl = "/api/organizations/create";
    private const string EditUrl = "/api/organizations/edit";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");

    private readonly AuthApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient AnaSignedInToCantina()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(_factory.Services, AnaId, CantinaId, Roles.Owner));
        return client;
    }

    // Scenario 15
    [Theory]
    [InlineData("POST", CreateUrl, "pt",
        """{ "name": "Pizzaria Vila Madalena", "currency": "BRL", "primaryColor": "vermelho" }""",
        "O campo PrimaryColor deve estar no formato hexadecimal (#RRGGBB).")]
    [InlineData("POST", CreateUrl, "en",
        """{ "name": "Pizzaria Vila Madalena", "currency": "BRL", "primaryColor": "vermelho" }""",
        "PrimaryColor must be in hex format (#RRGGBB).")]
    [InlineData("PUT", EditUrl, "pt",
        """{ "name": "Cantina da Nonna", "accentColor": "#12345G", "serviceChargeRate": 10, "discountApprovalLimit": 15.5, "businessDayCutoff": "05:30" }""",
        "O campo AccentColor deve estar no formato hexadecimal (#RRGGBB).")]
    [InlineData("PUT", EditUrl, "en",
        """{ "name": "Cantina da Nonna", "accentColor": "#12345G", "serviceChargeRate": 10, "discountApprovalLimit": 15.5, "businessDayCutoff": "05:30" }""",
        "AccentColor must be in hex format (#RRGGBB).")]
    [Trait("Rule", "OR24")]
    public async Task OrganizationRequest_InvalidColor_ReturnsLocalizedHexColorError(string method, string url, string culture, string body, string expected)
    {
        // Arrange
        var client = AnaSignedInToCantina();
        var request = Request(method, url, culture, body);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "an invalid color is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the color error is in the language the user asked for");
    }

    // Scenario 16
    [Theory]
    [InlineData("pt", "O campo Name é obrigatório.")]
    [InlineData("en", "The Name field is required.")]
    [Trait("Rule", "OR24")]
    public async Task CreateOrganization_NameMissing_ReturnsLocalizedRequiredError(string culture, string expected)
    {
        // Arrange
        var client = AnaSignedInToCantina();
        var request = Request("POST", CreateUrl, culture, """{ "currency": "BRL" }""");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "an organization without a name is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the missing-name error is in the language the user asked for");
    }

    // Scenario 17
    [Theory]
    [InlineData("POST", CreateUrl,
        """{ "name": "Pizzaria Vila Madalena", "currency": "EUR" }""",
        "O campo Currency deve ser BRL, USD ou CAD.")]
    [InlineData("POST", CreateUrl,
        """{ "name": "Pizzaria Vila Madalena", "currency": "BRL", "logoUrl": "logo-pizzaria" }""",
        "O campo LogoUrl não é uma URL válida.")]
    [InlineData("PUT", EditUrl,
        """{ "name": "Cantina da Nonna", "serviceChargeRate": 150, "discountApprovalLimit": 15.5, "businessDayCutoff": "05:30" }""",
        "O campo ServiceChargeRate deve estar entre 0 e 100.")]
    [InlineData("PUT", EditUrl,
        """{ "name": "Cantina da Nonna", "serviceChargeRate": 10, "discountApprovalLimit": 12.345, "businessDayCutoff": "05:30" }""",
        "O campo DiscountApprovalLimit permite no máximo 2 casas decimais.")]
    [InlineData("PUT", EditUrl,
        """{ "name": "Cantina da Nonna", "serviceChargeRate": 10, "discountApprovalLimit": 15.5, "businessDayCutoff": "25:00" }""",
        "O campo BusinessDayCutoff deve estar no formato HH:mm.")]
    [Trait("Rule", "OR23")]
    public async Task OrganizationRequest_InvalidSettingInPortuguese_ReturnsPortugueseFieldError(string method, string url, string body, string expected)
    {
        // Arrange
        var client = AnaSignedInToCantina();
        var request = Request(method, url, "pt", body);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "an invalid setting is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the setting error is in Portuguese, the language asked for");
    }

    // Scenario 23
    [Theory]
    [InlineData("pt", "name", 201, "O campo Name deve ter no máximo 200 caracteres.")]
    [InlineData("en", "description", 1001, "The field Description must be a maximum of 1000 characters.")]
    [Trait("Rule", "OR24")]
    public async Task EditOrganization_TextOverMaximumLength_ReturnsLocalizedMaxLengthError(string culture, string field, int length, string expected)
    {
        // Arrange
        var client = AnaSignedInToCantina();
        var body = JsonNode.Parse("""{ "name": "Cantina da Nonna", "serviceChargeRate": 10, "discountApprovalLimit": 15.5, "businessDayCutoff": "05:30" }""")!.AsObject();
        body[field] = string.Concat(Enumerable.Repeat("Cozinha italiana de família. ", 40))[..length];
        var request = Request("PUT", EditUrl, culture, body.ToJsonString());

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a text longer than the maximum is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the length error is in the language the user asked for");
    }
}
