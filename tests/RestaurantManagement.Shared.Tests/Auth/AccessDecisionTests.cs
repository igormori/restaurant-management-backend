using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Auth;

/// <summary>
/// Access decisions over all of a user's roles: permissions add up, a role for one location counts
/// only there, a role for all locations counts everywhere, and list order never matters.
/// </summary>
public class AccessDecisionTests
{
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid CentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");
    private static readonly Guid MercadoMunicipalId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");

    private static readonly string[] OwnerOrAdmin = { Roles.Owner, Roles.Admin };
    private static readonly string[] OwnerAdminOrManager = { Roles.Owner, Roles.Admin, Roles.Manager };

    private static RoleAssignmentDto AllLocations(string role) => new() { Role = role, LocationId = null };

    private static RoleAssignmentDto At(string role, Guid locationId) => new() { Role = role, LocationId = locationId };

    private static List<RoleAssignmentDto> InOrder(RoleAssignmentDto first, RoleAssignmentDto second, bool firstStoredFirst)
        => firstStoredFirst ? new() { first, second } : new() { second, first };

    // Scenario 1
    [Theory]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.Admin, false)]
    [InlineData(Roles.Owner, true)]
    [InlineData(Roles.Owner, false)]
    [Trait("Rule", "SH29")]
    public void AllowsOrganizationWide_AdminOrOwnerForAllLocationsPlusWaiterAtOneLocation_ReturnsTrueInEveryOrder(string role, bool adminStoredFirst)
    {
        // Arrange
        var assignments = InOrder(AllLocations(role), At(Roles.Waiter, BatelId), adminStoredFirst);

        // Act
        var allowed = AccessDecision.AllowsOrganizationWide(assignments, OwnerOrAdmin);

        // Assert
        allowed.ShouldBeTrue("an all-locations Admin or Owner keeps full access whatever other role the user also holds");
    }

    // Scenario 2
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Rule", "SH30")]
    public void AllowsOrganizationWide_WaiterForAllLocationsPlusManagerAtOneLocation_ReturnsFalseInEveryOrder(bool waiterStoredFirst)
    {
        // Arrange
        var assignments = InOrder(AllLocations(Roles.Waiter), At(Roles.Manager, BatelId), waiterStoredFirst);

        // Act
        var allowed = AccessDecision.AllowsOrganizationWide(assignments, OwnerAdminOrManager);

        // Assert
        allowed.ShouldBeFalse("a role held at one location never counts for an action that belongs to the whole organization");
    }

    // Scenario 3
    [Fact]
    [Trait("Rule", "SH30")]
    public void AllowsOrganizationWide_AdminAtEachOfThreeLocationsSeparately_ReturnsFalse()
    {
        // Arrange
        var assignments = new List<RoleAssignmentDto>
        {
            At(Roles.Admin, BatelId), At(Roles.Admin, CentroId), At(Roles.Admin, MercadoMunicipalId)
        };

        // Act
        var allowed = AccessDecision.AllowsOrganizationWide(assignments, OwnerOrAdmin);

        // Assert
        allowed.ShouldBeFalse("the same role at every current location as separate assignments is not an all-locations role");
    }

    // Scenario 4
    [Fact]
    [Trait("Rule", "SH31")]
    public void AllowsOrganizationWide_NoAssignments_ReturnsFalse()
    {
        // Arrange
        var assignments = new List<RoleAssignmentDto>();

        // Act
        var allowed = AccessDecision.AllowsOrganizationWide(assignments, OwnerOrAdmin);

        // Assert
        allowed.ShouldBeFalse("a user with no role in the organization is refused");
    }

    // Scenario 5
    [Theory]
    [InlineData(Roles.Waiter, Roles.Cashier)]
    [InlineData(Roles.Cashier, Roles.Waiter)]
    [InlineData(Roles.Kitchen, Roles.Waiter)]
    [InlineData(Roles.Waiter, Roles.Kitchen)]
    [Trait("Rule", "SH31")]
    public void AllowsOrganizationWide_TwoRolesWithoutAnAllowedRole_ReturnsFalseInEveryOrder(string first, string second)
    {
        // Arrange
        var assignments = new List<RoleAssignmentDto> { AllLocations(first), AllLocations(second) };

        // Act
        var allowed = AccessDecision.AllowsOrganizationWide(assignments, OwnerOrAdmin);

        // Assert
        allowed.ShouldBeFalse("roles that grant nothing do not add up to management access");
    }

    // Scenario 6
    [Theory]
    [InlineData("44444444-dddd-4ddd-8ddd-000000000001", true)]
    [InlineData("44444444-dddd-4ddd-8ddd-000000000001", false)]
    [InlineData("44444444-dddd-4ddd-8ddd-000000000003", true)]
    [InlineData("44444444-dddd-4ddd-8ddd-000000000003", false)]
    [InlineData("44444444-dddd-4ddd-8ddd-0000000000ff", true)]
    [InlineData("44444444-dddd-4ddd-8ddd-0000000000ff", false)]
    [Trait("Rule", "SH32")]
    public void AllowsAtLocation_AdminForAllLocationsPlusWaiterAtBatel_ReturnsTrueAtEveryLocationInEveryOrder(string location, bool adminStoredFirst)
    {
        // Arrange
        var assignments = InOrder(AllLocations(Roles.Admin), At(Roles.Waiter, BatelId), adminStoredFirst);

        // Act
        var allowed = AccessDecision.AllowsAtLocation(assignments, Guid.Parse(location), OwnerOrAdmin);

        // Assert
        allowed.ShouldBeTrue("an all-locations Admin has Admin permissions at every location, also one added later");
    }

    // Scenario 7
    [Theory]
    [InlineData("44444444-dddd-4ddd-8ddd-000000000001", true, true)]
    [InlineData("44444444-dddd-4ddd-8ddd-000000000001", true, false)]
    [InlineData("44444444-dddd-4ddd-8ddd-000000000002", false, true)]
    [InlineData("44444444-dddd-4ddd-8ddd-000000000002", false, false)]
    [InlineData("44444444-dddd-4ddd-8ddd-0000000000ff", false, true)]
    [InlineData("44444444-dddd-4ddd-8ddd-0000000000ff", false, false)]
    [Trait("Rule", "SH33")]
    public void AllowsAtLocation_WaiterForAllLocationsPlusManagerAtBatel_AllowsManagerActionsOnlyAtBatelInEveryOrder(string location, bool expected, bool waiterStoredFirst)
    {
        // Arrange
        var assignments = InOrder(AllLocations(Roles.Waiter), At(Roles.Manager, BatelId), waiterStoredFirst);

        // Act
        var allowed = AccessDecision.AllowsAtLocation(assignments, Guid.Parse(location), OwnerAdminOrManager);

        // Assert
        allowed.ShouldBe(expected, "a role for one location counts only at that location, never at others or at locations added later");
    }

    // Scenario 8
    [Fact]
    [Trait("Rule", "SH31")]
    public void AllowsAtLocation_NoAssignments_ReturnsFalse()
    {
        // Arrange
        var assignments = new List<RoleAssignmentDto>();

        // Act
        var allowed = AccessDecision.AllowsAtLocation(assignments, BatelId, OwnerOrAdmin);

        // Assert
        allowed.ShouldBeFalse("a user with no role in the organization is refused at every location");
    }
}
