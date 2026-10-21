using System.Net;
using Shouldly;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;
using static RestaurantManagement.Web.Tests.RoleWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Reading roles needs some access. Caio is a member of Cantina da Nonna whose grid is all None.
/// </summary>
public class RoleReadAccessEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _postgres;

    public RoleReadAccessEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "T132")]
    public async Task ListRoles_MemberWithNoAccessAnywhere_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await factory.SeedUserAsync(CaioId, "caio@cantinadanonna.com.br", "Caio", TenantApiFactory.MemberOf(CantinaId));
        var client = factory.ClientFor(CaioId, CantinaId);

        // Act
        var response = await client.GetAsync("/api/roles");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a member with every cell at None may not list the roles");
    }

    [Fact]
    [Trait("Rule", "T133")]
    public async Task GetRole_MemberWithNoAccessAnywhere_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await factory.SeedUserAsync(CaioId, "caio@cantinadanonna.com.br", "Caio", TenantApiFactory.MemberOf(CantinaId));
        var roleId = await RoleIdAsync(factory, CantinaId, "Waiter");
        var client = factory.ClientFor(CaioId, CantinaId);

        // Act
        var response = await client.GetAsync($"/api/roles/{roleId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a member with every cell at None may not read a role");
    }
}
