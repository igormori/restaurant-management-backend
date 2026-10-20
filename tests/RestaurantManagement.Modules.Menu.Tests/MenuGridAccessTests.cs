using NSubstitute;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;

namespace RestaurantManagement.Modules.Menu.Tests;

/// <summary>
/// What the menu and category services ask of the member's access grid, checked with the real guard
/// over a stated grid. Cantina da Nonna has two non-closed locations, Batel and Mercado Municipal.
/// Menus: "Jantar" (linked to Batel), "Almoço" (linked to both), "Sazonal" (linked to none).
/// Categories: "Massas" at Batel, "Sobremesas" at Mercado Municipal, "Bebidas" with no location.
/// </summary>
public class MenuGridAccessTests
{
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid MercadoId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");
    private static readonly Guid DinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");
    private static readonly Guid LunchMenuId = Guid.Parse("55555555-0000-4000-8000-000000000002");
    private static readonly Guid SeasonalMenuId = Guid.Parse("55555555-0000-4000-8000-000000000003");
    private static readonly Guid PastaCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000001");
    private static readonly Guid DessertCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000002");
    private static readonly Guid DrinksCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000003");
    private static readonly DateTime CreationTime = new(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);

    private sealed record Services(MenuService Menus, MenuCategoryService Categories);

    private static async Task SeedAsync(MenuTestDatabase database)
    {
        using var context = database.CreateContext();
        context.Menus.AddRange(
            new Entities.Menu { Id = DinnerMenuId, OrganizationId = CantinaId, Name = "Jantar", IsActive = true, CreatedAt = CreationTime, UpdatedAt = CreationTime },
            new Entities.Menu { Id = LunchMenuId, OrganizationId = CantinaId, Name = "Almoço", IsActive = true, CreatedAt = CreationTime, UpdatedAt = CreationTime },
            new Entities.Menu { Id = SeasonalMenuId, OrganizationId = CantinaId, Name = "Sazonal", IsActive = true, CreatedAt = CreationTime, UpdatedAt = CreationTime });
        context.LocationMenus.AddRange(
            new LocationMenu { Id = Guid.NewGuid(), OrganizationId = CantinaId, MenuId = DinnerMenuId, LocationId = BatelId, IsActive = true, CreatedAt = CreationTime },
            new LocationMenu { Id = Guid.NewGuid(), OrganizationId = CantinaId, MenuId = LunchMenuId, LocationId = BatelId, IsActive = true, CreatedAt = CreationTime },
            new LocationMenu { Id = Guid.NewGuid(), OrganizationId = CantinaId, MenuId = LunchMenuId, LocationId = MercadoId, IsActive = true, CreatedAt = CreationTime });
        context.MenuCategories.AddRange(
            new MenuCategory { Id = PastaCategoryId, MenuId = DinnerMenuId, OrganizationId = CantinaId, LocationId = BatelId, Name = "Massas", SortOrder = 1, CreatedAt = CreationTime },
            new MenuCategory { Id = DessertCategoryId, MenuId = LunchMenuId, OrganizationId = CantinaId, LocationId = MercadoId, Name = "Sobremesas", SortOrder = 2, CreatedAt = CreationTime },
            new MenuCategory { Id = DrinksCategoryId, MenuId = LunchMenuId, OrganizationId = CantinaId, LocationId = null, Name = "Bebidas", SortOrder = 3, CreatedAt = CreationTime });
        await context.SaveChangesAsync();
    }

