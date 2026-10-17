using System.Net;
using System.Text.Json.Nodes;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The title of a validation (400) response is the catalog message in the requested language,
/// while status and field errors keep their usual shape.
/// </summary>
public class ValidationTitleEndpointTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static object InvalidRegistration => new
    {
        email = "joana@padariasaojorge.com.br",
        password = "Pao@Queijo2026",
        firstName = "Joana",
        lastName = "Pereira",
        phoneNumber = "+55 41 ABCD-EFGH"
    };

    [Theory]
    [InlineData("pt", "Falha na validação. Verifique os campos informados.")]
    [InlineData("en", "Validation failed. Please check the input fields.")]
    [Trait("Rule", "W22")]
    public async Task Register_InvalidRequest_ReturnsLocalizedValidationTitle(string culture, string expected)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = Request("POST", "/api/auth/register", culture, InvalidRegistration);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "invalid input must be answered as a validation error");
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["title"]!.GetValue<string>().ShouldBe(expected, "the validation title is the catalog message in the caller's language");
    }

    [Fact]
    [Trait("Rule", "W22")]
    public async Task Register_InvalidRequest_KeepsStatusAndFieldErrorsInBody()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = Request("POST", "/api/auth/register", "en", InvalidRegistration);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["status"]!.GetValue<int>().ShouldBe(400, "the body still carries the 400 status");
        body["errors"]!.AsObject().ContainsKey("PhoneNumber").ShouldBeTrue("the body still lists the field that failed validation");
    }
}
