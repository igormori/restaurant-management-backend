using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Shared;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Menu and location links on a real PostgreSQL database: a location of another organization
/// is never found, so it can be neither read nor linked.
/// Cantina da Nonna and Trattoria do Sol each have a location and a menu.
/// </summary>
public class MenuLocationEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid CantinaCentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid TrattoriaCentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
    private static readonly Guid CantinaDinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");

    private readonly PostgresDatabaseFixture _postgres;

    public MenuLocationEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            new UserRole { OrganizationId = CantinaId, Role = Roles.Owner });
        await factory.SeedUserAsync(JoaoId, "joao@trattoriadosol.com.br", "João",
            new UserRole { OrganizationId = TrattoriaId, Role = Roles.Waiter });
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, CantinaCentroId, "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        await factory.SeedLocationAsync(TrattoriaId, TrattoriaCentroId, "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        await factory.SeedMenuAsync(CantinaId, CantinaDinnerMenuId, "Jantar");
        return factory;
    }

    private sealed record MenuBody(Guid Id, bool IsActive, List<Guid> LocationIds);

    // Scenario 45
    [Fact]
    [Trait("Rule", "M12")]
    public async Task GetMenusByLocation_LocationOfAnotherOrganization_ReturnsNotFoundWithoutTheMenu()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        await factory.InOrganizationAsync<MenuDbContext, bool>(CantinaId, async db =>
        {
            db.LocationMenus.Add(new LocationMenu
            {
                Id = Guid.NewGuid(),
                OrganizationId = CantinaId,
                MenuId = CantinaDinnerMenuId,
                LocationId = CantinaCentroId,
                IsActive = true
            });
            await db.SaveChangesAsync();
            return true;
        });
        var client = factory.ClientFor(JoaoId, TrattoriaId, Roles.Waiter);

        // Act
        var response = await client.GetAsync($"/api/menus/location/{CantinaCentroId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "another organization's location is answered as if it did not exist, never 403");
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("Jantar", customMessage: "the other organization's menu is never revealed");
        body.ShouldNotContain(CantinaDinnerMenuId.ToString(), customMessage: "the other organization's menu ID is never revealed");
    }

    // Scenario 46
    [Fact]
    [Trait("Rule", "M13")]
    public async Task AttachMenuToLocation_LocationOfAnotherOrganization_ReturnsNotFoundAndStoresNoLink()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.PostAsync($"/api/menus/{CantinaDinnerMenuId}/locations/{TrattoriaCentroId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "another organization's location is answered as if it did not exist");
        var links = await factory.InOrganizationAsync<MenuDbContext, int>(CantinaId,
            db => db.LocationMenus.IgnoreQueryFilters().CountAsync(lm => lm.LocationId == TrattoriaCentroId));
        links.ShouldBe(0, "a menu is never linked to another organization's location");
    }

    // Scenario 47
    [Fact]
    [Trait("Rule", "M16")]
    public async Task CreateMenu_LocationIdsOfTwoOrganizations_LinksOnlyTheTokenOrganizationsLocation()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.PostAsJsonAsync("/api/menus/create", new
        {
            name = "Almoço executivo",
            description = "Prato feito com arroz, feijão e bife",
            locationIds = new[] { CantinaCentroId, TrattoriaCentroId }
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var menu = await response.Content.ReadFromJsonAsync<MenuBody>();
        menu!.IsActive.ShouldBeTrue("a new menu is stored active");
        menu.LocationIds.ShouldBe(new[] { CantinaCentroId }, ignoreOrder: false,
            customMessage: "only the token organization's location is linked; another organization's location ID is ignored");
    }
}
