using System.Net;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The X-Location-Id header through the real pipeline (Rule 4), on GET /api/menus.
/// Cantina da Nonna has Batel and Mercado Municipal; Trattoria do Sol has Centro.
/// Bia is a Waiter assigned only to Batel; Ana is a Manager of all Cantina locations.
/// </summary>
public class LocationAccessEndpointTests : IClassFixture<AuditFieldsPostgresFixture>
{
    private const string Batel = "44444444-dddd-4ddd-8ddd-000000000001";
    private const string MercadoMunicipal = "44444444-dddd-4ddd-8ddd-000000000002";
    private const string TrattoriaCentro = "44444444-dddd-4ddd-8ddd-000000000003";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid BiaId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");

    private readonly AuditFieldsPostgresFixture _postgres;

    public LocationAccessEndpointTests(AuditFieldsPostgresFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, Guid.Parse(Batel), "Unidade Batel", "BATEL", "America/Sao_Paulo");
        await factory.SeedLocationAsync(CantinaId, Guid.Parse(MercadoMunicipal), "Unidade Mercado Municipal", "MERCADO", "America/Sao_Paulo");
        await factory.SeedLocationAsync(TrattoriaId, Guid.Parse(TrattoriaCentro), "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        await factory.SeedUserAsync(BiaId, "bia@cantinadanonna.com.br", "Bia",
            new UserRole { OrganizationId = CantinaId, LocationId = Guid.Parse(Batel), Role = Roles.Waiter });
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            new UserRole { OrganizationId = CantinaId, LocationId = null, Role = Roles.Manager });
        return factory;
    }

    // Scenario 38
    [Fact]
    public async Task GetMenus_WaiterAssignedOnlyToBatelSelectsMercadoMunicipal_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(BiaId, CantinaId, Roles.Waiter, locationHeader: MercadoMunicipal);

        // Act
        var response = await client.GetAsync("/api/menus");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Scenario 39
    [Theory]
    [InlineData(Batel)]
    [InlineData(MercadoMunicipal)]
    public async Task GetMenus_ManagerOfAllLocationsSelectsAnyCantinaLocation_ReturnsOk(string location)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Manager, locationHeader: location);

        // Act
        var response = await client.GetAsync("/api/menus");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Scenario 40
    [Fact]
    public async Task GetMenus_ManagerOfAllLocationsSelectsAnotherOrganizationsLocation_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Manager, locationHeader: TrattoriaCentro);

        // Act
        var response = await client.GetAsync("/api/menus");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
