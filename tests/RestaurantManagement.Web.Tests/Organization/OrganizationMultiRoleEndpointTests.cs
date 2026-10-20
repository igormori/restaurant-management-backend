using System.Net;
using System.Net.Http.Json;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Location and organization endpoints on a real PostgreSQL database for a user who holds several
/// roles. The service decides from every role stored for the user in the token organization, whatever
/// order the rows were stored in, and never counts a role held in another organization. The token
/// carries Admin so the endpoint's first filter passes and the service check is what is tested.
/// </summary>
public class OrganizationMultiRoleEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid CentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");

    // Carries the location and the organization settings, so one body passes validation on every endpoint below.
    private static readonly object Body = new
    {
        name = "Unidade Mercado Municipal",
        address = "Av. Sete de Setembro, 1865",
        city = "Curitiba",
        state = "PR",
        postalCode = "80060-070",
        country = "Brasil",
        phoneNumber = "+55 41 3000-3000",
        status = "Active",
        timeZone = "America/Sao_Paulo",
        code = "MERCADO",
        description = "Cozinha italiana de família",
        serviceChargeRate = 10m,
        discountApprovalLimit = 5m,
        businessDayCutoff = "04:00"
    };

    private readonly PostgresDatabaseFixture _postgres;

    public OrganizationMultiRoleEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync(params UserRole[] joaoRoles)
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(JoaoId, "joao@cantinadanonna.com.br", "João", joaoRoles);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, BatelId, "Unidade Batel", "BATEL", "America/Sao_Paulo");
        await factory.SeedLocationAsync(CantinaId, CentroId, "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        return factory;
    }

    // Scenario 33
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Rule", "OR31")]
    public async Task EditLocation_AdminForAllLocationsPlusWaiterAtBatelEditsCentro_ReturnsOkInEveryStorageOrder(bool adminStoredFirst)
    {
        // Arrange
        var admin = new UserRole { OrganizationId = CantinaId, LocationId = null, Role = Roles.Admin };
        var waiter = new UserRole { OrganizationId = CantinaId, LocationId = BatelId, Role = Roles.Waiter };
        await using var factory = adminStoredFirst
            ? await CreateFactoryAsync(admin, waiter)
            : await CreateFactoryAsync(waiter, admin);
        var client = factory.ClientFor(JoaoId, CantinaId, Roles.Admin);

        // Act
        var response = await client.PutAsJsonAsync($"/api/locations/{CentroId}", Body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an all-locations Admin may edit any location even when a Waiter role for another location is stored first");
    }

    // Scenario 34
    [Fact]
    [Trait("Rule", "OR32")]
    public async Task EditLocation_AdminAssignedOnlyToBatelEditsCentro_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(
            new UserRole { OrganizationId = CantinaId, LocationId = BatelId, Role = Roles.Admin });
        var client = factory.ClientFor(JoaoId, CantinaId, Roles.Admin);

        // Act
        var response = await client.PutAsJsonAsync($"/api/locations/{CentroId}", Body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "an Admin for one location may not edit another location");
    }

    // Scenario 35
    [Theory]
    [InlineData("POST", "/api/locations")]
    [InlineData("PUT", "/api/organizations/edit")]
    [Trait("Rule", "OR33")]
    public async Task OrganizationWideEndpoints_AdminAssignedOnlyToBatel_ReturnsForbidden(string method, string url)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(
            new UserRole { OrganizationId = CantinaId, LocationId = BatelId, Role = Roles.Admin });
        var client = factory.ClientFor(JoaoId, CantinaId, Roles.Admin);

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(Body) });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "an Admin for one location may not create locations or edit the organization");
    }

    // Scenario 36
    [Fact]
    [Trait("Rule", "OR34")]
    public async Task EditOrganization_AdminOfAnotherOrganizationPlusWaiterHere_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(
            new UserRole { OrganizationId = TrattoriaId, LocationId = null, Role = Roles.Admin },
            new UserRole { OrganizationId = CantinaId, LocationId = null, Role = Roles.Waiter });
        var client = factory.ClientFor(JoaoId, CantinaId, Roles.Admin);

        // Act
        var response = await client.PutAsJsonAsync("/api/organizations/edit", Body);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "an Admin role held in another organization grants nothing here");
    }
}
