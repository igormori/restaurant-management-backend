using System.Reflection;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Auth;

/// <summary>
/// The fixed permission list and the read-only role-to-permission map (spec Outcome #1).
/// </summary>
public class RolePermissionsTests
{
    private static readonly string[] AllFivePermissions =
    {
        "members.manage", "locations.manage", "settings.manage", "menu.manage", "reports.view"
    };

    // Scenario 1
    [Fact]
    [Trait("Rule", "SH2")]
    public void GetPermissions_RetiredStaffRole_ReturnsNoPermissions()
    {
        // Arrange
        const string retiredStaffRole = "Staff";

        // Act
        var permissions = RolePermissions.GetPermissions(retiredStaffRole);

        // Assert
        permissions.ShouldBeEmpty(customMessage: "a retired role grants no permissions");
    }

    // Scenario 2
    [Fact]
    [Trait("Rule", "SH2")]
    public void GetPermissions_UnknownRole_ReturnsNoPermissions()
    {
        // Arrange
        const string unknownRole = "Sommelier";

        // Act
        var permissions = RolePermissions.GetPermissions(unknownRole);

        // Assert
        permissions.ShouldBeEmpty(customMessage: "an unknown role grants no permissions");
    }

    // Scenario 3
    [Fact]
    [Trait("Rule", "SH1")]
    public void HasPermission_ManagerAndMembersManage_ReturnsFalse()
    {
        // Arrange
        var role = Roles.Manager;

        // Act
        var allowed = RolePermissions.HasPermission(role, Permissions.MembersManage);

        // Assert
        allowed.ShouldBeFalse("a manager cannot manage members");
    }

    // Scenario 4
    [Fact]
    [Trait("Rule", "SH1")]
    public void GetPermissions_Cashier_ReturnsNoPermissions()
    {
        // Arrange
        var role = Roles.Cashier;

        // Act
        var permissions = RolePermissions.GetPermissions(role);

        // Assert
        permissions.ShouldBeEmpty(customMessage: "a cashier has no permissions");
    }

    // Scenario 5
    [Fact]
    [Trait("Rule", "SH1")]
    public void GetPermissions_Waiter_ReturnsNoPermissions()
    {
        // Arrange
        var role = Roles.Waiter;

        // Act
        var permissions = RolePermissions.GetPermissions(role);

        // Assert
        permissions.ShouldBeEmpty(customMessage: "a waiter has no permissions");
    }

    // Scenario 6
    [Fact]
    [Trait("Rule", "SH1")]
    public void GetPermissions_Kitchen_ReturnsNoPermissions()
    {
        // Arrange
        var role = Roles.Kitchen;

        // Act
        var permissions = RolePermissions.GetPermissions(role);

        // Assert
        permissions.ShouldBeEmpty(customMessage: "kitchen staff have no permissions");
    }

    // Scenario 7
    [Fact]
    [Trait("Rule", "SH1")]
    public void GetPermissions_Owner_ReturnsAllFivePermissions()
    {
        // Arrange
        var role = Roles.Owner;

        // Act
        var permissions = RolePermissions.GetPermissions(role);

        // Assert
        permissions.ShouldBe(AllFivePermissions, ignoreOrder: true, customMessage: "an owner has all five permissions");
    }

    // Scenario 8
    [Fact]
    [Trait("Rule", "SH1")]
    public void GetPermissions_Admin_ReturnsAllFivePermissions()
    {
        // Arrange
        var role = Roles.Admin;

        // Act
        var permissions = RolePermissions.GetPermissions(role);

        // Assert
        permissions.ShouldBe(AllFivePermissions, ignoreOrder: true, customMessage: "an admin has all five permissions");
    }

    // Scenario 9
    [Fact]
    [Trait("Rule", "SH1")]
    public void GetPermissions_Manager_ReturnsMenuManageAndReportsViewOnly()
    {
        // Arrange
        var role = Roles.Manager;

        // Act
        var permissions = RolePermissions.GetPermissions(role);

        // Assert
        permissions.ShouldBe(new[] { "menu.manage", "reports.view" }, ignoreOrder: true, customMessage: "a manager has only menu management and report viewing");
    }

    // Scenario 10
    [Fact]
    [Trait("Rule", "SH1")]
    public void HasPermission_ManagerAndMenuManage_ReturnsTrue()
    {
        // Arrange
        var role = Roles.Manager;

        // Act
        var allowed = RolePermissions.HasPermission(role, Permissions.MenuManage);

        // Assert
        allowed.ShouldBeTrue("a manager can manage the menu");
    }

    // Scenario 11
    [Fact]
    [Trait("Rule", "SH3")]
    public void All_ProductPermissionList_IsExactlyTheFiveSpecPermissions()
    {
        // Arrange
        // (the list is fixed by the product)

        // Act
        var permissions = Permissions.All;

        // Assert
        permissions.ShouldBe(AllFivePermissions, ignoreOrder: true, customMessage: "the product has exactly these five permissions");
    }

    // Scenario 12
    [Fact]
    [Trait("Rule", "SH2")]
    public void GetPermissions_CallerTriesToAddPermissionToResult_IsRefused()
    {
        // Arrange
        var managerPermissions = (ICollection<string>)RolePermissions.GetPermissions(Roles.Manager);

        // Act
        var act = () => managerPermissions.Add(Permissions.MembersManage);

        // Assert
        act.ShouldThrow<NotSupportedException>(customMessage: "the permission list a caller receives cannot be changed to grant more");
    }

    // Scenario 31
    [Fact]
    [Trait("Rule", "SH3")]
    public void Roles_DeclaredRoleNames_AreExactlyTheSixProductRoles()
    {
        // Arrange
        var roleFields = typeof(Roles).GetFields(BindingFlags.Public | BindingFlags.Static);

        // Act
        var roleNames = roleFields.Select(f => (string)f.GetRawConstantValue()!).ToList();

        // Assert
        roleNames.ShouldBe(new[] { "Owner", "Admin", "Manager", "Cashier", "Waiter", "Kitchen" }, ignoreOrder: true, customMessage: "the product has exactly six roles");
    }
}