    private static Services CreateServices(MenuTestDatabase database, AccessGridDto grid)
    {
        var organizationLookup = Substitute.For<IOrganizationLookup>();
        organizationLookup.OrganizationExistsAsync(CantinaId).Returns(true);
        organizationLookup.GetLocationAsync(BatelId).Returns(new LocationSummaryDto { Id = BatelId, OrganizationId = CantinaId });
        organizationLookup.GetLocationAsync(MercadoId).Returns(new LocationSummaryDto { Id = MercadoId, OrganizationId = CantinaId });
        organizationLookup.GetLocationIdsInOrganizationAsync(CantinaId, Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(call => call.Arg<IReadOnlyCollection<Guid>>().ToList());
        var guard = TestAccess.Guard(MariaId, CantinaId, grid, BatelId, MercadoId);
        var tenant = TestTenant.Of(CantinaId);
        var context = database.CreateContext(tenant);
        return new Services(
            new MenuService(context, organizationLookup, guard, tenant, TestLocalizer.Create()),
            new MenuCategoryService(database.CreateContext(tenant), organizationLookup, guard, tenant, TestLocalizer.Create()));
    }

    private static AccessGridDto MenusGrid(AccessLevel atBatel, AccessLevel atMercado)
        => TestAccess.Grid(
            (BatelId, AccessModule.Menus, atBatel), (MercadoId, AccessModule.Menus, atMercado));

    private static AccessGridDto MenusAtBatelOnly(AccessLevel level)
        => TestAccess.Grid((BatelId, AccessModule.Menus, level), (MercadoId, AccessModule.Locations, AccessLevel.Read));

    private static CreateMenuRequest NewMenu(params Guid[] locationIds)
        => new() { Name = "Almoço executivo", Description = "Prato feito", LocationIds = locationIds.Length == 0 ? null : locationIds.ToList() };

    private static readonly EditMenuRequest MenuEdit = new() { Name = "Cardápio revisado", Description = "Revisado", IsActive = true };

    private static CreateMenuCategoryRequest NewCategory(Guid? locationId)
        => new() { MenuId = LunchMenuId, LocationId = locationId, Name = "Entradas", SortOrder = 4 };

    private static readonly UpdateMenuCategoryRequest CategoryEdit = new() { Name = "Sobremesas da casa", SortOrder = 2 };

    // The writes a test sends; the name is shown in the test results.
    private static readonly Dictionary<string, Func<Services, Task>> Writes = new()
    {
        ["create a menu for Batel and Mercado"] = s => s.Menus.CreateMenuAsync(MariaId, NewMenu(BatelId, MercadoId)),
        ["create a menu for every location"] = s => s.Menus.CreateMenuAsync(MariaId, NewMenu()),
        ["edit a menu linked to both locations"] = s => s.Menus.EditMenuAsync(MariaId, LunchMenuId, MenuEdit),
        ["edit a menu linked to no location"] = s => s.Menus.EditMenuAsync(MariaId, SeasonalMenuId, MenuEdit),
        ["delete a menu linked to both locations"] = s => s.Menus.DeleteMenuAsync(MariaId, LunchMenuId),
        ["link a menu to Mercado"] = s => s.Menus.AttachMenuToLocationAsync(MariaId, DinnerMenuId, MercadoId),
        ["unlink a menu from Mercado"] = s => s.Menus.DetachMenuFromLocationAsync(MariaId, LunchMenuId, MercadoId),
        ["create a category at Mercado"] = s => s.Categories.CreateAsync(MariaId, NewCategory(MercadoId)),
        ["update a category at Mercado"] = s => s.Categories.UpdateAsync(MariaId, DessertCategoryId, CategoryEdit),
        ["delete a category at Mercado"] = s => s.Categories.DeleteAsync(MariaId, DessertCategoryId),
        ["create a category with no location"] = s => s.Categories.CreateAsync(MariaId, NewCategory(null))
    };

    private static readonly Dictionary<string, Func<Services, Task>> Reads = new()
    {
        ["menus of Batel"] = s => s.Menus.GetMenusByLocationAsync(MariaId, BatelId),
        ["menus of Mercado"] = s => s.Menus.GetMenusByLocationAsync(MariaId, MercadoId),
        ["the full menu list"] = s => s.Menus.GetMenusAsync(MariaId),
        ["a category of Batel by ID"] = s => s.Categories.GetByIdAsync(MariaId, PastaCategoryId),
        ["a category of Mercado by ID"] = s => s.Categories.GetByIdAsync(MariaId, DessertCategoryId),
        ["a category with no location by ID"] = s => s.Categories.GetByIdAsync(MariaId, DrinksCategoryId),
        ["the categories of Batel"] = s => s.Categories.GetByLocationIdAsync(MariaId, BatelId),
        ["the categories of Mercado"] = s => s.Categories.GetByLocationIdAsync(MariaId, MercadoId),
        ["all categories"] = s => s.Categories.GetAllAsync(MariaId),
        ["the categories of a menu"] = s => s.Categories.GetByMenuIdAsync(MariaId, LunchMenuId)
    };

    public static TheoryData<string> WritesAllowedWithManageAtBothLocations => new(Writes.Keys);

    public static TheoryData<string> WritesRefusedWithManageAtBatelOnly => new(Writes.Keys);

    public static TheoryData<string> ReadsAllowedWithReadAtBothLocations => new(Reads.Keys);

    public static TheoryData<string> ReadsRefusedWithReadAtBatelOnly => new(
        "menus of Mercado", "the full menu list", "a category of Mercado by ID", "a category with no location by ID",
        "the categories of Mercado", "all categories", "the categories of a menu");

    [Theory]
    [MemberData(nameof(WritesAllowedWithManageAtBothLocations))]
    [Trait("Rule", "M40")]
    public async Task MenuAndCategoryWrites_ManageAtEveryInvolvedLocation_Succeeds(string write)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedAsync(database);
        var services = CreateServices(database, MenusGrid(AccessLevel.Manage, AccessLevel.Manage));

        // Act
        var act = () => Writes[write](services);

        // Assert
        await Should.NotThrowAsync(act, "Manage at every location the action involves, or at every non-closed location when none is named, allows it");
    }

