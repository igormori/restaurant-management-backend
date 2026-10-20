using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Menu and category endpoints on a real PostgreSQL database, decided by the member's stored access
/// grid. Cantina da Nonna has Batel (menu "Jantar", category "Massas") and Mercado Municipal (menu
/// "Almoço"); Trattoria do Sol has Centro. Bia is a Waiter, Caio a Manager, Dora a Cashier, Maria an
/// Admin with different levels per location, Fred has no Menus access, Ana is the Owner.
/// </summary>
public class MenuGridEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string BiaText = "22222222-bbbb-4bbb-8bbb-00000000000b";
    private const string FredText = "55555555-eeee-4eee-8eee-00000000000e";
    private const string CaioText = "33333333-cccc-4ccc-8ccc-00000000000c";
    private const string DoraText = "44444444-dddd-4ddd-8ddd-00000000000d";
    private const string Batel = "44444444-dddd-4ddd-8ddd-000000000001";
    private const string MercadoMunicipal = "44444444-dddd-4ddd-8ddd-000000000002";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid BiaId = Guid.Parse(BiaText);
    private static readonly Guid CaioId = Guid.Parse(CaioText);
    private static readonly Guid DoraId = Guid.Parse(DoraText);
    private static readonly Guid FredId = Guid.Parse(FredText);
    private static readonly Guid MariaId = Guid.Parse("66666666-ffff-4fff-8fff-00000000000f");
    private static readonly Guid EvaId = Guid.Parse("77777777-0000-4000-8000-0000000000e1");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid BatelId = Guid.Parse(Batel);
    private static readonly Guid MercadoId = Guid.Parse(MercadoMunicipal);
    private static readonly Guid TrattoriaCentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
    private static readonly Guid DinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");
    private static readonly Guid LunchMenuId = Guid.Parse("55555555-0000-4000-8000-000000000002");
    private static readonly Guid PastaCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000001");

    private readonly PostgresDatabaseFixture _postgres;

    public MenuGridEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private static readonly object MenuBody = new { name = "Almoço executivo", description = "Prato feito com arroz, feijão e bife", isActive = true };

    private async Task<TenantApiFactory> CreateFactoryAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, BatelId, "Unidade Batel", "BATEL", "America/Sao_Paulo");
        await factory.SeedLocationAsync(CantinaId, MercadoId, "Unidade Mercado Municipal", "MERCADO", "America/Sao_Paulo");
        await factory.SeedLocationAsync(TrattoriaId, TrattoriaCentroId, "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        await factory.SeedMenuAsync(CantinaId, DinnerMenuId, "Jantar");
        await factory.SeedMenuAsync(CantinaId, LunchMenuId, "Almoço");
        await factory.SeedCategoryAsync(CantinaId, DinnerMenuId, PastaCategoryId, "Massas");
        await LinkMenuAsync(factory, DinnerMenuId, BatelId);
        await LinkMenuAsync(factory, LunchMenuId, MercadoId);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana", TenantApiFactory.OwnerOf(CantinaId));
        await factory.SeedUserAsync(BiaId, "bia@cantinadanonna.com.br", "Bia", TenantApiFactory.MemberOf(CantinaId));
        await factory.SeedUserAsync(CaioId, "caio@cantinadanonna.com.br", "Caio", TenantApiFactory.MemberOf(CantinaId));
        await factory.SeedUserAsync(DoraId, "dora@cantinadanonna.com.br", "Dora", TenantApiFactory.MemberOf(CantinaId));
        await factory.SeedUserAsync(FredId, "fred@cantinadanonna.com.br", "Fred", TenantApiFactory.MemberOf(CantinaId));
        await factory.SeedUserAsync(MariaId, "maria@cantinadanonna.com.br", "Maria", TenantApiFactory.MemberOf(CantinaId));
        await factory.SeedUserAsync(EvaId, "eva@cantinadanonna.com.br", "Eva",
            TenantApiFactory.MemberOf(CantinaId), TenantApiFactory.MemberOf(TrattoriaId));

        // Bia, a Waiter at Batel only
        await factory.GrantAsync(BiaId, CantinaId, BatelId,
            (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read), (AccessModule.Orders, AccessLevel.Manage));
        // Fred: Locations Read only, no Menus level at Batel
        await factory.GrantAsync(FredId, CantinaId, BatelId, (AccessModule.Locations, AccessLevel.Read));
        // Caio, a Manager, and Dora, a Cashier, at both locations
        await factory.GrantAsync(CaioId, CantinaId, BatelId, ManagerLevels);
        await factory.GrantAsync(CaioId, CantinaId, MercadoId, ManagerLevels);
        await factory.GrantAsync(DoraId, CantinaId, BatelId, CashierLevels);
        await factory.GrantAsync(DoraId, CantinaId, MercadoId, CashierLevels);
        // Maria is Admin at both locations with different levels: Manage at Batel, Read at Mercado
        await factory.GrantAsync(MariaId, CantinaId, BatelId,
            (AccessModule.Locations, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Members, AccessLevel.Manage));
        await factory.GrantAsync(MariaId, CantinaId, MercadoId,
            (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read));
        // Eva manages everything in Trattoria do Sol and holds nothing in Cantina
        await factory.GrantAsync(EvaId, TrattoriaId, TrattoriaCentroId,
            (AccessModule.Locations, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage));
        return factory;
    }

    private static (AccessModule, AccessLevel)[] ManagerLevels => new[]
    {
        (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read),
        (AccessModule.Kitchen, AccessLevel.Read), (AccessModule.Reports, AccessLevel.Manage)
    };

    private static (AccessModule, AccessLevel)[] CashierLevels => new[]
    {
        (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read),
        (AccessModule.Cashier, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read)
    };

    private static Task LinkMenuAsync(TenantApiFactory factory, Guid menuId, Guid locationId)
        => factory.InOrganizationAsync<MenuDbContext, bool>(CantinaId, async db =>
        {
            db.LocationMenus.Add(new LocationMenu { Id = Guid.NewGuid(), OrganizationId = CantinaId, MenuId = menuId, LocationId = locationId, IsActive = true });
            await db.SaveChangesAsync();
            return true;
        });

    [Theory]
    [InlineData(Batel, HttpStatusCode.OK)]
    [InlineData(MercadoMunicipal, HttpStatusCode.Forbidden)]
    [Trait("Rule", "M32")]
    public async Task GetMenusByLocation_WaiterWithMenusReadAtBatel_ReadsBatelAndIsRefusedAtMercado(string location, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(BiaId, CantinaId);

        // Act
        var response = await client.GetAsync($"/api/menus/location/{location}");

        // Assert
        response.StatusCode.ShouldBe(expected, "a Waiter with Menus Read at Batel reads Batel's menus, and is refused at a location where she has no level");
    }

    [Theory]
    [InlineData(BiaText, HttpStatusCode.OK)]
    [InlineData(FredText, HttpStatusCode.Forbidden)]
    [Trait("Rule", "M33")]
    public async Task GetMenuCategoriesByLocation_MenusReadAllowedAndMenusNoneRefused(string member, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(Guid.Parse(member), CantinaId);

        // Act
        var response = await client.GetAsync($"/api/locations/{Batel}/menu-categories");

        // Assert
        response.StatusCode.ShouldBe(expected, "a member with Menus Read at the location sees its categories; one with Menus None does not");
    }

    [Theory]
    [InlineData(CaioText, HttpStatusCode.OK)]
    [InlineData(DoraText, HttpStatusCode.Forbidden)]
    [Trait("Rule", "M34")]
    public async Task EditMenu_ManagerWithMenusManageAllowedAndCashierWithMenusReadRefused(string member, HttpStatusCode expected)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(Guid.Parse(member), CantinaId);

        // Act
        var response = await client.PutAsJsonAsync($"/api/menus/{DinnerMenuId}", MenuBody);

        // Assert
        response.StatusCode.ShouldBe(expected, "Manage on Menus allows editing a menu; Read only does not");
    }

    [Theory]
    [InlineData("POST", "/api/menus/create")]
    [InlineData("PUT", "/api/menus/55555555-0000-4000-8000-000000000001")]
    [InlineData("DELETE", "/api/menus/55555555-0000-4000-8000-000000000001")]
    [InlineData("POST", "/api/menus/55555555-0000-4000-8000-000000000001/locations/" + MercadoMunicipal)]
    [InlineData("POST", "/api/menu-categories")]
    [InlineData("PUT", "/api/menu-categories/66666666-0000-4000-8000-000000000001")]
    [InlineData("DELETE", "/api/menu-categories/66666666-0000-4000-8000-000000000001")]
    [Trait("Rule", "M35")]
    public async Task MenuAndCategoryWrites_MemberWithMenusReadOnly_ReturnsForbiddenAndChangesNothing(string method, string url)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(DoraId, CantinaId);
        var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new { menuId = DinnerMenuId, name = "Almoço executivo", description = "Alterado", isActive = false, sortOrder = 2, locationIds = new[] { BatelId } })
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "Read lets a member see menus and categories, never create, change or delete them");
        var stored = await factory.InOrganizationAsync<MenuDbContext, (int Menus, int Categories, string Name)>(CantinaId, async db =>
            (await db.Menus.CountAsync(), await db.MenuCategories.CountAsync(), (await db.Menus.SingleAsync(m => m.Id == DinnerMenuId)).Name));
        stored.ShouldBe((2, 1, "Jantar"), "a refused write leaves the stored menus and categories as they were");
    }

    [Fact]
    [Trait("Rule", "M36")]
    public async Task EditMenu_AdminManagesBatelAndReadsMercado_EditsBatelMenuReadsMercadoAndIsRefusedEditingMercadoMenu()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(MariaId, CantinaId);

        // Act
        var editBatelMenu = await client.PutAsJsonAsync($"/api/menus/{DinnerMenuId}", MenuBody);
        var readMercadoMenus = await client.GetAsync($"/api/menus/location/{MercadoMunicipal}");
        var editMercadoMenu = await client.PutAsJsonAsync($"/api/menus/{LunchMenuId}", MenuBody);

        // Assert
        editBatelMenu.StatusCode.ShouldBe(HttpStatusCode.OK, "Maria manages Menus at Batel, so she edits Batel's menu");
        readMercadoMenus.StatusCode.ShouldBe(HttpStatusCode.OK, "Maria reads Menus at Mercado Municipal");
        editMercadoMenu.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "the same Admin only reads Menus at Mercado Municipal, so editing a menu linked there is refused");
    }

    [Fact]
    [Trait("Rule", "M37")]
    public async Task EditMenu_ManageLoweredToReadBetweenTwoRequestsWithTheSameToken_SecondRequestIsRefused()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(CaioId, CantinaId);
        var first = await client.PutAsJsonAsync($"/api/menus/{DinnerMenuId}", MenuBody);
        await factory.SetLevelAsync(CaioId, CantinaId, BatelId, AccessModule.Menus, AccessLevel.Read);

        // Act
        var second = await client.PutAsJsonAsync($"/api/menus/{DinnerMenuId}", MenuBody);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK, "the Manager may edit while the stored level is Manage");
        second.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a lowered level applies at once: the next request is refused with no new login");
    }

    [Fact]
    [Trait("Rule", "M38")]
    public async Task CreateMenu_OwnerWithNoStoredLevels_ReturnsOk()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.PostAsJsonAsync("/api/menus/create", MenuBody);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the Owner has Manage on everything at every location with no stored level");
    }

    [Fact]
    [Trait("Rule", "M39")]
    [Trait("Rule", "M4")]
    public async Task CreateMenu_ManageEverywhereOnlyInAnotherOrganization_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(EvaId, CantinaId);
        var otherOrganizationOnly = await client.PostAsJsonAsync("/api/menus/create", MenuBody);
        await factory.GrantAsync(EvaId, CantinaId, BatelId, (AccessModule.Menus, AccessLevel.Manage));
        await factory.GrantAsync(EvaId, CantinaId, MercadoId, (AccessModule.Menus, AccessLevel.Manage));

        // Act
        var afterGrantHere = await client.PostAsJsonAsync("/api/menus/create", MenuBody);

        // Assert
        otherOrganizationOnly.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "Manage everywhere in Trattoria do Sol gives nothing in Cantina da Nonna");
        afterGrantHere.StatusCode.ShouldBe(HttpStatusCode.OK, "the same token creates a menu once Manage is granted at every location of Cantina da Nonna");
    }
}
