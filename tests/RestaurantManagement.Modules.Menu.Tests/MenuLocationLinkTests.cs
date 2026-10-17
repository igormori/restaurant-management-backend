using NSubstitute;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using Shouldly;

namespace RestaurantManagement.Modules.Menu.Tests;

/// <summary>Linking a menu to a location twice keeps one link; unlinking an unlinked location changes nothing.</summary>
public class MenuLocationLinkTests
{
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid CentroLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000d");
    private static readonly Guid DinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly DateTime CreationTime = new(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc);

    private static async Task SeedUnlinkedDinnerMenuAsync(MenuTestDatabase database)
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
        await context.SaveChangesAsync();
    }

    // The interceptor stamps the organization on new links, as in production, so the tenant filter sees them
    private static AuditSaveChangesInterceptor CreateAuditInterceptor()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(AnaId);
        currentUser.OrganizationId.Returns(CantinaId);
        return new AuditSaveChangesInterceptor(currentUser, new TenantScope(), TimeProvider.System, TestLocalizer.Create());
    }

    private static MenuService CreateOwnerSut(Data.MenuDbContext context, ITenantContext tenant)
    {
        var userRoleLookup = Substitute.For<IUserRoleLookup>();
        userRoleLookup.GetRoleAsync(AnaId, CantinaId).Returns(Roles.Owner);
        var organizationLookup = Substitute.For<IOrganizationLookup>();
        organizationLookup.GetLocationAsync(CentroLocationId).Returns(new LocationSummaryDto
        {
            Id = CentroLocationId,
            OrganizationId = CantinaId
        });
        return new MenuService(context, organizationLookup, userRoleLookup, tenant, TestLocalizer.Create());
    }

    // Scenario 48
    [Fact]
    [Trait("Rule", "M17")]
    public async Task AttachMenuToLocationAsync_LinkedTwice_KeepsOneLink()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedUnlinkedDinnerMenuAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant, CreateAuditInterceptor());
        var sut = CreateOwnerSut(context, tenant);

        // Act
        await sut.AttachMenuToLocationAsync(AnaId, DinnerMenuId, CentroLocationId);
        await sut.AttachMenuToLocationAsync(AnaId, DinnerMenuId, CentroLocationId);

        // Assert
        using var checkContext = database.CreateContext(tenant);
        checkContext.LocationMenus.Count(lm => lm.MenuId == DinnerMenuId && lm.LocationId == CentroLocationId)
            .ShouldBe(1, "linking the same location twice stores one link only");
    }

    // Scenario 48
    [Fact]
    [Trait("Rule", "M17")]
    public async Task DetachMenuFromLocationAsync_LocationNotLinked_AnswersOkAndChangesNothing()
    {
        // Arrange
        using var database = new MenuTestDatabase();
        await SeedUnlinkedDinnerMenuAsync(database);
        var tenant = TestTenant.Of(CantinaId);
        using var context = database.CreateContext(tenant, CreateAuditInterceptor());
        var sut = CreateOwnerSut(context, tenant);

        // Act
        var act = () => sut.DetachMenuFromLocationAsync(AnaId, DinnerMenuId, CentroLocationId);

        // Assert
        await Should.NotThrowAsync(act, "unlinking a location that is not linked still answers OK");
        using var checkContext = database.CreateContext(tenant);
        checkContext.LocationMenus.Count().ShouldBe(0, "unlinking a location that is not linked changes nothing");
        checkContext.Menus.Count(m => m.Id == DinnerMenuId).ShouldBe(1, "the menu itself is kept");
    }
}
