using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Shared;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Menu endpoints on a real PostgreSQL database: the organization comes only from the sign-in token
/// (Rule 3) and another organization's menus and categories answer 404 (Rule 5).
/// Ana is Owner of both Cantina da Nonna and Trattoria do Sol, and is signed in to Cantina.
/// </summary>
public class MenuTenantEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string TrattoriaCentro = "44444444-dddd-4ddd-8ddd-000000000003";
    private const string Cantina = "a1a1a1a1-0000-4000-8000-000000000001";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse(Cantina);
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid TrattoriaCentroId = Guid.Parse(TrattoriaCentro);
    private static readonly Guid CantinaDinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaLunchMenuId = Guid.Parse("55555555-0000-4000-8000-000000000002");
    private static readonly Guid CantinaPastaCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaPizzaCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000002");

    private readonly PostgresDatabaseFixture _postgres;

    public MenuTenantEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private sealed record Row(Guid Id, Guid OrganizationId);

    private async Task<TenantApiFactory> CreateFactoryWithTwoOrganizationsAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            new UserRole { OrganizationId = CantinaId, Role = Roles.Owner },
            new UserRole { OrganizationId = TrattoriaId, Role = Roles.Owner });
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(TrattoriaId, TrattoriaCentroId, "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        await factory.SeedMenuAsync(CantinaId, CantinaDinnerMenuId, "Jantar");
        await factory.SeedMenuAsync(TrattoriaId, TrattoriaLunchMenuId, "Pranzo");
        await factory.SeedCategoryAsync(CantinaId, CantinaDinnerMenuId, CantinaPastaCategoryId, "Massas");
        await factory.SeedCategoryAsync(TrattoriaId, TrattoriaLunchMenuId, TrattoriaPizzaCategoryId, "Pizze");
        return factory;
    }

    /// <summary>Every request carries the same JSON body; endpoints that take no body ignore it.</summary>
    private static HttpRequestMessage Request(string method, string url, object body)
        => new(new HttpMethod(method), url) { Content = JsonContent.Create(body) };

    // Scenario 9
    [Theory]
    [InlineData("PUT", "")]
    [InlineData("DELETE", "")]
    [InlineData("POST", "/locations/" + TrattoriaCentro)]
    [InlineData("DELETE", "/locations/" + TrattoriaCentro)]
    [Trait("Rule", "M7")]
    public async Task MenuById_MenuOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged(string method, string pathAfterMenuId)
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);
        var request = Request(method, $"/api/menus/{TrattoriaLunchMenuId}{pathAfterMenuId}",
            new { name = "Cardápio Invadido", description = "Alterado por outra organização", isActive = false });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "another organization's menu is answered as if it did not exist");
        var stored = await factory.InOrganizationAsync<MenuDbContext, Modules.Menu.Entities.Menu?>(TrattoriaId,
            db => db.Menus.Include(m => m.LocationMenus).SingleOrDefaultAsync(m => m.Id == TrattoriaLunchMenuId));
        stored.ShouldNotBeNull("the other organization's menu is not deleted");
        stored.Name.ShouldBe("Pranzo", "the other organization's menu keeps its name");
        stored.LocationMenus.ShouldBeEmpty(customMessage: "no location is linked to or unlinked from the other organization's menu");
    }

    // Scenario 10
    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [Trait("Rule", "M8")]
    public async Task MenuCategoryById_CategoryOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged(string method)
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);
        var body = new { name = "Categoria Invadida", description = "Alterada", sortOrder = 9 };

        // Act
        var response = await client.SendAsync(Request(method, $"/api/menu-categories/{TrattoriaPizzaCategoryId}", body));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "another organization's category is answered as if it did not exist");
        var stored = await factory.InOrganizationAsync<MenuDbContext, Modules.Menu.Entities.MenuCategory?>(TrattoriaId,
            db => db.MenuCategories.SingleOrDefaultAsync(c => c.Id == TrattoriaPizzaCategoryId));
        stored.ShouldNotBeNull("the other organization's category is not deleted");
        stored.Name.ShouldBe("Pizze", "the other organization's category keeps its name");
    }

    // Scenario 12
    [Theory]
    [InlineData("POST", "/api/menus/create")]
    [InlineData("GET", "/api/menus")]
    [InlineData("POST", "/api/menu-categories")]
    [InlineData("GET", "/api/menu-categories")]
    [Trait("Rule", "M6")]
    public async Task MenuEndpoints_NoOrganizationInToken_ReturnsForbiddenOrganizationRequired(string method, string url)
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, organizationId: null, Roles.Owner);
        var body = new { menuId = CantinaDinnerMenuId, name = "Almoço executivo" };

        // Act
        var response = await client.SendAsync(Request(method, url, body));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a user who has not chosen an organization is refused as forbidden");
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        error!.Message.ShouldBe(factory.Message("OrganizationRequired"), "the refusal tells the user an organization is required");
    }

    private sealed record ErrorBody(string Message);

    // Scenario 13
    [Fact]
    [Trait("Rule", "M10")]
    public async Task CreateMenu_BodyNamesAnotherOrganization_StoresMenuInTokenOrganization()
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.PostAsJsonAsync("/api/menus/create",
            new { organizationId = TrattoriaId, name = "Almoço executivo", description = "Prato feito com arroz, feijão e bife" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the menu is created");
        (await response.Content.ReadFromJsonAsync<Row>())!.OrganizationId.ShouldBe(CantinaId, "the menu is stored in the signed-in organization, whatever organization the request names");
    }

    // Scenario 14
    [Fact]
    [Trait("Rule", "M10")]
    public async Task CreateMenuCategory_BodyNamesAnotherOrganization_StoresCategoryInTokenOrganization()
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.PostAsJsonAsync("/api/menu-categories",
            new { menuId = CantinaDinnerMenuId, organizationId = TrattoriaId, name = "Sobremesas", sortOrder = 2 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created, "the category is created");
        (await response.Content.ReadFromJsonAsync<Row>())!.OrganizationId.ShouldBe(CantinaId, "the category is stored in the signed-in organization, whatever organization the request names");
    }

    // Scenario 15
    [Fact]
    [Trait("Rule", "M9")]
    public async Task GetMenus_MenusInTwoOrganizations_ReturnsOnlyTokenOrganizationsMenus()
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.GetAsync("/api/menus");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an Owner may list the organization's menus");
        var menus = await response.Content.ReadFromJsonAsync<List<Row>>();
        menus!.Select(m => m.Id).ShouldBe(new[] { CantinaDinnerMenuId }, ignoreOrder: false, customMessage: "the list holds only the signed-in organization's menus, never another organization's");
    }

    // Scenario 16
    [Fact]
    [Trait("Rule", "M9")]
    public async Task GetMenuCategories_CategoriesInTwoOrganizations_ReturnsOnlyTokenOrganizationsCategories()
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.GetAsync("/api/menu-categories");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an Owner may list the organization's categories");
        var categories = await response.Content.ReadFromJsonAsync<List<Row>>();
        categories!.Select(c => c.Id).ShouldBe(new[] { CantinaPastaCategoryId }, ignoreOrder: false, customMessage: "the list holds only the signed-in organization's categories, never another organization's");
    }

    // Scenario 17
    [Theory]
    [InlineData("/api/menus/organization/" + Cantina)]
    [InlineData("/api/organizations/" + Cantina + "/menu-categories")]
    [Trait("Rule", "M14")]
    public async Task OldOrganizationInUrlRoute_Called_ReturnsNotFound(string url)
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "the organization is never taken from the address, so the old routes no longer exist");
    }
}
