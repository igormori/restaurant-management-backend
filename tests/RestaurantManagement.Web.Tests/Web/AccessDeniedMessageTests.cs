using System.Net;
using System.Net.Http.Headers;
using NSubstitute;
using RestaurantManagement.Shared.Auth;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// A signed-in user whose stored access lacks the level gets the translated "access denied" message.
/// </summary>
public class AccessDeniedMessageTests : IDisposable
{
    private const string MembersUrl = "/api/test-probe/members";
    private const string EnglishAccessDeniedMessage = "You do not have permission to access this resource. Please contact your administrator if you believe this is an error.";
    private const string PortugueseAccessDeniedMessage = "Você não tem permissão para acessar este recurso. Entre em contato com o administrador se acreditar que isso é um erro.";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");

    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");

    private readonly PermissionProbeApiFactory _factory = new();

    public AccessDeniedMessageTests()
    {
        // A Waiter: Menus Read and Orders Manage at Batel, nothing in Members.
        _factory.Organizations.GetNonClosedLocationIdsAsync(CantinaId).Returns(new List<Guid> { BatelId });
        _factory.MemberAccess.GetAccessGridAsync(MariaId, CantinaId).Returns(new AccessGridDto
        {
            Cells = new[]
            {
                new AccessCellDto { LocationId = BatelId, Module = AccessModule.Menus, Level = AccessLevel.Read },
                new AccessCellDto { LocationId = BatelId, Module = AccessModule.Orders, Level = AccessLevel.Manage }
            }
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<HttpResponseMessage> WaiterGetsMembersAsync(string? language = null)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, MembersUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(_factory.Services, MariaId, CantinaId));
        if (language is not null)
            request.Headers.Add("Accept-Language", language);
        return await client.SendAsync(request);
    }

    [Fact]
    [Trait("Rule", "W3")]
    public async Task MembersManagePolicy_WaiterWithoutMembersAccess_ReturnsAccessDeniedMessageInEnglish()
    {
        // Arrange
        // Act
        var response = await WaiterGetsMembersAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a waiter holds no level in the Members module");
        (await LocalizedResponses.MessageAsync(response)).ShouldBe(EnglishAccessDeniedMessage, "a signed-in user who is not allowed gets the translated access denied message");
    }

    [Fact]
    [Trait("Rule", "W3")]
    public async Task MembersManagePolicy_WaiterWithoutMembersAccessInPortuguese_ReturnsAccessDeniedMessageInPortuguese()
    {
        // Arrange
        // Act
        var response = await WaiterGetsMembersAsync("pt");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a waiter holds no level in the Members module");
        (await LocalizedResponses.MessageAsync(response)).ShouldBe(PortugueseAccessDeniedMessage, "the access denied answer follows the caller's language");
    }
}
