using RestaurantManagement.Shared.Services.AuditLog;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Auth;

public class AuditActionsMemberAccessTests
{
    [Fact]
    [Trait("Rule", "SH38")]
    public void All_FixedListOfAuditedActions_ContainsMemberAccessChanged()
    {
        // Arrange
        // Act
        var action = AuditActions.MemberAccessChanged;

        // Assert
        action.ShouldBe("member.access_changed", "a grid change is recorded under its own action name");
        AuditActions.All.ShouldContain(action, "the audit log accepts the action in its filter, so a grid change can be searched for");
    }
}
