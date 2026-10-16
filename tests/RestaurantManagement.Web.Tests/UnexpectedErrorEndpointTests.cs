using System.Net;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// An unexpected failure behind a real endpoint answers 500 in the language the client asked for
/// (spec Outcome #7). The sign-in service is a substitute, so it can be made to fail on purpose.
/// </summary>
public class UnexpectedErrorEndpointTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // Scenario 6
    [Fact]
    public async Task Login_UnexpectedFailureInPortuguese_ReturnsServerErrorWithPortugueseMessage()
    {
        // Arrange
        _factory.SessionService.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<ClientInfo>())
            .Returns(Task.FromException<SessionResult>(new InvalidOperationException("Session store unavailable")));
        var client = _factory.CreateClient();
        var request = Request("POST", "/api/auth/login", "pt", new { email = "ana@cantinadanonna.com.br", password = "Massa@Fresca2026" });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await MessageAsync(response)).ShouldBe("Ocorreu um erro inesperado.");
    }
}
