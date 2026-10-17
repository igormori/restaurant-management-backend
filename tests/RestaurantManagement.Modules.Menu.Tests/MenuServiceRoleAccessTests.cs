using NSubstitute;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;

namespace RestaurantManagement.Modules.Menu.Tests;

/// <summary>
/// Which roles may read and change an organization's menus, checked by the service itself.
/// Every role of the organization reads; only Owner and Admin write.
/// </summary>
public class MenuServiceRoleAccessTests
{
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("c2c2c2c2-0000-4000-8000-000000000002");
    private static readonly Guid CentroLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000d");
    private static readonly Guid DinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly DateTime CreationTime = new(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc);

    private static async Task SeedCantinaDinnerMenuAtCentroAsync(MenuTestDatabase database)
    {
        using var context = database.CreateContext();
        context.Menus.Add(new Entities.Menu
        {
            Id = DinnerMenuId,
            OrganizationId = CantinaId,
            Name = "Jantar",
            Description = "Feijoada, moqueca e caipirinha",
            IsActive = true,
            CreatedAt = CreationTime,
            UpdatedAt = CreationTime
        });
        context.LocationMenus.Add(new Entities.LocationMenu
        {
            Id = Guid.NewGuid(),
            OrganizationId = CantinaId,
            MenuId = DinnerMenuId,
            LocationId = CentroLocationId,
            IsActive = true,
            CreatedAt = CreationTime
        });
        await context.SaveChangesAsync();
    }

    private static IUserRoleLookup RoleIn(Guid organizationId, string role)
    {
        var userRoleLookup = Substitute.For<IUserRoleLookup>();
        userRoleLookup.GetRoleAsync(JoaoId, organizationId).Returns(role);
        return userRoleLookup;
    }

    private static IOrganizationLookup CentroBelongsToCantina()
    {
        var organizationLookup = Substitute.For<IOrganizationLookup>();
        organizationLookup.GetLocationAsync(CentroLocationId).Returns(new LocationSummaryDto
        {
            Id = CentroLocationId,
            OrganizationId = CantinaId
        });
        return organizationLookup;
    }

    private static MenuService CreateSut(Data.MenuDbContext context, IUserRoleLookup userRoleLookup, ITenantContext tenantContext)
        => new(context, CentroBelongsToCantina(), userRoleLookup, tenantContext, TestLocalizer.Create());

    // Scenario 34
    [Fact]
    [Trait("Rule", "M4")]
    public async Task GetMenusAsync_WaiterOfAnotherOrganization_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateSut(context, RoleIn(TrattoriaId, Roles.Waiter), tenant);

