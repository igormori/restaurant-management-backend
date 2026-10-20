using NSubstitute;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;

namespace RestaurantManagement.Modules.Menu.Tests;

/// <summary>
/// Menu and menu category access when a user holds several roles in the organization. Menus and
/// categories belong to the whole organization, so only an Owner or Admin role assigned for all
/// locations may change them; permissions add up across roles and the order of the roles never matters.
/// </summary>
public class MenuMultiRoleAccessTests
{
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid CentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");
    private static readonly Guid MercadoMunicipalId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
    private static readonly Guid DinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");

    private static RoleAssignmentDto AllLocations(string role) => new() { Role = role, LocationId = null };

    private static RoleAssignmentDto At(string role, Guid locationId) => new() { Role = role, LocationId = locationId };

    private static List<RoleAssignmentDto> InOrder(RoleAssignmentDto first, RoleAssignmentDto second, bool firstStoredFirst)
        => firstStoredFirst ? new() { first, second } : new() { second, first };

    private static IUserRoleLookup Holding(params RoleAssignmentDto[] assignments)
        => Holding(assignments.ToList());

    private static IUserRoleLookup Holding(List<RoleAssignmentDto> assignments)
    {
        var lookup = Substitute.For<IUserRoleLookup>();
        lookup.GetRoleAssignmentsAsync(JoaoId, CantinaId).Returns(assignments);
        return lookup;
    }

    private static IOrganizationLookup CantinaExists()
    {
        var lookup = Substitute.For<IOrganizationLookup>();
        lookup.OrganizationExistsAsync(CantinaId).Returns(true);
        return lookup;
    }

    private static MenuService CreateMenuService(Data.MenuDbContext context, IUserRoleLookup roles)
        => new(context, CantinaExists(), roles, TestTenant.Of(CantinaId), TestLocalizer.Create());

    private static MenuCategoryService CreateCategoryService(Data.MenuDbContext context, IUserRoleLookup roles, IOrganizationLookup? organizations = null)
        => new(context, organizations ?? CantinaExists(), roles, TestTenant.Of(CantinaId), TestLocalizer.Create());

    private static CreateMenuRequest NewMenu => new()
    {
        Name = "Almoço executivo",
        Description = "Prato feito com arroz, feijão e bife"
    };

    private static async Task SeedDinnerMenuAsync(MenuTestDatabase database)
    {
        using var context = database.CreateContext();
        context.Menus.Add(new Entities.Menu
        {
            Id = DinnerMenuId,
            OrganizationId = CantinaId,
            Name = "Jantar",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    // Scenario 12
    [Theory]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.Admin, false)]
    [InlineData(Roles.Owner, true)]
    [InlineData(Roles.Owner, false)]
    [Trait("Rule", "M25")]
    public async Task CreateMenuAsync_AdminOrOwnerForAllLocationsPlusWaiterAtOneLocation_CreatesMenuInEveryOrder(string role, bool adminStoredFirst)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateMenuService(context, Holding(InOrder(AllLocations(role), At(Roles.Waiter, BatelId), adminStoredFirst)));

        // Act
        var menu = await sut.CreateMenuAsync(JoaoId, NewMenu);

        // Assert
        menu.Name.ShouldBe("Almoço executivo", "an all-locations Admin or Owner may manage menus whatever other role the user also holds");
    }

    // Scenario 13
    [Theory]
    [InlineData(Roles.Waiter, Roles.Cashier)]
    [InlineData(Roles.Cashier, Roles.Waiter)]
    [InlineData(Roles.Kitchen, Roles.Waiter)]
    [InlineData(Roles.Waiter, Roles.Kitchen)]
    [InlineData(Roles.Manager, Roles.Waiter)]
    [InlineData(Roles.Waiter, Roles.Manager)]
    [Trait("Rule", "M26")]
    public async Task CreateMenuAsync_TwoRolesNeitherOwnerNorAdmin_ThrowsForbiddenInEveryOrder(string first, string second)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateMenuService(context, Holding(AllLocations(first), AllLocations(second)));

