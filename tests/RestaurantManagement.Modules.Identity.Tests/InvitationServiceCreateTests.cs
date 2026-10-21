using RestaurantManagement.Modules.Identity.Entities.Invitations;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.InvitationHarness;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Creating an invitation at Cantina da Nonna. Ana is the Owner; Maria is a member; Bruno
    /// (bruno@exemplo.com.br) is the person invited.
    /// </summary>
    public class InvitationServiceCreateTests
    {
        private const string BrunoEmail = "bruno@exemplo.com.br";

        [Theory]
        [InlineData("Maria@CantinaDaNonna.com.br")]
        [InlineData("ANA@cantinadanonna.com.br")]
        [Trait("Rule", "T56")]
        public async Task CreateAsync_EmailOfACurrentMemberInOtherCasing_Refuses400AndStoresNothing(string email)
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();
            var sut = harness.As(AnaId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.CreateAsync(Invite(email, (BatelId, new[] { "Waiter" })), "en"));

            // Assert
            exception.StatusCode.ShouldBe(400, "someone already in the organization, the Owner too, cannot be invited again");
            harness.InvitationCount().ShouldBe(0, "nothing is stored for a refused invitation");
            harness.SentEmails().ShouldBeEmpty("no email goes out for a refused invitation");
        }

        [Fact]
        [Trait("Rule", "T57")]
        public async Task CreateAsync_SamePendingEmailInvitedAgain_LeavesOneOpenInvitationAndReplacesTheFirst()
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();
            var sut = harness.As(AnaId);
            await sut.CreateAsync(Invite(BrunoEmail, (BatelId, new[] { "Waiter" })), "en");
            var firstToken = harness.LatestTokenSentTo(BrunoEmail);

            // Act
            await harness.As(AnaId).CreateAsync(Invite(BrunoEmail, (BatelId, new[] { "Cashier" })), "en");

            // Assert
            using var context = harness.Database.CreateContext();
            var invitations = context.Set<OrganizationInvitation>().ToList();
            invitations.Count(i => i.UsedAt == null && i.CancelledAt == null && i.ReplacedAt == null)
                .ShouldBe(1, "a new invitation to a pending email leaves exactly one valid invitation");
            invitations.Count(i => i.ReplacedAt != null).ShouldBe(1, "the first invitation is marked replaced, so its link stops working");
            harness.LatestTokenSentTo(BrunoEmail).ShouldNotBe(firstToken, "the new invitation has its own link");
        }

        [Fact]
        [Trait("Rule", "T58")]
        public async Task CreateAsync_AdminWithoutCashierAtBatelGivesTheCashierPreset_Refuses403AndStoresNothing()
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();
            await harness.HoldAsync(MariaId, BatelId,
                (AccessModule.Locations, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Manage),
                (AccessModule.Members, AccessLevel.Manage));
            var sut = harness.As(MariaId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.CreateAsync(Invite(BrunoEmail, (BatelId, new[] { "Cashier" })), "en"));

            // Assert
            exception.StatusCode.ShouldBe(403, "nobody can invite someone with a level above their own at that location");
            harness.InvitationCount().ShouldBe(0, "a refused invitation stores nothing");
            harness.SentEmails().ShouldBeEmpty("a refused invitation sends no email");
        }

        [Fact]
        [Trait("Rule", "T59")]
        public async Task CreateAsync_PresetNamedOwner_IsRefusedAndStoresNothing()
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();
            var sut = harness.As(AnaId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.CreateAsync(Invite(BrunoEmail, (BatelId, new[] { "Owner" })), "en"));

            // Assert
            exception.StatusCode.ShouldBe(404, "Owner is not a role that can be offered: it does not exist among the stored presets");
            harness.InvitationCount().ShouldBe(0, "an invitation cannot make anyone an Owner, and nothing is stored");
        }
    }
}
