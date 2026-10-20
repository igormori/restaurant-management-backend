using System.Net;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Shared;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Deleting a menu on a real PostgreSQL database: the cascade is a database rule, so it cannot be
/// proven on SQLite. Cantina da Nonna has two menus; only the dinner menu is deleted.
/// </summary>
public class MenuDeleteCascadeTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid CentroLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid DinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");
    private static readonly Guid LunchMenuId = Guid.Parse("55555555-0000-4000-8000-000000000002");
    private static readonly Guid PastaCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000001");
    private static readonly Guid DessertCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000002");
    private static readonly Guid LunchCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000003");

    private readonly PostgresDatabaseFixture _postgres;

    public MenuDeleteCascadeTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            TenantApiFactory.OwnerOf(CantinaId));
        await factory.SeedLocationAsync(CantinaId, CentroLocationId, "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        await factory.SeedMenuAsync(CantinaId, DinnerMenuId, "Jantar");
        await factory.SeedMenuAsync(CantinaId, LunchMenuId, "Almoço");
        await factory.SeedCategoryAsync(CantinaId, DinnerMenuId, PastaCategoryId, "Massas");
        await factory.SeedCategoryAsync(CantinaId, DinnerMenuId, DessertCategoryId, "Sobremesas");
        await factory.SeedCategoryAsync(CantinaId, LunchMenuId, LunchCategoryId, "Pratos executivos");
        await factory.InOrganizationAsync<MenuDbContext, bool>(CantinaId, async db =>
        {
            db.LocationMenus.Add(new LocationMenu
            {
                Id = Guid.NewGuid(),
                OrganizationId = CantinaId,
                MenuId = DinnerMenuId,
                LocationId = CentroLocationId,
                IsActive = true
            });
            await db.SaveChangesAsync();
            return true;
        });
        return factory;
    }

    [Fact]
    [Trait("Rule", "M18")]
    public async Task DeleteMenu_MenuWithCategoriesAndLocationLink_RemovesThemAndKeepsOtherMenusCategories()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.DeleteAsync($"/api/menus/{DinnerMenuId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an Owner may delete a menu");
        var remaining = await factory.InOrganizationAsync<MenuDbContext, (List<Guid> Menus, List<Guid> Categories, int Links)>(CantinaId, async db =>
            (await db.Menus.Select(m => m.Id).ToListAsync(),
             await db.MenuCategories.Select(c => c.Id).ToListAsync(),
             await db.LocationMenus.CountAsync()));
        remaining.Menus.Single().ShouldBe(LunchMenuId, customMessage: "the deleted menu is gone and the other menu stays");
        remaining.Categories.Single().ShouldBe(LunchCategoryId, customMessage: "the deleted menu's categories go with it, the other menu's category stays");
        remaining.Links.ShouldBe(0, "the deleted menu's location links go with it");
    }
}