        // Act
        var act = () => sut.CreateMenuAsync(JoaoId, NewMenu);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "roles that do not include Owner or Admin never add up to managing menus, not even a Manager");
        exception.StatusCode.ShouldBe(403, "a user without an Owner or Admin role is refused");
    }

    // Scenario 14
    [Fact]
    [Trait("Rule", "M27")]
    public async Task CreateMenuAsync_AdminAssignedOnlyToOneLocation_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateMenuService(context, Holding(At(Roles.Admin, BatelId)));

        // Act
        var act = () => sut.CreateMenuAsync(JoaoId, NewMenu);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "an Admin for one location may not manage the organization's menus");
        exception.StatusCode.ShouldBe(403, "a role for one location never counts for an action on the whole organization");
    }

    // Scenario 15
    [Fact]
    [Trait("Rule", "M27")]
    public async Task DeleteMenuAsync_AdminAssignedToThreeLocationsSeparately_ThrowsForbiddenAndKeepsMenu()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateMenuService(context, Holding(
            At(Roles.Admin, BatelId), At(Roles.Admin, CentroId), At(Roles.Admin, MercadoMunicipalId)));

        // Act
        var act = () => sut.DeleteMenuAsync(JoaoId, DinnerMenuId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "an Admin assigned location by location is not an all-locations Admin");
        exception.StatusCode.ShouldBe(403, "the same role at every current location as separate assignments is refused for organization-wide actions");
        using var checkContext = database.CreateContext(tenant);
        checkContext.Menus.Count(m => m.Id == DinnerMenuId).ShouldBe(1, "a refused delete keeps the menu");
    }

    private static MenuService CreateMenuServiceWithLocations(Data.MenuDbContext context, IUserRoleLookup roles)
    {
        var organizations = CantinaExists();
        foreach (var locationId in new[] { BatelId, CentroId })
            organizations.GetLocationAsync(locationId).Returns(new LocationSummaryDto { Id = locationId, OrganizationId = CantinaId });
        return new MenuService(context, organizations, roles, TestTenant.Of(CantinaId), TestLocalizer.Create());
    }

    private static async Task SeedDinnerMenuAtBatelAsync(MenuTestDatabase database)
    {
        await SeedDinnerMenuAsync(database);
        using var context = database.CreateContext();
        context.LocationMenus.Add(new Entities.LocationMenu
        {
            MenuId = DinnerMenuId,
            LocationId = BatelId,
            OrganizationId = CantinaId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    // Scenario 16
    [Fact]
    [Trait("Rule", "M31")]
    public async Task GetMenusAsync_WaiterAssignedOnlyToOneLocation_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateMenuService(context, Holding(At(Roles.Waiter, BatelId)));

        // Act
        var act = () => sut.GetMenusAsync(JoaoId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "the full list of menus needs a role for all locations");
        exception.StatusCode.ShouldBe(403, "a Waiter for one location may not list every menu of the organization");
    }

    [Fact]
    [Trait("Rule", "M3")]
    public async Task GetMenusAsync_WaiterForAllLocations_ReturnsMenus()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateMenuService(context, Holding(AllLocations(Roles.Waiter)));

        // Act
        var menus = await sut.GetMenusAsync(JoaoId);

        // Assert
        menus.Select(m => m.Id).ShouldBe(new[] { DinnerMenuId }, "a role for all locations may list every menu");
    }

    [Fact]
    [Trait("Rule", "M3")]
    public async Task GetMenusByLocationAsync_WaiterAtBatel_ReturnsBatelMenus()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAtBatelAsync(database);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateMenuServiceWithLocations(context, Holding(At(Roles.Waiter, BatelId)));

        // Act
        var menus = await sut.GetMenusByLocationAsync(JoaoId, BatelId);

        // Assert
        menus.Select(m => m.Id).ShouldBe(new[] { DinnerMenuId }, "a role for a location reads that location's menus");
    }

    [Fact]
    [Trait("Rule", "M3")]
    public async Task GetMenusByLocationAsync_WaiterForAllLocations_ReturnsMenusOfAnyLocation()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAtBatelAsync(database);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateMenuServiceWithLocations(context, Holding(AllLocations(Roles.Waiter)));

        // Act
        var menus = await sut.GetMenusByLocationAsync(JoaoId, BatelId);

        // Assert
        menus.Select(m => m.Id).ShouldBe(new[] { DinnerMenuId }, "a role for all locations reads every location's menus");
    }

    [Fact]
    [Trait("Rule", "M31")]
    public async Task GetMenusByLocationAsync_WaiterAtBatelAsksForCentro_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAtBatelAsync(database);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateMenuServiceWithLocations(context, Holding(At(Roles.Waiter, BatelId)));

        // Act
        var act = () => sut.GetMenusByLocationAsync(JoaoId, CentroId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "a role for one location does not open another location's menus");
        exception.StatusCode.ShouldBe(403, "a Waiter at Batel may not read Centro's menus");
    }

    // Scenario 17
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Rule", "M25")]
    public async Task CreateAsync_AdminForAllLocationsPlusWaiterAtOneLocation_CreatesCategoryInEveryOrder(bool adminStoredFirst)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateCategoryService(context, Holding(InOrder(AllLocations(Roles.Admin), At(Roles.Waiter, BatelId), adminStoredFirst)));

        // Act
        var category = await sut.CreateAsync(JoaoId, new CreateMenuCategoryRequest { MenuId = DinnerMenuId, Name = "Massas", SortOrder = 1 });

        // Assert
        category.Name.ShouldBe("Massas", "an all-locations Admin may manage menu categories whatever other role the user also holds");
    }

    // Scenario 18
    [Theory]
    [InlineData(Roles.Waiter, Roles.Cashier)]
    [InlineData(Roles.Cashier, Roles.Waiter)]
    [InlineData(Roles.Manager, Roles.Waiter)]
    [InlineData(Roles.Waiter, Roles.Manager)]
    [Trait("Rule", "M26")]
    public async Task CreateAsync_TwoRolesNeitherOwnerNorAdmin_ThrowsForbiddenInEveryOrder(string first, string second)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateCategoryService(context, Holding(AllLocations(first), AllLocations(second)));

        // Act
        var act = () => sut.CreateAsync(JoaoId, new CreateMenuCategoryRequest { MenuId = DinnerMenuId, Name = "Massas", SortOrder = 1 });

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "roles that do not include Owner or Admin never add up to managing categories");
        exception.StatusCode.ShouldBe(403, "a user without an Owner or Admin role is refused");
    }

    // Scenario 19
    [Fact]
    [Trait("Rule", "M27")]
    public async Task CreateAsync_AdminAssignedOnlyToOneLocation_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateCategoryService(context, Holding(At(Roles.Admin, BatelId)));

        // Act
        var act = () => sut.CreateAsync(JoaoId, new CreateMenuCategoryRequest { MenuId = DinnerMenuId, Name = "Massas", SortOrder = 1 });

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "an Admin for one location may not manage the organization's categories");
        exception.StatusCode.ShouldBe(403, "a role for one location never counts for an action on the whole organization");
    }

    private static async Task<Guid> SeedCategoryAsync(MenuTestDatabase database, Guid? locationId)
    {
        var categoryId = Guid.NewGuid();
        using var seed = database.CreateContext();
        seed.MenuCategories.Add(new Entities.MenuCategory { Id = categoryId, OrganizationId = CantinaId, MenuId = DinnerMenuId, LocationId = locationId, Name = "Massas", SortOrder = 1 });
        await seed.SaveChangesAsync();
        return categoryId;
    }

    // Scenario 39
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Rule", "M29")]
    public async Task GetByIdAsync_AdminOfBatelPlusWaiterAtCentro_ReadsBatelCategoryInEveryOrder(bool adminStoredFirst)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        var categoryId = await SeedCategoryAsync(database, BatelId);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateCategoryService(context, Holding(InOrder(At(Roles.Admin, BatelId), At(Roles.Waiter, CentroId), adminStoredFirst)));

        // Act
        var category = await sut.GetByIdAsync(JoaoId, categoryId);

        // Assert
        category.ShouldNotBeNull("an Admin assigned to a location may read that location's categories");
        category.Id.ShouldBe(categoryId, "the category of the Admin's own location is returned");
    }

    // Scenario 40
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Rule", "M30")]
    public async Task GetByIdAsync_AdminOfBatelPlusWaiterAtCentro_ThrowsForbiddenForCentroCategoryInEveryOrder(bool adminStoredFirst)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        var categoryId = await SeedCategoryAsync(database, CentroId);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateCategoryService(context, Holding(InOrder(At(Roles.Admin, BatelId), At(Roles.Waiter, CentroId), adminStoredFirst)));

        // Act
        var act = () => sut.GetByIdAsync(JoaoId, categoryId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "an Admin of one location may not read another location's categories");
        exception.StatusCode.ShouldBe(403, "a role for one location never counts at another location, whatever other role the user holds");
    }

    // Scenario 41
    [Fact]
    [Trait("Rule", "M30")]
    public async Task GetByIdAsync_AdminOfBatelOnCategoryWithoutLocation_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        var categoryId = await SeedCategoryAsync(database, null);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateCategoryService(context, Holding(At(Roles.Admin, BatelId)));

        // Act
        var act = () => sut.GetByIdAsync(JoaoId, categoryId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "a category without a location belongs to the whole organization");
        exception.StatusCode.ShouldBe(403, "only an Admin or Owner for all locations reads a category without a location");
    }

    // Scenario 42
    [Fact]
    [Trait("Rule", "M29")]
    public async Task GetByLocationIdAsync_AdminOfBatelForBatel_ReturnsBatelCategories()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        var categoryId = await SeedCategoryAsync(database, BatelId);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateCategoryService(context, Holding(At(Roles.Admin, BatelId)), LocationInCantina(BatelId));

        // Act
        var categories = await sut.GetByLocationIdAsync(JoaoId, BatelId);

        // Assert
        categories.Select(c => c.Id).ShouldBe(new[] { categoryId }, "an Admin of a location lists that location's categories");
    }

    // Scenario 43
    [Fact]
    [Trait("Rule", "M30")]
    public async Task GetByLocationIdAsync_AdminOfBatelForCentro_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        await SeedCategoryAsync(database, CentroId);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateCategoryService(context, Holding(At(Roles.Admin, BatelId)), LocationInCantina(CentroId));

        // Act
        var act = () => sut.GetByLocationIdAsync(JoaoId, CentroId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "an Admin of one location may not list another location's categories");
        exception.StatusCode.ShouldBe(403, "a role for one location never counts at another location");
    }

    // Scenario 44
    [Fact]
    [Trait("Rule", "M30")]
    public async Task GetAllAsync_AdminOfBatelOnly_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        await SeedCategoryAsync(database, BatelId);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateCategoryService(context, Holding(At(Roles.Admin, BatelId)));

        // Act
        var act = () => sut.GetAllAsync(JoaoId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "the organization-wide category list needs an Admin or Owner for all locations");
        exception.StatusCode.ShouldBe(403, "an Admin of one location is refused the whole list");
    }

    // Scenario 45
    [Fact]
    [Trait("Rule", "M30")]
    public async Task GetByIdAsync_WaiterAtBatelOnBatelCategory_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedDinnerMenuAsync(database);
        var categoryId = await SeedCategoryAsync(database, BatelId);
        using var context = database.CreateContext(TestTenant.Of(CantinaId));
        var sut = CreateCategoryService(context, Holding(At(Roles.Waiter, BatelId)));

        // Act
        var act = () => sut.GetByIdAsync(JoaoId, categoryId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "only an Admin or Owner reads categories, even at the Waiter's own location");
        exception.StatusCode.ShouldBe(403, "a Waiter is refused category reads");
    }

    private static IOrganizationLookup LocationInCantina(Guid locationId)
    {
        var lookup = CantinaExists();
        lookup.GetLocationAsync(locationId).Returns(new LocationSummaryDto { Id = locationId, OrganizationId = CantinaId });
        return lookup;
    }
}
