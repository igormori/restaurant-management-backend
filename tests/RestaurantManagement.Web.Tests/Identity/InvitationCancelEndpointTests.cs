using System.Net;
using System.Net.Http.Json;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// <c>DELETE api/invitations/{invitationId}</c> on a real PostgreSQL database. Ana (Owner of Cantina da Nonna)
/// invited Rita as a Waiter at Batel; Maria is an Admin with Members = Manage at Batel.
/// </summary>
public class InvitationCancelEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _postgres;

    public InvitationCancelEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "T76")]
    public async Task CancelInvitation_PendingInvitation_StopsTheLinkFromWorking()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var invitationId = await InviteAsync(factory, AnaId, RitaEmail, BatelId, new[] { "Waiter" });
        var token = LatestTokenSentTo(factory, RitaEmail);

        // Act
        var cancel = await factory.ClientFor(AnaId, CantinaId).DeleteAsync($"/api/invitations/{invitationId}");
        var accept = await factory.ClientFor(RitaId, null).PostAsJsonAsync("/api/invitations/accept", new { token });

        // Assert
        cancel.IsSuccessStatusCode.ShouldBeTrue("an authorized member can cancel a pending invitation");
        accept.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a cancelled link is refused");
        (await IsMemberAsync(factory, CantinaId, RitaId)).ShouldBeFalse("nothing is joined through a cancelled link");
    }

    [Fact]
    [Trait("Rule", "T77")]
    public async Task CancelInvitation_PendingInvitation_WritesOneAuditEntryWithBeforeAndAfter()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var invitationId = await InviteAsync(factory, AnaId, RitaEmail, BatelId, new[] { "Waiter" });

        // Act
        await factory.ClientFor(AnaId, CantinaId).DeleteAsync($"/api/invitations/{invitationId}");

        // Assert
        var entry = (await AuditEntriesAsync(factory, CantinaId, AuditActions.MemberInvitationCancelled)).ShouldHaveSingleItem("a cancellation writes one audit entry");
        entry.Before.ShouldContain(RitaEmail, customMessage: "the entry records who had been invited");
        entry.Before.ShouldContain("Waiter", customMessage: "the entry records the roles that had been offered");
        entry.After!.ToLowerInvariant().ShouldContain("cancelled", customMessage: "the entry records the new state");
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Cashier")]
    [InlineData("Waiter")]
    [InlineData("Kitchen")]
    [Trait("Rule", "T78")]
    public async Task CancelInvitation_CallerWithADefaultRoleThatLacksMembersManage_ReturnsForbiddenAndKeepsItPending(string role)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var invitationId = await InviteAsync(factory, AnaId, RitaEmail, BatelId, new[] { "Waiter" });
        var caller = Guid.Parse("88888888-0000-4000-8000-0000000000a1");
        await factory.SeedUserAsync(caller, "caio@cantinadanonna.com.br", "Caio", TenantApiFactory.MemberOf(CantinaId));
        await factory.GrantAsync(caller, CantinaId, BatelId, DefaultLevelsOf(role));

        // Act
        var response = await factory.ClientFor(caller, CantinaId).DeleteAsync($"/api/invitations/{invitationId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"a {role} has no Members = Manage, so she cannot cancel invitations");
        (await InvitationsAsync(factory, CantinaId)).Single().CancelledAt.ShouldBeNull("the invitation stays pending");
    }

    [Fact]
    [Trait("Rule", "T79")]
    public async Task CancelInvitation_InvitationOfAnotherOrganization_ReturnsNotFoundAndStaysPending()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var response = await factory.ClientFor(TerezaId, TrattoriaId).PostAsJsonAsync("/api/invitations", InviteBody(BrunoEmail, CentroId, new[] { "Waiter" }));
        var trattoriaInvitation = (await response.Content.ReadFromJsonAsync<InvitationReply>())!.Id;

        // Act
        var cancel = await factory.ClientFor(AnaId, CantinaId).DeleteAsync($"/api/invitations/{trattoriaInvitation}");

        // Assert
        cancel.StatusCode.ShouldBe(HttpStatusCode.NotFound, "another organization's invitation is answered as if it did not exist");
        (await InvitationsAsync(factory, TrattoriaId)).Single().CancelledAt.ShouldBeNull("the other organization's invitation stays pending");
    }

    private static (AccessModule, AccessLevel)[] DefaultLevelsOf(string role)
    {
        return role switch
        {
            "Manager" => new[]
            {
                (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read),
                (AccessModule.Kitchen, AccessLevel.Read), (AccessModule.Reports, AccessLevel.Manage)
            },
            "Cashier" => CashierLevels,
            "Waiter" => WaiterLevels,
            _ => new[]
            {
                (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read), (AccessModule.Orders, AccessLevel.Read),
                (AccessModule.Kitchen, AccessLevel.Manage)
            }
        };
    }
}
