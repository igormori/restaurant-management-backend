using RestaurantManagement.Shared.Auth;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Auth;

/// <summary>
/// The pure decision over a user's access grid in one organization (location x module -> level).
/// Cantina da Nonna has Batel and Mercado Municipal; the Owner passes every question with no cells.
/// </summary>
public class AccessGridDecisionTests
{
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid MercadoId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");

    private static AccessGridDto Grid(params (Guid Location, AccessModule Module, AccessLevel Level)[] cells) => new()
    {
        Cells = cells.Select(c => new AccessCellDto { LocationId = c.Location, Module = c.Module, Level = c.Level }).ToList()
    };

    private static AccessGridDto Owner => new() { IsOwner = true };

    // Held: Menus Read at Batel, Reports Manage at Batel, Menus Manage at Mercado Municipal.
    private static AccessGridDto MixedGrid => Grid(
        (BatelId, AccessModule.Menus, AccessLevel.Read),
        (BatelId, AccessModule.Reports, AccessLevel.Manage),
        (MercadoId, AccessModule.Orders, AccessLevel.Read));

    [Theory]
    [InlineData(AccessModule.Menus, AccessLevel.Manage, "Batel")]   // Read never passes a Manage question
    [InlineData(AccessModule.Menus, AccessLevel.Read, "Mercado")]   // a cell at another location never counts
    [InlineData(AccessModule.Cashier, AccessLevel.Read, "Batel")]   // no cell in that module (None)
    [InlineData(AccessModule.Orders, AccessLevel.Read, "Batel")]    // a cell in that module at another location only
    [Trait("Rule", "SH34")]
    public void HasLevelAt_LevelMissingOrTooLow_IsRefused(AccessModule module, AccessLevel needed, string location)
    {
        // Arrange
        var locationId = location == "Batel" ? BatelId : MercadoId;

        // Act
        var allowed = AccessGridDecision.HasLevelAt(MixedGrid, module, needed, locationId);

        // Assert
        allowed.ShouldBeFalse("None refuses Read, Read refuses Manage, and a cell at another location or in another module never counts");
    }

    [Theory]
    [InlineData(AccessModule.Menus, AccessLevel.Read)]      // Read passes Read
    [InlineData(AccessModule.Reports, AccessLevel.Read)]    // Manage passes Read
    [InlineData(AccessModule.Reports, AccessLevel.Manage)]  // Manage passes Manage
    [Trait("Rule", "SH34")]
    public void HasLevelAt_LevelEnoughAtTheLocation_IsAllowed(AccessModule module, AccessLevel needed)
    {
        // Arrange
        var grid = MixedGrid;

        // Act
        var allowed = AccessGridDecision.HasLevelAt(grid, module, needed, BatelId);

        // Assert
        allowed.ShouldBeTrue("the held level or a higher one passes: Manage includes Read");
    }

    [Fact]
    [Trait("Rule", "SH35")]
    public void AnyQuestion_OwnerWithNoCells_PassesEverything()
    {
        // Arrange
        var owner = Owner;
        var unknownLocation = Guid.Parse("44444444-dddd-4ddd-8ddd-0000000000ff");

        // Act
        var level = AccessGridDecision.HasLevelAt(owner, AccessModule.Reports, AccessLevel.Manage, unknownLocation);
        var everyLocation = AccessGridDecision.HasLevelAtAll(owner, AccessModule.Members, AccessLevel.Manage, new[] { BatelId, MercadoId });
        var noLocationAtAll = AccessGridDecision.HasLevelAtAll(owner, AccessModule.Members, AccessLevel.Manage, Array.Empty<Guid>());
        var works = AccessGridDecision.WorksAt(owner, unknownLocation);
        var any = AccessGridDecision.HasAnyAccess(owner);

        // Assert
        (level && everyLocation && noLocationAtAll && works && any).ShouldBeTrue("the Owner has Manage on everything at every location and cannot be restricted, with no stored cell, and passes a check over an organization with no location too");
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [Trait("Rule", "SH36")]
    public void HasLevelAtAll_LevelAtEveryLocationOfTheSet_PassesOnlyWhenNoneIsMissing(bool menusManageAtBatel, bool menusManageAtMercado, bool expected)
    {
        // Arrange
        var cells = new List<(Guid, AccessModule, AccessLevel)>();
        if (menusManageAtBatel)
            cells.Add((BatelId, AccessModule.Menus, AccessLevel.Manage));
        if (menusManageAtMercado)
            cells.Add((MercadoId, AccessModule.Menus, AccessLevel.Manage));

        // Act
        var allowed = AccessGridDecision.HasLevelAtAll(Grid(cells.ToArray()), AccessModule.Menus, AccessLevel.Manage, new[] { BatelId, MercadoId });

        // Assert
        allowed.ShouldBe(expected, "a level missing at even one location of the set refuses the whole action");
    }

    [Fact]
    [Trait("Rule", "SH37")]
    public void WorksAtAndHasAnyAccess_CellInAnyModule_CountsOnlyAtItsLocationAndNoCellMeansNoAccess()
    {
        // Arrange
        var grid = Grid((BatelId, AccessModule.Kitchen, AccessLevel.Read));

        // Act
        var worksAtBatel = AccessGridDecision.WorksAt(grid, BatelId);
        var worksAtMercado = AccessGridDecision.WorksAt(grid, MercadoId);
        var anyAccess = AccessGridDecision.HasAnyAccess(grid);
        var anyAccessWithNoCells = AccessGridDecision.HasAnyAccess(Grid());

        // Assert
        worksAtBatel.ShouldBeTrue("a cell in any module at a location means the member works there");
        worksAtMercado.ShouldBeFalse("a cell at another location does not");
        anyAccess.ShouldBeTrue("a cell anywhere is some access");
        anyAccessWithNoCells.ShouldBeFalse("a member with no cell has no access at all");
    }
}
