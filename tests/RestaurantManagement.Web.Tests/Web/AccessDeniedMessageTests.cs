using System.Net;
using System.Net.Http.Headers;
using RestaurantManagement.Shared;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// A signed-in user whose role lacks the permission gets the translated "access denied" message.
/// </summary>
public class AccessDeniedMessageTests : IDisposable
{
    private const string MembersUrl = "/api/test-probe/members";
    private const string EnglishAccessDeniedMessage = "You do not have permission to access this resource. Please contact your administrator if you believe this is an error.";
    private const string PortugueseAccessDeniedMessage = "Você não tem permissão para acessar este recurso. Entre em contato com o administrador se acreditar que isso é um erro.";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");

    private readonly PermissionProbeApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<HttpResponseMessage> WaiterGetsMembersAsync(string? language = null)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, MembersUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(_factory.Services, MariaId, CantinaId, Roles.Waiter));
        if (language is not null)
            request.Headers.Add("Accept-Language", language);
        return await client.SendAsync(request);
    }

    [Fact]
    [Trait("Rule", "W3")]
    public async Task MembersManagePolicy_WaiterWithoutPermission_ReturnsAccessDeniedMessageInEnglish()
    {
        // Arrange
        // Act
        var response = await WaiterGetsMembersAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a waiter does not hold the members permission");
        (await LocalizedResponses.MessageAsync(response)).ShouldBe(EnglishAccessDeniedMessage, "a signed-in user who is not allowed gets the translated access denied message");
    }

    [Fact]
    [Trait("Rule", "W3")]
    public async Task MembersManagePolicy_WaiterWithoutPermissionInPortuguese_ReturnsAccessDeniedMessageInPortuguese()
    {
        // Arrange
        // Act
        var response = await WaiterGetsMembersAsync("pt");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a waiter does not hold the members permission");
        (await LocalizedResponses.MessageAsync(response)).ShouldBe(PortugueseAccessDeniedMessage, "the access denied answer follows the caller's language");
    }
}
