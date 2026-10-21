using RestaurantManagement.Shared.Services.AuditLog;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Auth;

public class RoleAuditActionsTests
{
    [Theory]
    [InlineData(AuditActions.RoleCreated, "role.created")]
    [InlineData(AuditActions.RoleLevelsChanged, "role.levels_changed")]
    [InlineData(AuditActions.RoleRenamed, "role.renamed")]
    [Trait("Rule", "SH45")]
    public void All_FixedListOfAuditedActions_ContainsTheRoleActions(string action, string expectedName)
    {
        // Arrange
        // Act
        var listed = AuditActions.All;

        // Assert
        action.ShouldBe(expectedName, "creating, editing the levels of and renaming a role are recorded under their own action names");
        listed.ShouldContain(action, "the audit log accepts the action in its filter, so role changes can be searched for");
    }
}
