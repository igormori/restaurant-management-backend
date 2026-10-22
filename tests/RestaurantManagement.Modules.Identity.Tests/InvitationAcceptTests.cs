using RestaurantManagement.Modules.Identity.Models.Invitations;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.InvitationHarness;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Accepting an invitation to Cantina da Nonna. Ana (Owner) invites Rita (rita@exemplo.com.br, who has an
    /// account and no organization) as a Waiter at Batel on 2026-10-20 12:00 UTC.
    /// </summary>
    public class InvitationAcceptTests
    {
        private const string RitaEmail = "rita@exemplo.com.br";

        public enum LinkState { Expired, AlreadyUsed, Replaced, Cancelled }

        private static async Task<string> InviteRitaAsync(InvitationHarness harness)
        {
            await harness.As(AnaId).CreateAsync(Invite(RitaEmail, (BatelId, new[] { "Waiter" })), "en");
            return harness.LatestTokenSentTo(RitaEmail);
        }

        private static async Task MakeLinkUnusableAsync(InvitationHarness harness, LinkState state, string token)
        {
            switch (state)
            {
                case LinkState.Expired:
                    harness.Clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1));
                    break;
                case LinkState.AlreadyUsed:
                    await harness.As(RitaId).AcceptAsync(new AcceptInvitationRequest { Token = token });
                    break;
                case LinkState.Replaced:
                    await harness.As(AnaId).CreateAsync(Invite(RitaEmail, (BatelId, new[] { "Cashier" })), "en");
                    break;
                case LinkState.Cancelled:
                    using (var context = harness.Database.CreateContext())
                    {
                        var id = context.Set<RestaurantManagement.Modules.Identity.Entities.Invitations.OrganizationInvitation>().Single().Id;
                        await harness.As(AnaId).CancelAsync(id);
                    }
                    break;
            }
        }

        [Theory]
        [InlineData(LinkState.Expired)]
        [InlineData(LinkState.AlreadyUsed)]
        [InlineData(LinkState.Replaced)]
        [InlineData(LinkState.Cancelled)]
        [Trait("Rule", "T69")]
        public async Task AcceptAsync_LinkExpiredUsedReplacedOrCancelled_RefusesWithTheSameGeneric400(LinkState state)
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();
            var token = await InviteRitaAsync(harness);
            await MakeLinkUnusableAsync(harness, state, token);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => harness.As(RitaId).AcceptAsync(new AcceptInvitationRequest { Token = token }));

            // Assert
            exception.StatusCode.ShouldBe(400, "an expired (after 7 days), used, replaced or cancelled link is refused");
            exception.Message.ShouldBe("InvitationLinkInvalid", "every case gives the same message, so the reason is not revealed");
        }

        [Fact]
        [Trait("Rule", "T70")]
        public async Task AcceptAsync_SignedInAccountHasAnotherEmail_RefusesAndJoinsNothing()
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();
            var token = await InviteRitaAsync(harness);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => harness.As(PaoloId).AcceptAsync(new AcceptInvitationRequest { Token = token }));

            // Assert
            exception.StatusCode.ShouldBe(403, "only the account with the invited email can use the link");
            harness.IsMemberOfCantina(PaoloId).ShouldBeFalse("a different account joins nothing");
            harness.IsMemberOfCantina(RitaId).ShouldBeFalse("the invitation stays unused for Rita");
        }

        [Fact]
        [Trait("Rule", "T71")]
        public async Task AcceptWithNewAccountAsync_InvitedEmailAlreadyHasAnAccount_Refuses400AskingToSignIn()
        {
            // Arrange
            using var harness = await InvitationHarness.CreateAsync();
            var token = await InviteRitaAsync(harness);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(() => harness.Anonymous().AcceptWithNewAccountAsync(
                new AcceptInvitationWithAccountRequest { Token = token, FirstName = "Rita", LastName = "Souza", Password = "Senha@Segura9" }));

            // Assert
            exception.StatusCode.ShouldBe(400, "a person with an account accepts while signed in, not by creating a second account");
            exception.Message.ShouldBe("InvitationAccountExistsSignIn");
            harness.IsMemberOfCantina(RitaId).ShouldBeFalse("nothing is joined by the refused request");
        }
    }
}
