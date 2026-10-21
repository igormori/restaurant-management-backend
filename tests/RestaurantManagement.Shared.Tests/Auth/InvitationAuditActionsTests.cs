using RestaurantManagement.Shared.Services.AuditLog;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Auth;

public class InvitationAuditActionsTests
{
    [Theory]
    [InlineData(AuditActions.MemberInvited, "member.invited")]
    [InlineData(AuditActions.MemberInvitationCancelled, "member.invitation_cancelled")]
    [Trait("Rule", "SH43")]
    public void All_FixedListOfAuditedActions_ContainsTheInvitationActions(string action, string expectedName)
    {
        // Arrange
        // Act
        var listed = AuditActions.All;

        // Assert
        action.ShouldBe(expectedName, "an invitation and its cancellation are recorded under their own action names");
        listed.ShouldContain(action, "the audit log accepts the action in its filter, so invitations can be searched for");
    }
}
