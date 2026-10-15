using NSubstitute;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared;
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

    private static MenuService CreateSut(Data.MenuDbContext context, IUserRoleLookup userRoleLookup)
        => new(context, CentroBelongsToCantina(), userRoleLookup, TestLocalizer.Create());

    // Scenario 34
    [Fact]
    public async Task GetMenusByOrganizationAsync_WaiterOfAnotherOrganization_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        using var context = database.CreateContext();
        var sut = CreateSut(context, RoleIn(TrattoriaId, Roles.Waiter));

        // Act
        var act = () => sut.GetMenusByOrganizationAsync(JoaoId, CantinaId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>();
        exception.StatusCode.ShouldBe(403);
    }

    // Scenario 45
    [Fact]
    public async Task GetMenusByLocationAsync_WaiterOfAnotherOrganization_ThrowsForbidden()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        using var context = database.CreateContext();
        var sut = CreateSut(context, RoleIn(TrattoriaId, Roles.Waiter));

        // Act
        var act = () => sut.GetMenusByLocationAsync(JoaoId, CentroLocationId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>();
        exception.StatusCode.ShouldBe(403);
    }

    // Scenario 35
    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    public async Task CreateMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbidden(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        using var context = database.CreateContext();
        var sut = CreateSut(context, RoleIn(CantinaId, role));
        var request = new CreateMenuRequest
        {
            OrganizationId = CantinaId,
            Name = "Almoço executivo",
            Description = "Prato feito com arroz, feijão e bife"
        };

        // Act
        var act = () => sut.CreateMenuAsync(JoaoId, request);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>();
        exception.StatusCode.ShouldBe(403);
    }

    // Scenario 36
    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    public async Task DeleteMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndKeepsMenu(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        using var context = database.CreateContext();
        var sut = CreateSut(context, RoleIn(CantinaId, role));

        // Act
        var act = () => sut.DeleteMenuAsync(JoaoId, DinnerMenuId);

        // Assert
        var exception = await act.ShouldThrowAsync<BusinessException>();
        exception.StatusCode.ShouldBe(403);
        using var checkContext = database.CreateContext();
        checkContext.Menus.Count(m => m.Id == DinnerMenuId).ShouldBe(1);
    }

    // Scenario 32
    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    public async Task GetMenusByOrganizationAsync_AnyRoleOfTheOrganization_ReturnsItsMenus(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        using var context = database.CreateContext();
        var sut = CreateSut(context, RoleIn(CantinaId, role));

        // Act
        var menus = await sut.GetMenusByOrganizationAsync(JoaoId, CantinaId);

        // Assert
        menus.Select(m => m.Id).ShouldBe(new[] { DinnerMenuId });
    }

    // Scenario 33
    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    public async Task GetMenusByLocationAsync_AnyRoleOfTheOrganization_ReturnsLocationMenus(string role)
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedCantinaDinnerMenuAtCentroAsync(database);
        using var context = database.CreateContext();
        var sut = CreateSut(context, RoleIn(CantinaId, role));

        // Act
        var menus = await sut.GetMenusByLocationAsync(JoaoId, CentroLocationId);

        // Assert
        menus.Select(m => m.Id).ShouldBe(new[] { DinnerMenuId });
    }
}
