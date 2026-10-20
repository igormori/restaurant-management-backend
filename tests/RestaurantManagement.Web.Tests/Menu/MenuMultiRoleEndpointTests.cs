using System.Net;
using System.Net.Http.Json;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Menu endpoints on a real PostgreSQL database for a user who holds several roles. The service
/// decides from every role stored for the user in the token organization, whatever order the rows were
/// stored in, and never counts a role held in another organization. The token carries Admin so the
/// endpoint's first filter passes and the service check is what is tested.
/// </summary>
public class MenuMultiRoleEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");

    private readonly PostgresDatabaseFixture _postgres;

    public MenuMultiRoleEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync(params UserRole[] joaoRoles)
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(JoaoId, "joao@cantinadanonna.com.br", "João", joaoRoles);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, BatelId, "Unidade Batel", "BATEL", "America/Sao_Paulo");
        return factory;
    }

    private static readonly object NewMenu = new { name = "Almoço executivo", description = "Prato feito com arroz, feijão e bife" };

    // Scenario 20
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Rule", "M25")]
    public async Task CreateMenu_AdminForAllLocationsPlusWaiterAtBatel_ReturnsOkInEveryStorageOrder(bool adminStoredFirst)
    {
        // Arrange
        var admin = new UserRole { OrganizationId = CantinaId, LocationId = null, Role = Roles.Admin };
        var waiter = new UserRole { OrganizationId = CantinaId, LocationId = BatelId, Role = Roles.Waiter };
        await using var factory = adminStoredFirst
            ? await CreateFactoryAsync(admin, waiter)
            : await CreateFactoryAsync(waiter, admin);
        var client = factory.ClientFor(JoaoId, CantinaId, Roles.Admin);

        // Act
        var response = await client.PostAsJsonAsync("/api/menus/create", NewMenu);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an all-locations Admin may manage menus even when a Waiter role for one location is stored first");
    }

    // Scenario 21
    [Fact]
    [Trait("Rule", "M27")]
    public async Task CreateMenu_AdminAssignedOnlyToBatel_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(
            new UserRole { OrganizationId = CantinaId, LocationId = BatelId, Role = Roles.Admin });
        var client = factory.ClientFor(JoaoId, CantinaId, Roles.Admin);

        // Act
        var response = await client.PostAsJsonAsync("/api/menus/create", NewMenu);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "an Admin for one location may not manage the organization's menus");
    }

    // Scenario 22
    [Fact]
    [Trait("Rule", "M28")]
    public async Task CreateMenu_AdminOfAnotherOrganizationPlusWaiterHere_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(
            new UserRole { OrganizationId = TrattoriaId, LocationId = null, Role = Roles.Admin },
            new UserRole { OrganizationId = CantinaId, LocationId = null, Role = Roles.Waiter });
        var client = factory.ClientFor(JoaoId, CantinaId, Roles.Admin);

        // Act
        var response = await client.PostAsJsonAsync("/api/menus/create", NewMenu);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "an Admin role held in another organization grants nothing here");
    }
}