        // Act
        var act = () => sut.GetMenusAsync(JoaoId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "a user with a role only in another organization may not read this organization's menus");
        exception.StatusCode.ShouldBe(403, "a role in another organization gives no access here");
    }

    // Scenario 45
    [Fact]
    [Trait("Rule", "M4")]
    public async Task GetMenusByLocationAsync_WaiterOfAnotherOrganization_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        var tenant = TestTenant.Of(TrattoriaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateSut(context, RoleIn(TrattoriaId, Roles.Waiter), tenant);

        // Act
        var act = () => sut.GetMenusByLocationAsync(JoaoId, CentroLocationId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "a user with a role only in another organization may not read this location's menus");
        exception.StatusCode.ShouldBe(403, "a role in another organization gives no access here");
    }

    // Scenario 35
    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M1")]
    public async Task CreateMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbidden(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateSut(context, RoleIn(CantinaId, role), tenant);
        var request = new CreateMenuRequest
        {
            Name = "Almoço executivo",
            Description = "Prato feito com arroz, feijão e bife"
        };

        // Act
        var act = () => sut.CreateMenuAsync(JoaoId, request);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "only an Owner or Admin may create a menu");
        exception.StatusCode.ShouldBe(403, "Manager, Cashier, Waiter and Kitchen may never create a menu");
    }

    // Scenario 36
    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M1")]
    public async Task DeleteMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndKeepsMenu(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateSut(context, RoleIn(CantinaId, role), tenant);

        // Act
        var act = () => sut.DeleteMenuAsync(JoaoId, DinnerMenuId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "only an Owner or Admin may delete a menu");
        exception.StatusCode.ShouldBe(403, "Manager, Cashier, Waiter and Kitchen may never delete a menu");
        using var checkContext = database.CreateContext(tenant);
        checkContext.Menus.Count(m => m.Id == DinnerMenuId).ShouldBe(1, "a refused delete keeps the menu");
    }

    // Scenario 36
    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M2")]
    public async Task EditMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndKeepsMenu(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateSut(context, RoleIn(CantinaId, role), tenant);
        var request = new EditMenuRequest { Name = "Cardápio alterado", Description = "Sem permissão", IsActive = false };

        // Act
        var act = () => sut.EditMenuAsync(JoaoId, DinnerMenuId, request);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "only an Owner or Admin may edit a menu");
        exception.StatusCode.ShouldBe(403, "only an Owner or Admin may edit a menu");
        using var checkContext = database.CreateContext(tenant);
        var stored = checkContext.Menus.Single(m => m.Id == DinnerMenuId);
        stored.Name.ShouldBe("Jantar", "a refused edit leaves the menu name unchanged");
        stored.IsActive.ShouldBeTrue("a refused edit leaves the menu active");
    }

    // Scenario 36
    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M2")]
    public async Task AttachMenuToLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndStoresNoLink(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateSut(context, RoleIn(CantinaId, role), tenant);
        var otherLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000e");

        // Act
        var act = () => sut.AttachMenuToLocationAsync(JoaoId, DinnerMenuId, otherLocationId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "only an Owner or Admin may link a menu to a location");
        exception.StatusCode.ShouldBe(403, "only an Owner or Admin may link a menu to a location");
        using var checkContext = database.CreateContext(tenant);
        checkContext.LocationMenus.Select(lm => lm.LocationId).ToList()
            .ShouldBe(new[] { CentroLocationId }, ignoreOrder: false, customMessage: "a refused link leaves the menu's links as they were");
    }

    // Scenario 36
    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M2")]
    public async Task DetachMenuFromLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndKeepsLink(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateSut(context, RoleIn(CantinaId, role), tenant);

        // Act
        var act = () => sut.DetachMenuFromLocationAsync(JoaoId, DinnerMenuId, CentroLocationId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "only an Owner or Admin may unlink a menu from a location");
        exception.StatusCode.ShouldBe(403, "only an Owner or Admin may unlink a menu from a location");
        using var checkContext = database.CreateContext(tenant);
        checkContext.LocationMenus.Select(lm => lm.LocationId).ToList()
            .ShouldBe(new[] { CentroLocationId }, ignoreOrder: false, customMessage: "a refused unlink keeps the link");
    }

    // Scenario 32
    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M3")]
    public async Task GetMenusAsync_AnyRoleOfTheOrganization_ReturnsItsMenus(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateSut(context, RoleIn(CantinaId, role), tenant);

        // Act
        var menus = await sut.GetMenusAsync(JoaoId);

        // Assert
        menus.Select(m => m.Id).ShouldBe(new[] { DinnerMenuId }, ignoreOrder: false, customMessage: "every role of the organization may read its menus");
    }

    // Scenario 33
    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M3")]
    public async Task GetMenusByLocationAsync_AnyRoleOfTheOrganization_ReturnsLocationMenus(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant);
        var sut = CreateSut(context, RoleIn(CantinaId, role), tenant);

        // Act
        var menus = await sut.GetMenusByLocationAsync(JoaoId, CentroLocationId);

        // Assert
        menus.Select(m => m.Id).ShouldBe(new[] { DinnerMenuId }, ignoreOrder: false, customMessage: "every role of the organization may read a location's menus");
    }
}
