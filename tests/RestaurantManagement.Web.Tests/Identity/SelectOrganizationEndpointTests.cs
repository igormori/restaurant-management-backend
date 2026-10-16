using System.Net;
using System.Net.Http.Json;
using RestaurantManagement.Modules.Identity.Models;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

public class SelectOrganizationEndpointTests : IDisposable
{
    private const string SelectOrganizationUrl = "/api/auth/select-organization";
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");

    private readonly AuthApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    // Scenario 31
    [Fact]
    [Trait("Rule", "S1")]
    public async Task SelectOrganization_NoAuthenticationToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new SelectOrganizationRequest { OrganizationId = CantinaId };

        // Act
        var response = await client.PostAsJsonAsync(SelectOrganizationUrl, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "only a signed-in person can choose an organization");
    }
}
