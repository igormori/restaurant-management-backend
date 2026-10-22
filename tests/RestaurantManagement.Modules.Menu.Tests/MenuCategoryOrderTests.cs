using NSubstitute;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared.Data.Tenancy;
using RestaurantManagement.Shared.Services.Organization;
using Shouldly;

namespace RestaurantManagement.Modules.Menu.Tests;

/// <summary>Category lists come back by sort order, then by name.</summary>
public class MenuCategoryOrderTests
{
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid DinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

    private static readonly string[] ExpectedOrder = { "Entradas", "Bebidas", "Massas", "Sobremesas" };

    // Stored in an order that is neither by sort order nor by name
    private static async Task SeedCategoriesAsync(MenuTestDatabase database)
    {
        using var context = database.CreateContext();
        context.Menus.Add(new Entities.Menu { Id = DinnerMenuId, OrganizationId = CantinaId, Name = "Jantar", IsActive = true });
        context.MenuCategories.AddRange(
            Category("Sobremesas", sortOrder: 3),
            Category("Massas", sortOrder: 2),
            Category("Entradas", sortOrder: 1),
            Category("Bebidas", sortOrder: 2));
        await context.SaveChangesAsync();
    }

    private static MenuCategory Category(string name, int sortOrder) => new()
    {
        Id = Guid.NewGuid(),
        MenuId = DinnerMenuId,
        OrganizationId = CantinaId,
        Name = name,
        SortOrder = sortOrder
    };

    private static MenuCategoryService CreateOwnerSut(Data.MenuDbContext context, ITenantContext tenant)
    {
        return new MenuCategoryService(context, Substitute.For<IOrganizationLookup>(), TestAccess.Permissive(), tenant, TestLocalizer.Create());
    }

    [Fact]
    [Trait("Rule", "M20")]
    public async Task GetAllAsync_SortOrdersTieAndDiffer_ReturnsBySortOrderThenName()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCategoriesAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateOwnerSut(context, tenant);

        // Act
        var categories = await sut.GetAllAsync(AnaId);

        // Assert
        categories.Select(c => c.Name).ToArray().ShouldBe(ExpectedOrder, ignoreOrder: false,
            customMessage: "categories are ordered by sort order, and by name when sort orders tie");
    }

    [Fact]
    [Trait("Rule", "M20")]
    public async Task GetByMenuIdAsync_SortOrdersTieAndDiffer_ReturnsBySortOrderThenName()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCategoriesAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateOwnerSut(context, tenant);

        // Act
        var categories = await sut.GetByMenuIdAsync(AnaId, DinnerMenuId);

        // Assert
        categories.Select(c => c.Name).ToArray().ShouldBe(ExpectedOrder, ignoreOrder: false,
            customMessage: "a menu's categories are ordered by sort order, and by name when sort orders tie");
    }
}