    [Theory]
    [MemberData(nameof(WritesRefusedWithManageAtBatelOnly))]
    [Trait("Rule", "M41")]
    public async Task MenuAndCategoryWrites_ManageMissingAtAnInvolvedLocation_ThrowsForbiddenAndChangesNothing(string write)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedAsync(database);
        var services = CreateServices(database, MenusGrid(AccessLevel.Manage, AccessLevel.Read));

        // Act
        var act = () => Writes[write](services);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>();
        exception.StatusCode.ShouldBe(403, "Manage is needed at every involved location; Read at Mercado Municipal is not enough");
        using var verifyContext = database.CreateContext(TestTenant.Of(CantinaId));
        verifyContext.Menus.Count().ShouldBe(3, "a refused write creates or deletes no menu");
        verifyContext.MenuCategories.Count().ShouldBe(3, "a refused write creates or deletes no category");
        verifyContext.LocationMenus.Count().ShouldBe(3, "a refused write links or unlinks no location");
    }

    [Theory]
    [MemberData(nameof(ReadsAllowedWithReadAtBothLocations))]
    [Trait("Rule", "M42")]
    public async Task MenuAndCategoryReads_MenusReadAtEveryInvolvedLocation_Succeeds(string read)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedAsync(database);
        var services = CreateServices(database, MenusGrid(AccessLevel.Read, AccessLevel.Read));

        // Act
        var act = () => Reads[read](services);

        // Assert
        await Should.NotThrowAsync(act, "Read at the location, or at every non-closed location for a list across locations, allows the read");
    }

    [Theory]
    [MemberData(nameof(ReadsRefusedWithReadAtBatelOnly))]
    [Trait("Rule", "M43")]
    public async Task MenuAndCategoryReads_MenusReadMissingAtAnInvolvedLocation_ThrowsForbidden(string read)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedAsync(database);
        var services = CreateServices(database, MenusAtBatelOnly(AccessLevel.Read));

        // Act
        var act = () => Reads[read](services);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>();
        exception.StatusCode.ShouldBe(403, "Menus Read only at Batel gives nothing at Mercado Municipal, and a list across locations needs every non-closed location");
    }
}
