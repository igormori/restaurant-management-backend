using System.Net;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Registration and login field errors in the requested language (spec Outcomes #3 and #6).
/// Field errors are answered before the sign-in services run, so they stay substituted.
/// </summary>
public class IdentityMessagesEndpointTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // Scenario 13
    [Theory]
    [InlineData("pt", "O campo PhoneNumber não é um número de telefone válido.")]
    [InlineData("en", "The PhoneNumber field is not a valid phone number.")]
    public async Task Register_InvalidPhoneNumber_ReturnsLocalizedPhoneError(string culture, string expected)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = Request("POST", "/api/auth/register", culture, new
        {
            email = "joana@padariasaojorge.com.br",
            password = "Pao@Queijo2026",
            firstName = "Joana",
            lastName = "Pereira",
            phoneNumber = "+55 41 ABCD-EFGH"
        });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorsAsync(response)).ShouldContain(expected);
    }

    // Scenario 14
    [Theory]
    [InlineData("pt", """{ "password": "Massa@Fresca2026" }""", "O campo Email é obrigatório.")]
    [InlineData("pt", """{ "email": "ana.cantinadanonna.com.br", "password": "Massa@Fresca2026" }""", "O campo Email não é um endereço de email válido.")]
    [InlineData("pt", """{ "email": "ana@cantinadanonna.com.br", "password": "Massa@1" }""", "O campo Password deve ter pelo menos 8 caracteres.")]
    [InlineData("en", """{ "email": "ana.cantinadanonna.com.br", "password": "Massa@Fresca2026" }""", "The Email field is not a valid email address.")]
    public async Task Login_InvalidField_ReturnsLocalizedFieldError(string culture, string body, string expected)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = Request("POST", "/api/auth/login", culture, body);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorsAsync(response)).ShouldContain(expected);
    }
}
