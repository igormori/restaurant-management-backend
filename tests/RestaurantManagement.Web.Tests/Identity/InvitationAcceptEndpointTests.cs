using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Accepting an invitation on a real PostgreSQL database. Ana (Owner of Cantina da Nonna) invites people as
/// a Waiter at Batel; Rita already has an account (and belongs to Trattoria do Sol); Bruno has none.
/// </summary>
public class InvitationAcceptEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string AcceptUrl = "/api/invitations/accept";
    private const string AcceptWithAccountUrl = "/api/auth/accept-invitation";

    private readonly PostgresDatabaseFixture _postgres;

    public InvitationAcceptEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "T72")]
    public async Task Accept_SignedInAccountWithTheInvitedEmail_JoinsWithExactlyTheInvitedGridAndIsNeverOwner()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await InviteAsync(factory, AnaId, RitaEmail, BatelId, new[] { "Waiter" }, new Dictionary<string, string> { ["Menus"] = "Manage" });
        var token = LatestTokenSentTo(factory, RitaEmail);

        // Act
        var response = await factory.ClientFor(RitaId, null).PostAsJsonAsync(AcceptUrl, new { token });

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue("the invited account joins with the link");
        (await CellsAsync(factory, CantinaId, RitaId, BatelId)).ShouldBe(
            Cells((AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Manage)),
            "the member has exactly the invited grid: the Waiter preset with Menus changed to Manage");
        var membership = await factory.InOrganizationAsync<IdentityDbContext, OrganizationMember>(CantinaId,
            db => db.Set<OrganizationMember>().SingleAsync(m => m.UserId == RitaId && m.OrganizationId == CantinaId));
        membership.IsOwner.ShouldBeFalse("an invitation never makes anyone an Owner, whatever the request said");
        (await IsMemberAsync(factory, TrattoriaId, RitaId)).ShouldBeTrue("her membership in the other organization is untouched");
    }

    [Fact]
    [Trait("Rule", "T73")]
    public async Task GetMenus_InviteeWhoHasNotAcceptedYet_IsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await InviteAsync(factory, AnaId, RitaEmail, BatelId, new[] { "Waiter" });

        // Act
        var response = await factory.ClientFor(RitaId, CantinaId).GetAsync($"/api/menus/location/{BatelId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "until the link is accepted the invitee has no access at all");
        (await IsMemberAsync(factory, CantinaId, RitaId)).ShouldBeFalse("a pending invitation creates no membership");
    }

    [Fact]
    [Trait("Rule", "T74")]
    public async Task AcceptWithNewAccount_PersonWithoutAccount_CreatesAVerifiedAccountAndJoinsWithTheInvitedGrid()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await InviteAsync(factory, AnaId, BrunoEmail, BatelId, new[] { "Waiter" });
        var token = LatestTokenSentTo(factory, BrunoEmail);

        // Act
        var response = await factory.CreateClient().PostAsJsonAsync(AcceptWithAccountUrl,
            new { token, firstName = "Bruno", lastName = "Souza", password = "Senha@Segura9" });

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue("the invitee creates an account while accepting");
        var bruno = await factory.InOrganizationAsync<IdentityDbContext, User>(CantinaId,
            db => db.Users.SingleAsync(u => u.Email == BrunoEmail));
        bruno.IsVerified.ShouldBeTrue("the invitation link already proves the address, so the account is verified");
        (await IsMemberAsync(factory, CantinaId, bruno.Id)).ShouldBeTrue();
        (await CellsAsync(factory, CantinaId, bruno.Id, BatelId)).ShouldBe(Cells(WaiterLevels), "the new member has exactly the invited grid");
    }

    [Fact]
    [Trait("Rule", "T75")]
    public async Task Accept_SameLinkTwiceAtTheSameMoment_JoinsOnceAndRefusesTheOther()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await InviteAsync(factory, AnaId, RitaEmail, BatelId, new[] { "Waiter" });
        var token = LatestTokenSentTo(factory, RitaEmail);
        var first = factory.ClientFor(RitaId, null);
        var second = factory.ClientFor(RitaId, null);

        // Act
        var responses = await Task.WhenAll(first.PostAsJsonAsync(AcceptUrl, new { token }), second.PostAsJsonAsync(AcceptUrl, new { token }));

        // Assert
        responses.Count(r => r.IsSuccessStatusCode).ShouldBe(1, "a link works once, even when used twice at the same moment");
        responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest).ShouldBe(1, "the other request gets the generic invalid link answer");
        (await CellsAsync(factory, CantinaId, RitaId, BatelId)).ShouldBe(Cells(WaiterLevels), "the member has the grid once, not twice");
    }
}
